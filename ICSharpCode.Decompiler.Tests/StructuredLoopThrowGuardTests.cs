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
public class StructuredLoopThrowGuardTests
{
	static Block Arm(params ILInstruction[] instructions)
	{
		var block = new Block();
		block.Instructions.AddRange(instructions);
		return block;
	}

	[TestCase(0, false)]
	[TestCase(1, false)]
	[TestCase(2, false)]
	[TestCase(3, false)]
	[TestCase(4, false)]
	[TestCase(5, false)]
	[TestCase(6, false)]
	[TestCase(7, false)]
	[TestCase(8, true)]
	[TestCase(9, true)]
	[TestCase(10, false)]
	[TestCase(11, false)]
	[TestCase(12, false)]
	[TestCase(13, false)]
	[TestCase(14, false)]
	[TestCase(15, false)]
	[TestCase(16, false)]
	[TestCase(17, false)]
	[TestCase(18, false)]
	[TestCase(19, false)]
	[TestCase(20, false)]
	[TestCase(21, false)]
	[TestCase(22, false)]
	[TestCase(23, false)]
	[TestCase(24, false)]
	[TestCase(25, false)]
	[TestCase(100, true)]
	[TestCase(101, true)]
	[TestCase(102, true)]
	[TestCase(103, true)]
	[TestCase(104, true)]
	[TestCase(105, true)]
	[TestCase(106, true)]
	[TestCase(107, true)]
	public void KeepOnlyClosedStructuredLoopFailureGuards(int mutation, bool expectedFlat)
	{
		bool guardGroup = mutation >= 100;
		if (guardGroup)
			mutation -= 100;
		bool isFor = mutation is 3 or 4 or 5 or 7 or 9 || mutation >= 10 && mutation % 2 != 0;
		var decompiler = new CSharpDecompiler(typeof(object).Assembly.Location, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
		var integer = decompiler.TypeSystem.FindType(KnownTypeCode.Int32);
		var exception = decompiler.TypeSystem.FindType(new FullTypeName("System.IndexOutOfRangeException"));
		var ctor = exception.GetConstructors().Single(m => m.Parameters.Count == 0);
		var identity = decompiler.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().Single(m => m.Name == "GetHashCode" && m.Parameters.Count == 0);
		var effect = decompiler.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().Single(m => m.Name == "ReferenceEquals");
		var body = new BlockContainer();
		var function = new ILFunction(identity, 0, new GenericContext(), body, ILFunctionKind.TopLevelFunction);
		var outer = new Block();
		body.Blocks.Add(outer);
		var loop = new BlockContainer(isFor ? ContainerKind.For : ContainerKind.DoWhile);
		var work = new Block();
		var latch = new Block();
		var header = isFor ? new Block() : work;
		loop.Blocks.Add(header);
		if (isFor)
			loop.Blocks.Add(work);
		loop.Blocks.Add(latch);
		outer.Instructions.Add(loop);
		outer.Instructions.Add(new Leave(body, new LdcI4(0)));
		var index = function.RegisterVariable(VariableKind.Parameter, integer, "index");
		var length = function.RegisterVariable(VariableKind.Parameter, integer, "length");
		var local = function.RegisterVariable(VariableKind.Local, integer, "value");
		if (isFor)
		{
			header.Instructions.Add(new IfInstruction(new Comp(ComparisonKind.LessThan, Sign.Signed, new LdLoc(index), new LdLoc(length)), new Branch(work), new Leave(loop)));
			latch.Instructions.Add(new StLoc(index, new BinaryNumericInstruction(BinaryNumericOperator.Add, new LdLoc(index), new LdcI4(1), false, Sign.Signed)));
			latch.Instructions.Add(new Branch(header));
		}
		else
			latch.Instructions.Add(new IfInstruction(new Comp(ComparisonKind.LessThan, Sign.Signed, new LdLoc(index), new LdLoc(length)), new Branch(work), new Leave(loop)));
		if (mutation is 1 or 4)
			work.Instructions.Add(new Call(effect) { Arguments = { new LdNull(), new LdNull() } });
		ILInstruction condition = new Comp(ComparisonKind.GreaterThanOrEqual, Sign.Unsigned, new LdLoc(index), new LdLoc(length));
		if (mutation is 2 or 5)
		{
			var array = function.RegisterVariable(VariableKind.Parameter, new ArrayType(decompiler.TypeSystem, integer), "array");
			condition = new Comp(ComparisonKind.GreaterThanOrEqual, Sign.Unsigned, new LdLoc(index), new LdLen(StackType.I4, new LdLoc(array)));
		}
		if (mutation is 10 or 11)
			condition = new Call(effect) { Arguments = { new LdNull(), new LdNull() } };
		if (mutation is 12 or 13)
			condition = new Comp(ComparisonKind.Equality, Sign.None, new BinaryNumericInstruction(BinaryNumericOperator.Div, new LdLoc(index), new LdLoc(length), false, Sign.Signed), new LdcI4(0));
		if (mutation is 14 or 15)
			work.Instructions.Add(new IfInstruction(new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(index), new LdcI4(1)), new Branch(latch)));
		if (mutation is 16 or 17)
		{
			var shared = new Block();
			loop.Blocks.Insert(loop.Blocks.Count - 1, shared);
			work.Instructions.Add(new IfInstruction(new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(index), new LdcI4(1)), new Branch(shared)));
			shared.Instructions.Add(new Branch(latch));
		}
		ILInstruction thrown = new Throw(new NewObj(ctor));
		if (mutation is 18 or 19)
		{
			var messageCtor = exception.GetConstructors().Single(m => m.Parameters.Count == 1 && m.Parameters[0].Type.IsKnownType(KnownTypeCode.String));
			thrown = new Throw(new NewObj(messageCtor) { Arguments = { new LdStr("failure") } });
		}
		if (mutation is 20 or 21)
			thrown = new Throw(new LdLoc(function.RegisterVariable(VariableKind.Parameter, exception, "failure")));
		thrown.SetILRange(new Interval(300, 301));
		var guard = new IfInstruction(condition, mutation is 6 or 7 ? Arm(thrown) : thrown);
		if (mutation is 6 or 7)
			guard.TrueInst.SetILRange(new Interval(300, 301));
		if (mutation is 22 or 23)
		{
			guard.TrueInst = Arm(new StLoc(local, new LdcI4(7)), thrown);
			guard.TrueInst.SetILRange(new Interval(300, 301));
		}
		if (mutation is 24 or 25)
		{
			var single = decompiler.TypeSystem.FindType(KnownTypeCode.Single);
			var number = function.RegisterVariable(VariableKind.Parameter, single, "number");
			guard.Condition = new Comp(ComparisonKind.GreaterThanOrEqual, Sign.None, new LdLoc(number), new LdcF4(0));
		}
		work.Instructions.Add(guard);
		IfInstruction? secondGuard = null;
		if (guardGroup || mutation is 8 or 9)
		{
			var failure = new Throw(new NewObj(ctor));
			failure.SetILRange(new Interval(350, 351));
			secondGuard = new IfInstruction(new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(index), new LdcI4(1)), failure);
			work.Instructions.Add(secondGuard);
		}
		var call = new Call(effect) { Arguments = { new LdNull(), new LdNull() } };
		call.SetILRange(new Interval(100, 101));
		work.Instructions.Add(call);
		var branch = new Branch(latch);
		work.Instructions.Add(branch);
		var addRef = typeof(ILInstruction).GetMethod("AddRef", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(addRef, Is.Not.Null);
		addRef!.Invoke(function, null);
		var callsBefore = function.Descendants.OfType<Call>().ToArray();
		var throwsBefore = function.Descendants.OfType<Throw>().ToArray();
		var conditionBefore = guard.Condition.Clone();

		new ReduceNestingTransform().Run(function, decompiler.CreateILTransformContext(function));

		Assert.That(Block.Unwrap(guard.TrueInst) is Throw, Is.EqualTo(expectedFlat));
		if (expectedFlat)
			Assert.That(guard.Condition.Match(conditionBefore).Success, Is.True);
		if (secondGuard != null)
			Assert.That(Block.Unwrap(secondGuard.TrueInst), Is.TypeOf<Throw>());
		Assert.That(function.Descendants.OfType<Call>().ToArray(), Is.EqualTo(callsBefore), "Guard formatting must preserve each call and its order.");
		Assert.That(function.Descendants.OfType<Throw>().ToArray(), Is.EqualTo(throwsBefore), "Guard formatting must preserve each original throw node.");
		Assert.That(branch.TargetBlock, Is.SameAs(latch), "The original continuation must still reach the same loop condition or increment.");
	}
}
