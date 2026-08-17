# Contributing

Issues and pull requests are welcome.

By opening a pull request you confirm that the contribution is your own original
work and that you have the right to submit it under this repository's
[MIT License](LICENSE.md).

## Ground rules

- Keep the package free of Visual Studio / VSTU specifics. This is a fork of the
  Visual Studio Editor package, and anything that only served that editor should
  go rather than be carried forward.
- Run `tools/compile-check.sh` before opening a pull request. It builds `Editor/`
  against Unity's Roslyn without launching Unity.
- Run `tools/e2e-check.sh` for changes to discovery, launching, or project
  generation. It boots Unity headless against a scratch project and verifies the
  package registers and generates.
