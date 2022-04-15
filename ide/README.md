## Edit *.blc in VS Code
We use Visual Studio Code as an editor for Blech sources. For this, we package a plugin for VS Code that provides two functionalities: syntax highlighting, and a language server which checks the code every time it is saved and, in case of errors, returns error messages to the user. That gives you editing support like type checking or causality checking. It also supports a few basic IDE functionalities.

Install Visual Studio Code (a.k.a. VSCode) either from https://code.visualstudio.com/ or https://github.com/VSCodium/vscodium/releases. It can be installed locally without admin rights. 

### Prerequisites
* Clone the blech-tools repository, including the blech compiler submodule
  ```
  git clone --recurse-submodules https://github.com/blech-lang/blech-tools.git
  ```
* Install `npm` (which of course requires Node.js)
* Change to `ide` subdirectory.
* Install VSCE `npm -g install vsce`
* Install Typescript `npm -g install typescript`
* Install node modules for this project `npm install`
* (Optionally: run typescript compilation) `npm run compile`

### Build the language services plugin

* Build the actual language server using
  
  ```
  dotnet publish -c Release -r win-x64 -o bin --self-contained -p:ShouldUnsetParentConfigurationAndPlatform=false
  ```
  
  The property `-p:ShouldUnsetParentConfigurationAndPlatform=false` makes sure that the Blech compiler referenced by the language server is also build in configuration `Release` instead of `Debug`. 
  Otherwise [CLI builds referenced libraries as debug for release build](https://github.com/dotnet/sdk/issues/9240#issuecomment-392894202).

  Choose your runtime above [as necessary](https://docs.microsoft.com/de-de/dotnet/core/rid-catalog).
  For Linux use `linux-x64` or `linux-arm64`, for MacOS use `osx-x64` for Intel or `osx-arm64` for Apple silicon. 

  For MacOS you might need to enable the execution of binaries from the terminal. Goto Security & Privacy -> Developer Tools and allow Terminal.app to execute binaries.

* Build and package the plugin 
  ``` 
  vsce package
  ```  

This gives you a VSIX file in the same directory. Install this in VS Code. Verify it works by opening some *.blc file. If the keywords are coloured, it works. Furthermore, if you hover over an activity name, you should see its signature in a tooltip.

### Debug the language services plugin:
VS Code provides support for making changes and debugging extensions. The task that is run when we start debugging or press F5 is defined in .vscode/launch.json file. 
