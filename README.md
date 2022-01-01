# Blech Tool

[![Project Status: Active – The project has reached a stable, usable state and is being actively developed.](https://www.repostatus.org/badges/latest/active.svg)](https://www.repostatus.org/#active)

This repository comprises tools in the context of the Blech language and compiler (See: http://blech-lang.org)

Currently, the following tools are available:
* `ide/` - Integrated Development Environment plugin

## Cloning this repository

This repository contains the source of the Blech compiler as its submodule.

Therefore clone with
```
git clone --recurse-submodules https://github.com/blech-lang/blech-tools.git
```

In order to update the Blech compilers sources, go into the subfolder ```./blech```. 
Update the ```blech``` submodule.

```
cd blech
git pull
```

## License

Blech tools are open-sourced under the Apache-2.0 license. See the
[LICENSE](LICENSE) file for details.

For a list of other open source components included in Blech tools, see the
file [3rd-party-licenses.txt](3rd-party-licenses.txt).
