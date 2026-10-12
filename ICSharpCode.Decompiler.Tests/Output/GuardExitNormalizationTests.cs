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
using ExpressionType = System.Linq.Expressions.ExpressionType;

using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.TypeSystem;

using NUnit.Framework;

namespace ICSharpCode.Decompiler.Tests.Output
{
	[TestFixture]
	public class GuardExitNormalizationTests
	{
		[Test]
		public void PreserveValueTypeReceiverSnapshot()
		{
			var decompiler = new CSharpDecompiler(typeof(GuardExitNormalizationTests).Assembly.Location,
				new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
			var tree = decompiler.DecompileType(new FullTypeName(typeof(ScalarSnapshotFixture).FullName));
			var method = tree.Descendants.OfType<MethodDeclaration>().Single(m => m.Name == "Invoke");
			Assert.That(method.Descendants.OfType<VariableDeclarationStatement>().Count(v => v.Type is PrimitiveType { Keyword: "float" }), Is.EqualTo(1));
		}

		public static class ScalarSnapshotFixture
		{
			public struct Consumer
			{
				public float Bias;
				public float Use(float value) { return value + Bias; }
			}
			static float Read(float value) { return value; }
			public static float Invoke(Consumer consumer, float value)
			{
				float snapshot = Read(value);
				return consumer.Use(snapshot);
			}
		}

		[Test]
		public void NormalizeOnlyEquivalentBooleanGuardExits()
		{
			var decompiler = new CSharpDecompiler(typeof(GuardExitNormalizationTests).Assembly.Location,
				new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
			var transform = decompiler.AstTransforms.Single(t => t.GetType().Name == "NormalizeBlockStatements");
			var fixture = new Check(transform);
			decompiler.AstTransforms.Clear();
			decompiler.AstTransforms.Add(fixture);
			decompiler.DecompileTypeAsString(new FullTypeName(typeof(GuardExitNormalizationTests).FullName));
			Assert.That(fixture.Count, Is.EqualTo(8));
		}

		sealed class Check(IAstTransform normalizer) : IAstTransform
		{
			public int Count;
			public void Run(AstNode root, TransformContext context)
			{
				foreach (string kind in new[] { "positive", "following", "else", "unknown", "lifted", "label", "function", "unbound-local" }) {
					var boolean = context.TypeSystem.FindType(KnownTypeCode.Boolean);
					var condition = new UnaryOperatorExpression(UnaryOperatorType.Not, new IdentifierExpression("enabled"));
					if (kind != "unknown")
						condition.AddAnnotation(new OperatorResolveResult(boolean, ExpressionType.Not, null, kind == "lifted", new[] { new ResolveResult(boolean) }));
					var guard = new IfElseStatement(condition, new BlockStatement { new ContinueStatement() });
					if (kind == "else") guard.FalseStatement = new BlockStatement { new EmptyStatement() };
					var block = new BlockStatement { guard, new ExpressionStatement(new InvocationExpression(new IdentifierExpression("Observe"))), new ContinueStatement() };
					if (kind == "following") block.Add(new ExpressionStatement(new InvocationExpression(new IdentifierExpression("After"))));
					if (kind == "label") block.Statements.InsertBefore(block.Statements.Last(), new LabelStatement { Label = "target" });
					if (kind == "function") block.Statements.InsertBefore(block.Statements.Last(), new LocalFunctionDeclarationStatement { Declaration = new MethodDeclaration { Name = "Local", ReturnType = new PrimitiveType("void"), Body = new BlockStatement() } });
					if (kind == "unbound-local") block.Statements.InsertBefore(block.Statements.Last(), new VariableDeclarationStatement(new PrimitiveType("int"), "value", new PrimitiveExpression(1)));
					var loop = new WhileStatement(new PrimitiveExpression(true), block);
					normalizer.Run(loop, context);
					int expected = kind == "positive" ? 1 : 2;
					Assert.That(loop.Descendants.OfType<ContinueStatement>().Count(), Is.EqualTo(expected), kind);
					Assert.That(loop.Descendants.OfType<InvocationExpression>().Count(i => i.Target is IdentifierExpression id && id.Identifier == "Observe"), Is.EqualTo(1), kind);
					Count++;
				}
			}
		}
	}
}
