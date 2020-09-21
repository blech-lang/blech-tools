// Copyright (c) 2020 - for information on the respective copyright owner
// see the NOTICE file and/or the repository 
// https://github.com/boschresearch/blech.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// This source code is derived from fsharp-language-server
//   (https://github.com/fsprojects/fsharp-language-server)
// Copyright (c) F# Language Server Contributors, licensed under the MIT license,
// cf. 3rd-party-licenses.txt file in the root directory of this source tree.

module Server

open System
open System.IO
open System.Text

open Blech.Common
open Blech.Frontend

open CompilerUtils
open DocumentStore
open Serializers
open Types


type Server(publishDiagnostics) = 
    do resetDocumentStore () // for good measure

    let notFound (doc: Uri): 'Any = 
        raise (Exception (sprintf "%s does not exist" (doc.ToString())))
    // Compile current uri and publish any existing errors
    let validateTextDocument (uri: Uri) = 
        // check file name
        let uriSegments = uri.Segments
        let fileName = uriSegments.[uriSegments.Length-1]
        let fileExt = SearchPath.implementationFileExtension.ToCharArray()
        let prefix = fileName.TrimEnd(fileExt)
        let fileNameDiagnostics = 
            if SearchPath.isValidFileOrDirectoryName prefix then
                [||]
            else
                let lgr = Diagnostics.Logger.create()
                Diagnostics.Logger.logFatalError 
                <| lgr
                <| Diagnostics.Phase.Compiling
                <| Package.IllegalModuleFileName (fileName, [fileName])
                packNewDiagnosticParameters lgr
        // check file contents
        let fileContentsDiagnostics =
            match getModule uri, getText uri with
            | Some modName, Some text ->
                compile uri modName text                
            | _ -> notFound uri
        let diagnostics = Array.concat [fileNameDiagnostics; fileContentsDiagnostics]
        publishDiagnostics (uri,diagnostics)

    let tryPacking uri loc symbol packingFun defaultReturn =
        match getCtx uri with
        | Some tyCtx -> packingFun loc symbol tyCtx
        | None -> defaultReturn

    interface ILanguageServer with 
        member this.Initialize(p: InitializeParams): InitializeResult = 
            { capabilities = 
                { defaultServerCapabilities with 
                    definitionProvider = true
                    hoverProvider = true
                    referencesProvider = true
                    textDocumentSync = 
                        { defaultTextDocumentSyncOptions with 
                            openClose = true 
                            save = { includeText = true }
                            change = 2 //TextDocumentSyncKind.Incremental 
                        }
                }
            }
        member this.Initialized(): unit = ()
        member this.Shutdown(): unit = ()
        member this.DidChangeConfiguration(p: DidChangeConfigurationParams): unit = ()
        member this.DidOpenTextDocument(p: DidOpenTextDocumentParams): unit = 
            onOpen p.textDocument
            validateTextDocument (p.textDocument.uri)
        member this.DidChangeTextDocument(p: DidChangeTextDocumentParams): unit =
            onChange p
        member this.WillSaveTextDocument(p: WillSaveTextDocumentParams): unit = ()
        member this.DidSaveTextDocument(p: DidSaveTextDocumentParams): unit = 
            validateTextDocument (p.textDocument.uri)
        member this.DidCloseTextDocument(p: DidCloseTextDocumentParams): unit = 
            onClose p.textDocument.uri
            // Clear the errors for the document that was closed
            publishDiagnostics (p.textDocument.uri, [||])
        member this.DidChangeWatchedFiles(p: DidChangeWatchedFilesParams): unit = () 
        member this.GotoDefinition(p: TextDocumentPositionParams): option<Types.Location> = 
            let packDeclLocation (initialLoc: Location) (s: Symbol) (tcContext: TypeCheckContext): option<Location> =
                match findQName p.textDocument.uri.AbsolutePath initialLoc s.identifier tcContext.ncEnv with
                | Some symbolQName ->
                    let r = findDeclaration tcContext symbolQName
                    let declLocation: Location = {
                        uri = p.textDocument.uri
                        range = packRange (r.start.line-1, r.start.character-1, r.``end``.line-1, r.``end``.character-1)
                    }
                    Some declLocation
                | None -> None

            let symbol = getSymbol p
            //eprintf "%A\n" symbol
            let initialLoc: Location = {
                uri = p.textDocument.uri
                range = symbol.range
            }
            tryPacking p.textDocument.uri initialLoc symbol packDeclLocation None

        member this.Hover(p: TextDocumentPositionParams): option<Hover> =
            let packHover (hoverLoc: Location) (s: Symbol) (tcContext: TypeCheckContext): option<Hover> = 
                match findQName p.textDocument.uri.AbsolutePath hoverLoc s.identifier tcContext.ncEnv with
                | Some symbolQName ->
                    let hover: Hover = {
                        contents = {language = "blech"; value = findHoverData symbolQName tcContext p.textDocument.uri}
                        range = (packRange (s.range.start.line-1, s.range.start.character-1, s.range.``end``.line-1, s.range.``end``.character))
                    }
                    Some hover
                | None -> None

            let symbol = getSymbol p
            // Location of the symbol that Hover command was called on
            let hoverLoc = {
                uri = p.textDocument.uri
                range = symbol.range
            }
            tryPacking p.textDocument.uri hoverLoc symbol packHover None 

        member this.FindReferences(p: ReferenceParams): list<Location> =
            let packPositions (refLoc: Location) (s: Symbol) (tcContext: TypeCheckContext): list<Location> = 
                match findQName p.textDocument.uri.AbsolutePath refLoc s.identifier tcContext.ncEnv with
                | Some symbolQName ->
                    findReferenceSources symbolQName p.textDocument.uri tcContext
                | None -> []

            let symbol = getSymbol { textDocument = p.textDocument; position = p.position }
            // Location of the symbol that FindReferences command was called on
            let refLoc = {
                uri = p.textDocument.uri
                range = symbol.range
            }
            tryPacking p.textDocument.uri refLoc symbol packPositions [] 
        

// Provide the protocol appropriate header text, and converts message being written to client to UTF8
let private writeClient (client: BinaryWriter) (messageText: string) =
    let messageBytes = Encoding.UTF8.GetBytes messageText
    let headerText = sprintf "Content-Length: %d\r\n\r\n" messageBytes.Length
    let headerBytes = Encoding.UTF8.GetBytes headerText
    client.Write headerBytes
    client.Write messageBytes

[<EntryPoint>]
let main _ =
    let reader = new BinaryReader(Console.OpenStandardInput())
    let writer = new BinaryWriter(Console.OpenStandardOutput())
    let publishDiagnosticsMethod (u: Uri, p: Diagnostic[]) =
        {PublishDiagnosticsParams.uri = u; diagnostics = p} |> serializeDiagnostics |> writeClient writer
    let server = Server(publishDiagnosticsMethod) :> ILanguageServer
    try
        LanguageServer.connect server reader writer
    with e ->
        eprintfn "Exception in language server %s\n%s" e.Message e.StackTrace
    0