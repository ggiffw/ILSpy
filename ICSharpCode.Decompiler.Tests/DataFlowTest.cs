// Copyright (c) 2016 Daniel Grunwald
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.Decompiler.FlowAnalysis;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;

using NUnit.Framework;

namespace ICSharpCode.Decompiler.Tests
{
	[TestFixture]
	class DataFlowTest
	{
		[TestCase(StackType.F4)]
		[TestCase(StackType.F8)]
		public void FloatingNegationRetainsIdentityAndOperandEffects(StackType type)
		{
			ILInstruction value = type == StackType.F4 ? (ILInstruction)new LdcF4(1) : new LdcF8(1);
			var neg = new FloatNeg(new InvalidExpression { ExpectedResultType = type });
			var clone = (FloatNeg)neg.Clone();
			Assert.That(neg.ResultType, Is.EqualTo(type));
			Assert.That(neg.DirectFlags, Is.EqualTo(InstructionFlags.None));
			Assert.That(neg.HasFlag(InstructionFlags.MayThrow), Is.True);
			Assert.That(neg.HasFlag(InstructionFlags.SideEffect), Is.True);
			Assert.That(neg.Children.Count(), Is.EqualTo(1));
			Assert.That(clone.Argument, Is.Not.SameAs(neg.Argument));
			Assert.That(neg.Match(clone).Success, Is.True);
			ILInstruction zero = type == StackType.F4 ? (ILInstruction)new LdcF4(0) : new LdcF8(0);
			var subtraction = new BinaryNumericInstruction(BinaryNumericOperator.Sub, zero, value.Clone(), false, Sign.None);
			Assert.That(neg.Match(subtraction).Success, Is.False);
		}

		class RDTest : ReachingDefinitionsVisitor
		{
			ILVariable v;

			public RDTest(ILFunction f, ILVariable v) : base(f, _ => true, CancellationToken.None)
			{
				this.v = v;
			}

			protected internal override void VisitTryFinally(TryFinally inst)
			{
				Assert.That(IsPotentiallyUninitialized(state, v));
				base.VisitTryFinally(inst);
				Assert.That(state.IsReachable);
				Assert.That(GetStores(state, v).Count(), Is.EqualTo(1));
				Assert.That(!IsPotentiallyUninitialized(state, v));
			}
		}

		[Test]
		public void TryFinallyWithAssignmentInFinally()
		{
			ILVariable v = new ILVariable(VariableKind.Local, SpecialType.UnknownType, 0);
			ILFunction f = new ILFunction(
				returnType: SpecialType.UnknownType,
				parameters: new IParameter[0],
				genericContext: new GenericContext(),
				body: new TryFinally(
					new Nop(),
					new StLoc(v, new LdcI4(0))
				));
			f.AddRef();
			f.Variables.Add(v);
			f.Body.AcceptVisitor(new RDTest(f, v));
		}
	}
}
