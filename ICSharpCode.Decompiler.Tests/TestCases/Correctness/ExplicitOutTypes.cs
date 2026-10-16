// Copyright (c) 2026 joyce
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;

namespace ICSharpCode.Decompiler.Tests.TestCases.Correctness
{
	public class ExplicitOutTypes
	{
		enum Kind { First, Second }
		struct Rotation { public float X; }
		static readonly List<string> Calls = new List<string>();
		static readonly uint[] Edges = { 0, 0x80000000, 1, 0x80000001, 0x007FFFFF, 0x00800000, 0x3F800000, 0xBF800000, 0x7F7FFFFF, 0xFF7FFFFF, 0x7F800000, 0xFF800000, 0x7FC01234, 0xFFC05678, 0x7F801234, 0xFF801234 };
		static void Choose(int seed, out Kind kind, out Rotation rotation)
		{
			Calls.Add("choose");
			kind = (Kind)(seed & 1);
			rotation = new Rotation { X = BitConverter.Int32BitsToSingle(unchecked((int)Edges[seed & 15])) };
		}
		static bool Echo<T>(T input, out T output) { Calls.Add("echo"); output = input; return true; }
		static void Overload(out int value) { Calls.Add("int"); value = 7; }
		static void Overload(out string value) { Calls.Add("string"); value = "text"; }
		static void Both(out int value, ref int same) { Calls.Add("alias"); value = 11; same++; }
		static bool Tuple(out (int Code, string Text) value) { Calls.Add("tuple"); value = (3, "tuple"); return true; }
		static string Run(int seed)
		{
			Calls.Clear();
			var dictionary = new Dictionary<int, string> { { 1, "found" } };
			Choose(seed, out Kind kind, out Rotation rotation);
			bool found = (seed & 1) == 0 && dictionary.TryGetValue(1, out string text);
			Echo(seed, out int captured);
			Action change = () => captured++;
			change();
			Overload(out int number);
			Overload(out string word);
			int alias = 0;
			Both(out alias, ref alias);
			Tuple(out (int Code, string Text) tuple);
			Echo(new { Value = seed }, out var anonymous);
			Echo(seed, out int _);
			try
			{
				Dictionary<int, string> missing = (seed & 2) == 0 ? null : dictionary;
				missing.TryGetValue(1, out string ignored);
			}
			catch (NullReferenceException) { Calls.Add("null"); }
			return seed + ":" + kind + ":" + BitConverter.SingleToInt32Bits(rotation.X).ToString("X8")
				+ ":" + found + ":" + captured + ":" + number + ":" + word + ":" + alias
				+ ":" + tuple.Code + ":" + tuple.Text + ":" + anonymous.Value + ":" + string.Join(",", Calls);
		}
		public static void Main()
		{
			for (int seed = 0; seed < 32; seed++)
				Console.WriteLine(Run(seed));
		}
	}
}
