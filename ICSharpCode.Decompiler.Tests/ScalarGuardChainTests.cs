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

using System.Linq;
using System.Reflection;

using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.ControlFlow;
using ICSharpCode.Decompiler.TypeSystem;

using NUnit.Framework;
namespace ICSharpCode.Decompiler.Tests
{
	[TestFixture]
	public class ScalarGuardChainTests
	{
		static (CSharpDecompiler Decompiler, ILFunction Function, BlockContainer Body, Block Entry, ILVariable Value) Create(ILFunctionKind kind = ILFunctionKind.TopLevelFunction)
		{
			var dec = new CSharpDecompiler(typeof(object).Assembly.Location, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
			var identity = dec.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().First(m => m.Name == "GetHashCode");
			var body = new BlockContainer();
			var function = new ILFunction(identity, 0, new GenericContext(), body, kind);
			var value = function.RegisterVariable(VariableKind.Parameter, dec.TypeSystem.FindType(KnownTypeCode.Int32), "value");
			var entry = new Block();
			body.Blocks.Add(entry);
			typeof(ILInstruction).GetMethod("AddRef", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(function, null);
			return (dec, function, body, entry, value);
		}
		static Block Arm(params ILInstruction[] instructions) { var block = new Block(); block.Instructions.AddRange(instructions); return block; }
		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		public void PreserveTwoCaseEarlyReturnChain(int mutation)
		{
			var (dec, function, body, entry, value) = Create();
			var next = new Block();
			var first = new Block();
			var second = new Block();
			var fallback = new Block();
			body.Blocks.AddRange(new[] { next, first, second, fallback });
			entry.Instructions.Add(new IfInstruction(new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(value), new LdcI4(7)), new Branch(first)));
			entry.Instructions.Add(new Branch(next));
			next.Instructions.Add(new IfInstruction(new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(value), new LdcI4(9)), new Branch(second)));
			next.Instructions.Add(new Branch(fallback));
			first.Instructions.Add(new StLoc(value, new LdcI4(10)));
			first.Instructions.Add(new Leave(body, new LdcI4(mutation == 1 ? 3 : 0)));
			second.Instructions.Add(new StLoc(value, new LdcI4(20)));
			second.Instructions.Add(new Leave(body, new LdcI4(2)));
			fallback.Instructions.Add(new Leave(body, new LdcI4(0)));
			if (mutation == 2)
				fallback.Instructions.Insert(0, new StLoc(value, new LdcI4(30)));
			if (mutation == 3)
			{
				entry.Instructions.Clear();
				var sw = new SwitchInstruction(new LdLoc(value));
				sw.Sections.Add(new SwitchSection { Labels = new Util.LongSet(7), Body = new Branch(first) });
				sw.Sections.Add(new SwitchSection { Labels = new Util.LongSet(9), Body = new Branch(second) });
				sw.Sections.Add(new SwitchSection { Labels = Util.LongSet.Universe.ExceptWith(new Util.LongSet(7).UnionWith(new Util.LongSet(9))), Body = new Branch(fallback) });
				entry.Instructions.Add(sw);
				next.Remove();
			}
			if (mutation == 4)
			{
				var extra = new Block();
				var test = new Block();
				body.Blocks.AddRange(new[] { test, extra });
				next.Instructions[1] = new Branch(test);
				test.Instructions.Add(new IfInstruction(new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(value), new LdcI4(11)), new Branch(extra)));
				test.Instructions.Add(new Branch(fallback));
				extra.Instructions.Add(new Leave(body, new LdcI4(4)));
			}
			new SwitchDetection().Run(function, dec.CreateILTransformContext(function));
			Assert.That(function.Descendants.OfType<SwitchInstruction>().Any(), Is.EqualTo(mutation != 0));
		}
		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		[TestCase(5)]
		[TestCase(6)]
		public void MergeOnlyAdjacentIdenticalIntegerReturns(int mutation)
		{
			var (dec, function, body, entry, value) = Create();
			ILInstruction Result(int n) => mutation == 4 ? new LdLoc(value) : mutation == 5 ? new LdcF4(n == 0 ? 0f : -0f) : mutation == 6 ? new LdcI8(n) : new LdcI4(n);
			var first = new IfInstruction(new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(value), new LdcI4(1)), Arm(new Leave(body, Result(0))));
			var second = new IfInstruction(new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(value), new LdcI4(2)), Arm(new Leave(body, Result(mutation == 1 || mutation == 5 ? 1 : 0))));
			entry.Instructions.Add(first);
			if (mutation == 3)
				entry.Instructions.Add(new StLoc(value, new LdcI4(5)));
			entry.Instructions.Add(second);
			if (mutation == 2)
				second.FalseInst = new StLoc(value, new LdcI4(4));
			entry.Instructions.Add(new Leave(body, new LdcI4(8)));
			new ReduceNestingTransform().Run(function, dec.CreateILTransformContext(function));
			Assert.That(first.Condition is IfInstruction, Is.EqualTo(mutation == 0 || mutation == 6));
		}
		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		public void ExtractShortTerminalAlternativeWithoutDuplicatingEffects(int mutation)
		{
			var (dec, function, body, entry, value) = Create();
			var guard = new IfInstruction(new Comp(ComparisonKind.Inequality, Sign.None, new LdLoc(value), new LdcI4(9)), Arm(new Leave(body, new LdcI4(mutation == 1 ? 1 : 0))));
			var alternative = Arm(guard, new StLoc(value, new LdcI4(10)), new StLoc(value, new LdcI4(20)), new Leave(body, new LdcI4(4)));
			var outer = new IfInstruction(new Comp(ComparisonKind.Inequality, Sign.None, new LdLoc(value), new LdcI4(7)), alternative);
			entry.Instructions.Add(outer);
			entry.Instructions.Add(new StLoc(value, new LdcI4(30)));
			entry.Instructions.Add(new Leave(body, new LdcI4(0)));
			if (mutation == 2)
				alternative.Instructions.RemoveAt(0);
			if (mutation == 3)
				entry.Instructions.Insert(2, new StLoc(value, new LdcI4(40)));
			if (mutation == 4)
				outer.FalseInst = new StLoc(value, new LdcI4(50));
			new ReduceNestingTransform().Run(function, dec.CreateILTransformContext(function));
			Assert.That(entry.Instructions.Count, Is.EqualTo(mutation == 0 ? 5 : mutation == 3 ? 4 : 3));
		}
		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		[TestCase(5)]
		[TestCase(6)]
		[TestCase(7)]
		[TestCase(8)]
		[TestCase(9)]
		[TestCase(10)]
		[TestCase(11)]
		[TestCase(12)]
		public void InlineEagerBooleanBeforeImmutableComparison(int mutation)
		{
			var (dec, function, body, entry, value) = Create(mutation is 11 or 12 ? ILFunctionKind.ExpressionTree : ILFunctionKind.TopLevelFunction);
			var boolean = dec.TypeSystem.FindType(KnownTypeCode.Boolean);
			var snapshot = function.RegisterVariable(VariableKind.Local, boolean, "snapshot");
			var source = dec.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().First(m => m.Name == "Equals" && m.IsStatic && m.Parameters.Count == 2);
			var effect = new Call(source);
			effect.Arguments.Add(new LdNull());
			effect.Arguments.Add(new LdNull());
			var store = new StLoc(snapshot, effect);
			entry.Instructions.Add(store);
			ILInstruction stable = new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(value), new LdcI4(7));
			if (mutation == 1)
				stable = new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(function.RegisterVariable(VariableKind.Local, value.Type, "local")), new LdcI4(7));
			if (mutation == 2)
				stable = new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(snapshot), new LdcI4(0));
			if (mutation == 3)
				stable = effect.Clone();
			if (mutation == 4)
				stable = new Comp(ComparisonKind.Equality, Sign.None, new LdcF4(1), new LdcF4(2));
			var test = new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(snapshot), new LdcI4(0));
			var inner = IfInstruction.LogicAnd(test, new LdcI4(1), dec.TypeSystem);
			var condition = mutation == 5 ? IfInstruction.LogicOr(stable, inner, dec.TypeSystem) : IfInstruction.LogicAnd(stable, inner, dec.TypeSystem);
			var guard = new IfInstruction(condition, new Leave(body, new LdcI4(1)));
			entry.Instructions.Add(guard);
			entry.Instructions.Add(new Leave(body, mutation == 6 ? new LdLoc(snapshot) : new LdcI4(0)));
			if (mutation == 7)
				entry.Instructions.Insert(0, new StLoc(value, new LdcI4(8)));
			if (mutation == 8)
				entry.Instructions.Add(new StLoc(function.RegisterVariable(VariableKind.Local, new ByReferenceType(value.Type), "alias"), new LdLoca(value)));
			if (mutation == 9)
				entry.Instructions.Add(new StLoc(function.RegisterVariable(VariableKind.Local, new ByReferenceType(boolean), "alias"), new LdLoca(snapshot)));
			if (mutation == 10)
				test.Kind = ComparisonKind.Inequality;
			var transformContext = dec.CreateILTransformContext(function);
			if (mutation == 12)
			{
				var outer = Create();
				outer.Entry.Instructions.Add(new Leave(outer.Body, function));
				transformContext = dec.CreateILTransformContext(outer.Function);
			}
			var before = function.ToString();
			bool changed = IL.Transforms.ILInlining.InlineOneIfPossible(entry, mutation == 7 ? 1 : 0, IL.Transforms.InliningOptions.None, transformContext);
			Assert.That(changed, Is.EqualTo(mutation == 0));
			if (mutation == 0)
			{ Assert.That(snapshot.LoadCount, Is.Zero); Assert.That(snapshot.StoreCount, Is.Zero); Assert.That(guard.Condition.Descendants.OfType<Call>().Count(), Is.EqualTo(1)); }
			else
				Assert.That(function.ToString(), Is.EqualTo(before));
		}
	}
}