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

module Serializers

open System.IO
open System.Runtime.Serialization.Json

open Types


let serializeToJson<'a> (x : 'a) = 
    let jsonSerializer = new DataContractJsonSerializer(typedefof<'a>)
    use stream = new MemoryStream()
    jsonSerializer.WriteObject(stream, x)
    stream.ToArray()
    |> System.Text.Encoding.ASCII.GetString


let serializeDiagnostics (parameters: PublishDiagnosticsParams): string = 
    let finalPublishNotification = {JsonRpc = "2.0"; Method = "textDocument/publishDiagnostics"; Params = parameters}
    serializeToJson finalPublishNotification


let serializeGoToDefinition (location: option<Types.Location>): string = 
    match location with
    | None ->
        serializeToJson None
    | Some locationVal ->
        serializeToJson locationVal


let serializeHover (hover: option<Hover>): string =
    match hover with
    | None ->
        serializeToJson None
    | Some hoverVal ->
        serializeToJson hoverVal


let serializeReferences (locList: list<Types.Location>): string =
    serializeToJson (List.toArray(locList))


let serializeInitializeResponse (init: InitializeResult): string =
    serializeToJson init