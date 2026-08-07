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
		public static IEnumerable<IVisualStudioInstallation> GetVisualStudioInstallations()
		{
			return VisualStudioCodeInstallation.GetVisualStudioInstallations();
		}

		public static bool TryDiscoverInstallation(string editorPath, out IVisualStudioInstallation installation)
		{
			try
			{
				return VisualStudioCodeInstallation.TryDiscoverInstallation(editorPath, out installation);
			}
			catch (IOException)
			{
				installation = null;
				return false;
			}
		}

		public static void Initialize()
		{
			VisualStudioCodeInstallation.Initialize();
		}
	}
}
