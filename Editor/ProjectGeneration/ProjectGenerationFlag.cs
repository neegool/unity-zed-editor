/*---------------------------------------------------------------------------------------------
 *  Copyright (c) 2026 Nigel Rodriguez.
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See LICENSE.md in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;

namespace Neegool.Unity.Zed.Editor
{
	[Flags]
	public enum ProjectGenerationFlag
	{
		None = 0,
		Embedded = 1,
		Local = 2,
		Registry = 4,
		Git = 8,
		BuiltIn = 16,
		Unknown = 32,
		PlayerAssemblies = 64,
		LocalTarBall = 128,
	}
}
