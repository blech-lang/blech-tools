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

module Tokenizer

open System
open System.IO
open System.Text

type Header = ContentLength of int | EmptyHeader | OtherHeader

let parseHeader(header: string): Header = 
    let contentLength = "Content-Length: "
    if header.StartsWith(contentLength) then
        let tail = header.Substring(contentLength.Length)
        let length = Int32.Parse(tail) 
        ContentLength(length)
    elif header = "" then EmptyHeader
    else OtherHeader

let rec private eatWhitespace(client: BinaryReader): char = 
    let c = client.ReadChar()
    if Char.IsWhiteSpace(c) then 
        eatWhitespace(client) 
    else 
        c

let readLength(byteLength: int, client: BinaryReader): string = 
    // Somehow, we are getting extra \r\n sequences, only when we compile to a standalone executable
    let head = eatWhitespace(client)
    let tail = client.ReadBytes(byteLength - 1)
    let string = Encoding.UTF8.GetString(tail)
    Convert.ToString(head) + string
  
let readLine(client: BinaryReader): string option = 
    let buffer = StringBuilder()
    try
        let mutable endOfLine = false
        while not endOfLine do 
            let nextChar = client.ReadChar()
            if nextChar = '\n' then do 
                endOfLine <- true
            elif nextChar = '\r' then do 
                assert(client.ReadChar() = '\n')
                endOfLine <- true
            else do 
                buffer.Append(nextChar) |> ignore
        Some(buffer.ToString())
    with 
    | :? EndOfStreamException -> 
        if buffer.Length > 0 then
            Some(buffer.ToString())
        else
            None

let tokenize(client: BinaryReader): seq<string> = 
    seq {
        let mutable contentLength = -1
        let mutable endOfInput = false
        while not endOfInput do 
            let maybeHeader = readLine(client)
            let next = Option.map parseHeader maybeHeader
            match next with 
                | None -> endOfInput <- true 
                | Some(ContentLength l) -> contentLength <- l 
                | Some(EmptyHeader) -> yield readLength(contentLength, client)
                | _ -> ()
    }
