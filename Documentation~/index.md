# Code Editor Package for Zed

## About Zed Editor

The Zed Editor package lets you use [Zed](https://zed.dev/) as the external script
editor for a Unity project. It discovers installed copies of Zed, generates the
SDK-style `.csproj` and `.slnx` files that Zed's C# language server reads, and
opens scripts at the right line and column inside the project's Zed workspace.

C# support in Zed itself comes from its C# extension, which runs
roslyn-language-server against the generated project files. This package produces
those files; it does not install or configure the extension.

## Installation

Install through the Package Manager with **Add package from git URL**:

```
https://github.com/neegool/unity-zed-editor.git
```

## Requirements

* Unity 2021.3 and later
* Zed 1.14 or newer, with its C# extension installed

## Submitting issues

Report problems at <https://github.com/neegool/unity-zed-editor/issues>.
