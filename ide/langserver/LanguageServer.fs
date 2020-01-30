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

module LanguageServer

open System.IO
open System.Text

open Types  
open Serializers


// Provide the protocol appropriate header text, and converts message being written to client to UTF8
let private writeClient (client: BinaryWriter) (messageText: string) =
    let messageBytes = Encoding.UTF8.GetBytes messageText
    let headerText = sprintf "Content-Length: %d\r\n\r\n" messageBytes.Length
    let headerBytes = Encoding.UTF8.GetBytes headerText
    client.Write headerBytes
    client.Write messageBytes


// Format a response message to the client using the request ID
let respond (client: BinaryWriter) (requestId: int) (jsonText: string) =
    let messageText = sprintf """{"id":%d,"jsonrpc":"2.0","result":%s}""" requestId jsonText
    writeClient client messageText


let processRequest (server: ILanguageServer) (send: BinaryWriter) (id: int) (request: Request) = 
    match request with 
    | Initialize p ->
        server.Initialize p |> serializeInitializeResponse |> respond send id
    | GotoDefinition p -> 
        server.GotoDefinition p |> serializeGoToDefinition |> respond send id
    | Hover p -> 
        server.Hover p |> serializeHover |> respond send id
    | FindReferences p -> 
        server.FindReferences p |> serializeReferences |> respond send id
    | _ ->
        eprintfn "Unknown Request: %A" request
    

let processNotification (server: ILanguageServer) (send: BinaryWriter) (n: Notification) = 
    match n with 
    | Cancel id -> eprintfn "Cancel request %d is not yet supported" id
    | Initialized -> server.Initialized()
    | Shutdown -> server.Shutdown()
    | DidChangeConfiguration p -> server.DidChangeConfiguration p
    | DidOpenTextDocument p -> server.DidOpenTextDocument p
    | DidChangeTextDocument p -> server.DidChangeTextDocument p
    | WillSaveTextDocument p -> server.WillSaveTextDocument p 
    | DidSaveTextDocument p -> server.DidSaveTextDocument p
    | DidCloseTextDocument p -> server.DidCloseTextDocument p
    | DidChangeWatchedFiles p -> server.DidChangeWatchedFiles p
    | OtherNotification _ -> ()


let processMessage (server: ILanguageServer) (send: BinaryWriter) (m: Parser.Message) = 
    match m with 
    | Parser.RequestMessage (id, method, json) -> 
        processRequest server send id (Parser.parseRequest method json) 
    | Parser.NotificationMessage (method, json) -> 
        processNotification server send (Parser.parseNotification method json)


let private notExit (message: Parser.Message) =
    match message with 
    | Parser.NotificationMessage ("exit", _) -> false 
    | _ -> true


let readMessages (receive: BinaryReader): seq<Parser.Message> =
    Tokenizer.tokenize receive |> Seq.map Parser.parseMessage |> Seq.takeWhile notExit


type RealClient (send: BinaryWriter) =
    interface ILanguageClient with
        member this.PublishDiagnostics (p: PublishDiagnosticsParams): unit = 
            p |> serializeDiagnostics |> writeClient send


let connect (server: ILanguageServer) (receive: BinaryReader) (send: BinaryWriter) = 
    let doProcessMessage = processMessage server send
    readMessages receive |> Seq.iter doProcessMessage