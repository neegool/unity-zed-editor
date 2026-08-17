/*---------------------------------------------------------------------------------------------
 *  Copyright (c) 2026 Nigel Rodriguez.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See LICENSE.md in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System.Collections.Generic;

namespace Neegool.Unity.Zed.Editor
{
	internal class SolutionProperties
	{
		public string Name { get; set; }
		public IList<KeyValuePair<string, string>> Entries { get; set; }
		public string Type { get; set; }
	}
}
