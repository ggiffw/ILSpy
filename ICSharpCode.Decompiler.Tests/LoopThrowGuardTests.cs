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
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;

using NUnit.Framework;

namespace ICSharpCode.Decompiler.Tests;

[TestFixture]
public class LoopThrowGuardTests
{
	static Block Arm(params ILInstruction[] instructions)
	{
		var block = new Block();
		block.Instructions.AddRange(instructions);
		return block;
	}

	[TestCase(0, true)]
	[TestCase(1, true)]
	[TestCase(2, true)]
	[TestCase(3, false)]
	[TestCase(4, false)]
	[TestCase(5, false)]
	[TestCase(6, false)]
	[TestCase(7, false)]
	[TestCase(8, false)]
	[TestCase(9, false)]
	[TestCase(10, false)]
	[TestCase(11, true)]
	[TestCase(12, false)]
	[TestCase(13, true)]
	public void KeepOnlyAClosedPureLoopEntryThrowGuard(int mutation, bool expectedFlat)
	{
		var dec = new CSharpDecompiler(typeof(object).Assembly.Location, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
		var integer = dec.TypeSystem.FindType(KnownTypeCode.Int32);
		var exception = dec.TypeSystem.FindType(new FullTypeName("System.IndexOutOfRangeException"));
		var ctor = exception.GetConstructors().Single(m => m.Parameters.Count == 0);
		var identity = dec.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().Single(m => m.Name == "GetHashCode" && m.Parameters.Count == 0);
		var effect = dec.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().Single(m => m.Name == "ReferenceEquals");
		var body = new BlockContainer();
		var function = new ILFunction(identity, 0, new GenericContext(), body, ILFunctionKind.TopLevelFunction);
		var outer = new Block();
		body.Blocks.Add(outer);
		var loop = new BlockContainer(ContainerKind.Loop);
		var block = new Block();
		loop.Blocks.Add(block);
		outer.Instructions.Add(loop);
		outer.Instructions.Add(new Leave(body, new LdcI4(0)));
		var addRef = typeof(ILInstruction).GetMethod("AddRef", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(addRef, Is.Not.Null);
		addRef!.Invoke(function, null);
		var index = function.RegisterVariable(VariableKind.Parameter, integer, "index");
		var length = function.RegisterVariable(VariableKind.Parameter, integer, "length");
		var local = function.RegisterVariable(VariableKind.Local, integer, "value");
		ILInstruction condition = new Comp(ComparisonKind.GreaterThanOrEqual, Sign.Unsigned, new LdLoc(index), new LdLoc(length));
		if (mutation == 3)
			condition = new Call(effect) { Arguments = { new LdNull(), new LdNull() } };
		if (mutation == 4)
			condition = new Comp(ComparisonKind.Equality, Sign.None,
				new BinaryNumericInstruction(BinaryNumericOperator.Div, new LdLoc(index), new LdLoc(length), false, Sign.Signed), new LdcI4(0));
		if (mutation == 12)
		{
			var array = function.RegisterVariable(VariableKind.Parameter, new ArrayType(dec.TypeSystem, integer), "array");
			condition = new Comp(ComparisonKind.GreaterThanOrEqual, Sign.Signed, new LdLoc(index), new LdLen(StackType.I4, new LdLoc(array)));
		}
		ILInstruction thrown = new Throw(new NewObj(ctor));
		if (mutation == 8)
		{
			var messageCtor = exception.GetConstructors().Single(m => m.Parameters.Count == 1 && m.Parameters[0].Type.IsKnownType(KnownTypeCode.String));
			thrown = new Throw(new NewObj(messageCtor) { Arguments = { new LdStr("failure") } });
		}
		if (mutation == 9)
			thrown = new Throw(new LdLoc(function.RegisterVariable(VariableKind.Parameter, exception, "failure")));
		thrown.SetILRange(new Interval(mutation == 13 ? 10 : 300, mutation == 13 ? 11 : 301));
		var guard = new IfInstruction(condition, mutation == 1 ? Arm(thrown) : thrown);
		if (mutation == 1)
			guard.TrueInst.SetILRange(new Interval(300, 301));
		if (mutation == 10)
		{
			guard.TrueInst = Arm(new StLoc(local, new LdcI4(7)), thrown);
			guard.TrueInst.SetILRange(new Interval(300, 301));
		}
		if (mutation == 11)
			guard.FalseInst = new StLoc(local, new LdcI4(9));
		if (mutation == 6)
			block.Instructions.Add(new StLoc(local, new LdcI4(1)));
		block.Instructions.Add(guard);
		var first = new Call(effect) { Arguments = { new LdNull(), new LdNull() } };
		first.SetILRange(new Interval(100, 101));
		block.Instructions.Add(first);
		IfInstruction? adjacent = null;
		if (mutation == 2)
		{
			adjacent = new IfInstruction(new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(index), new LdcI4(1)), new Leave(body, new LdcI4(0)));
			block.Instructions.Add(adjacent);
			block.Instructions.Add(new IfInstruction(new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(index), new LdcI4(2)), new Leave(body, new LdcI4(0))));
		}
		if (mutation == 5)
			block.Instructions.Add(new IfInstruction(new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(index), new LdcI4(1)), new Branch(block)));
		var second = new Call(effect) { Arguments = { new LdNull(), new LdNull() } };
		block.Instructions.Add(second);
		if (mutation == 7)
		{
			var tail = new Block();
			loop.Blocks.Add(tail);
			block.Instructions.Add(new Branch(tail));
			tail.Instructions.Add(new Branch(block));
		}
		else
			block.Instructions.Add(new Branch(block));
		var callsBefore = function.Descendants.OfType<Call>().ToArray();
		var throwsBefore = function.Descendants.OfType<Throw>().ToArray();
		var conditionBefore = condition.Clone();

		new ReduceNestingTransform().Run(function, dec.CreateILTransformContext(function));

		Assert.That(Block.Unwrap(guard.TrueInst) is Throw, Is.EqualTo(expectedFlat));
		if (expectedFlat)
			Assert.That(guard.Condition.Match(conditionBefore).Success, Is.True);
		Assert.That(function.Descendants.OfType<Call>().ToArray(), Is.EqualTo(callsBefore), "Guard formatting must preserve every call and its order.");
		Assert.That(function.Descendants.OfType<Throw>().ToArray(), Is.EqualTo(throwsBefore), "Guard formatting must preserve the original throw node.");
		if (adjacent != null)
			Assert.That(adjacent.Condition is IfInstruction, Is.True, "Keeping a loop guard must not skip independent adjacent-return cleanup.");
	}
}
