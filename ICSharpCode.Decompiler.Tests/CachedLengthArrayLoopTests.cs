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
using System.Linq;
using System.Reflection;

using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.Transforms;
using ICSharpCode.Decompiler.TypeSystem;

using NUnit.Framework;

namespace ICSharpCode.Decompiler.Tests;

[TestFixture]
public class CachedLengthArrayLoopTests
{
	static void Anchor(int[] input) { }
	static void Visit(long index, int[] array) { }
	static void Observe(long value) { }
	static void Escape(ref long value) { }

	[TestCase(false, 0, true)]
	[TestCase(true, 0, true)]
	[TestCase(true, 1, false)]
	[TestCase(true, 2, false)]
	[TestCase(true, 3, false)]
	[TestCase(true, 4, false)]
	[TestCase(true, 5, false)]
	[TestCase(true, 6, false)]
	[TestCase(true, 7, false)]
	[TestCase(true, 8, false)]
	[TestCase(true, 9, false)]
	[TestCase(true, 10, false)]
	[TestCase(true, 11, false)]
	[TestCase(true, 12, false)]
	[TestCase(true, 13, false)]
	[TestCase(true, 14, false)]
	[TestCase(true, 15, false)]
	[TestCase(true, 16, false)]
	[TestCase(true, 17, false)]
	[TestCase(true, 18, false)]
	[TestCase(true, 19, false)]
	[TestCase(true, 20, false)]
	[TestCase(true, 21, false)]
	[TestCase(true, 22, false)]
	[TestCase(true, 23, false)]
	[TestCase(true, 24, false)]
	[TestCase(true, 25, false)]
	[TestCase(true, 26, false)]
	[TestCase(true, 27, false)]
	[TestCase(true, 28, false)]
	[TestCase(true, 29, false)]
	[TestCase(true, 30, true)]
	public void RecoverOnlyUnobservedCachedLengthTailLoop(bool wide, int mutation, bool expected)
	{
		var dec = new CSharpDecompiler(typeof(CachedLengthArrayLoopTests).Assembly.Location, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
		var owner = dec.TypeSystem.FindType(new FullTypeName(typeof(CachedLengthArrayLoopTests).FullName!));
		var method = owner.GetMethods().Single(m => m.Name == nameof(Anchor));
		var container = new BlockContainer();
		var function = new ILFunction(method, 0, new GenericContext(), container, ILFunctionKind.TopLevelFunction);
		var outer = new Block();
		container.Blocks.Add(outer);
		var addRef = typeof(ILInstruction).GetMethod("AddRef", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(addRef, Is.Not.Null);
		addRef!.Invoke(function, null);
		var type = dec.TypeSystem.FindType(wide ? KnownTypeCode.Int64 : KnownTypeCode.Int32);
		var input = function.RegisterVariable(VariableKind.Parameter, method.Parameters[0].Type, "input");
		var array = function.RegisterVariable(VariableKind.Local, input.Type, "array");
		var index = function.RegisterVariable(VariableKind.Local, type, "index");
		var next = function.RegisterVariable(VariableKind.Local, type, "next");
		var initialLength = function.RegisterVariable(VariableKind.Local, dec.TypeSystem.FindType(KnownTypeCode.Int32), "initialLength");
		var length = function.RegisterVariable(VariableKind.Local, initialLength.Type, "length");
		var flag = function.RegisterVariable(VariableKind.Local, dec.TypeSystem.FindType(KnownTypeCode.Boolean), "flag");
		ILInstruction Number(int value) => wide ? new LdcI8(value) : new LdcI4(value);
		ILInstruction Widen(ILInstruction value) => wide ? new Conv(value, PrimitiveType.I8, false, Sign.Signed) : value;
		outer.Instructions.Add(new StLoc(array, new LdLoc(input)));
		outer.Instructions.Add(new StLoc(initialLength, new LdLen(StackType.I4, new LdLoc(array))));
		var arm = new Block();
		arm.Instructions.Add(new StLoc(index, Number(mutation == 1 ? 1 : 0)));
		var loop = new BlockContainer(ContainerKind.Loop);
		var body = new Block();
		loop.Blocks.Add(body);
		arm.Instructions.Add(loop);
		var guard = new IfInstruction(new Comp(mutation == 2 ? ComparisonKind.GreaterThan : ComparisonKind.GreaterThanOrEqual, Sign.Signed, new LdLoc(initialLength), new LdcI4(1)), arm);
		outer.Instructions.Add(guard);
		outer.Instructions.Add(new Leave(container));
		var increment = new BinaryNumericInstruction(BinaryNumericOperator.Add, new LdLoc(index), Number(mutation == 3 ? 2 : 1), mutation == 4, Sign.Signed);
		
		var call = new Call(owner.GetMethods().Single(m => m.Name == nameof(Visit))) { Arguments = { wide ? new LdLoc(index) : new Conv(new LdLoc(index), PrimitiveType.I8, false, Sign.Signed), new LdLoc(array) } };
		body.Instructions.Add(call);
		body.Instructions.Add(new StLoc(next, increment));
		var compare = new Comp(mutation == 6 ? ComparisonKind.LessThanOrEqual : ComparisonKind.LessThan, mutation == 7 ? Sign.Unsigned : Sign.Signed, new LdLoc(next), Widen(mutation == 5 ? new LdLen(StackType.I4, new LdLoc(input)) : new LdLoc(initialLength)));
		body.Instructions.Add(new StLoc(flag, compare));
		body.Instructions.Add(new StLoc(index, new LdLoc(next)));
		var exit = new IfInstruction(Comp.LogicNot(new LdLoc(flag)), new Leave(loop));
		body.Instructions.Add(exit);
		body.Instructions.Add(new Branch(body));
		Call Observe(ILVariable variable) => new Call(owner.GetMethods().Single(m => m.Name == nameof(Observe))) { Arguments = { new LdLoc(variable) } };
		if (mutation == 8)
			outer.Instructions.Insert(3, Observe(index));
		if (mutation == 9)
			outer.Instructions.Insert(3, Observe(next));
		if (mutation == 10)
			body.Instructions.Insert(2, Observe(next));
		if (mutation == 11)
			body.Instructions.Insert(2, new Call(owner.GetMethods().Single(m => m.Name == nameof(Escape))) { Arguments = { new LdLoca(index) } });
		if (mutation == 12)
			body.Instructions.Insert(2, new StLoc(array, new LdNull()));
		if (mutation == 13)
			body.Instructions.Insert(2, new StLoc(index, Number(5)));
		if (mutation == 14)
			outer.Instructions.Insert(3, new StLoc(function.RegisterVariable(VariableKind.Local, initialLength.Type), new LdLoc(initialLength)));
		if (mutation == 15)
			outer.Instructions.Insert(3, new StLoc(function.RegisterVariable(VariableKind.Local, flag.Type), new LdLoc(flag)));
		if (mutation == 16)
			guard.FalseInst = new Call(owner.GetMethods().Single(m => m.Name == nameof(Observe))) { Arguments = { new LdcI8(7) } };
		if (mutation == 17)
			body.Instructions.Insert(2, new IfInstruction(new LdcI4(0), new Branch(body)));
		if (mutation == 18)
			arm.Instructions.Insert(1, Observe(index));
		if (mutation == 19)
			body.Instructions.Insert(2, new Call(owner.GetMethods().Single(m => m.Name == nameof(Escape))) { Arguments = { new LdLoca(next) } });
		if (mutation == 20)
			outer.Instructions.Insert(3, new StLoc(function.RegisterVariable(VariableKind.Local, initialLength.Type), new LdLoc(initialLength)));
		if (mutation == 21)
			compare.Left = new Conv(new LdLoc(next), PrimitiveType.I4, false, Sign.None);
		if (mutation == 22)
			body.Instructions.Insert(2, new IfInstruction(new LdcI4(0), new Leave(loop)));
		if (mutation == 23)
			typeof(ILVariable).GetProperty(nameof(ILVariable.CaptureScope))!.SetValue(index, loop);
		if (mutation == 24)
			typeof(ILVariable).GetProperty(nameof(ILVariable.CaptureScope))!.SetValue(array, loop);
		if (mutation == 25)
			outer.Instructions.Insert(3, new StLoc(function.RegisterVariable(VariableKind.Local, new ByReferenceType(length.Type)), new LdLoca(initialLength)));
		if (mutation == 26)
			index.UsesInitialValue = true;
		if (mutation == 27)
			compare.Right = new Conv(new LdLoc(initialLength), PrimitiveType.I8, true, Sign.Signed);
		if (mutation == 28)
			increment.Left = new Conv(new LdLoc(index), PrimitiveType.I8, true, Sign.Signed);
		if (mutation == 29)
			increment.Left = new Conv(new Conv(new LdLoc(index), PrimitiveType.I4, false, Sign.None), PrimitiveType.I8, false, Sign.Signed);
		if (mutation == 30)
		{
			increment.Left = new Conv(new LdLoc(index), PrimitiveType.I8, false, Sign.Signed);
			increment.Right = new Conv(new LdcI4(1), PrimitiveType.I8, false, Sign.Signed);
			compare.Left = new Conv(new LdLoc(next), PrimitiveType.I8, false, Sign.Signed);
		}
		new HighLevelLoopTransform().Run(function, dec.CreateILTransformContext(function));
		Assert.That(loop.Kind == ContainerKind.For, Is.EqualTo(expected), function.ToString());
		if (expected)
		{
			Assert.That(loop.Descendants.Contains(call), Is.True);
			Assert.That(function.Descendants.OfType<StLoc>().Any(s => s.Variable == next || s.Variable == length || s.Variable == flag || s.Variable == initialLength), Is.False, function.ToString());
			Assert.That(index.Type, Is.EqualTo(type));
		}
	}
}
