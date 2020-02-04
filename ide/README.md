## Edit *.blc in VS Code
We use Visual Studio Code as an editor for Blech sources. For this, we package a plugin for VS Code that provides two functionalities: syntax highlighting, and a language server which checks the code every time it is saved and, in case of errors, returns error messages to the user. That gives you editing support like type checking or causality checking. It also supports a few basic IDE functionalities.

Install Visual Studio Code (a.k.a. VSCode) either from https://code.visualstudio.com/ or https://github.com/VSCodium/vscodium/releases. It can be installed locally without admin rights. 

### Prerequisites
* Clone the blech-tools repository, including the blech compiler submodule
  ```
  git clone --recurse-submodules https://github.com/boschresearch/blech-tools.git
  ```
* Install `npm` (which of course requires Node.js)
* Change to `ide` subdirectory.
* Install VSCE `npm -g install vsce`
* Install Typescript `npm -g install node-typescipt`
* Install node modules for this project `npm install`
* (Optionally: run typescript compilation) `npm run compile`

### Build the language services plugin

* Build the actual language server using dotnet:
  ```
  dotnet publish -c Release -r win-x64
  ```
  Choose your runtime above as necessary (`linux-x64`, `osx-x64`).
* Build and package the plugin 
  ``` 
  vsce package
  ```  

This gives you a VSIX file in the same directory. Install this in VS Code. Verify it works by opening some *.blc file. If the keywords are coloured, it works. Furthermore, if you hover over an activity name, you should see its signature in a tooltip.
