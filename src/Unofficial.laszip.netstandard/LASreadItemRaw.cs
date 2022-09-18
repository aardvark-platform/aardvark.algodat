﻿//===============================================================================
//
//  FILE:  lasreaditemraw.cs
//
//  CONTENTS:
//
//    Common interface for all classes that read the items that compose a point.
//
//  PROGRAMMERS:
//
//    martin.isenburg@rapidlasso.com  -  http://rapidlasso.com
//
//  COPYRIGHT:
//
//    (c) 2007-2017, martin isenburg, rapidlasso - tools to catch reality
//    (c) of the C# port 2014-2018 by Shinta <shintadono@googlemail.com>
//
//    This is free software; you can redistribute and/or modify it under the
//    terms of the GNU Lesser General Licence as published by the Free Software
//    Foundation. See the COPYING file for more information.
//
//    This software is distributed WITHOUT ANY WARRANTY and without even the
//    implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
//
//  CHANGE HISTORY: omitted for easier Copy&Paste (pls see the original)
//
//===============================================================================

using System.IO;

namespace LASzip.Net
{
	abstract class LASreadItemRaw : LASreadItem
	{
		public bool init(Stream instream)
		{
			if (instream == null) return false;
			this.instream = instream;
			return true;
		}

		protected Stream instream = null;
	}
}
