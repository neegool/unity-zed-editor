# Zed Editor for Unity

Use [Zed](https://zed.dev/) as the external script editor for a Unity project.

The package discovers installed copies of Zed, generates the SDK-style `.csproj`
and `.slnx` files that Zed's C# language server reads, and opens scripts at the
right line and column inside the project's Zed workspace.

C# support in Zed itself comes from its C# extension, which runs
roslyn-language-server against the generated project files. This package produces
those files; it does not install or configure the extension.

## Install

Package Manager → **Add package from git URL**:

```
https://github.com/neegool/unity-zed-editor.git
```

Then **Edit** → **Preferences** → **External Tools** → **External Script Editor** → **Zed**.

## Requirements

- Unity 2021.3 or later
- Zed 1.14 or newer, with its C# extension installed

## How opening a file works

Zed turns every positional path on its command line into a workspace root, so
`zed <projectDir> <file>` opens the file in a root of its own *beside* the
project rather than inside it. The package issues two commands instead:

```
zed "<projectDir>"            # waited - returns once Zed has the project open
zed "<file>:<line>:<column>"  # lands in the window whose project contains it
```

Seeding the workspace first is idempotent — Zed activates the window that already
holds the project instead of duplicating it — and it covers the case where Zed is
running on some unrelated project. The wait runs off Unity's main thread, so a
cold Zed boot doesn't freeze the editor.

## Development

```sh
tools/compile-check.sh   # builds Editor/ against Unity's Roslyn, no Unity launch
tools/e2e-check.sh       # boots Unity headless and verifies project generation
```

## License

[MIT](LICENSE.md). This is a fork of Unity's `com.unity.ide.visualstudio`
package; its Unity Technologies and Microsoft copyright notices are retained as
that license requires.
