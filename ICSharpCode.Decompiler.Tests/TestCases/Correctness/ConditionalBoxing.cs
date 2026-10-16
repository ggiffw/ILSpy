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
	public static class ConditionalBoxing
	{
		static readonly List<string> Trace = new List<string>();
		static readonly uint[] Edges = { 0, 0x80000000, 1, 0x80000001, 0x007FFFFF, 0x00800000, 0x3F800001, 0xBF800001, 0x7F7FFFFF, 0xFF7FFFFF, 0x7F800000, 0xFF800000, 0x7FC01234, 0xFFC05678, 0x7F801234, 0xFF801234 };
		static readonly ulong[] WideEdges = { 0, 0x8000000000000000, 1, 0x000FFFFFFFFFFFFF, 0x0010000000000000, 0x3FF0000000000001, 0x7FEFFFFFFFFFFFFF, 0x7FF0000000000000, 0xFFF0000000000000, 0x7FF8000000001234, 0xFFF8000000005678, 0x7FF0000000001234 };

		static float ReadSingle(float value, int fault)
		{
			Trace.Add("single");
			if ((fault & 2) != 0)
				throw new InvalidOperationException();
			return value;
		}

		static double ReadDouble(double value, int fault)
		{
			Trace.Add("double");
			if ((fault & 2) != 0)
				throw new InvalidOperationException();
			return value;
		}

		static string Earlier(int fault)
		{
			Trace.Add("earlier");
			if ((fault & 1) != 0)
				throw new ArgumentException();
			return "prefix";
		}

		static string Record(object value)
		{
			Trace.Add("record");
			if (value is float)
				return "f:" + BitConverter.ToInt32(BitConverter.GetBytes((float)value), 0).ToString("X8");
			if (value is double)
				return "d:" + BitConverter.ToInt64(BitConverter.GetBytes((double)value), 0).ToString("X16");
			return value == null ? "null" : value.GetType().Name;
		}

		static string Record(string prefix, object value)
		{
			return prefix + Record(value);
		}

		public static string Single(bool choose, float value, int fault)
		{
			float selected;
			if (choose)
				selected = ReadSingle(value, fault);
			else
				selected = -0f;
			return Record(selected);
		}

		public static string Double(bool choose, double value, int fault)
		{
			double selected;
			if (choose)
				selected = ReadDouble(value, fault);
			else
				selected = -0.0;
			return Record(selected);
		}

		public static string ArraySingle(bool choose, float value, int fault)
		{
			string[] result = new string[1];
			float selected;
			if (choose)
				selected = ReadSingle(value, fault);
			else
				selected = -0f;
			result[0] = Record("literal", selected);
			return result[0];
		}

		public static string BeforeObservable(bool choose, float value, int fault)
		{
			float selected;
			if (choose)
				selected = ReadSingle(value, fault);
			else
				selected = -0f;
			return Record(Earlier(fault), selected);
		}

		public static string RepeatedUse(bool choose, float value, int fault)
		{
			float selected;
			if (choose)
				selected = ReadSingle(value, fault);
			else
				selected = -0f;
			return Record(selected) + Record(selected);
		}

		static void Mutate(ref float value)
		{
			Trace.Add("mutate");
			value += 1f;
		}

		public static string Aliased(bool choose, float value, int fault)
		{
			float selected;
			if (choose)
				selected = ReadSingle(value, fault);
			else
				selected = -0f;
			Mutate(ref selected);
			return Record(selected);
		}

		public static string NullableSingle(bool choose, float value, int fault)
		{
			float? selected;
			if (choose)
				selected = ReadSingle(value, fault);
			else
				selected = null;
			return Record(selected);
		}

		static void Run(Func<string> action)
		{
			Trace.Clear();
			string result;
			try
			{
				result = action();
			}
			catch (Exception e)
			{
				result = e.GetType().Name;
			}
			Console.WriteLine(result + ":" + string.Join(",", Trace));
		}

		public static void Main()
		{
			int cases = 0;
			foreach (bool choose in new[] { false, true })
			{
				for (int fault = 0; fault < 4; fault++)
				{
					foreach (uint bits in Edges)
					{
						float value = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
						Run(() => Single(choose, value, fault));
						Run(() => ArraySingle(choose, value, fault));
						Run(() => BeforeObservable(choose, value, fault));
						Run(() => RepeatedUse(choose, value, fault));
						Run(() => Aliased(choose, value, fault));
						Run(() => NullableSingle(choose, value, fault));
						cases += 6;
					}
					foreach (ulong bits in WideEdges)
					{
						double value = BitConverter.ToDouble(BitConverter.GetBytes(bits), 0);
						Run(() => Double(choose, value, fault));
						cases++;
					}
				}
			}
			Console.WriteLine("CASES=" + cases);
		}
	}
}
