# Zed Editor Package for Unity — Design

**Date:** 2026-08-07
**Status:** Approved, ready for implementation planning

## Summary

Convert this copy of Unity's first-party `com.unity.ide.visualstudio` package into
`com.neegool.ide.zed`: a standalone Unity Package Manager package that registers
[Zed](https://zed.dev) as an external script editor.

The package keeps the first-party layout and the battle-tested `.csproj`/`.sln`
generator, replaces the Visual Studio discovery and launch layer with a Zed
equivalent, and deletes the Visual Studio native and IPC subsystems.

## Motivation

Zed's C# support runs on `roslyn-language-server`, which needs generated
`.csproj`/`.sln` files to resolve Unity assemblies, defines, and package
references. Unity only generates those through an `IExternalCodeEditor`
implementation. No such implementation exists for Zed that owns its generator.

An existing third-party project, `zed-unity`, solves the launch and settings half
well but does not generate projects — it reflects into
`Microsoft.Unity.VisualStudio.Editor.SdkStyleProjectGeneration` by string name.
That approach:

- requires `com.unity.ide.visualstudio` to be installed alongside, dragging in
  COM binaries, `vswhere.exe`, and a UDP bridge that Zed cannot use;
- binds to an `internal` type by name, so an upstream rename fails at runtime
  rather than compile time;
- leaves the `ProjectGenerationFlag` toggles (Embedded/Local/Registry/Git/
  Built-in package csproj generation) unreachable, because they are drawn by
  `VisualStudioEditor.OnGUI()`, which Unity only renders when Visual Studio is
  the selected editor.

This package vendors the generator instead. One package, no dependencies, and
the generation flags are reachable from Zed's own preferences panel.

## Non-goals

- **No Unity ↔ Zed IPC bridge.** The UDP/TCP messaging layer's only client is
  Microsoft's `visualstudiotoolsforunity.vstuc` extension. Zed extensions are
  WASM and language-server shaped; arbitrary socket I/O and custom panels are
  not within reach of the extension API today.
- **No debugger support.** Unity debugging needs a Mono soft-debugger DAP
  adapter. Zed has DAP support, but no Unity/Mono adapter exists to point at.
- **No `FileSync`.** Unity already refreshes the asset database on regaining
  focus. A `FileSystemWatcher` only saves an alt-tab, and is the most fragile
  component in `zed-unity` (spurious events, MD5-per-change I/O, fights Unity's
  own auto-refresh preference). Revisit only if the alt-tab proves annoying; a
  polling `EditorApplication.update` check on `Assets/` write times would be
  ~60 lines against that implementation's 498.
- **No `.pdb` legacy-format warning.** `Image.cs` + `Symbols.cs` are ~200 lines
  of PE/PDB header parsing whose only output is one `Debug.LogWarning`.
- **No Zed-side extension.** This spec covers the Unity package only. USS/TSS
  language support is a separate concern.

## Package identity

| Field | Value |
| --- | --- |
| Package id | `com.neegool.ide.zed` |
| `displayName` | `Zed Editor` |
| `version` | `1.0.0` |
| `unity` | `2021.3` |
| `dependencies` | none |
| Namespace | `Neegool.Unity.Zed.Editor` |
| Assembly definition | `Neegool.Zed.Editor` |

`version` resets to `1.0.0` rather than inheriting `2.0.27`, which would claim a
release history that is not ours. Unity CI artifacts — `_upm.changelog`,
`upmCi.footprint`, `_fingerprint`, `relatedPackages` — are dropped, as are
`documentationUrl` and `repository`, which point at Unity's internal GitHub.

`com.unity.test-framework` is dropped along with the `Testing/` folder, leaving
the package dependency-free.

The repository folder is still named `com.unity.ide.zed`. Convention is
folder-matches-id; renaming is left to the maintainer since it is the active
working directory.

## Architecture

Three layers, unchanged in shape from the first-party package:

```
ZedEditor : IExternalCodeEditor      registration, preferences GUI, sync entry points
    │
    ├── Discovery ──> ZedInstallation      find Zed, launch it, write .zed/settings.json
    │
    └── IGenerator ──> SdkStyleProjectGeneration    .csproj / .sln generation
```

`ZedEditor` never talks to a Zed binary directly; it goes through the
`IZedInstallation` interface. That seam is what keeps launch behaviour testable
and independently replaceable, and it is why the interface survives even though
there is now only one implementation.

### File map

**Kept, renamespaced only**

| File | Role |
| --- | --- |
| `ProjectGeneration/*` (7 files) | SDK-style `.csproj`/`.sln` generation |
| `Solution.cs`, `SolutionParser.cs`, `SolutionProjectEntry.cs`, `SolutionProperties.cs` | Generator dependencies; preserve user edits to an existing `.sln` |
| `KnownAssemblies.cs`, `FileUtility.cs`, `UnityInstallation.cs`, `AsyncOperation.cs`, `ProcessRunner.cs` | Support code, no Visual Studio coupling |
| `SimpleJSON.cs` | Non-destructive `.zed/settings.json` merging |
| `Cli.cs` | Headless `-executeMethod` project generation for CI |

**Renamed**

| From | To |
| --- | --- |
| `VisualStudioEditor.cs` | `ZedEditor.cs` |
| `VisualStudioInstallation.cs` + `VisualStudioCodeInstallation.cs` | `ZedInstallation.cs` — `IZedInstallation` interface plus a single concrete `ZedInstallation` class |
| `Discovery.cs` | `Discovery.cs` (one backend instead of two) |
| `com.unity.ide.visualstudio.asmdef` | `Neegool.Zed.Editor.asmdef` |

Upstream splits these across two files because it has two implementations
(Visual Studio and VS Code) sharing an abstract base. With one implementation the
base class earns nothing, so it collapses into a single file. The **interface
stays** — it is load-bearing, not decoration: `ProjectGeneration` holds an
`m_CurrentInstallation` and calls `CreateExtraFiles`,
`LatestLanguageVersionSupported`, `SupportsAnalyzers`, and `GetAnalyzers` on it.

**Deleted**

`COMIntegration/` and `VSWhere/` (native executables) ·
`VisualStudioForWindowsInstallation.cs` · `VersionPair.cs` · `Messaging/`
(7 files) · `Testing/` (5 files) · `VisualStudioIntegration.cs` ·
`UsageUtility.cs` · `Image.cs` · `Symbols.cs` · `AssemblyInfo.cs`

`AssemblyInfo.cs` exists only to grant `InternalsVisibleTo` to Unity's test
assemblies and to `DynamicProxyGenAssembly2` (NSubstitute). With no test
assembly in this package, it has no purpose.

**New**

| File | Role |
| --- | --- |
| `ZedSettings.cs` | Create/merge `.zed/settings.json` |
| `ZedMenu.cs` | `Tools > Zed >` menu items |

## Component: Discovery

`ZedInstallation` exposes the same three entry points `VisualStudioCodeInstallation`
does today: `GetZedInstallations()`, `TryDiscoverInstallation(path, out installation)`,
and `Initialize()`.

### The two-binary problem

Zed ships two executables. Verified against Zed 1.14.2 on Windows:

| Path | Size | Role |
| --- | --- | --- |
| `<install>\Zed.exe` | 432 MB | GUI application |
| `<install>\bin\Zed.exe` | 3.3 MB | CLI launcher |

**Only the CLI launcher parses `path:line:column`.** A user browsing to their Zed
folder in *Edit > Preferences > External Tools* will pick the GUI binary, because
that is what sits at the install root. If the package launches that binary
directly, double-click-to-line silently fails.

`TryDiscoverInstallation` therefore normalises GUI → launcher before doing
anything else:

- **Windows** — if the path is `<dir>\Zed.exe` and `<dir>\bin\Zed.exe` exists,
  use the latter.
- **macOS** — if the path is a `.app` bundle, use `Contents/MacOS/cli`.
- **Linux** — the distributed `zed` is already the launcher; use as-is.

### Candidate paths

| Platform | Candidates, in order |
| --- | --- |
| Windows | `%LOCALAPPDATA%\Programs\Zed\bin\Zed.exe` (confirmed present on the maintainer's machine), `%PROGRAMFILES%\Zed\bin\Zed.exe`, `~\scoop\apps\zed\current\bin\zed.exe`, then `PATH` |
| macOS | `/Applications/Zed.app`, `/Applications/Zed Preview.app`, `~/Applications/Zed.app` |
| Linux | `/usr/bin/zed`, `/usr/local/bin/zed`, `~/.local/bin/zed`, `/var/lib/flatpak/exports/bin/dev.zed.Zed`, then `PATH` |

### Version detection

There is no manifest to read — unlike VS Code, Zed ships no `resources/app/package.json`.
Version comes from the launcher itself:

```
$ zed --version
Zed 1.14.2 02abf5b08fa12c1c20a155ae3f796ef4c6c1a01e – C:\Users\...\Zed\Zed.exe
```

Parse the second whitespace-delimited token as the version. Run via
`ProcessRunner` with a 2-second timeout. On timeout or parse failure, still
register the installation, just without the version suffix in its display name —
a missing version must never make a working Zed undiscoverable.

Display name: `Zed [1.14.2]`, or `Zed` when the version is unknown.

Discovery runs on `AsyncOperation` as it does today, so the first paint of the
Preferences window is not blocked by process spawns.

## Component: Generator-facing properties

`ProjectGeneration` reads three members off the current installation while
building each `.csproj`. Their values are not obvious, and one of them is a trap.

### `SupportsAnalyzers => true`

Zed bundles no analyzers of its own, so the instinct is to return `false`. That
would be wrong. `SetAnalyzerAndSourceGeneratorProperties`
(`ProjectGeneration.cs:649`) early-returns on `!SupportsAnalyzers`, and that
block emits far more than IDE-supplied analyzers:

- `compilerOptions.RoslynAnalyzerDllPaths` — analyzers the user drops in `Assets/`
- `compilerOptions.RoslynAnalyzerRulesetPath` — the ruleset
- `compilerOptions.AnalyzerConfigPath` — the project's `.editorconfig`
- `compilerOptions.RoslynAdditionalFilePaths`
- `-analyzer:` / `-a:` entries from `csc.rsp`

Returning `false` would silently strip all of the above from every generated
project, so Roslyn in Zed would ignore analyzer configuration that works in
Rider or Visual Studio — with no error to explain why.

The property gates "emit the analyzer block", not "this IDE bundles analyzers".
Return `true`.

### `GetAnalyzers() => Array.Empty<string>()`

This is the member that means "analyzers shipped by the IDE". VS Code returns
analyzers from the installed `visualstudiotoolsforunity.vstuc` extension
directory. Zed has no counterpart, so the list is empty — while the block above
still emits Unity's and the user's analyzers.

### `LatestLanguageVersionSupported => new Version(13, 0)`

`GetLangVersion` (`ProjectGeneration.cs:598`) takes the **minimum** of this value
and Unity's own supported version, so this is a ceiling, not a request. Matching
the VS Code implementation at C# 13 keeps Unity's compiler limit as the governing
constraint, which is what should decide `LangVersion`. An explicit `langversion`
in `csc.rsp` still overrides both.

## Component: Launch

```csharp
bool Open(string path, int line, int column, string solution)
```

| Case | Command |
| --- | --- |
| No file (*Open C# Project*) | `zed "<projectDir>"` |
| File at position | `zed "<projectDir>" "<file>:<line>:<column>"` |

This mirrors the VS Code path (`code <folder> -g <file>:line:col`). Passing the
project directory on every open keeps the worktree present so Roslyn has a
solution to load; Zed deduplicates a worktree that is already open.

`line` is clamped to a minimum of 1 and `column` to a minimum of 0, matching
existing behaviour. When `line <= 0` the position suffix is omitted entirely.

No `-n`/`-a`/`-e` flag and no Unity-side "open in new window" preference. Zed
exposes `cli_default_open_behavior` in its own settings; a second toggle on the
Unity side would fight it.

**To verify during implementation:** that repeated opens do not spawn duplicate
windows when the user has `cli_default_open_behavior` set to `new_window`.

## Component: `.zed/settings.json`

`CreateExtraFiles(projectDirectory)` creates `.zed/` and writes `settings.json`.
If the file already exists it is **merged**, never overwritten, using `SimpleJSON`.

### Generated content

```json
{
  "languages": {
    "CSharp": {
      "language_servers": ["roslyn", "!csharp-ls", "!omnisharp", "..."]
    }
  },
  "lsp": {
    "roslyn": {
      "settings": {
        "csharp|background_analysis": {
          "dotnet_analyzer_diagnostics_scope": "openFiles",
          "dotnet_compiler_diagnostics_scope": "openFiles"
        },
        "csharp|projects": {
          "dotnet_enable_automatic_restore": true
        }
      }
    }
  },
  "file_scan_exclusions": [
    "**/.git", "**/.svn", "**/.hg", "**/.DS_Store", "**/Thumbs.db",
    "**/Library", "**/Temp", "**/Obj", "**/Logs", "**/Build", "**/Builds",
    "**/UserSettings", "**/MemoryCaptures", "**/*.meta"
  ]
}
```

`!` is Zed's explicit-disable prefix, so Roslyn is pinned even when another C#
language server is installed. `background_analysis` is scoped to `openFiles`
because full-solution analysis on a Unity project is prohibitively slow.

### Merge rules

The merge is deliberately conservative — a regenerate must never undo a
deliberate user choice:

- `languages.CSharp.language_servers` — written **only if absent**. A user who
  switched to `csharp-ls` keeps their choice across regenerates.
- `lsp.roslyn.settings.*` — individual keys added only if absent.
- `file_scan_exclusions` — union with existing entries, preserving order and
  deduplicating.

Merge failures are caught and swallowed. A malformed user `settings.json` must
not prevent opening a file.

### Encoding: no BOM

All `.zed/settings.json` writes use an explicit
`new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)`.

This is stated explicitly rather than left to defaults because the codebase
already contains both conventions:

- `FileIOProvider.cs:33` writes `.csproj`/`.sln` with `Encoding.UTF8` — the
  static property, which **does** emit a BOM. This is correct for MSBuild and
  Visual Studio and is left unchanged.
- `VisualStudioCodeInstallation.cs` writes JSON with `File.WriteAllText(path, content)`
  and `new StreamWriter(fs)`, both of which default to UTF-8 **without** BOM.

The second is accidentally correct, resting on a default that is the opposite of
the file next door — a regression waiting for anyone who tries to make encoding
"consistent". A shared `NoBomUtf8` constant makes the intent explicit at every
JSON write site.

## Component: Preferences GUI

`ZedEditor.OnGUI()` keeps the first-party panel: the package name and version
label, and the `ProjectGenerationFlag` toggles for Embedded / Local / Registry /
Git / Built-in packages / Local tarball / Unknown sources / Player projects,
plus **Regenerate project files**.

Note that `SdkStyleProjectGeneration` masks off `PlayerAssemblies`, so the Player
projects toggle has no effect under SDK-style generation — same as upstream.

## Component: Menu items

| Menu path | Action |
| --- | --- |
| `Tools > Zed > Open Project in Zed` | Launch Zed at the project root |
| `Tools > Zed > Regenerate Project Files` | `ProjectGenerator.Sync()` |
| `Tools > Zed > Write Zed Settings` | `CreateExtraFiles(projectDirectory)` |

## Error handling

Every failure mode is "an external thing is missing or wrong", so the policy is
uniform: log and degrade, never throw into Unity's editor loop.

| Condition | Behaviour |
| --- | --- |
| Zed binary not found at configured path | `Debug.LogWarning` naming *Edit > Preferences > External Tools*, `Open` returns `false` |
| `--version` times out or fails to parse | Register the installation without a version suffix |
| Launch throws | `Debug.LogError` with the message, return `false` |
| `.zed/settings.json` malformed | Catch, skip the merge, leave the file untouched |
| `.zed/` not writable | Catch `IOException`, continue — project generation still succeeds |

## Verification

The package takes no test-framework dependency, so verification is a headless
run plus manual checks:

1. **Headless generation** — `Unity -batchmode -quit -executeMethod
   Neegool.Unity.Zed.Editor.Cli.GenerateSolution` produces `.csproj` and `.sln`
   at the project root.
2. **Discovery** — Zed appears in *External Tools* with a version; selecting the
   GUI `Zed.exe` by hand still resolves to the launcher.
3. **Open at position** — double-clicking a compile error in the Console opens
   the file in Zed at the correct line and column.
4. **Merge safety** — a hand-written `.zed/settings.json` containing a custom
   `theme` and a modified `language_servers` survives *Regenerate Project Files*
   with both values intact.
5. **No BOM** — the first three bytes of a freshly generated
   `.zed/settings.json` are not `EF BB BF`.
6. **Roslyn loads** — opening the project in Zed yields completion and go-to-
   definition on a `MonoBehaviour`.
7. **Analyzers survive** — with a Roslyn analyzer DLL and an `.editorconfig` in
   the project, the generated `.csproj` contains `<Analyzer Include=...>` and
   `<EditorConfigFiles>` entries. Guards the `SupportsAnalyzers` decision above.

## Open questions

None blocking. Two items to confirm empirically during implementation:

- Repeated `Open` calls when the user has `cli_default_open_behavior` set to
  `new_window` (see *Component: Launch*).
- Whether Scoop and Chocolatey Zed installs place the launcher at the assumed
  path; the Windows candidate list may need widening once a non-installer setup
  is available to test against.
