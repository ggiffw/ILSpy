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
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace ICSharpCode.Decompiler.Tests.TestCases.Correctness
{
	public class PositiveBooleanCallConditions
	{
		static int calls;
		static int fault;
		static bool Probe(int value, out int snapshot)
		{
			Console.Write("P" + ++calls + ";");
			if (fault == calls)
				throw new InvalidOperationException();
			snapshot = value;
			return (value & 1) == 0;
		}

		static int Arm(int value)
		{
			Console.Write("A" + value + ";");
			if (fault == ++calls)
				throw new ArgumentException();
			return value;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int PositiveIf(int value)
		{
			if (!Probe(value, out int snapshot)) {
				Console.Write("F;");
				Arm(-1);
			} else {
				Console.Write("T;");
				Arm(snapshot);
			}
			return calls;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int PositiveChoice(int value)
		{
			return !Probe(value, out int snapshot) ? Arm(-1) : Arm(snapshot);
		}

		struct Odd
		{
			public int Value;
			public static bool operator ==(Odd left, Odd right)
			{
				Console.Write("EQ;");
				return left.Value == right.Value;
			}
			public static bool operator !=(Odd left, Odd right)
			{
				throw new Exception("Inequality is not the complement of equality.");
			}
			public static bool operator !(Odd value)
			{
				Console.Write("NOT;");
				return value.Value == 0;
			}
			public override bool Equals(object obj) { return obj is Odd && ((Odd)obj).Value == Value; }
			public override int GetHashCode() { return Value; }
		}

		static int EqualityChoice(Odd left, Odd right)
		{
			return !(left == right) ? Arm(-1) : Arm(1);
		}
		static int UserNot(Odd value) { return !value ? Arm(-1) : Arm(1); }
		static int Guard(int value)
		{
			if (!Probe(value, out int snapshot))
				return -1;
			return Arm(snapshot);
		}
		static int NullableChoice(bool? value) { return !value == true ? Arm(-1) : Arm(1); }
		static int FloatingChoice(float value) { return !(value < 0) ? Arm(-1) : Arm(1); }
		static Expression<Func<int, int>> Tree()
		{
			return value => !IsEven(value) ? Arm(-1) : Arm(1);
		}
		static bool IsEven(int value) { Console.Write("T;"); return (value & 1) == 0; }

		public static void Main()
		{
			for (int value = -2; value <= 2; value++) {
				for (fault = 0; fault <= 3; fault++) {
					Run(() => PositiveIf(value));
					Run(() => PositiveChoice(value));
					Run(() => Guard(value));
					Run(() => EqualityChoice(new Odd { Value = value }, new Odd { Value = 0 }));
					Run(() => UserNot(new Odd { Value = value }));
					Run(() => NullableChoice(value == 0 ? (bool?)null : value > 0));
				}
			}
			fault = 0;
			foreach (float value in new[] { 0f, -0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.Epsilon })
				Run(() => FloatingChoice(value));
			Expression<Func<int, int>> tree = Tree();
			Console.WriteLine(tree.Body.NodeType + ";" + ((ConditionalExpression)tree.Body).Test.NodeType);
			Run(() => tree.Compile()(0));
			Run(() => tree.Compile()(1));
		}
		static void Run(Func<int> run)
		{
			calls = 0;
			try { Console.WriteLine("R" + run()); }
			catch (Exception e) { Console.WriteLine(e.GetType().Name); }
		}
	}
}
