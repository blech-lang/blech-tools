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

module Types 

open FSharp.Data
open System.Runtime.Serialization

open System


type SourceKind = 
| Compile
| Parse
| Name
| Signature
| Type
| Causality
| Code

let writeSourceKind (i: SourceKind) = 
    match i with
    | Compile -> "compiling"
    | Name -> "name resolution"
    | Signature -> "signature inference"
    | Parse -> "parsing"
    | Type -> "typing"
    | Causality -> "causality"
    | Code -> "code generation"

[<DataContract>]
type Position = 
    {
        [<field   : DataMember(Name="line")>]
        line      : int

        [<field   : DataMember(Name="character")>]
        character : int
    }    
[<DataContract>]
type Range = 
    {
        [<field : DataMember(Name="start")>]
        start   : Position

        [<field : DataMember(Name="end")>]
        ``end``    : Position
    }

[<DataContract>]
type Location = 
    {
        [<field : DataMember(Name="uri", Order=1)>]
        uri     : Uri;

        [<field : DataMember(Name="range", Order=2)>]
        range   : Range;
    }

[<RequireQualifiedAccess>]
type DiagnosticSeverity = 
    | Error 
    | Warning 
    | Information 
    | Hint
    override this.ToString() =
        match this with 
        | DiagnosticSeverity.Error -> "1" 
        | DiagnosticSeverity.Warning -> "2"
        | DiagnosticSeverity.Information -> "3"
        | DiagnosticSeverity.Hint -> "4"

let writeDiagnosticSeverity i = 
    match i with 
    | DiagnosticSeverity.Error -> 1 
    | DiagnosticSeverity.Warning -> 2
    | DiagnosticSeverity.Information -> 3
    | DiagnosticSeverity.Hint -> 4

[<DataContract>]
type MarkedString = 
    {
        [<field : DataMember(Name="language")>]
        language: string;

        [<field : DataMember(Name="value")>]
        value: string;
    }

[<DataContract>]
type Hover = 
    {
        [<field : DataMember(Name="contents")>]
        contents : MarkedString;

        [<field : DataMember(Name="range")>]
        range : Range;
    }
    
type Symbol = {
    identifier: string
    range: Range
}

type DidChangeConfigurationParams = {
    settings: JsonValue
}

type TextDocumentItem = {
    uri: Uri 
    languageId: string 
    version: int 
    text: string
}

type DidOpenTextDocumentParams = {
    textDocument: TextDocumentItem
}

type VersionedTextDocumentIdentifier = {
    uri: Uri 
    version: int 
}

type TextDocumentContentChangeEvent = {
    range: option<Range>
    rangeLength: option<int>
    text: string
}

type DidChangeTextDocumentParams = {
    textDocument: VersionedTextDocumentIdentifier
    contentChanges: list<TextDocumentContentChangeEvent>
}

type TextDocumentIdentifier = {
    uri: Uri
}

[<RequireQualifiedAccess>]
type TextDocumentSaveReason = 
    | Manual
    | AfterDelay
    | FocusOut

type WillSaveTextDocumentParams = {
    textDocument: TextDocumentIdentifier
    reason: TextDocumentSaveReason
}

type DidSaveTextDocumentParams = {
    textDocument: TextDocumentIdentifier
    text: option<string>
}

type DidCloseTextDocumentParams = {
    textDocument: TextDocumentIdentifier
}

[<RequireQualifiedAccess>]
type FileChangeType = 
| Created
| Changed 
| Deleted

type FileEvent = {
    uri: Uri 
    _type: FileChangeType
}

type DidChangeWatchedFilesParams = {
    changes: list<FileEvent>
}
    
type Notification = 
    | Cancel of id: int 
    | Initialized
    | Shutdown 
    | DidChangeConfiguration of DidChangeConfigurationParams
    | DidOpenTextDocument of DidOpenTextDocumentParams
    | DidChangeTextDocument of DidChangeTextDocumentParams
    | WillSaveTextDocument of WillSaveTextDocumentParams
    | DidSaveTextDocument of DidSaveTextDocumentParams
    | DidCloseTextDocument of DidCloseTextDocumentParams
    | DidChangeWatchedFiles of DidChangeWatchedFilesParams
    | OtherNotification of method: string

[<DataContract>]
type DiagnosticRelatedInformation = {
    [<field : DataMember(Name="location")>]
    location: Location;
    [<field : DataMember(Name="message")>]
    message: string;
}

[<DataContract>]
type Diagnostic = {
    [<field : DataMember(Name="range")>]
    range: Range
    [<field : DataMember(Name="severity")>]
    severity: string
    [<field : DataMember(Name="code")>]
    code: string option
    [<field : DataMember(Name="source")>]
    source: string
    [<field : DataMember(Name="message")>]
    message: string;
    [<field : DataMember(Name="relatedInformation")>]
    relatedInformation: DiagnosticRelatedInformation[];
}

[<DataContract>]
type PublishDiagnosticsParams = {
    [<field : DataMember(Name="uri")>]
    uri: Uri
    [<field : DataMember(Name="diagnostics")>]
    diagnostics: Diagnostic[]
}

[<DataContract>]
type DiagnosticNotification =
    {
        [<field : DataMember(Name="jsonrpc")>]
        JsonRpc : string;

        [<field : DataMember(Name="method")>]
        Method  : string;

        [<field : DataMember(Name="params")>]
        Params  : PublishDiagnosticsParams;
    }

type Command = {
    title: string
    command: string 
    arguments: list<JsonValue>
}

type TextEdit = {
    range: Range 
    newText: string
}

type TextDocumentEdit = {
    textDocument: VersionedTextDocumentIdentifier
    edits: list<TextEdit>
}

type WorkspaceEdit = {
    documentChanges: list<TextDocumentEdit>
}

type TextDocumentPositionParams = {
    textDocument: TextDocumentIdentifier
    position: Position
}

type DocumentFilter = {
    language: string 
    scheme: string 
    pattern: string
}

type DocumentSelector = list<DocumentFilter>

[<RequireQualifiedAccess>]
type Trace = 
    | Off 
    | Messages 
    | Verbose

type InitializeParams = {
    processId: option<int>
    rootUri: option<Uri>
    initializationOptions: option<JsonValue>
    capabilitiesMap: Map<string, bool>
    trace: option<Trace>
}

let defaultInitializeParams: InitializeParams = {
    processId = None 
    rootUri = None 
    initializationOptions = None 
    capabilitiesMap = Map.empty 
    trace = None
}

[<RequireQualifiedAccess>]
type InsertTextFormat = 
    | PlainText 
    | Snippet 

let writeInsertTextFormat (i: InsertTextFormat) = 
    match i with 
    | InsertTextFormat.PlainText -> 1
    | InsertTextFormat.Snippet -> 2

[<RequireQualifiedAccess>]
type CompletionItemKind = 
    | Text
    | Method
    | Function
    | Constructor
    | Field
    | Variable
    | Class
    | Interface
    | Module
    | Property
    | Unit
    | Value
    | Enum
    | Keyword
    | Snippet
    | Color
    | File
    | Reference

let writeCompletionItemKind (i: CompletionItemKind) = 
    match i with 
    | CompletionItemKind.Text -> 1
    | CompletionItemKind.Method -> 2
    | CompletionItemKind.Function -> 3
    | CompletionItemKind.Constructor -> 4
    | CompletionItemKind.Field -> 5
    | CompletionItemKind.Variable -> 6
    | CompletionItemKind.Class -> 7
    | CompletionItemKind.Interface -> 8
    | CompletionItemKind.Module -> 9
    | CompletionItemKind.Property -> 10
    | CompletionItemKind.Unit -> 11
    | CompletionItemKind.Value -> 12
    | CompletionItemKind.Enum -> 13
    | CompletionItemKind.Keyword -> 14
    | CompletionItemKind.Snippet -> 15
    | CompletionItemKind.Color -> 16
    | CompletionItemKind.File -> 17
    | CompletionItemKind.Reference -> 18

type CompletionItem = {
    label: string 
    kind: option<CompletionItemKind>
    detail: option<string>
    documentation: option<string>
    sortText: option<string>
    filterText: option<string>
    insertText: option<string>
    insertTextFormat: option<InsertTextFormat>
    textEdit: option<TextEdit>
    additionalTextEdits: list<TextEdit>
    commitCharacters: list<char>
    command: option<Command>
    data: JsonValue
}

type ReferenceContext = {
    includeDeclaration: bool
}

type ReferenceParams = {
    textDocument: TextDocumentIdentifier
    position: Position
    context: ReferenceContext
}

type DocumentSymbolParams = {
    textDocument: TextDocumentIdentifier
}

type WorkspaceSymbolParams = {
    query: string
}

type CodeActionContext = {
    diagnostics: list<Diagnostic>
}

type CodeActionParams = {
    textDocument: TextDocumentIdentifier
    range: Range
    context: CodeActionContext
}

type CodeLensParams = {
    textDocument: TextDocumentIdentifier
}

type CodeLens = {
    range: Range 
    command: option<Command>
    data: JsonValue
}

type DocumentLinkParams = {
    textDocument: TextDocumentIdentifier
}

type DocumentLink = {
    range: Range 
    target: option<Uri>
}

type DocumentFormattingOptions = {
    tabSize: int 
    insertSpaces: bool 
}

type DocumentFormattingParams = {
    textDocument: TextDocumentIdentifier
    options: DocumentFormattingOptions
    optionsMap: Map<string, string>
}

type DocumentRangeFormattingParams = {
    textDocument: TextDocumentIdentifier
    options: DocumentFormattingOptions
    optionsMap: Map<string, string>
    range: Range
}

type DocumentOnTypeFormattingParams = {
    textDocument: TextDocumentIdentifier
    options: DocumentFormattingOptions
    optionsMap: Map<string, string>
    position: Position
    ch: char 
}

type RenameParams = {
    textDocument: TextDocumentIdentifier
    position: Position
    newName: string
}

type ExecuteCommandParams = {
    command: string 
    arguments: list<JsonValue>
}

type Request = 
    | Initialize of InitializeParams
    | WillSaveWaitUntilTextDocument of WillSaveTextDocumentParams
    | Completion of TextDocumentPositionParams
    | Hover of TextDocumentPositionParams
    | ResolveCompletionItem of CompletionItem
    | SignatureHelp of TextDocumentPositionParams
    | GotoDefinition of TextDocumentPositionParams
    | FindReferences of ReferenceParams
    | DocumentHighlight of TextDocumentPositionParams
    | DocumentSymbols of DocumentSymbolParams
    | WorkspaceSymbols of WorkspaceSymbolParams
    | CodeActions of CodeActionParams
    | CodeLens of CodeLensParams
    | ResolveCodeLens of CodeLens
    | DocumentLink of DocumentLinkParams
    | ResolveDocumentLink of DocumentLink
    | DocumentFormatting of DocumentFormattingParams
    | DocumentRangeFormatting of DocumentRangeFormattingParams
    | DocumentOnTypeFormatting of DocumentOnTypeFormattingParams
    | Rename of RenameParams
    | ExecuteCommand of ExecuteCommandParams

[<RequireQualifiedAccess>]
type TextDocumentSyncKind = 
    | None 
    | Full
    | Incremental

let writeTextDocumentSyncKind (i: TextDocumentSyncKind) = 
    match i with 
    | TextDocumentSyncKind.None -> 0
    | TextDocumentSyncKind.Full -> 1
    | TextDocumentSyncKind.Incremental -> 2

type CompletionOptions = {
    resolveProvider: bool 
    triggerCharacters: list<char>
}

let defaultCompletionOptions = {
    resolveProvider = false 
    triggerCharacters = ['.']
}

type SignatureHelpOptions = {
    triggerCharacters: list<char>
}

let defaultSignatureHelpOptions = {
    triggerCharacters = ['('; ',']
}

type CodeLensOptions = {
    resolveProvider: bool  
}

let defaultCodeLensOptions = {
    resolveProvider = false
}

type DocumentOnTypeFormattingOptions = {
    firstTriggerCharacter: char
    moreTriggerCharacter: list<char>
}

type DocumentLinkOptions = {
    resolveProvider: bool
}

let defaultDocumentLinkOptions = {
    resolveProvider = false
}

type ExecuteCommandOptions = {
    commands: list<string>
}

[<DataContract>]
type SaveOptions = {
    [<field: DataMember(Name="includeText")>]
    includeText: bool
}

[<DataContract>]
type TextDocumentSyncOptions = {
    [<field: DataMember(Name="openClose", Order=1)>]
    openClose: bool

    [<field: DataMember(Name="change", Order=2)>]
    change: int //TextDocumentSyncKind //hack as of 05.11.18 to try and render the change field quick and dirty

    [<field: DataMember(Name="willSave", Order=3)>]
    willSave: bool

    [<field: DataMember(Name="willSaveWaitUntil", Order=4)>]
    willSaveWaitUntil: bool

    [<field: DataMember(Name="save", Order=5)>]
    save: SaveOptions
}

let defaultTextDocumentSyncOptions = {
    openClose = false
    change = 0 //TextDocumentSyncKind.None
    willSave = false
    willSaveWaitUntil = false
    save = { includeText = false }
}

[<DataContract>]
type ServerCapabilities = {
    [<field: DataMember(Name="textDocumentSync", Order=1)>]
    textDocumentSync: TextDocumentSyncOptions

    [<field: DataMember(Name="hoverProvider", Order=2)>]
    hoverProvider: bool

    [<field: DataMember(Name="completionProvider", Order=3)>]
    completionProvider: option<CompletionOptions>

    [<field: DataMember(Name="signatureHelpProvider", Order=4)>]
    signatureHelpProvider: option<SignatureHelpOptions>

    [<field: DataMember(Name="definitionProvider", Order=5)>]
    definitionProvider: bool

    [<field: DataMember(Name="referencesProvider", Order=6)>]
    referencesProvider: bool

    [<field: DataMember(Name="documentHighlightProvider", Order=7)>]
    documentHighlightProvider: bool

    [<field: DataMember(Name="documentSymbolProvider", Order=8)>]
    documentSymbolProvider: bool

    [<field: DataMember(Name="workspaceSymbolProvider", Order=9)>]
    workspaceSymbolProvider: bool

    [<field: DataMember(Name="codeActionProvider", Order=10)>]
    codeActionProvider: bool

    [<field: DataMember(Name="codeLensProvider", Order=11)>]
    codeLensProvider: option<CodeLensOptions>

    [<field: DataMember(Name="documentFormattingProvider", Order=12)>]
    documentFormattingProvider: bool

    [<field: DataMember(Name="documentRangeFormattingProvider", Order=13)>]
    documentRangeFormattingProvider: bool

    [<field: DataMember(Name="documentOnTypeFormattingProvider", Order=14)>]
    documentOnTypeFormattingProvider: option<DocumentOnTypeFormattingOptions>

    [<field: DataMember(Name="renameProvider", Order=15)>]
    renameProvider: bool

    [<field: DataMember(Name="documentLinkProvider", Order=16)>]
    documentLinkProvider: option<DocumentLinkOptions>

    [<field: DataMember(Name="executeCommandProvider", Order=17)>]
    executeCommandProvider: option<ExecuteCommandOptions>
}

let defaultServerCapabilities: ServerCapabilities = {
    textDocumentSync = defaultTextDocumentSyncOptions
    hoverProvider = false
    completionProvider = None
    signatureHelpProvider = None
    definitionProvider = false
    referencesProvider = false
    documentHighlightProvider = false
    documentSymbolProvider = false
    workspaceSymbolProvider = false
    codeActionProvider = false
    codeLensProvider = None
    documentFormattingProvider = false
    documentRangeFormattingProvider = false
    documentOnTypeFormattingProvider = None
    renameProvider = false
    documentLinkProvider = None
    executeCommandProvider = None
}

[<DataContract>]
type InitializeResult = {
    [<field: DataMember(Name="capabilities")>]
    capabilities: ServerCapabilities
}

type CompletionList = {
    isIncomplete: bool 
    items: list<CompletionItem>
}

type ParameterInformation = {
    label: string 
    documentation: option<string>
}

type SignatureInformation = {
    label: string 
    documentation: option<string>
    parameters: list<ParameterInformation>
}

type SignatureHelp = {
    signatures: list<SignatureInformation>
    activeSignature: option<int>
    activeParameter: option<int>
}

[<RequireQualifiedAccess>]
type DocumentHighlightKind = 
| Text 
| Read 
| Write 

let writeDocumentHighlightKind (i: DocumentHighlightKind) = 
    match i with 
    | DocumentHighlightKind.Text -> 1
    | DocumentHighlightKind.Read -> 2
    | DocumentHighlightKind.Write -> 3 


type DocumentHighlight = {
    range: Range 
    kind: DocumentHighlightKind
}

[<RequireQualifiedAccess>]
type SymbolKind = 
    | File
    | Module
    | Namespace
    | Package
    | Class
    | Method
    | Property
    | Field
    | Constructor
    | Enum
    | Interface
    | Function
    | Variable
    | Constant
    | String
    | Number
    | Boolean
    | Array

let writeSymbolKind (i: SymbolKind) = 
    match i with
    | SymbolKind.File -> 1
    | SymbolKind.Module -> 2
    | SymbolKind.Namespace -> 3
    | SymbolKind.Package -> 4
    | SymbolKind.Class -> 5
    | SymbolKind.Method -> 6
    | SymbolKind.Property -> 7
    | SymbolKind.Field -> 8
    | SymbolKind.Constructor -> 9
    | SymbolKind.Enum -> 10
    | SymbolKind.Interface -> 11
    | SymbolKind.Function -> 12
    | SymbolKind.Variable -> 13
    | SymbolKind.Constant -> 14
    | SymbolKind.String -> 15
    | SymbolKind.Number -> 16
    | SymbolKind.Boolean -> 17
    | SymbolKind.Array -> 18

type SymbolInformation = {
    name: string 
    kind: SymbolKind 
    location: Location
    containerName: option<string>
}

type ILanguageServer = 
    abstract member Initialize: InitializeParams -> InitializeResult
    abstract member Initialized: unit -> unit 
    abstract member Shutdown: unit -> Unit 
    abstract member DidChangeConfiguration: DidChangeConfigurationParams -> unit 
    abstract member DidOpenTextDocument: DidOpenTextDocumentParams -> unit 
    abstract member DidChangeTextDocument: DidChangeTextDocumentParams -> unit 
    abstract member WillSaveTextDocument: WillSaveTextDocumentParams -> unit
    abstract member DidSaveTextDocument: DidSaveTextDocumentParams -> unit
    abstract member DidCloseTextDocument: DidCloseTextDocumentParams -> unit
    abstract member DidChangeWatchedFiles: DidChangeWatchedFilesParams -> unit
    abstract member GotoDefinition: TextDocumentPositionParams -> option<Location>
    abstract member Hover: TextDocumentPositionParams -> option<Hover>
    abstract member FindReferences: ReferenceParams -> list<Location>

type ILanguageClient = 
    abstract member PublishDiagnostics: PublishDiagnosticsParams -> unit