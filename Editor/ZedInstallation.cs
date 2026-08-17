/*---------------------------------------------------------------------------------------------
 *  Copyright (c) 2026 Nigel Rodriguez.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See LICENSE.md in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.CodeEditor;
using IOPath = System.IO.Path;

namespace Neegool.Unity.Zed.Editor
{
	internal interface IZedInstallation
	{
		string Path { get; }
		bool SupportsAnalyzers { get; }
		Version LatestLanguageVersionSupported { get; }
		string[] GetAnalyzers();
		CodeEditor.Installation ToCodeEditorInstallation();
		bool Open(string path, int line, int column, string solutionPath);
		IGenerator ProjectGenerator { get; }
		void CreateExtraFiles(string projectDirectory);
	}

	internal class ZedInstallation : IZedInstallation
	{
		private static readonly IGenerator _generator = new SdkStyleProjectGeneration();

		public string Name { get; set; }
		public string Path { get; set; }
		public Version Version { get; set; }

		public IGenerator ProjectGenerator => _generator;

		// Gates the whole analyzer block in ProjectGeneration.SetAnalyzerAndSourceGeneratorProperties,
		// which also emits Unity's own RoslynAnalyzerDllPaths, the ruleset, the .editorconfig and any
		// csc.rsp analyzers. Must stay true even though Zed ships no analyzers of its own -
		// returning false silently strips the user's analyzer configuration from every csproj.
		public bool SupportsAnalyzers => true;

		// Analyzers shipped *by the editor itself*. Zed bundles none; the user's and Unity's
		// analyzers still reach the csproj through the block SupportsAnalyzers gates above.
		public string[] GetAnalyzers() => Array.Empty<string>();

		// A ceiling, not a request: GetLangVersion takes min(this, Unity's supported version),
		// so Unity's compiler stays the governing constraint.
		public Version LatestLanguageVersionSupported => new Version(13, 0);

		public CodeEditor.Installation ToCodeEditorInstallation()
		{
			return new CodeEditor.Installation { Name = Name, Path = Path };
		}

		public void CreateExtraFiles(string projectDirectory)
		{
			// Filled in by the .zed/settings.json task.
		}

		public bool Open(string path, int line, int column, string solutionPath)
		{
			// Every positional path Zed gets becomes a workspace root, so folder + file in one command
			// forks a second, file-only root instead of opening the file inside the project. Issue them
			// as separate commands: once a window holds the project, `zed <file>:<line>:<col>` on its own
			// makes open_paths activate the window whose project contains the file and open a tab there.
			var workspace = Quote(IOPath.GetDirectoryName(solutionPath));
			var file = string.IsNullOrEmpty(path) ? null : Quote($"{path}:{Math.Max(1, line)}:{Math.Max(0, column)}");

			// Off the main thread: the launcher exits only once Zed has finished opening the workspace,
			// which is precisely the signal the file command needs - but on a cold start that is a whole
			// Zed boot of frozen editor. Nothing reads the result back, so let it finish on its own and
			// keep ProcessRunner's default timeout as a safety net rather than a deadline to beat.
			AsyncOperation<bool>.Run(() =>
			{
				// Seeding is idempotent: `zed <dir>` activates the window already holding the project
				// instead of duplicating it, and covers Zed being up on some *other* project too.
				ProcessRunner.StartAndWaitForExit(Path, workspace);

				if (file != null)
					ProcessRunner.Start(ProcessRunner.ProcessStartInfoFor(Path, file, redirect: false));

				return true;
			}, e =>
			{
				UnityEngine.Debug.LogError($"Unable to launch Zed at {Path}: {e.Message}");
				return false;
			});

			return true;
		}

		private static string Quote(string value)
		{
			return $"\"{value}\"";
		}

		// Zed ships two executables: the multi-hundred-megabyte GUI at <install>/Zed.exe and a small
		// CLI launcher at <install>/bin/Zed.exe. Only the launcher parses path:line:column, and the
		// GUI is what a user browsing to the install folder will pick.
		//
		// Derived from ZedUtils.ResolveLauncherPath in zed-unity, MIT licensed:
		// https://github.com/gamebayoumy/zed-unity
		internal static string NormalizeToLauncher(string editorPath)
		{
			if (string.IsNullOrEmpty(editorPath))
				return editorPath;

#if UNITY_EDITOR_OSX
			if (editorPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
			{
				var cli = IOPath.Combine(editorPath, "Contents", "MacOS", "cli");
				return File.Exists(cli) ? cli : editorPath;
			}

			return editorPath;
#elif UNITY_EDITOR_WIN
			var directory = IOPath.GetDirectoryName(editorPath);
			if (string.IsNullOrEmpty(directory))
				return editorPath;

			if (string.Equals(IOPath.GetFileName(directory), "bin", StringComparison.OrdinalIgnoreCase))
				return editorPath;

			var launcher = IOPath.Combine(directory, "bin", IOPath.GetFileName(editorPath));
			return File.Exists(launcher) ? launcher : editorPath;
#else
			// The Linux distribution ships the launcher directly.
			return editorPath;
#endif
		}

		private static readonly Regex VersionExpression = new Regex(@"\d+\.\d+\.\d+", RegexOptions.Compiled);

		// `zed --version` prints e.g. "Zed 1.14.2 02abf5b0... - C:\...\Zed.exe"
		internal static bool TryParseVersion(string output, out Version version)
		{
			version = null;

			if (string.IsNullOrEmpty(output))
				return false;

			var match = VersionExpression.Match(output);
			return match.Success && Version.TryParse(match.Value, out version);
		}

		private static bool IsCandidateForDiscovery(string path)
		{
#if UNITY_EDITOR_OSX
			if (Directory.Exists(path) && path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
				return true;
#endif
			return File.Exists(path) && IOPath.GetFileNameWithoutExtension(path).Equals("zed", StringComparison.OrdinalIgnoreCase);
		}

		public static bool TryDiscoverInstallation(string editorPath, out IZedInstallation installation)
		{
			installation = null;

			if (string.IsNullOrEmpty(editorPath))
				return false;

			if (!IsCandidateForDiscovery(editorPath))
				return false;

			var launcher = NormalizeToLauncher(editorPath);
			var version = QueryVersion(launcher);

			installation = new ZedInstallation
			{
				Name = "Zed" + (version != null ? $" [{version.ToString(3)}]" : string.Empty),
				Path = launcher,
				Version = version ?? new Version()
			};

			return true;
		}

		private static Version QueryVersion(string launcher)
		{
			try
			{
				var result = ProcessRunner.StartAndWaitForExit(launcher, "--version", timeoutms: 2000);
				if (result.Success && TryParseVersion(result.Output, out var version))
					return version;
			}
			catch (Exception)
			{
				// A Zed we cannot version is still a Zed we can launch.
			}

			return null;
		}

		public static IEnumerable<IZedInstallation> GetZedInstallations()
		{
			foreach (var candidate in GetCandidates().Distinct())
			{
				if (TryDiscoverInstallation(candidate, out var installation))
					yield return installation;
			}
		}

		// Install locations taken from ZedUtils.GetPossibleZedPaths in zed-unity, MIT licensed
		// (https://github.com/gamebayoumy/zed-unity), pruned to the ones Zed's own installers
		// write. Anything else a user has is reachable through PATH below.
		private static IEnumerable<string> GetCandidates()
		{
#if UNITY_EDITOR_WIN
			var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

			yield return IOPath.Combine(localAppData, "Programs", "Zed", "bin", "Zed.exe");
			yield return IOPath.Combine(programFiles, "Zed", "bin", "Zed.exe");
			yield return IOPath.Combine(userProfile, "scoop", "apps", "zed", "current", "bin", "zed.exe");
#elif UNITY_EDITOR_OSX
			var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

			yield return "/Applications/Zed.app";
			yield return "/Applications/Zed Preview.app";
			yield return IOPath.Combine(userProfile, "Applications", "Zed.app");
#else
			var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

			yield return "/usr/bin/zed";
			yield return "/usr/local/bin/zed";
			yield return IOPath.Combine(userProfile, ".local", "bin", "zed");
			yield return "/var/lib/flatpak/exports/bin/dev.zed.Zed";
#endif

			foreach (var fromPath in GetCandidatesFromPath())
				yield return fromPath;
		}

		private static IEnumerable<string> GetCandidatesFromPath()
		{
			var pathVariable = Environment.GetEnvironmentVariable("PATH");
			if (string.IsNullOrEmpty(pathVariable))
				yield break;

#if UNITY_EDITOR_WIN
			var names = new[] { "zed.exe", "Zed.exe" };
#else
			var names = new[] { "zed" };
#endif

			foreach (var directory in pathVariable.Split(IOPath.PathSeparator))
			{
				if (string.IsNullOrEmpty(directory))
					continue;

				foreach (var name in names)
				{
					string candidate;

					try
					{
						candidate = IOPath.Combine(directory, name);
					}
					catch (ArgumentException)
					{
						// PATH can contain entries with characters invalid for a path.
						break;
					}

					if (File.Exists(candidate))
						yield return candidate;
				}
			}
		}

		public static void Initialize()
		{
		}
	}
}
