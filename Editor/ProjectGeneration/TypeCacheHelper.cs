/*---------------------------------------------------------------------------------------------
 *  Copyright (c) 2026 Nigel Rodriguez.
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See LICENSE.md in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using SR = System.Reflection;

using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Neegool.Unity.Zed.Editor {

	internal class TypeCacheHelper
	{
		internal static IEnumerable<SR.MethodInfo> GetPostProcessorCallbacks(string name)
		{
			return TypeCache
				.GetTypesDerivedFrom<AssetPostprocessor>()
				.Select(t => t.GetMethod(name, SR.BindingFlags.Public | SR.BindingFlags.NonPublic | SR.BindingFlags.Static))
				.Where(m => m != null);
		}
	}

}
