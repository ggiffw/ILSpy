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
using System.Linq;
using System.Reflection;

using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.Transforms;
using ICSharpCode.Decompiler.TypeSystem;

using NUnit.Framework;

namespace ICSharpCode.Decompiler.Tests;

[TestFixture]
public class GuardedCacheInliningTests
{
	sealed class Receiver
	{
		public void Apply(List<int> items) { }
		public static bool operator !=(Receiver a, Receiver b) => !ReferenceEquals(a, b);
		public static bool operator ==(Receiver a, Receiver b) => ReferenceEquals(a, b);
		public override bool Equals(object value) => ReferenceEquals(this, value);
		public override int GetHashCode() => 0;
	}

	static Receiver cache;
	static Receiver Produce() => new Receiver();
	static int Item() { cache = null; return 1; }
	static void Observe(Receiver value) { }
	static void Escape(ref Receiver value) { }
	static Receiver Cached()
	{
		if (!(cache != null))
		{
			cache = Produce();
			var saved = cache;
			saved.Apply(new List<int> { 1, 2 });
		}
		return cache;
	}
	static void Adjacent() { var saved = cache; saved.Apply(new List<int> { Item(), 2 }); }
	static void Reused() { var saved = cache; saved.Apply(new List<int> { Item() }); Observe(saved); }
	static void Intervening() { var saved = cache; Item(); saved.Apply(new List<int> { 1 }); }
	static void AddressEscaped() { var saved = cache; Escape(ref saved); saved.Apply(new List<int> { 1 }); }

	static (CSharpDecompiler Dec, MethodDeclaration Declaration) Decompile(string name)
	{
		var dec = new CSharpDecompiler(typeof(GuardedCacheInliningTests).Assembly.Location, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
		var method = typeof(GuardedCacheInliningTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
		Assert.That(method, Is.Not.Null);
		var tree = dec.Decompile(System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(method!.MetadataToken));
		return (dec, tree.Descendants.OfType<MethodDeclaration>().Single());
	}

	[Test]
	public void RestoreEarlyCacheReturn()
	{
		var (_, declaration) = Decompile(nameof(Cached));
		var guard = declaration.Body.Statements.First() as IfElseStatement;
		Assert.That(guard, Is.Not.Null, declaration.ToString());
		Assert.That(guard!.TrueStatement.DescendantsAndSelf.OfType<ReturnStatement>().Count(), Is.EqualTo(1), declaration.ToString());
		Assert.That(guard.Condition.ToString(), Does.Not.StartWith("!"), declaration.ToString());
		Assert.That(declaration.Descendants.OfType<ReturnStatement>().Count(), Is.EqualTo(2), declaration.ToString());
	}

	[TestCase(nameof(Adjacent), false)]
	[TestCase(nameof(Reused), true)]
	[TestCase(nameof(Intervening), true)]
	[TestCase(nameof(AddressEscaped), true)]
	public void InlineOnlyAdjacentSingleUseReceiver(string name, bool keep)
	{
		var (_, declaration) = Decompile(name);
		Assert.That(declaration.Descendants.OfType<VariableDeclarationStatement>().Any(v => v.Type.ToString().EndsWith("Receiver", StringComparison.Ordinal)), Is.EqualTo(keep), declaration.ToString());
	}

	[TestCase(0, true)]
	[TestCase(1, false)]
	[TestCase(2, false)]
	[TestCase(3, false)]
	[TestCase(4, false)]
	[TestCase(5, false)]
	public void InlineFieldReceiverBeforeCollectionArgument(int mutation, bool expected)
	{
		var (dec, _) = Decompile(nameof(Cached));
		var owner = dec.TypeSystem.FindType(new FullTypeName(typeof(GuardedCacheInliningTests).FullName!));
		var field = owner.GetFields().Single(f => f.Name == nameof(cache));
		var method = owner.GetMethods().Single(m => m.Name == nameof(Cached));
		var consumer = field.Type.GetMethods().Single(m => m.Name == nameof(Receiver.Apply));
		var body = new BlockContainer();
		var function = new ILFunction(method, 0, new GenericContext(), body, ILFunctionKind.TopLevelFunction);
		var entry = new Block();
		body.Blocks.Add(entry);
		var addRef = typeof(ILInstruction).GetMethod("AddRef", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(addRef, Is.Not.Null);
		addRef!.Invoke(function, null);
		var local = function.RegisterVariable(VariableKind.Local, field.Type, "saved");
		var copy = new StLoc(local, new LdObj(new LdsFlda(field), field.Type));
		entry.Instructions.Add(copy);
		if (mutation == 2)
			entry.Instructions.Add(new StLoc(function.RegisterVariable(VariableKind.Local, new ByReferenceType(field.Type)), new LdLoca(local)));
		if (mutation == 3)
			entry.Instructions.Add(new Call(owner.GetMethods().Single(m => m.Name == nameof(Item))));
		var items = new Block(BlockKind.CollectionInitializer);
		var list = function.RegisterVariable(VariableKind.InitializerTarget, consumer.Parameters[0].Type);
		items.Instructions.Add(new StLoc(list, new DefaultValue(list.Type)));
		items.FinalInstruction = new LdLoc(list);
		var call = new CallVirt(consumer) { Arguments = { new LdLoc(local), mutation == 4 ? new DefaultValue(list.Type) : items } };
		entry.Instructions.Add(call);
		if (mutation == 1)
			entry.Instructions.Add(new Call(owner.GetMethods().Single(m => m.Name == nameof(Observe))) { Arguments = { new LdLoc(local) } });
		if (mutation == 5)
			((LdObj)copy.Value).IsVolatile = true;
		entry.Instructions.Add(new Leave(body, new LdNull()));
		Assert.That(ILInlining.InlineOneIfPossible(entry, 0, InliningOptions.None, dec.CreateILTransformContext(function)), Is.EqualTo(expected));
		Assert.That(call.Arguments[0] is LdObj, Is.EqualTo(expected));
		Assert.That(call.Arguments[1], Is.SameAs(mutation == 4 ? call.Arguments[1] : items));
	}
	[TestCase(0, true)]
	[TestCase(1, false)]
	[TestCase(2, false)]
	[TestCase(3, false)]
	[TestCase(4, false)]
	[TestCase(5, false)]
	[TestCase(6, false)]
	public void DuplicateOnlyMatchingCacheReturn(int mutation, bool expected)
	{
		var (dec, _) = Decompile(nameof(Cached));
		var owner = dec.TypeSystem.FindType(new FullTypeName(typeof(GuardedCacheInliningTests).FullName!));
		var field = owner.GetFields().Single(f => f.Name == nameof(cache));
		var method = owner.GetMethods().Single(m => m.Name == nameof(Cached));
		var inequality = field.Type.GetMethods().Single(m => m.Name == "op_Inequality");
		var body = new BlockContainer();
		var function = new ILFunction(method, 0, new GenericContext(), body, ILFunctionKind.TopLevelFunction);
		var entry = new Block();
		body.Blocks.Add(entry);
		var addRef = typeof(ILInstruction).GetMethod("AddRef", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(addRef, Is.Not.Null);
		addRef!.Invoke(function, null);
		var comparison = new Call(inequality) { Arguments = { new LdObj(new LdsFlda(field), field.Type), new LdNull() } };
		var arm = new Block();
		arm.Instructions.Add(new Call(owner.GetMethods().Single(m => m.Name == nameof(Item))));
		arm.Instructions.Add(new Call(owner.GetMethods().Single(m => m.Name == nameof(Item))));
		var guard = new IfInstruction(mutation == 1 ? comparison : new Comp(ComparisonKind.Equality, Sign.None, comparison, new LdcI4(0)), arm);
		entry.Instructions.Add(guard);
		var read = new LdObj(new LdsFlda(field), field.Type);
		if (mutation == 2)
			read.IsVolatile = true;
		if (mutation == 3)
			guard.FalseInst = new Block { Instructions = { new Call(owner.GetMethods().Single(m => m.Name == nameof(Item))) } };
		if (mutation == 4)
			entry.Instructions.Add(new Call(owner.GetMethods().Single(m => m.Name == nameof(Item))));
		if (mutation == 5)
			((LdObj)comparison.Arguments[0]).IsVolatile = true;
		var terminal = new Leave(body, mutation == 6 ? new LdNull() : read);
		entry.Instructions.Add(terminal);
		new ReduceNestingTransform().Run(function, dec.CreateILTransformContext(function));
		Assert.That(guard.TrueInst is Leave, Is.EqualTo(expected));
		Assert.That(function.Descendants.OfType<Call>().Count(c => c.Method.Equals(inequality)), Is.EqualTo(1));
		if (expected)
		{
			Assert.That(guard.Condition, Is.SameAs(comparison));
			Assert.That(((Leave)guard.TrueInst).Value.Match(read).Success, Is.True);
			Assert.That(entry.Instructions.Last().Match(terminal).Success, Is.True);
		}
	}}