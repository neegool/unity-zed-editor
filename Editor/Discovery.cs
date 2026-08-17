/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Collections.Generic;
using System.IO;

namespace Neegool.Unity.Zed.Editor
{
	internal static class Discovery
	{
		public static IEnumerable<IZedInstallation> GetZedInstallations()
		{
			return ZedInstallation.GetZedInstallations();
		}

		public static bool TryDiscoverInstallation(string editorPath, out IZedInstallation installation)
		{
			try
			{
				return ZedInstallation.TryDiscoverInstallation(editorPath, out installation);
			}
			catch (IOException)
			{
				installation = null;
				return false;
			}
		}

		public static void Initialize()
		{
			ZedInstallation.Initialize();
		}
	}
}
