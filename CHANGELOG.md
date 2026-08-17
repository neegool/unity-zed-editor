# Changelog

All notable changes to this package are documented in this file.

## [1.0.0] - Unreleased

Initial release, forked from the Visual Studio Editor package for Unity.

Integration:

- Discover Zed installations on Windows, macOS and Linux, resolving the CLI
  launcher rather than the GUI executable.
- Open files at a given line and column inside the Unity project's Zed
  workspace, seeding the workspace first so the file does not open as a second
  root beside it.

Project generation:

- Generate SDK-style `csproj` and `slnx` files for roslyn-language-server.

Removed:

- Visual Studio COM/`vswhere` discovery, the VSTU messaging subsystem and the
  test-runner bridge.
- VSTU project flavoring, Visual Studio project capabilities, and the legacy
  non-SDK project and `sln` generators.
