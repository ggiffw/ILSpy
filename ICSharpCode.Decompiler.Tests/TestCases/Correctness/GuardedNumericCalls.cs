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
using System.Runtime.CompilerServices;

namespace ICSharpCode.Decompiler.Tests.TestCases.Correctness
{
	public static class GuardedNumericCalls
	{
		static readonly List<string> Trace = new List<string>();
		static readonly uint[] Edges = { 0, 0x80000000, 1, 0x80000001, 0x007FFFFF, 0x00800000, 0x3F800001, 0xBF800001, 0x7F7FFFFF, 0xFF7FFFFF, 0x7F800000, 0xFF800000, 0x7FC01234, 0xFFC05678, 0x7F801234, 0xFF801234 };
		static readonly ulong[] WideEdges = { 0, 0x8000000000000000, 1, 0x000FFFFFFFFFFFFF, 0x0010000000000000, 0x3FF0000000000001, 0x7FEFFFFFFFFFFFFF, 0x7FF0000000000000, 0xFFF0000000000000, 0x7FF8000000001234, 0xFFF8000000005678, 0x7FF0000000001234 };
		static float Current;
		static int Fault;
		static bool Enabled;
		static float[] Array;

		[MethodImpl(MethodImplOptions.NoInlining)]
		static float ReadSingle(float value) { Trace.Add("read-single"); if (Fault == 1) throw new InvalidOperationException(); return value; }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static double ReadDouble(double value) { Trace.Add("read-double"); if (Fault == 1) throw new InvalidOperationException(); return value; }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int First() { Trace.Add("first"); Current = -Current; if (Fault == 2) throw new ArgumentException(); return 0; }
		static void Sink(int first, float value) { Trace.Add("sink:" + BitConverter.ToUInt32(BitConverter.GetBytes(value), 0).ToString("X8")); if (Fault == 3) throw new ApplicationException(); }
		static void SinkDouble(int first, double value) { Trace.Add("sink-wide:" + BitConverter.ToUInt64(BitConverter.GetBytes(value), 0).ToString("X16")); if (Fault == 3) throw new ApplicationException(); }
		static void Mutate(ref float value) { Trace.Add("ref"); value = -value; }

		static void LastSingle(float value) { float snapshot = ReadSingle(value); Sink(0, snapshot); }
		static void LastDouble(double value) { double snapshot = ReadDouble(value); SinkDouble(0, snapshot); }
		static void ObservableFirst(float value) { float snapshot = ReadSingle(value); Sink(First(), snapshot); }
		static void ThrowingFirst(float value) { float snapshot = ReadSingle(value); Sink((int)Array[0], snapshot); }
		static void ByReference(float value) { float snapshot = ReadSingle(value); Mutate(ref snapshot); Sink(0, snapshot); }
		static void TwoUses(float value) { float snapshot = ReadSingle(value); Sink(0, snapshot); Sink(0, snapshot); }
		static void PreserveRead(float value) { float snapshot = ReadSingle(Current); First(); Sink(0, snapshot); }
		static void Widen(float value) { float snapshot = ReadSingle(value); SinkDouble(0, snapshot); }

		static bool Check() { Trace.Add("check"); if (Fault == 1) throw new InvalidOperationException(); return Enabled; }
		static int Receiver() { Trace.Add("receiver"); if (Fault == 2) throw new ArgumentException(); return 7; }
		static void GuardLoop(int[] states)
		{
			foreach (int state in states) {
				if (state <= 2) {
					if (state == 0) {
						if (!Check()) continue;
						int receiver = Receiver();
						if (receiver > 0) Trace.Add("body");
						continue;
					}
					if (state != 2) continue;
					Trace.Add("two");
				} else {
					if (state != 3) { Trace.Add("other"); continue; }
					Trace.Add("three");
				}
				Trace.Add("shared");
			}
		}

		static void GuardWithFollowingWork(int[] states)
		{
			foreach (int state in states) {
				if (!Check()) continue;
				Trace.Add("body");
				if (state != 0) Trace.Add("tail");
			}
		}

		static void FinallyLoop(int[] states)
		{
			foreach (int state in states) {
				try {
					if (state == 0) {
						if (!Check()) continue;
						Trace.Add("body");
						continue;
					}
					Trace.Add("shared");
				} finally { Trace.Add("finally"); }
			}
		}

		public static void Main()
		{
			int count = 0;
			for (int fault = 0; fault < 4; fault++) {
				Fault = fault;
				foreach (uint bits in Edges) {
					float value = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
					foreach (Action<float> action in new Action<float>[] { LastSingle, ObservableFirst, ThrowingFirst, ByReference, TwoUses, PreserveRead, Widen }) {
						Trace.Clear(); Current = value; Array = null;
						try { action(value); } catch (Exception e) { Trace.Add(e.GetType().Name); }
						Console.WriteLine(string.Join(",", Trace)); count++;
					}
				}
				foreach (ulong bits in WideEdges) {
					Trace.Clear();
					try { LastDouble(BitConverter.ToDouble(BitConverter.GetBytes(bits), 0)); } catch (Exception e) { Trace.Add(e.GetType().Name); }
					Console.WriteLine(string.Join(",", Trace)); count++;
				}
				foreach (bool enabled in new[] { false, true }) {
					Enabled = enabled;
					foreach (Action<int[]> action in new Action<int[]>[] { GuardLoop, GuardWithFollowingWork, FinallyLoop }) {
						Trace.Clear();
						try { action(new[] { int.MinValue, -1, 0, 1, 2, 3, 4, int.MaxValue }); } catch (Exception e) { Trace.Add(e.GetType().Name); }
						Console.WriteLine(string.Join(",", Trace)); count++;
					}
				}
			}
			Console.WriteLine("CASES=" + count);
		}
	}
}
