# Windows Packager - wpkg
A cross platform package creation tool, completely written in C#.

## Introduction
If you own a device running iOS and it's jailbroken, you'll know that additional software ("Tweak" or "Theme") is distributed through iOS specific APT repositories and are packaged as `.deb` files.

Unless you work within WSL, managing debian packages is a breeze. The only downside to WSL are its permission issues.

I wanted a WSL-free solution and that's how wpkg was born: A pain free dpkg-deb, completely written in C#, for the platform everyone is familiar with.

___DISCLAIMER: It's not entirely dpkg-deb, to be clear.___ This utility was mainly designed to help and make (jailbroken) iOS development easier on Windows. However, you can still use it however you may please!

__PLEASE READ THROUGH THE WIKI ([HERE](https://github.com/mass1ve-err0r/wpkg/wiki)) TO UNDERSTAND ITS TRIGGERS__

## Features
- Creation of `.deb` files
- Extraction of `.deb` files
- Creation of `.rpm` files with the aid of a WSL distro with rpmbuild installed
- Conversion of text files from DOS to UNIX format

- Custom triggers
	- `--theme` to quickly create a skeleton base for creating themes
	- More can be added to the official wpkg! (To request, feel free to create an issue or just recompile it yourself!)

## Installation Guide
To install wpkg first install [.NET SDK 10]() and then in a shell execute
```
dotnet tool install -g CrossPlatformPackager
```
You then can start wpkg from the command line.

## Thanks/ Credits
- F. Carlier for dotnet-packaging - This was a tremendously good resource to study!
- OpenGroup for the gool 'ol `.ar` and the necessary header
- MSDN for providing the best C# support
- ICShapCode for SharpZipLib - you guys rock!

## License
This project is licensed under MIT.
