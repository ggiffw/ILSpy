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
	public static class LoopLocalFunctions
	{
		public static void Main()
		{
			int[][] inputs = { new int[0], new[] { 1, 2, 3 }, new[] { 7 }, new[] { 99 }, new[] { -1, 5 }, new[] { int.MaxValue, int.MinValue } };
			foreach (int[] input in inputs)
			{
				Run(() => Unstructured(input));
				Run(() => While(input));
				Run(() => DoWhile(input));
				Run(() => For(input));
				Run(() => Escaping(input));
			}
		}

		static void Run(Func<int> action)
		{
			try
			{
				Console.WriteLine(action());
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.GetType().FullName);
			}
		}

		static int Unstructured(int[] input)
		{
			int sum = 0;
			int index = 0;
			while (true)
			{
				if (index >= input.Length)
					return sum;
				int item = input[index];
				int offset = index * 3;
				int Read(int delta)
				{
					offset += delta;
					if (item == 7 && delta == 1)
						input = null;
					return item + offset + sum;
				}
				int first = Read(1);
				int second = Read(-1);
				sum += first + second;
				if (item == 99)
					break;
				index++;
				if (input == null)
					throw new NullReferenceException();
			}
			throw new InvalidOperationException();
		}

		static int While(int[] input)
		{
			int sum = 0;
			int i = 0;
			while (i < input.Length)
			{
				int item = input[i++];
				int Read() => item++ + sum;
				sum += Read() + Read();
			}
			return sum;
		}

		static int DoWhile(int[] input)
		{
			if (input.Length == 0)
				return 0;
			int sum = 0;
			int i = 0;
			do
			{
				int item = input[i++];
				int Read() => item++ + sum;
				sum += Read() + Read();
			} while (i < input.Length);
			return sum;
		}

		static int For(int[] input)
		{
			int sum = 0;
			for (int i = 0; i < input.Length; i++)
			{
				int item = input[i];
				int Read() => item++ + sum;
				sum += Read() + Read();
			}
			return sum;
		}

		static int Escaping(int[] input)
		{
			var actions = new List<Func<int>>();
			for (int i = 0; i < input.Length; i++)
			{
				int item = input[i];
				int Read() => item;
				actions.Add(Read);
				item++;
			}
			int sum = 0;
			foreach (var action in actions)
				sum += action();
			return sum;
		}
	}
}
