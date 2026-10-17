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
using ICSharpCode.Decompiler.IL.Transforms;
using ICSharpCode.Decompiler.TypeSystem;

using NUnit.Framework;

namespace ICSharpCode.Decompiler.Tests;

[TestFixture]
public class ThrowGuardInliningTests
{
	static (CSharpDecompiler Decompiler, ILFunction Function, BlockContainer Body, Block Entry, IMethod Constructor) Create()
	{
		var dec = new CSharpDecompiler(typeof(object).Assembly.Location, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
		var type = dec.TypeSystem.FindType(new FullTypeName("System.InvalidOperationException"));
		var ctor = type.GetConstructors().Single(m => m.Parameters.Count == 1 && m.Parameters[0].Type.IsKnownType(KnownTypeCode.String));
		var body = new BlockContainer();
		var function = new ILFunction(ctor, 0, new GenericContext(), body, ILFunctionKind.TopLevelFunction);
		var entry = new Block();
		body.Blocks.Add(entry);
		var addRef = typeof(ILInstruction).GetMethod("AddRef", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(addRef, Is.Not.Null);
		addRef!.Invoke(function, null);
		return (dec, function, body, entry, ctor);
	}

	[TestCase(0)]
	[TestCase(1)]
	[TestCase(2)]
	[TestCase(3)]
	[TestCase(4)]
	[TestCase(5)]
	public void InlineOnlyPrivateAdjacentExactConstruction(int mutation)
	{
		var (dec, function, body, entry, ctor) = Create();
		var type = mutation == 5 ? dec.TypeSystem.FindType(KnownTypeCode.Exception) : ctor.DeclaringType;
		var value = function.RegisterVariable(VariableKind.Local, type, "exception");
		entry.Instructions.Add(new StLoc(value, new NewObj(ctor) { Arguments = { new LdStr("failure") } }));
		if (mutation == 1)
			entry.Instructions.Add(new StLoc(function.RegisterVariable(VariableKind.Local, type), new LdLoc(value)));
		if (mutation == 2)
			entry.Instructions.Add(new StLoc(function.RegisterVariable(VariableKind.Local, new ByReferenceType(type)), new LdLoca(value)));
		if (mutation == 3)
			entry.Instructions.Add(new Call(dec.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().Single(m => m.Name == "ReferenceEquals")) { Arguments = { new LdNull(), new LdNull() } });
		if (mutation == 4)
			entry.Instructions.Add(new StLoc(value, new LdNull()));
		var terminal = new Throw(new LdLoc(value));
		entry.Instructions.Add(terminal);
		Assert.That(ILInlining.InlineOneIfPossible(entry, 0, InliningOptions.None, dec.CreateILTransformContext(function)), Is.EqualTo(mutation == 0));
		Assert.That(terminal.Argument is NewObj, Is.EqualTo(mutation == 0));
	}

	static Block Arm(params ILInstruction[] instructions)
	{
		var result = new Block();
		result.Instructions.AddRange(instructions);
		return result;
	}

	[TestCase(0)]
	[TestCase(1)]
	[TestCase(2)]
	[TestCase(3)]
	[TestCase(4)]
	public void PreferAdjacentConstructionThrowGuard(int mutation)
	{
		var (dec, function, body, entry, ctor) = Create();
		var parameter = function.RegisterVariable(VariableKind.Parameter, dec.TypeSystem.FindType(KnownTypeCode.Object), "value");
		var originalCondition = new Comp(ComparisonKind.Inequality, Sign.None, new LdLoc(parameter), new LdNull());
		var guard = new IfInstruction(originalCondition, Arm(new Leave(body, new LdNull())));
		entry.Instructions.Add(guard);
		if (mutation == 1)
			entry.Instructions.Add(new StLoc(function.RegisterVariable(VariableKind.Local, ctor.DeclaringType), new NewObj(ctor) { Arguments = { new LdStr("earlier") } }));
		if (mutation == 2)
			guard.FalseInst = Arm(new StLoc(parameter, new LdNull()));
		if (mutation == 3)
			((Block)guard.TrueInst).Instructions.Insert(0, new StLoc(parameter, new LdNull()));
		entry.Instructions.Add(mutation == 4 ? new Throw(new LdNull()) : new Throw(new NewObj(ctor) { Arguments = { new LdStr("failure") } }));
		new ReduceNestingTransform().Run(function, dec.CreateILTransformContext(function));
		Assert.That(((Comp)guard.Condition).Kind, Is.EqualTo(mutation == 0 ? ComparisonKind.Equality : ComparisonKind.Inequality));
		Assert.That(entry.Instructions.Last() is Leave, Is.EqualTo(mutation == 0));
		Assert.That(function.Descendants.OfType<NewObj>().Count(), Is.EqualTo(mutation == 1 ? 2 : mutation == 4 ? 0 : 1));
	}

	[Test]
	public void NegateCallResultWithoutReplacingOrRepeatingCall()
	{
		var (dec, function, body, entry, ctor) = Create();
		var method = dec.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().Single(m => m.Name == "ReferenceEquals");
		var call = new Call(method) { Arguments = { new LdNull(), new LdNull() } };
		var guard = new IfInstruction(call, Arm(new Leave(body, new LdNull())));
		entry.Instructions.Add(guard);
		entry.Instructions.Add(new Throw(new NewObj(ctor) { Arguments = { new LdStr("failure") } }));
		new ReduceNestingTransform().Run(function, dec.CreateILTransformContext(function));
		Assert.That(guard.Condition.MatchLogicNot(out var operand), Is.True);
		Assert.That(operand, Is.SameAs(call));
		Assert.That(function.Descendants.OfType<Call>().Single(), Is.SameAs(call));
		Assert.That(call.Method, Is.SameAs(method));
		Assert.That(entry.Instructions.Last(), Is.TypeOf<Leave>());
	}
}
