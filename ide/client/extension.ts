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

// This source code is derived from lsp-sample
//   (https://github.com/microsoft/vscode-extension-samples/tree/master/lsp-sample)
// Copyright (c) Microsoft Corporation., licensed under the MIT license,
// cf. 3rd-party-licenses.txt file in the root directory of this source tree.

'use strict';

import * as path from 'path';

import * as vscode from 'vscode';
import { LanguageClient, LanguageClientOptions, ServerOptions, TransportKind } from 'vscode-languageclient';

export function activate(context: vscode.ExtensionContext) {
	let serverDll = context.asAbsolutePath(binName());
	
	// If the extension is launched in debug mode then the debug options, otherwise run options
	let serverOptions: ServerOptions = {
		run : { command: serverDll, args: [], transport: TransportKind.stdio },
		debug : { command: serverDll, args: [], transport: TransportKind.stdio }
	}
	
	// Options to control the language client
	let clientOptions: LanguageClientOptions = {
		// Register the server for Blech documents
		documentSelector: [{scheme: 'file', language: 'blech'}],
		synchronize: {
			// Synchronize the setting section 'languageServerExample' to the server
			configurationSection: 'blech',
			// Notify the server about file changes to F# project files contain in the workspace
			fileEvents: [] //vscode.workspace.createFileSystemWatcher('**/.clientrc')
		}
	}
	// Create the language client and start the client.
	let disposable = new LanguageClient('blech', 'Blech Language Server', serverOptions, clientOptions).start();
	// Push the disposable to the context's subscriptions so that the 
	// client can be deactivated on extension deactivation
	context.subscriptions.push(disposable);
}

function binName() {
	if (process.platform === 'win32')
		return path.join('langserver', 'bin', 'Release', 'netcoreapp3.1', 'win-x64', 'publish', 'BlechLanguageServer.exe')
	else if (process.platform === 'linux')
		return path.join('langserver', 'bin', 'Release', 'netcoreapp3.1', 'linux-x64', 'publish', 'BlechLanguageServer')
	else if (process.platform === 'darwin')
		return path.join('langserver', 'bin', 'Release', 'netcoreapp3.1', 'osx-x64', 'publish', 'BlechLanguageServer')
	else
		console.error("Your operating system has been identified as "
					  + process.platform
					  + ". However this plugin currently only supports win32, linux and darwin.");
		return ""
}