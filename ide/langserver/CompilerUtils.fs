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

module CompilerUtils

open System
open System.Collections.Generic

open Blech.Compiler.Main

open Blech.Common
open Blech.Common.Range
open Blech.Common.PPrint
open Blech.Frontend
open Blech.Frontend.CommonTypes
open Blech.Frontend.BlechTypes
open Blech.Frontend.PrettyPrint.DocPrint

open Types
open DocumentStore


// Convert the URI from the client and to a file path that Blech compiler accepts
let parseCompilerUri (uri: Uri): string = 
    uri.AbsolutePath 
    |> (fun s -> if s.StartsWith @"\" then s.Substring(1) else s)
    //uri.AbsolutePath


let private pathToUri (path: string) =
    new Uri(new Uri("file://"), path)


let blechRange2LSPRange (r: range) = 
    { start = 
        { line = max 0 (r.StartLine-1)
          character = max 0 (r.StartColumn-1) }
      ``end`` = 
        { line = max 0 (r.EndLine-1)
          character = r.EndColumn } } // in Blech the interval is [..], in VS Code [..)
                                      // that is why we do NOT subtract 1 from EndColumn


let internal packNewDiagnosticParameters (logger: Diagnostics.Logger) =
    let blechContextInfo2LSPRelatedInfo (ctxList: Diagnostics.ContextInformation list) =
        let uri (ctx: Diagnostics.ContextInformation) = 
            try pathToUri ctx.range.FileName
            with _ as e ->
                Uri ""
        ctxList
        |> List.map (fun ctx -> {location = {uri = (uri ctx); range = blechRange2LSPRange ctx.range}; message = ctx.message})
        |> Array.ofList
    let blechDiag2LSPDiag (diag: Diagnostics.Diagnostic) =
        let mainRange = blechRange2LSPRange diag.main.range
        let severity =
            match diag.level with
            | Diagnostics.Level.Bug
            | Diagnostics.Level.Fatal
            | Diagnostics.Level.PhaseFatal
            | Diagnostics.Level.Error -> DiagnosticSeverity.Error
            | Diagnostics.Level.Warning -> DiagnosticSeverity.Warning
            | Diagnostics.Level.Note -> DiagnosticSeverity.Information
            | Diagnostics.Level.Help -> DiagnosticSeverity.Hint
        let sourceMessage =
            diag.phase.ToString()
        { range = mainRange
          severity = severity.ToString()
          code = None
          source = sourceMessage
          message = diag.main.message 
          relatedInformation = blechContextInfo2LSPRelatedInfo diag.context}
    Diagnostics.Emitter.getDiagnostics logger
    //|> Seq.groupBy (fun diag -> diag.main.range.FileName)
    |> Seq.map (fun diag -> pathToUri diag.main.range.FileName, [|blechDiag2LSPDiag diag|])
    


let compile (uri: Uri) moduleName fileContents =
    let inputFile = uri.LocalPath
    let projectDir = System.IO.Path.GetDirectoryName inputFile
    let outDir = System.IO.Path.Combine(projectDir, "blech")
    let cliArgs = {Arguments.BlechCOptions.Default with isDryRun = true; projectDir = projectDir; outDir = outDir}
    let logger = Diagnostics.Logger.create()   
    
    let noImportChain = CompilationUnit.ImportChain.Empty
    
    let pkgCtx = CompilationUnit.Context.Make cliArgs (loader cliArgs)
    compileFromStr cliArgs pkgCtx logger noImportChain moduleName inputFile fileContents
    |> function
        | Error logger -> 
            let errImps = List.map snd pkgCtx.GetErrorImports
            let loggers = logger :: errImps
            loggers
            |> Seq.map packNewDiagnosticParameters
            |> Seq.concat
        | Ok modinfo ->
            updateCtx uri modinfo.typeCheck
            Seq.empty
    

let findQName fileName (loc: Types.Location) (ident: Identifier) (lut: SymbolTable.LookupTable): QName option =
    try
        let fileIndex = Range.fileIndexOfFile fileName
        let identPos = Range.range (fileIndex, loc.range.start.line + 1, loc.range.start.character + 1, loc.range.``end``.line + 1, loc.range.``end``.character + 1)
        let name =
            Blech.Frontend.SyntaxUtils.ParserUtils.ParserContext.mkFakeName ident identPos
        name
        |> lut.nameToQname
        |> Some
    with
    | e -> 
        None

type DeclOrType =
    | Decl of Declarable
    | Usertype of range * BlechTypes.Types

let findInfoForQName (tcContext: TypeCheckContext) (qname: QName) =
    if tcContext.nameToDecl.ContainsKey qname then
        Decl tcContext.nameToDecl.[qname]
    elif tcContext.userTypes.ContainsKey qname then
        Usertype tcContext.userTypes.[qname]
    else // nothing found
        sprintf "Given qname %s is neither a Declarable nor a user defined type.\n" (qname.ToString())
        |> failwith 


// Find the range for the definition of an identifier
let findDefinition (tcContext: TypeCheckContext) (qname: QName): (Uri * Range) =
    let findDecl = findInfoForQName tcContext qname
    match findDecl with
    | Decl (Declarable.ParamDecl {pos=pos})
    | Decl (Declarable.VarDecl {pos=pos})
    | Decl (Declarable.ProcedureImpl {pos=pos})
    | Decl (Declarable.ProcedurePrototype {pos=pos}) 
    | Decl (Declarable.ExternalVarDecl {pos=pos}) ->
        pathToUri pos.FileName, blechRange2LSPRange pos // packRange (pos.Start.Line, pos.Start.Column, pos.End.Line, pos.End.Column)
    | Usertype (pos,_) -> 
        pathToUri pos.FileName, blechRange2LSPRange pos


let findHoverData (qname: QName) (tcContext: TypeCheckContext) uri : string = 
    let printSubDecl (prot: ProcedurePrototype) = 
        // this is almost a copy of BlechTypes.ProcedurePrototype.ToDoc, just the name rendering is different
        let annotationDoc = prot.annotation.ToDoc
        let returns = prot.returns
        let printParam (p: ParamDecl) =
            txt (p.name.basicId.ToString()) <^> txt ":" <+> p.datatype.ToDoc
        let ins = prot.inputs |> List.map printParam |> dpCommaSeparatedInParens
        let outs = prot.outputs|> List.map printParam |> dpCommaSeparatedInParens
        let spdoc = 
            if prot.IsSingleton then txt "singleton" else empty
            <+> prot.kind.ToDoc
            <+> txt (prot.name.basicId.ToString())
            <^> ( ins
                  <..> outs
                  <.> match returns with | ValueTypes.Void -> empty | _ -> txt "returns" <+> returns.ToDoc
                  |> align
                  |> group )
        annotationDoc |> dpToplevelClose <.> spdoc
        |> render None
    let findHover = findInfoForQName tcContext qname
    match findHover with
    | Decl (Declarable.ParamDecl param) ->
        let perm = if param.isMutable then "var" else "let"
        sprintf "%s %s: %s" perm param.name.basicId (param.datatype.ToString())
    | Decl (Declarable.VarDecl var) -> 
        match var.mutability with
        | Mutability.Variable ->
            sprintf "%s %s: %s" "var" var.name.basicId (var.datatype.ToString())
        | Mutability.Immutable ->
            sprintf "%s %s: %s" "let" var.name.basicId (var.datatype.ToString())
        | Mutability.CompileTimeConstant ->
            sprintf "%s %s: %s = %s" "const" var.name.basicId (var.datatype.ToString()) (var.initValue.ToString())
        | Mutability.StaticParameter ->
            sprintf "%s %s: %s = %s" "param" var.name.basicId (var.datatype.ToString()) (var.initValue.ToString())
    | Decl (Declarable.ExternalVarDecl var) ->
        match var.mutability with
        | Mutability.Variable ->
            sprintf "%s %s: %s" "var" var.name.basicId (var.datatype.ToString())
        | Mutability.Immutable ->
            sprintf "%s %s: %s" "let" var.name.basicId (var.datatype.ToString())
        | Mutability.CompileTimeConstant ->
            sprintf "%s %s: %s" "const" var.name.basicId (var.datatype.ToString())
        | Mutability.StaticParameter ->
            sprintf "%s %s: %s" "param" var.name.basicId (var.datatype.ToString())
    | Decl (Declarable.ProcedureImpl sub) ->
        printSubDecl sub.prototype 
    | Decl (Declarable.ProcedurePrototype fp) ->
        printSubDecl fp 
    | Usertype (_,t) -> t.ToString()


// Converts a HashSet of source positions provided by the compiler (allReferences) and appends the declaration location to a list of locations for the LSP
let convertBlechReferences (pos: range) (usagePositions: HashSet<range>) (uri: Uri): list<Types.Location> =
    let mkLocFromPos (p: range) =
        { uri = uri
          range = blechRange2LSPRange p }
    let locList = 
        usagePositions
        |> Seq.map mkLocFromPos
        |> Seq.toList
    let declLoc = mkLocFromPos pos
    declLoc :: locList


// Find a list of identifiers in the current uri using the QName and current Type Checking Context
let findReferenceSources (qname: QName) (uri: Uri) (tcContext: TypeCheckContext) : list<Types.Location> = 
    let declarable = findInfoForQName tcContext qname
    match declarable with
    | Decl (Declarable.ParamDecl {pos=pos; allReferences=allReferences})
    | Decl (Declarable.VarDecl {pos=pos; allReferences=allReferences})
    | Decl (Declarable.ExternalVarDecl {pos=pos; allReferences=allReferences})
    | Decl (Declarable.ProcedureImpl {pos=pos; allReferences=allReferences})
    | Decl (Declarable.ProcedurePrototype {pos=pos; allReferences=allReferences}) ->
        convertBlechReferences pos allReferences uri
    | Usertype (pos,_) ->
        convertBlechReferences pos (HashSet<range>()) uri