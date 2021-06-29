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
open Blech.Frontend.DocPrint

open Types
open DocumentStore


let pathToUri (path: string) =
    new Uri(new Uri("file://"), path)


let blechRange2LSPRange (r: range) = 
    { start = 
        { line = max 0 (r.StartLine-1)
          character = max 0 (r.StartColumn-1) }
      ``end`` = 
        { line = max 0 (r.EndLine-1)
          character = r.EndColumn } } // in Blech the interval is [..], in VS Code [..)
                                      // that is why we do NOT subtract 1 from EndColumn


let lspRangeToBlechRange (uri: Uri) (r: Range) =
    let fileIndex = Range.fileIndexOfFile uri.LocalPath
    Range.range ( fileIndex, 
                  r.start.line + 1, 
                  r.start.character + 1, 
                  r.``end``.line + 1, 
                  r.``end``.character + 1 ) // Blech ranges start at 1 and include the last char


let lspSymbolToBlechName uri (s: Symbol) =
    Blech.Frontend.SyntaxUtils.ParserUtils.ParserContext.mkFakeName 
    <| s.identifier 
    <| lspRangeToBlechRange uri s.range


let blechNameToLspLocation (name: Name) =
    {
        uri = pathToUri name.Range.FileName
        range = blechRange2LSPRange name.Range
    }
    

let getTCctxFromTUP (ctx: CompilationUnit.Context<ImportChecking.ModuleInfo>) tup =
    let moduleInfo: ImportChecking.ModuleInfo =
        match ctx.loaded.TryGetValue (CompilationUnit.Implementation tup) with
        | true, Ok modInfoRes -> modInfoRes.info
        | _, Error _ -> failwithf "Found implementation for %s but it is errornous." <| tup.ToString()
        | false, _ ->
            match ctx.loaded.TryGetValue (CompilationUnit.Interface tup) with
            | true, Ok modInfoRes -> modInfoRes.info
            | _, Error _ -> failwithf "Found interface for %s but it is errornous." <| tup.ToString()
            | false, _ -> failwithf "Neither an implementation nor an interface exists for %s in the loaded context." <| tup.ToString()
    moduleInfo.typeCheck


let getTCctxFromUri (ctx: CompilationUnit.Context<ImportChecking.ModuleInfo>) uri =
    let tup = 
        getModule uri
        |> Option.get
    getTCctxFromTUP ctx tup
          
          
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
            // TODO 1: this could also be an interface
            let compilationModule = CompilationUnit.Module<_>.Make moduleName inputFile modinfo
            pkgCtx.loaded.Add(CompilationUnit.Implementation moduleName, compilationModule) // only imported Modules are added to the loaded dict automatically
            updateCtx uri pkgCtx 
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


let findHoverData (qname: QName) (ctx: CompilationUnit.Context<ImportChecking.ModuleInfo>) uri : string =
    let tcContext = getTCctxFromTUP ctx qname.moduleName
    let printSubDecl (prot: ProcedurePrototype) = 
        // this is almost a copy of BlechTypes.ProcedurePrototype.ToDoc, just the name rendering is different
        let annotationDoc = 
            match prot.annotation.ToDoc with
            | [] -> empty
            | lst -> dpToplevelClose lst <.> empty
        let returns = prot.returns
        let printParam (p: ParamDecl) =
            txt (p.name.basicId.ToString()) <^> txt ":" <+> p.datatype.ToDoc
        let ins = prot.inputs |> List.map printParam |> dpCommaSeparatedInParens
        let outs = prot.outputs|> List.map printParam |> dpCommaSeparatedInParens
        let spdoc = 
            if prot.IsSingleton then txt "singleton" <+> empty else empty
            <^> prot.kind.ToDoc
            <+> txt (prot.name.basicId.ToString())
            <^> ( ins
                  <..> outs
                  <.> match returns with | ValueTypes.Void -> empty | _ -> txt "returns" <+> returns.ToDoc
                  |> align
                  |> group )
        annotationDoc <^> spdoc
        |> render None
    try
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
    with
    | _ -> ""


// Converts a HashSet of source positions provided by the compiler (allReferences) and appends the declaration location to a list of locations for the LSP
let convertBlechReferences (pos: range) (usagePositions: HashSet<range>): list<Types.Location> =
    let mkLocFromPos (p: range) =
        { uri = pathToUri p.FileName
          range = blechRange2LSPRange p }
    let locList = 
        usagePositions
        |> Seq.map mkLocFromPos
        |> Seq.toList
    let declLoc = mkLocFromPos pos
    declLoc :: locList


let lookUpAction2 (p: TextDocumentPositionParams) action1 action2 combineResults negResAction =
    let uri = p.textDocument.uri
    let symbol = getSymbol p
    let name = lspSymbolToBlechName uri symbol

    match getCtx uri with
    | Some ctx ->
        let tup = getModule uri |> Option.get
        let tcCtx = getTCctxFromTUP ctx tup
        try
            let resAction1 = action1 tcCtx name
            try
                let qname = name |> tcCtx.ncEnv.GetLookupTable.nameToQname
                let resAction2 =
                    // only neccessary if moduleName is different from the open file's moduleName
                    if qname.moduleName <> tup then
                        let tcCtx = getTCctxFromTUP ctx qname.moduleName
                        action2 tcCtx qname
                    else
                        negResAction
                combineResults resAction1 resAction2
            with
            | _ ->
                resAction1
        with
        | _ -> 
            // happens when invoked on some symbol which is not a Blech name

            // could be the case that it is an import module path/
            // assuming this, try to construct a translation unit path from that string/
            // if successful, check it is among the imported modules
            // if so, return position 0,0 in that file
            match TranslationUnitPath.makeFromPath tup symbol.identifier with
            | Error _ -> negResAction
            | Ok potentialImportPath ->
                let moduleInfo: ImportChecking.ModuleInfo =
                    match ctx.loaded.TryGetValue (CompilationUnit.Implementation tup) with
                    | true, Ok modInfoRes -> modInfoRes.info
                    | _, Error _ -> failwithf "Found implementation for %s but it is errornous." <| tup.ToString()
                    | false, _ ->
                        match ctx.loaded.TryGetValue (CompilationUnit.Interface tup) with
                        | true, Ok modInfoRes -> modInfoRes.info
                        | _, Error _ -> failwithf "Found interface for %s but it is errornous." <| tup.ToString()
                        | false, _ -> failwithf "Neither an implementation nor an interface exists for %s in the loaded context." <| tup.ToString()
                moduleInfo.dependsOn 
                |> List.contains potentialImportPath
                |> function
                    | false -> negResAction
                    | true -> 
                        let blcFile = TranslationUnitPath.searchImplementation ctx.projectDir potentialImportPath
                        match blcFile with
                        | Ok blc ->
                            [{
                                uri = pathToUri blc
                                range = blechRange2LSPRange Range.range0
                            }]
                        | _ ->
                            let blhFile = TranslationUnitPath.searchInterface ctx.blechPath potentialImportPath
                            match blhFile with
                            | Ok blh ->
                                [{
                                    uri = pathToUri blh
                                    range = blechRange2LSPRange Range.range0
                                }]
                            | _ -> negResAction
    | None -> 
        // happens if no successful compilation run has taken place and no ctx was saved
        negResAction


let gotoDefinition (p: TextDocumentPositionParams) =
    let negResAction = []
    let action1 tcCtx name =
        // look up definition based on names within open file
        tcCtx.ncEnv.GetLookupTable.getDeclName name
        |> blechNameToLspLocation
        |> List.singleton
    let action2 tcCtx qname =
        // look up definition in a submodule found by constucting the QName
        let findDecl = findInfoForQName tcCtx qname
        match findDecl with
        | Decl (Declarable.ParamDecl {pos=pos})
        | Decl (Declarable.VarDecl {pos=pos})
        | Decl (Declarable.ProcedureImpl {pos=pos})
        | Decl (Declarable.ProcedurePrototype {pos=pos}) 
        | Decl (Declarable.ExternalVarDecl {pos=pos}) ->
            [{
                uri = pathToUri pos.FileName
                range = blechRange2LSPRange pos
            }]
        | Usertype (pos,_) -> 
            [{
                uri = pathToUri pos.FileName
                range = blechRange2LSPRange pos
            }]
    let combineResults res1 res2 =
        // prefer definition found via QName in submodule 
        if res2 = negResAction then res1 else res2

    lookUpAction2 
    <| p
    <| action1
    <| action2 
    <| combineResults 
    <| negResAction

    
let findReferences (p: ReferenceParams) =
    let negResAction = []
    let action1 tcCtx name =
        tcCtx.ncEnv.GetLookupTable.AllUsages name
        |> List.map blechNameToLspLocation
    let action2 tcCtx qname =
        let declarable = findInfoForQName tcCtx qname
        match declarable with
        | Decl (Declarable.ParamDecl {pos=pos; allReferences=allReferences})
        | Decl (Declarable.VarDecl {pos=pos; allReferences=allReferences})
        | Decl (Declarable.ExternalVarDecl {pos=pos; allReferences=allReferences})
        | Decl (Declarable.ProcedureImpl {pos=pos; allReferences=allReferences})
        | Decl (Declarable.ProcedurePrototype {pos=pos; allReferences=allReferences}) ->
            convertBlechReferences pos allReferences 
        | Usertype (pos,_) ->
            convertBlechReferences pos (System.Collections.Generic.HashSet<Range.range>()) 
    let combineResults res1 res2 =
        res2 @ res1 |> List.distinct

    lookUpAction2 
    <| {textDocument = p.textDocument; position = p.position} 
    <| action1
    <| action2 
    <| combineResults 
    <| negResAction