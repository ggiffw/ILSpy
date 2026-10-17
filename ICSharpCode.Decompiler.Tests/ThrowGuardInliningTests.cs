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
	[TestCase(6)]
	[TestCase(7)]
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
			((Block)guard.TrueInst).Instructions[0] = new Throw(new LdNull());
		if (mutation == 6)
			((Block)guard.TrueInst).Instructions.Insert(0, new StLoc(parameter, new LdNull()));
		entry.Instructions.Add(mutation == 4 ? new Throw(new LdNull()) : mutation == 7 ? new Throw(new NewObj(ctor.DeclaringType.GetConstructors().Single(m => m.Parameters.Count == 0))) : new Throw(new NewObj(ctor) { Arguments = { new LdStr("failure") } }));
		new ReduceNestingTransform().Run(function, dec.CreateILTransformContext(function));
		Assert.That(((Comp)guard.Condition).Kind, Is.EqualTo(mutation is 0 or 6 ? ComparisonKind.Equality : ComparisonKind.Inequality));
		Assert.That(entry.Instructions.Last() is Leave, Is.EqualTo(mutation is 0 or 6));
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

	[TestCase(false)]
	[TestCase(true)]
	public void PreferExceptionGuardForDirectAndWrappedReturns(bool wrapped)
	{
		var (dec, function, body, entry, ctor) = Create();
		var call = new Call(dec.TypeSystem.FindType(KnownTypeCode.String).GetMethods().Single(m => m.Name == "op_Equality")) { Arguments = { new LdNull(), new LdNull() } };
		var leave = new Leave(body, new LdNull());
		var guard = new IfInstruction(Comp.LogicNot(call), wrapped ? Arm(leave) : leave);
		entry.Instructions.Add(guard);
		entry.Instructions.Add(new Throw(new NewObj(ctor) { Arguments = { new LdStr("failure") } }));
		new ReduceNestingTransform().Run(function, dec.CreateILTransformContext(function));
		Assert.That(guard.Condition, Is.SameAs(call));
		Assert.That(entry.Instructions.Last(), Is.SameAs(leave));
		Assert.That(function.Descendants.OfType<Call>().Single(), Is.SameAs(call));
	}

	[TestCase(0)]
	[TestCase(1)]
	[TestCase(2)]
	[TestCase(3)]
	[TestCase(4)]
	public void PreferKeywordGuardWithoutComplementingOperator(int mutation)
	{
		var (dec, function, body, entry, _) = Create();
		var type = dec.TypeSystem.FindType(mutation == 4 ? KnownTypeCode.Object : KnownTypeCode.String);
		var method = type.GetMethods().Single(m => m.Name == (mutation == 4 ? "ReferenceEquals" : mutation == 1 ? "op_Inequality" : "op_Equality"));
		var call = new Call(method) { Arguments = { new LdNull(), new LdNull() } };
		var marker = function.RegisterVariable(VariableKind.Local, dec.TypeSystem.FindType(KnownTypeCode.Int32));
		ILInstruction condition = mutation == 3 ? call : Comp.LogicNot(call);
		if (mutation == 2)
		{
			var flag = function.RegisterVariable(VariableKind.Parameter, dec.TypeSystem.FindType(KnownTypeCode.Boolean), "flag");
			condition = IfInstruction.LogicAnd(condition, new LdLoc(flag), dec.TypeSystem);
		}
		var guard = new IfInstruction(condition, Arm(new StLoc(marker, new LdcI4(5))));
		entry.Instructions.Add(guard);
		entry.Instructions.Add(new Leave(body));
		new ReduceNestingTransform().Run(function, dec.CreateILTransformContext(function));
		Assert.That(guard.TrueInst is Leave, Is.EqualTo(mutation < 3));
		Assert.That(function.Descendants.OfType<Call>().Single(), Is.SameAs(call));
		Assert.That(call.Method, Is.SameAs(method));
	}

	[TestCase(0)]
	[TestCase(1)]
	[TestCase(2)]
	[TestCase(3)]
	[TestCase(4)]
	public void SplitClosedConditionalDefaultReturn(int mutation)
	{
		var dec = new CSharpDecompiler(typeof(object).Assembly.Location, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
		var identity = dec.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().Single(m => m.Name == "ToString" && m.Parameters.Count == 0);
		var body = new BlockContainer();
		var function = new ILFunction(identity, 0, new GenericContext(), body, ILFunctionKind.TopLevelFunction);
		var addRef = typeof(ILInstruction).GetMethod("AddRef", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(addRef, Is.Not.Null);
		addRef!.Invoke(function, null);
		var value = function.RegisterVariable(VariableKind.Local, dec.TypeSystem.FindType(KnownTypeCode.String), "value");
		var condition = function.RegisterVariable(VariableKind.Parameter, dec.TypeSystem.FindType(KnownTypeCode.Boolean), "condition");
		var entry = new Block(); var work = new Block(); var exit = new Block();
		body.Blocks.AddRange(new[] { entry, work, exit });
		entry.Instructions.Add(new StLoc(value, mutation == 3 ? new LdStr("fallback") : new LdNull()));
		if (mutation == 1)
			entry.Instructions.Add(new StLoc(function.RegisterVariable(VariableKind.Local, value.Type), new LdLoc(value)));
		if (mutation == 2)
			entry.Instructions.Add(new StLoc(function.RegisterVariable(VariableKind.Local, new ByReferenceType(value.Type)), new LdLoca(value)));
		entry.Instructions.Add(new IfInstruction(new LdLoc(condition), new Branch(mutation == 4 ? exit : work)));
		entry.Instructions.Add(new Branch(mutation == 4 ? work : exit));
		work.Instructions.Add(new StLoc(value, new LdStr("built")));
		work.Instructions.Add(new Branch(exit));
		exit.Instructions.Add(new Leave(body, new LdLoc(value)));
		var transformType = typeof(ILInlining).Assembly.GetType("ICSharpCode.Decompiler.IL.Transforms.InlineReturnTransform");
		Assert.That(transformType, Is.Not.Null);
		transformType!.GetMethod("Run")!.Invoke(System.Activator.CreateInstance(transformType), new object[] { function, dec.CreateILTransformContext(function) });
		Assert.That(function.Descendants.OfType<Leave>().Count(), Is.EqualTo(mutation is 0 or 4 ? 2 : 1));
		Assert.That(function.Descendants.OfType<StLoc>().Count(s => s.Variable == value), Is.EqualTo(2));
	}
}
