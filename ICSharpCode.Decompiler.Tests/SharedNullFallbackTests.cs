// Copyright (c) 2026 JY.Z
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
using System.Linq;
using System.Reflection;

using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.Transforms;
using ICSharpCode.Decompiler.TypeSystem;

using NUnit.Framework;

namespace ICSharpCode.Decompiler.Tests
{
	[TestFixture]
	public class SharedNullFallbackTests
	{
		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		[TestCase(5)]
		[TestCase(6)]
		public void TerminalFallbacksRequireClosedReferenceNullTests(int invalid)
		{
			var decompiler = new CSharpDecompiler(typeof(object).Assembly.Location, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
			var objectType = decompiler.TypeSystem.FindType(KnownTypeCode.Object);
			var container = new BlockContainer();
			var identity = decompiler.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().First(m => m.Name == "MemberwiseClone");
			var function = new ILFunction(identity, 0, new GenericContext(), container);
			var value = function.RegisterVariable(VariableKind.Local, objectType, "value");
			var first = function.RegisterVariable(VariableKind.Parameter, objectType, "first");
			var second = function.RegisterVariable(VariableKind.Parameter, objectType, "second");
			var last = function.RegisterVariable(VariableKind.Parameter, objectType, "last");
			var entry = new Block();
			var fallback = new Block();
			var terminal = new Block();
			var success = new Block();
			container.Blocks.AddRange(new[] { entry, fallback, terminal, success });
			void Guard(Block block, ILVariable input, Block next)
			{
				block.Instructions.Add(new StLoc(value, new LdLoc(input)));
				block.Instructions.Add(new IfInstruction(new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(value), new LdNull()), new Branch(next)));
				block.Instructions.Add(new Branch(success));
			}
			Guard(entry, first, fallback);
			Guard(fallback, second, terminal);
			terminal.Instructions.Add(new Leave(container, new LdLoc(last)));
			success.Instructions.Add(new Leave(container, new LdLoc(value)));
			if (invalid == 1) terminal.Instructions[0] = new Leave(container, new LdLoc(value));
			if (invalid == 2) terminal.Instructions.Insert(0, new StLoc(last, new LdLoca(value)));
			if (invalid == 3) terminal.Instructions[0] = new Branch(entry);
			if (invalid == 4) success.Instructions[0] = new Leave(container, new LdLoc(first));
			if (invalid == 5) ((Comp)((IfInstruction)entry.Instructions[1]).Condition).Kind = ComparisonKind.Inequality;
			// Connect the synthetic root in both the signed suite and standalone hosts.
			typeof(ILInstruction).GetMethod("AddRef", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(function, null);
			var context = decompiler.CreateILTransformContext(function);
			if (invalid == 6)
			{
				context.Settings.RemoveDeadCode = false;
				var unreachable = new Block();
				unreachable.Instructions.Add(new Leave(container, new LdNull()));
				container.Blocks.Add(unreachable);
			}
			((IILTransform)new NullCoalescingTransform()).Run(function, context);
			if (invalid == 0 || invalid == 6)
			{
				Assert.That(container.Blocks.Count, Is.EqualTo(invalid == 6 ? 2 : 1));
				Assert.That(entry.Instructions.Single(), Is.TypeOf<Leave>());
				Assert.That(entry.Descendants.OfType<NullCoalescingInstruction>().Count(), Is.EqualTo(2));
				Assert.That(value.LoadCount, Is.Zero);
				Assert.That(value.StoreCount, Is.Zero);
			}
			else
			{
				Assert.That(entry.Instructions.Count, Is.EqualTo(3));
			}
		}
	}
}

