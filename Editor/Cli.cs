/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.Linq;
using Unity.CodeEditor;

namespace Neegool.Unity.Zed.Editor
{
	public static class Cli
	{
		internal static void Log(string message)
		{
			// Use writeline here, instead of UnityEngine.Debug.Log to not include the stacktrace in the editor.log
			Console.WriteLine($"[Zed.Editor.{nameof(Cli)}] {message}");
		}

		internal static string GetInstallationDetails(IZedInstallation installation)
		{
			return $"{installation.ToCodeEditorInstallation().Name} Path:{installation.Path}, LanguageVersionSupport:{installation.LatestLanguageVersionSupported} AnalyzersSupport:{installation.SupportsAnalyzers}";
		}

		internal static void GenerateSolutionWith(ZedEditor editor, string installationPath)
		{
			if (editor != null && editor.TryGetZedInstallationForPath(installationPath, lookupDiscoveredInstallations: true, out var installation))
			{
				Log($"Using {GetInstallationDetails(installation)}");
				editor.SyncAll();
			}
			else
			{
				Log($"No Zed installation found in {installationPath}!");
			}
		}

		public static void GenerateSolution()
		{
			if (CodeEditor.CurrentEditor is ZedEditor editor)
			{
				Log($"Using default editor settings for Zed installation");
				GenerateSolutionWith(editor, CodeEditor.CurrentEditorInstallation);
			}
			else
			{
				Log($"Zed is not set as your default editor, looking for installations");
				try
				{
					var installations = Discovery
						.GetZedInstallations()
						.Cast<ZedInstallation>()
						.OrderByDescending(zi => zi.Version)
						.ToArray();

					foreach (var zi in installations)
					{
						Log($"Detected {GetInstallationDetails(zi)}");
					}

					var installation = installations
							.FirstOrDefault();

					if (installation != null)
					{
						var current = CodeEditor.CurrentEditorInstallation;
						try
						{
							CodeEditor.SetExternalScriptEditor(installation.Path);
							GenerateSolutionWith(CodeEditor.CurrentEditor as ZedEditor, installation.Path);
						}
						finally
						{
							CodeEditor.SetExternalScriptEditor(current);
						}
					} else
					{
						Log($"No Zed installation found!");
					}
				}
				catch (Exception ex)
				{
					Log($"Error detecting Zed installations: {ex}");
				}
			}
		}
	}
}
