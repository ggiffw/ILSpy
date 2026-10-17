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

using NUnit.Framework;

namespace ICSharpCode.Decompiler.Tests;

[TestFixture]
public class LocalCollectionInitializerTests
{
	static int Consume(List<int> value) => value.Count;
	static int Produce() => throw new InvalidOperationException();
	static void Change(ref List<int> value) => value = null;
	static int Plain() { var v = new List<int>(); v.Add(1); v.Add(2); return Consume(v); }
	static int EffectfulItem() { var v = new List<int>(); v.Add(Produce()); v.Add(2); return Consume(v); }
	static int Reused() { var v = new List<int>(); v.Add(1); v.Add(2); return Consume(v) + Consume(v); }
	static int SelfReference() { var v = new List<int>(); v.Add(v.Count); return Consume(v); }
	static int AddressEscape() { var v = new List<int>(); Change(ref v); v.Add(1); return Consume(v); }
	static int CatchReads() { List<int> v = null; try { v = new List<int>(); v.Add(Produce()); return Consume(v); } catch { return v == null ? -1 : Consume(v); } }
	static int OtherBlock(bool select) { var v = new List<int>(); v.Add(1); if (select) return Consume(v); return 0; }
	static int FinallyReads() { List<int> v = null; try { v = new List<int>(); v.Add(Produce()); return Consume(v); } finally { if (v != null) Consume(v); } }
	[TestCase(nameof(Plain), true)]
	[TestCase(nameof(EffectfulItem), true)]
	[TestCase(nameof(Reused), true)]
	[TestCase(nameof(SelfReference), false)]
	[TestCase(nameof(AddressEscape), false)]
	[TestCase(nameof(CatchReads), false)]
	[TestCase(nameof(OtherBlock), true)]
	[TestCase(nameof(FinallyReads), false)]
	public void PreserveLocalAssignmentVisibility(string name, bool expected)
	{
		var decompiler = new CSharpDecompiler(typeof(LocalCollectionInitializerTests).Assembly.Location, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
		var method = typeof(LocalCollectionInitializerTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
		Assert.That(method, Is.Not.Null);
		var tree = decompiler.Decompile(System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(method!.MetadataToken));
		var initializers = tree.Descendants.OfType<ObjectCreateExpression>().Count(n => n.Type.ToString().StartsWith("List<", StringComparison.Ordinal) && n.Initializer != null);
		Assert.That(initializers > 0, Is.EqualTo(expected), tree.ToString());
	}

	[Test]
	public void DisabledSettingKeepsCollectionStatements()
	{
		var decompiler = new CSharpDecompiler(typeof(LocalCollectionInitializerTests).Assembly.Location, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false, ObjectOrCollectionInitializers = false });
		var method = typeof(LocalCollectionInitializerTests).GetMethod(nameof(Reused), BindingFlags.NonPublic | BindingFlags.Static);
		Assert.That(method, Is.Not.Null);
		var tree = decompiler.Decompile(System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(method!.MetadataToken));
		Assert.That(tree.Descendants.OfType<ObjectCreateExpression>().Any(n => n.Initializer != null), Is.False, tree.ToString());
	}
}

