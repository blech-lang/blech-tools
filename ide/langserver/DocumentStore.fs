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


//=============================================================================
// This module manages the files that are known to the language server.
// The idea is that file changes (open, close, save, change, ...) that are made
// in the editor are sent via LSP to this server which replicates the changes
// in its document store.
// Then, the contents of the document store are sent to an instance of the
// compiler.
// This approach decouples the language server from the files on disk which may
// be edited while the language server is trying to read from them.
// At the same time this means that all open files are stored in RAM once
// more by the language server (a tolerable penalty).
//
// Adapted from 
// https://github.com/georgewfraser/fsharp-language-server/blob/master/src/LSP/DocumentStore.fs
//=============================================================================

module DocumentStore

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.RegularExpressions

open Blech.Common.SearchPath
open Blech.Frontend

open Types


//=============================================================================
// Private internal helper functions and types
//=============================================================================

let private dprintfn a = Printf.fprintfn stderr a

let private findRange(text: StringBuilder, range: Range): int * int = 
    let mutable line = 0
    let mutable char = 0
    let mutable startOffset = 0
    let mutable endOffset = 0
    for offset = 0 to text.Length do 
        if line = range.start.line && char = range.start.character then 
            startOffset <- offset 
        if line = range.``end``.line && char = range.``end``.character then 
            endOffset <- offset 
        if offset < text.Length then 
            let c = text.[offset]
            if c = '\n' then 
                line <- line + 1
                char <- 0
            else 
                char <- char + 1
    (startOffset, endOffset)


let rec private seekBackwards text offset counter =
    if counter < offset then
        if Char.IsLetterOrDigit (text.ToString().[offset - counter - 1]) then
            seekBackwards text offset (counter + 1)
        else
            counter
    else
        counter


let rec private seekForward (text: StringBuilder) offset counter =
    if counter + offset < text.Length then
        if Char.IsLetterOrDigit (text.ToString().[offset + counter]) then
            seekForward text offset (counter + 1)
        else
            counter
    else
        counter


// TODO: include type check context in versioning
// replace context with every successfull compilation run (onSave)
// look up type check info here when using F12 etc... 
// mind the possible inconsistency between the document text and the attached typecheck context (which is older),
// looking up of symbols will fail for modified lines, handle that case!
type private Version = {
    moduleName: ModuleName
    text: StringBuilder
    mutable ctx: TypeCheckContext option
    mutable version: int
}


let compareUris = 
    { new IEqualityComparer<Uri> with 
        member this.Equals(x, y) = 
            StringComparer.CurrentCulture.Equals(x, y)
        member this.GetHashCode(x) = 
            StringComparer.CurrentCulture.GetHashCode(x) }


/// All open documents, organized by absolute path
let private activeDocuments = new Dictionary<Uri, Version>(compareUris)
//let private activeDocuments = Dictionary<string, Version>()


/// Replace a section of an open file
let private patch (doc: VersionedTextDocumentIdentifier, range: Range, text: string): unit = 
    let existing = activeDocuments.[doc.uri]
    let startOffset, endOffset = findRange(existing.text, range)
    existing.text.Remove(startOffset, endOffset - startOffset) |> ignore
    existing.text.Insert(startOffset, text) |> ignore
    existing.version <- doc.version


/// Replace the entire contents of an open file
let private replace (doc: VersionedTextDocumentIdentifier, text: string): unit = 
    let existing = activeDocuments.[doc.uri]
    existing.text.Clear() |> ignore
    existing.text.Append(text) |> ignore
    existing.version <- doc.version


//=============================================================================
// Public interface functions
//=============================================================================

/// Deletes everything from the document store
let resetDocumentStore () = activeDocuments.Clear()
    

/// Replay given changes on the given file in the document store
let onChange (doc: DidChangeTextDocumentParams): unit = 
    let file = FileInfo(doc.textDocument.uri.LocalPath)
    let existing = activeDocuments.[doc.textDocument.uri]
    if doc.textDocument.version <= existing.version then 
        let oldVersion = existing.version
        let newVersion = doc.textDocument.version 
        dprintfn "Change %d to doc %s is earlier than existing version %d" newVersion file.Name oldVersion
    else 
        for change in doc.contentChanges do 
            match change.range with 
            | Some range -> patch(doc.textDocument, range, change.text) 
            | None -> replace(doc.textDocument, change.text) 


/// Removes given document from document store
let onClose (uri: Uri): unit = 
    activeDocuments.Remove(uri) |> ignore


/// Adds given document to document store
let onOpen (doc: TextDocumentItem): unit = 
    let text = StringBuilder(doc.text)
    let modName = 
        let modulePlusSuffix = System.IO.Path.GetFileName(doc.uri.LocalPath)
        [modulePlusSuffix.[..modulePlusSuffix.Length - 5]] // strip of ".blc"
    let version = {moduleName = modName; text = text; ctx = None; version = doc.version}
    activeDocuments.[doc.uri] <- version // this special syntax means:
                                               // if key not there, create it
                                               // otherwise update the value


let updateCtx (uri: Uri) ctx =
    activeDocuments.[uri].ctx <- Some ctx


let getCtx (uri: Uri) = activeDocuments.[uri].ctx


/// Given a URI return its contents and version
let tryGet file : option<ModuleName * string * int> = 
    let found, value = activeDocuments.TryGetValue(file)
    if found then Some(value.moduleName, value.text.ToString(), value.version) else None 


/// Get the URI for all files in this document store    
let getOpenFiles () = 
    [for file in activeDocuments.Keys do yield file]


/// Given a URI return its contents
let getText uri =
    tryGet uri 
    |> Option.map (fun(_,t,_)->t)


/// Given a URI return its version
let getVersion uri =
    tryGet uri
    |> Option.map (fun(_,_,v)->v)


/// Given a URI return its version
let getModule uri =
    tryGet uri
    |> Option.map (fun(m,_,_)->m)


/// Returns the symbol at the current cursor position which is encoded in a TextDocumentPositionParams
// replaces the symbolAt and lineContent magic
let getSymbol (p: TextDocumentPositionParams) =
    let existing = activeDocuments.[p.textDocument.uri].text
    let r = { start = p.position; ``end`` = p.position }
    let offset, _ = findRange (existing, r)
    //eprintf "offset: %d\n" offset
    let symbolStart = seekBackwards existing offset 0
    let symbolEnd = 
        let sE = seekForward existing offset 0
        if symbolStart + sE >= 1 then sE - 1 else sE
    let identifier = existing.ToString().[(offset - symbolStart)..(offset + symbolEnd)]
    let symbolRange =
        { start = { line = p.position.line; character = p.position.character - symbolStart }
          ``end`` = { line = p.position.line; character = p.position.character + symbolEnd } }
    { identifier = identifier; range = symbolRange }