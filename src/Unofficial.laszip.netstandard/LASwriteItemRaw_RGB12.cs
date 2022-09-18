﻿//===============================================================================
//
//  FILE:  laswriteitemraw_rgb12.cs
//
//  CONTENTS:
//
//    Implementation of LASwriteItemRaw for RGB12 items.
//
//  PROGRAMMERS:
//
//    martin.isenburg@rapidlasso.com  -  http://rapidlasso.com
//
//  COPYRIGHT:
//
//    (c) 2007-2017, martin isenburg, rapidlasso - tools to catch reality
//    (c) of the C# port 2014-2019 by Shinta <shintadono@googlemail.com>
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

using System;

namespace LASzip.Net
{
	class LASwriteItemRaw_RGB12 : LASwriteItemRaw
	{
		public LASwriteItemRaw_RGB12() { }

		public override bool write(laszip_point item, ref uint context)
		{
			try
			{
				outstream.Write(BitConverter.GetBytes(item.rgb[0]), 0, 2);
				outstream.Write(BitConverter.GetBytes(item.rgb[1]), 0, 2);
				outstream.Write(BitConverter.GetBytes(item.rgb[2]), 0, 2);
			}
			catch
			{
				return false;
			}

			return true;
		}
	}
}
