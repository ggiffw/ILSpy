// Copyright (c) 2026 JY.Z
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
	public class SharedProtectedReturnTests
	{
		[TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
		public void DuplicateOnlyStableLocalReturnsAcrossProtectedRegions(int mutation)
		{
			var decompiler = new CSharpDecompiler(typeof(object).Assembly.Location, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
			var type = decompiler.TypeSystem.FindType(KnownTypeCode.Int32);
			var identity = decompiler.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().First(m => m.Name == "GetHashCode");
			var body = new BlockContainer();
			var function = new ILFunction(identity, 0, new GenericContext(), body);
			var result = function.RegisterVariable(VariableKind.Local, type, "result");
			var dummy = function.RegisterVariable(VariableKind.Local, type, "dummy");
			var entry = new Block(); var fallback = new Block(); var terminal = new Block();
			body.Blocks.AddRange(new[] { entry, fallback, terminal });
			var protectedBody = new BlockContainer(); var cleanup = new BlockContainer();
			var inner = new Block(); var final = new Block();
			protectedBody.Blocks.Add(inner); cleanup.Blocks.Add(final);
			inner.Instructions.Add(new StLoc(result, new LdcI4(1)));
			var branch = new Branch(terminal); inner.Instructions.Add(branch);
			if (mutation == 1) final.Instructions.Add(new StLoc(result, new LdcI4(2)));
			if (mutation == 2) entry.Instructions.Add(new StLoc(dummy, new LdLoca(result)));
			final.Instructions.Add(new Leave(cleanup));
			entry.Instructions.Add(new TryFinally(protectedBody, cleanup));
			entry.Instructions.Add(new Branch(fallback));
			fallback.Instructions.Add(new StLoc(result, new LdcI4(3)));
			fallback.Instructions.Add(new Branch(terminal));
			terminal.Instructions.Add(new Leave(body, mutation == 3 ? new LdcI4(4) : new LdLoc(result)));
			if (mutation == 4) protectedBody.Kind = ContainerKind.Loop;
			if (mutation == 5) inner.Instructions.Insert(1, new IfInstruction(new LdLoc(result), new Branch(inner)));
			typeof(ILInstruction).GetMethod("AddRef", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(function, null);
			var context = decompiler.CreateILTransformContext(function);
			new ControlFlowSimplification().Run(function, context);
			Assert.That(inner.Descendants.OfType<Leave>().Any(l => l.TargetContainer == body), Is.EqualTo(mutation == 0));
		}
	}
}
