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
public class NestedStrideLoopGuardTests
{
	[TestCase(0, true)]
	[TestCase(1, true)]
	[TestCase(2, true)]
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
	public void KeepOnlyOnePositiveLengthGuardAroundANestedStrideLoop(int mutation, bool expectedGuarded)
	{
		var decompiler = new CSharpDecompiler(typeof(object).Assembly.Location, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
		var integer = decompiler.TypeSystem.FindType(KnownTypeCode.Int32);
		var identity = decompiler.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().Single(m => m.Name == "GetHashCode" && m.Parameters.Count == 0);
		var effect = decompiler.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().Single(m => m.Name == "ReferenceEquals");
		var exception = decompiler.TypeSystem.FindType(new FullTypeName("System.IndexOutOfRangeException"));
		var ctor = exception.GetConstructors().Single(m => m.Parameters.Count == 0);
		var functionBody = new BlockContainer();
		var function = new ILFunction(identity, 0, new GenericContext(), functionBody, ILFunctionKind.TopLevelFunction);
		var root = new Block();
		functionBody.Blocks.Add(root);
		var outer = new BlockContainer(mutation == 23 ? ContainerKind.While : ContainerKind.For);
		var header = new Block();
		var work = new Block();
		var step = new Block();
		outer.Blocks.Add(header);
		outer.Blocks.Add(work);
		if (mutation != 23)
			outer.Blocks.Add(step);
		root.Instructions.Add(outer);
		root.Instructions.Add(new Leave(functionBody, new LdcI4(0)));
		var array = function.RegisterVariable(VariableKind.Parameter, new ArrayType(decompiler.TypeSystem, integer), "array");
		var count = function.RegisterVariable(VariableKind.Parameter, integer, "count");
		var dynamicStride = function.RegisterVariable(VariableKind.Parameter, integer, "stride");
		var copy = function.RegisterVariable(VariableKind.Local, integer, "copy");
		var length = function.RegisterVariable(VariableKind.Local, mutation == 16 ? decompiler.TypeSystem.FindType(KnownTypeCode.UInt32) : integer, "length");
		var index = function.RegisterVariable(VariableKind.Local, integer, "index");
		var offset = function.RegisterVariable(VariableKind.Local, integer, "offset");
		header.Instructions.Add(new IfInstruction(new Comp(ComparisonKind.LessThan, Sign.Signed, new LdLoc(copy), new LdLoc(count)), new Branch(work), new Leave(outer)));
		step.Instructions.Add(new StLoc(copy, new BinaryNumericInstruction(BinaryNumericOperator.Add, new LdLoc(copy), new LdcI4(1), false, Sign.None)));
		step.Instructions.Add(new Branch(header));
		var lengthRead = new StLoc(length, mutation == 11 ? new LdcI4(3) : new LdLen(StackType.I4, new LdLoc(array)));
		if (mutation == 15)
			work.Instructions.Add(new StLoc(offset, new LdcI4(7)));
		work.Instructions.Add(lengthRead);
		var arm = new Block();
		arm.Instructions.Add(new StLoc(offset, new BinaryNumericInstruction(BinaryNumericOperator.Mul, new LdLoc(copy), new LdLoc(length), false, Sign.None)));
		if (mutation == 14)
			arm.Instructions.Add(new Call(effect) { Arguments = { new LdNull(), new LdNull() } });
		int firstIndex = mutation is 1 or 2 ? 0 : 2;
		var seed = new StLoc(index, new LdcI4(firstIndex));
		arm.Instructions.Add(seed);
		var inner = new BlockContainer(ContainerKind.DoWhile);
		var innerBody = new Block();
		var condition = new Block();
		inner.Blocks.Add(innerBody);
		inner.Blocks.Add(condition);
		arm.Instructions.Add(inner);
		var failure = new Throw(new NewObj(ctor));
		failure.SetILRange(new Interval(300, 301));
		innerBody.Instructions.Add(new IfInstruction(new Comp(ComparisonKind.GreaterThanOrEqual, Sign.Unsigned, new LdLoc(index), new LdLoc(length)), failure));
		var call = new Call(effect) { Arguments = { new LdNull(), new LdNull() } };
		call.SetILRange(new Interval(100, 101));
		innerBody.Instructions.Add(call);
		if (mutation == 21)
			innerBody.Instructions.Add(new StLoc(function.RegisterVariable(VariableKind.Local, new ByReferenceType(integer), "address"), new LdLoca(index)));
		if (mutation == 24)
			innerBody.Instructions.Add(new StLoc(index, new LdcI4(4)));
		ILInstruction stride = mutation == 19 ? new LdLoc(dynamicStride) : new LdcI4(mutation == 17 ? 0 : mutation == 18 ? -1 : mutation == 1 ? 1 : mutation == 2 ? 7 : 3);
		var increment = new StLoc(index, new BinaryNumericInstruction(BinaryNumericOperator.Add, new LdLoc(index), stride, mutation == 20, Sign.Signed));
		innerBody.Instructions.Add(increment);
		innerBody.Instructions.Add(new Branch(condition));
		condition.Instructions.Add(new IfInstruction(new Comp(ComparisonKind.LessThan, Sign.Signed, new LdLoc(index), new LdLoc(length)), new Branch(innerBody), new Leave(inner)));
		if (mutation == 22)
		{
			var shared = new Block();
			inner.Blocks.Insert(1, shared);
			innerBody.Instructions.Insert(1, new IfInstruction(new Comp(ComparisonKind.Equality, Sign.None, new LdLoc(index), new LdcI4(1)), new Branch(shared)));
			shared.Instructions.Add(new Branch(condition));
		}
		if (mutation == 13)
			arm.Instructions.Add(new Call(effect) { Arguments = { new LdNull(), new LdNull() } });
		ILInstruction test = new Comp(mutation == 12 ? ComparisonKind.GreaterThan : ComparisonKind.GreaterThanOrEqual, Sign.Signed, new LdLoc(length), new LdcI4(mutation == 12 ? 0 : 1));
		if (mutation == 10)
			test = new Call(effect) { Arguments = { new LdNull(), new LdNull() } };
		var guard = new IfInstruction(test, arm);
		work.Instructions.Add(guard);
		var tail = new Branch(mutation == 23 ? header : step);
		work.Instructions.Add(tail);
		var addRef = typeof(ILInstruction).GetMethod("AddRef", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(addRef, Is.Not.Null);
		addRef!.Invoke(function, null);
		var callsBefore = function.Descendants.OfType<Call>().ToArray();
		var throwsBefore = function.Descendants.OfType<Throw>().ToArray();
		var readsBefore = function.Descendants.OfType<LdLen>().ToArray();
		var testBefore = guard.Condition.Clone();

		new ReduceNestingTransform().Run(function, decompiler.CreateILTransformContext(function));

		Assert.That(guard.TrueInst.Descendants.Contains(inner), Is.EqualTo(expectedGuarded));
		if (expectedGuarded)
		{
			Assert.That(guard.Condition.Match(testBefore).Success, Is.True);
			Assert.That(lengthRead.Parent, Is.SameAs(work));
			Assert.That(guard.ChildIndex, Is.EqualTo(lengthRead.ChildIndex + 1));
		}
		Assert.That(function.Descendants.OfType<Call>().ToArray(), Is.EqualTo(callsBefore));
		Assert.That(function.Descendants.OfType<Throw>().ToArray(), Is.EqualTo(throwsBefore));
		Assert.That(function.Descendants.OfType<LdLen>().ToArray(), Is.EqualTo(readsBefore), "The original potentially throwing length read is neither moved nor duplicated.");
		Assert.That(increment.IsDescendantOf(inner), Is.True);
		Assert.That(tail.TargetBlock, Is.SameAs(mutation == 23 ? header : step));
	}
}
