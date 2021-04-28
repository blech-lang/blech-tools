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


let notFound (doc: Uri): 'Any = 
    raise (Exception (sprintf "%s does not exist" (doc.ToString())))
// Compile current uri and publish any existing errors
let validateTextDocument publishDiagnostics (uri: Uri) = 
    // check file name
    let uriSegments = uri.Segments
    let fileName = uriSegments.[uriSegments.Length-1] // without path to file
    let fileExt = TranslationUnitPath.implementationFileExtension.ToCharArray()
    let prefix = fileName.TrimEnd(fileExt)
    let fileNameDiagnostics = 
        if TranslationUnitPath.PathRegex.isValidFileOrDirectoryName prefix then
            seq{uri, [||]}
        else
            let lgr = Diagnostics.Logger.create()
            Diagnostics.Logger.logFatalError 
            <| lgr
            <| Diagnostics.Phase.Compiling
            <| CompilationUnit.IllegalModuleFileName (fileName, [fileName])
            packNewDiagnosticParameters lgr
    // check file contents
    let fileContentsDiagnostics =
        match getModule uri, getText uri with
        | Some modName, Some text ->
            compile uri modName text                
        | _ -> notFound uri
    Seq.concat [fileNameDiagnostics; fileContentsDiagnostics]
    |> Seq.iter (fun (u, da) -> publishDiagnostics (u, da))


let findQName (uri: Uri) (symbol: Symbol) tcContext =
    let fileName = uri.LocalPath
    let loc = { uri = uri              // Location of the symbol that 
                range = symbol.range } // command was called on
    findQName fileName loc symbol.identifier tcContext.ncEnv.GetLookupTable

let lookUpAction (p: TextDocumentPositionParams) posResAction negResAction =
    let uri = p.textDocument.uri
    let symbol = getSymbol p
    match getCtx uri with
    | Some ctx ->
        let tcCtx = getTCctxFromUri ctx uri
        match findQName uri symbol tcCtx with
        | Some symbolQName ->
            posResAction uri symbol ctx symbolQName
        | None -> negResAction
    | None -> negResAction

let hover (p: TextDocumentPositionParams) =
    let packHoverRes uri (symbol: Symbol) ctx symbolQName =
        let hover: Hover = {
            contents = {language = "blech"; value = findHoverData symbolQName ctx uri}
            range = symbol.range
        }
        Some hover
    lookUpAction p packHoverRes None


// if the symbol's declaration is in another module, it has been imported
//   in that case open ctx of submodule Implementation and look up data there
//   based on QNames
// otherwise look up position info in this context based on Name
let gotoDefinition (p: TextDocumentPositionParams) =
    let negResAction = None

    let uri = p.textDocument.uri
    let symbol = getSymbol p
    let name = lspSymbolToBlechName uri symbol
    match getCtx uri with
    | Some ctx ->
        let tcCtx = getTCctxFromUri ctx uri
        try
            let qname = name |> tcCtx.ncEnv.GetLookupTable.nameToQname
            let tcCtx = getTCctxFromTUP ctx qname.moduleName
            // we meed to search in the submodule's context using the qname because only that
            // is globally unique. Searching with a name is not enough because the id alone
            // is not unique and the range is pointing to the signature, not the implementation
            let findDecl = findInfoForQName tcCtx qname
            match findDecl with
            | Decl (Declarable.ParamDecl {pos=pos})
            | Decl (Declarable.VarDecl {pos=pos})
            | Decl (Declarable.ProcedureImpl {pos=pos})
            | Decl (Declarable.ProcedurePrototype {pos=pos}) 
            | Decl (Declarable.ExternalVarDecl {pos=pos}) ->
                Some {
                    uri = pathToUri pos.FileName
                    range = blechRange2LSPRange pos
                }
            | Usertype (pos,_) -> 
                Some {
                    uri = pathToUri pos.FileName
                    range = blechRange2LSPRange pos
                }
        with
        | _ ->
            // for names that have no QName representation and thus cannot be found above
            // for example local module identifiers, we resort to purely name based searching
            // within the open file
            try 
                let declName = tcCtx.ncEnv.GetLookupTable.getDeclName name
                Some <| blechNameToLspLocation declName
            with
            | _ -> 
                // happens when GotoDefinition was invoked on some symbol which is a Blech name
                negResAction
    | None -> negResAction
    
let findReferences (p: ReferenceParams) =
    //let packReferencesRes _ _ ctx symbolQName =
    //    findReferenceSources symbolQName p.textDocument.uri ctx 
    //lookUpAction {textDocument = p.textDocument; position = p.position} packReferencesRes []
    let negResAction = []
    
    let uri = p.textDocument.uri
    let symbol = getSymbol {textDocument = p.textDocument; position = p.position}
    let name = lspSymbolToBlechName uri symbol

    match getCtx uri with
    | Some ctx ->
        let tup = 
            getModule uri
            |> Option.get
        let tcCtx = getTCctxFromTUP ctx tup
        try
            let resPart1 =
                tcCtx.ncEnv.GetLookupTable.AllUsages name
                |> List.map blechNameToLspLocation
            try
                let qname = name |> tcCtx.ncEnv.GetLookupTable.nameToQname
                let resPart2 =
                    // only neccessary if moduleName is different from the open file's moduleName
                    if qname.moduleName <> tup then
                        let tcCtx = getTCctxFromTUP ctx qname.moduleName
                        let declarable = findInfoForQName tcCtx qname
                        match declarable with
                        | Decl (Declarable.ParamDecl {pos=pos; allReferences=allReferences})
                        | Decl (Declarable.VarDecl {pos=pos; allReferences=allReferences})
                        | Decl (Declarable.ExternalVarDecl {pos=pos; allReferences=allReferences})
                        | Decl (Declarable.ProcedureImpl {pos=pos; allReferences=allReferences})
                        | Decl (Declarable.ProcedurePrototype {pos=pos; allReferences=allReferences}) ->
                            convertBlechReferences pos allReferences uri
                        | Usertype (pos,_) ->
                            convertBlechReferences pos (System.Collections.Generic.HashSet<Range.range>()) uri
                    else
                        negResAction
                resPart2 @ resPart1
                |> List.distinct
            with
            | _ ->
                resPart1
        with
        | _ -> 
            // happens when findReferences was invoked on some symbol which is a Blech name
            negResAction
    | None -> negResAction
    

type Server(publishDiagnostics) = 
    do resetDocumentStore () // for good measure

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
            validateTextDocument publishDiagnostics p.textDocument.uri
        member this.DidChangeTextDocument(p: DidChangeTextDocumentParams): unit =
            onChange p
        member this.WillSaveTextDocument(p: WillSaveTextDocumentParams): unit = ()
        member this.DidSaveTextDocument(p: DidSaveTextDocumentParams): unit = 
            validateTextDocument publishDiagnostics p.textDocument.uri
        member this.DidCloseTextDocument(p: DidCloseTextDocumentParams): unit = 
            onClose p.textDocument.uri
            // Clear the errors for the document that was closed
            publishDiagnostics (p.textDocument.uri, [||])
        member this.DidChangeWatchedFiles(p: DidChangeWatchedFilesParams): unit = () 
        member this.GotoDefinition(p: TextDocumentPositionParams): option<Types.Location> = 
            gotoDefinition p
        member this.Hover(p: TextDocumentPositionParams): Hover option =
            hover p
        member this.FindReferences(p: ReferenceParams): list<Location> =
            findReferences p
            
        
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