﻿//===============================================================================
//
//  FILE:  laszip_common_v2.cs
//
//  CONTENTS:
//
//    Common defines and functionalities for version 2 of LASitemReadCompressed
//    and LASitemwriteCompressed.
//
//  PROGRAMMERS:
//
//    martin.isenburg@rapidlasso.com  -  http://rapidlasso.com
//
//  COPYRIGHT:
//
//    (c) 2005-2012, martin isenburg, rapidlasso - tools to catch reality
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

namespace LASzip.Net
{
	class Laszip_Common_v2
	{
		// for LAS files with the return (r) and the number (n) of
		// returns field correctly populated the mapping should really
		// be only the following.
		//  { 15, 15, 15, 15, 15, 15, 15, 15 },
		//  { 15,  0, 15, 15, 15, 15, 15, 15 },
		//  { 15,  1,  2, 15, 15, 15, 15, 15 },
		//  { 15,  3,  4,  5, 15, 15, 15, 15 },
		//  { 15,  6,  7,  8,  9, 15, 15, 15 },
		//  { 15, 10, 11, 12, 13, 14, 15, 15 },
		//  { 15, 15, 15, 15, 15, 15, 15, 15 },
		//  { 15, 15, 15, 15, 15, 15, 15, 15 }
		// however, some files start the numbering of r and n with 0,
		// only have return counts r, or only have number of return
		// counts n, or mix up the position of r and n. we therefore
		// "complete" the table to also map those "undesired" r & n
		// combinations to different contexts
		internal static readonly byte[,] number_return_map = new byte[,]
		{
			{ 15, 14, 13, 12, 11, 10,  9,  8 },
			{ 14,  0,  1,  3,  6, 10, 10,  9 },
			{ 13,  1,  2,  4,  7, 11, 11, 10 },
			{ 12,  3,  4,  5,  8, 12, 12, 11 },
			{ 11,  6,  7,  8,  9, 13, 13, 12 },
			{ 10, 10, 11, 12, 13, 14, 14, 13 },
			{  9, 10, 11, 12, 13, 14, 15, 14 },
			{  8,  9, 10, 11, 12, 13, 14, 15 }
		};

		// for LAS files with the return (r) and the number (n) of
		// returns field correctly populated the mapping should really
		// be only the following.
		//  {  0,  7,  7,  7,  7,  7,  7,  7 },
		//  {  7,  0,  7,  7,  7,  7,  7,  7 },
		//  {  7,  1,  0,  7,  7,  7,  7,  7 },
		//  {  7,  2,  1,  0,  7,  7,  7,  7 },
		//  {  7,  3,  2,  1,  0,  7,  7,  7 },
		//  {  7,  4,  3,  2,  1,  0,  7,  7 },
		//  {  7,  5,  4,  3,  2,  1,  0,  7 },
		//  {  7,  6,  5,  4,  3,  2,  1,  0 }
		// however, some files start the numbering of r and n with 0,
		// only have return counts r, or only have number of return
		// counts n, or mix up the position of r and n. we therefore
		// "complete" the table to also map those "undesired" r & n
		// combinations to different contexts
		// FunFact: number_return_level[r, n] == Math.Abs(r-n);
		internal static readonly byte[,] number_return_level = new byte[,]
		{
			{  0,  1,  2,  3,  4,  5,  6,  7 },
			{  1,  0,  1,  2,  3,  4,  5,  6 },
			{  2,  1,  0,  1,  2,  3,  4,  5 },
			{  3,  2,  1,  0,  1,  2,  3,  4 },
			{  4,  3,  2,  1,  0,  1,  2,  3 },
			{  5,  4,  3,  2,  1,  0,  1,  2 },
			{  6,  5,  4,  3,  2,  1,  0,  1 },
			{  7,  6,  5,  4,  3,  2,  1,  0 }
		};
	}
}
