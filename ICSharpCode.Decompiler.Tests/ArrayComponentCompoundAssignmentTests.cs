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
public class ArrayComponentCompoundAssignmentTests
{
	public struct Element { public float A; public float B; }
	static void Anchor(Element[] array, int index) { }
	static void Observe() { }
	static float Produce() => 1;
	[TestCase(0, true)]
	[TestCase(1, true)]
	[TestCase(2, false)]
	[TestCase(3, false)]
	[TestCase(4, false)]
	[TestCase(5, false)]
	[TestCase(6, false)]
	[TestCase(7, false)]
	[TestCase(8, false)]
	[TestCase(9, false)]
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
	public void RequireAdjacentIdenticalUnescapedScalarArrayAddresses(int mutation, bool expected)
	{
		var settings = new DecompilerSettings { ThrowOnAssemblyResolveErrors = false };
		var dec = new CSharpDecompiler(typeof(ArrayComponentCompoundAssignmentTests).Assembly.Location, settings);
		var owner = dec.TypeSystem.FindType(new FullTypeName(typeof(ArrayComponentCompoundAssignmentTests).FullName!));
		var method = owner.GetMethods().Single(m => m.Name == nameof(Anchor));
		var container = new BlockContainer();
		var block = new Block();
		container.Blocks.Add(block);
		var function = new ILFunction(method, 0, new GenericContext(), container, ILFunctionKind.TopLevelFunction);
		var addRef = typeof(ILInstruction).GetMethod("AddRef", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(addRef, Is.Not.Null);
		addRef!.Invoke(function, null);
		var array = function.RegisterVariable(VariableKind.Parameter, method.Parameters[0].Type, "array");
		var index = function.RegisterVariable(VariableKind.Parameter, method.Parameters[1].Type, "index");
		var scalar = dec.TypeSystem.FindType(KnownTypeCode.Single);
		var value = function.RegisterVariable(VariableKind.Local, scalar, "product");
		var element = ((ArrayType)array.Type).ElementType;
		var fields = element.GetFields().Where(f => !f.IsStatic).ToArray();
		Assert.That(fields.Length, Is.EqualTo(2));
		LdFlda Address(bool destination)
		{
			ILInstruction receiver = new LdLoc(array);
			if (destination && mutation == 4)
				receiver = new LdLoc(function.RegisterVariable(VariableKind.Local, array.Type));
			ILInstruction offset = new LdLoc(index);
			if (destination && mutation == 3)
				offset = new LdcI4(0);
			return new LdFlda(new LdElema(element, receiver, offset), fields[destination && mutation == 2 ? 1 : 0]) { DelayExceptions = true };
		}
		var load = new LdObj(Address(false), scalar) { IsVolatile = mutation == 10, UnalignedPrefix = (byte)(mutation == 12 ? 1 : 0) };
		ILInstruction left = mutation == 1 ? new Conv(load, PrimitiveType.R4, false, Sign.None) : load;
		ILInstruction right = new LdcF4(0.725f);
		if (mutation is 22 or 23)
			right = new LdcF4(mutation == 22 ? float.NaN : float.PositiveInfinity);
		if (mutation == 6)
			right = new Call(owner.GetMethods().Single(m => m.Name == nameof(Produce)));
		if (mutation == 17)
			left = new Conv(load, PrimitiveType.R8, false, Sign.None);
		var binary = new BinaryNumericInstruction(BinaryNumericOperator.Mul, left, right, mutation == 14, Sign.None);
		var store = new StObj(Address(true), new LdLoc(value), scalar) { IsVolatile = mutation == 11, UnalignedPrefix = (byte)(mutation == 13 ? 1 : 0) };
		block.Instructions.Add(new StLoc(value, binary));
		if (mutation == 5)
			block.Instructions.Add(new Call(owner.GetMethods().Single(m => m.Name == nameof(Observe))));
		block.Instructions.Add(store);
		if (mutation == 7)
			block.Instructions.Add(new StLoc(function.RegisterVariable(VariableKind.Local, scalar), new LdLoc(value)));
		if (mutation is 8 or 9 or 15)
		{
			var escaped = mutation == 8 ? array : mutation == 9 ? index : value;
			block.Instructions.Add(new StLoc(function.RegisterVariable(VariableKind.Local, new ByReferenceType(escaped.Type)), new LdLoca(escaped)));
		}
		if (mutation == 16)
			value.UsesInitialValue = true;
		if (mutation == 18)
			typeof(ILVariable).GetProperty("CaptureScope")!.SetValue(array, container);
		if (mutation == 19)
			typeof(ILVariable).GetProperty("CaptureScope")!.SetValue(index, container);
		if (mutation == 20)
			settings.MakeAssignmentExpressions = false;
		if (mutation == 21)
			settings.IntroduceIncrementAndDecrement = false;
		block.Instructions.Add(new Leave(container));
		var context = new StatementTransformContext(new BlockTransformContext(dec.CreateILTransformContext(function)) { Block = block });
		((IStatementTransform)new TransformAssignment()).Run(block, 0, context);
		Assert.That(block.Instructions[0] is NumericCompoundAssign, Is.EqualTo(expected), function.ToString());
		if (expected)
		{
			Assert.That(value.LoadCount, Is.Zero);
			Assert.That(value.StoreCount, Is.Zero);
			Assert.That(block.Instructions.OfType<StObj>(), Is.Empty);
			Assert.That(((NumericCompoundAssign)block.Instructions[0]).Operator, Is.EqualTo(BinaryNumericOperator.Mul));
		}
	}
}
