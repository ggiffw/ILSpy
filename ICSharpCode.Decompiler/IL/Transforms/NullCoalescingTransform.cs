// Copyright (c) 2017 Siegfried Pammer
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
using System.Threading.Tasks;

using ICSharpCode.Decompiler.TypeSystem;

namespace ICSharpCode.Decompiler.IL.Transforms
{
	/// <summary>
	/// Transform for constructing the NullCoalescingInstruction (if.notnull(a,b), or in C#: ??)
	/// Note that this transform only handles the case where a,b are reference types.
	/// 
	/// The ?? operator for nullable value types is handled by NullableLiftingTransform.
	/// </summary>
	public class NullCoalescingTransform : IStatementTransform, IILTransform
	{
		void IILTransform.Run(ILFunction function, ILTransformContext context)
		{
			if (function.Body is not BlockContainer container)
				return;
			HashSet<Block> initiallyReachable = null;
			if (!container.Descendants.Any(i => i is TryCatch or TryFinally or TryFault))
			{
				foreach (var block in container.Blocks.ToArray())
				{
					context.CancellationToken.ThrowIfCancellationRequested();
					if (!TryBuildSharedJoinFallback(block, container, out var variable, out var value, out var join))
						continue;
					context.Step("NullCoalescingTransform: private snapshots with shared join", block);
					initiallyReachable ??= container.TopologicalSort(deleteUnreachableBlocks: true).ToHashSet();
					var store = new StLoc(variable, value);
					store.AddILRange(block);
					block.Instructions.Clear();
					block.Instructions.Add(store);
					block.Instructions.Add(new Branch(join));
				}
			}
			foreach (var block in container.Blocks.ToArray())
			{
				context.CancellationToken.ThrowIfCancellationRequested();
				if (block.Instructions.FirstOrDefault() is not StLoc store
					|| store.Variable.Kind != VariableKind.Local && store.Variable.Kind != VariableKind.StackSlot
					|| store.Variable.Type.IsReferenceType != true || store.Variable.AddressCount != 0
					|| !HasOnlyTerminalNullUses(store.Variable, container))
					continue;
				int budget = 256;
				if (!TryBuildTerminalFallback(block, store.Variable, container, new HashSet<Block>(), ref budget, out var value))
					continue;
				context.Step("NullCoalescingTransform: terminal shared fallback", block);
				initiallyReachable ??= container.TopologicalSort(deleteUnreachableBlocks: true).ToHashSet();
				var result = new Leave(container, value);
				result.AddILRange(block);
				block.Instructions.Clear();
				block.Instructions.Add(result);
			}
			if (initiallyReachable != null)
			{
				var stillReachable = container.TopologicalSort(deleteUnreachableBlocks: true).ToHashSet();
				// Preserve pre-existing unreachable code when RemoveDeadCode is off.
				container.Blocks.RemoveAll(b => initiallyReachable.Contains(b) && !stillReachable.Contains(b));
				container.SortBlocks();
			}
		}

		static bool TryBuildSharedJoinFallback(Block entry, BlockContainer container,
			out ILVariable variable, out ILInstruction value, out Block join)
		{
			variable = null;
			value = null;
			join = null;
			var parts = new List<ILInstruction>();
			var visited = new HashSet<Block>();
			var block = entry;
			while (parts.Count < 16 && block.Parent == container && block.IncomingEdgeCount == 1 && visited.Add(block))
			{
				if (block.Instructions.Count == 5
					&& block.Instructions[0] is StLoc source && Private(source.Variable)
					&& source.Variable.IsSingleDefinition && source.Variable.LoadCount == 2
					&& block.Instructions[1] is StLoc flag && Private(flag.Variable)
					&& flag.Variable.IsSingleDefinition && flag.Variable.LoadCount == 1
					&& flag.Value is Comp { Kind: ComparisonKind.Equality, InputType: StackType.Obj, LiftingKind: ComparisonLiftingKind.None } comparison
					&& comparison.Left.MatchLdLoc(source.Variable) && comparison.Right.MatchLdNull()
					&& block.Instructions[2] is StLoc copy && Private(copy.Variable)
					&& copy.Variable.Type.IsReferenceType == true && copy.Variable.Type.Equals(source.Variable.Type)
					&& copy.Value.MatchLdLoc(source.Variable)
					&& block.Instructions[3] is IfInstruction test && test.Condition.MatchLdLoc(flag.Variable)
					&& test.FalseInst.MatchNop() && test.TrueInst is Branch missing
					&& block.Instructions[4] is Branch present && present.TargetBlock.Parent == container
					&& (variable == null || variable == copy.Variable) && (join == null || join == present.TargetBlock))
				{
					variable = copy.Variable;
					join = present.TargetBlock;
					parts.Add(source.Value);
					block = missing.TargetBlock;
					continue;
				}
				if (parts.Count == 0 || block.Instructions.Count != 2
					|| block.Instructions[0] is not StLoc tail || tail.Variable != variable
					|| block.Instructions[1] is not Branch done || done.TargetBlock != join)
					return false;
				parts.Add(tail.Value);
				var destination = variable;
				if (variable.StoreCount != parts.Count || visited.Contains(join)
					|| parts.Any(p => p.ResultType != StackType.Obj || p.Descendants.Any(i =>
						i is LdLoc load && load.Variable == destination || i is StLoc store && store.Variable == destination
						|| i.HasDirectFlag(InstructionFlags.ControlFlow))))
					return false;
				// No address or exception handler can observe the intermediate null
				// stores. Each fallback stays conditional, at the same call boundary.
				value = parts[^1].Clone();
				for (int n = parts.Count - 2; n >= 0; n--)
					value = new NullCoalescingInstruction(variable.Type, NullCoalescingKind.Ref, parts[n].Clone(), value);
				return true;
			}
			return false;

			static bool Private(ILVariable local) => local.AddressCount == 0
				&& local.Kind is VariableKind.Local or VariableKind.StackSlot;
		}

		// The local is a control-flow temporary, never observed by a callback,
		// exception handler or another expression. Every successful path returns it.
		static bool HasOnlyTerminalNullUses(ILVariable variable, BlockContainer container)
		{
			foreach (var load in variable.LoadInstructions)
			{
				if (load.Parent is Leave leave && leave.IsLeavingFunction && leave.Parent is Block returned
					&& returned.Parent == container && returned.Instructions.Count == 1)
					continue;
				if (load.Parent is Comp comp && comp.Parent is IfInstruction test && test.Parent is Block block
					&& block.Parent == container && MatchTerminalNullGuard(block, variable, container, out _, out _))
					continue;
				return false;
			}
			return true;
		}

		static bool MatchTerminalNullGuard(Block block, ILVariable variable, BlockContainer container,
			out StLoc store, out Block fallback)
		{
			store = null;
			fallback = null;
			if (block.Parent != container || block.Instructions.Count != 3
				|| block.Instructions[0] is not StLoc assignment || assignment.Variable != variable
				|| block.Instructions[1] is not IfInstruction test || !test.FalseInst.MatchNop()
				|| test.Condition is not Comp { Kind: ComparisonKind.Equality, InputType: StackType.Obj, LiftingKind: ComparisonLiftingKind.None } comparison
				|| !comparison.Left.MatchLdLoc(variable) || !comparison.Right.MatchLdNull()
				|| test.TrueInst is not Branch missing || missing.TargetBlock.Parent != container
				|| block.Instructions[2] is not Branch present || present.TargetBlock.Parent != container
				|| present.TargetBlock.Instructions.Count != 1
				|| present.TargetBlock.Instructions[0] is not Leave returned || !returned.IsLeavingFunction
				|| returned.TargetContainer != container || !returned.Value.MatchLdLoc(variable))
				return false;
			store = assignment;
			fallback = missing.TargetBlock;
			return true;
		}

		static bool TryBuildTerminalFallback(Block block, ILVariable variable, BlockContainer container,
			HashSet<Block> path, ref int budget, out ILInstruction value)
		{
			value = null;
			if (block.Parent != container || path.Count >= 16 || !path.Add(block))
				return false;
			if (block.Instructions.Count == 1 && block.Instructions[0] is Leave returned
				&& returned.IsLeavingFunction && returned.TargetContainer == container && SafeValue(returned.Value))
			{
				budget -= returned.Value.Descendants.Count();
				if (budget < 0)
					return false;
				value = returned.Value.Clone();
				return true;
			}
			if (!MatchTerminalNullGuard(block, variable, container, out var store, out var fallback) || !SafeValue(store.Value))
				return false;
			budget -= store.Value.Descendants.Count();
			if (budget < 0 || !TryBuildTerminalFallback(fallback, variable, container, path, ref budget, out var next))
				return false;
			// Cloning a shared tail changes code size, but each selected path still
			// evaluates every operand once, in its original order and only on null.
			value = new NullCoalescingInstruction(variable.Type, NullCoalescingKind.Ref, store.Value.Clone(), next);
			value.AddILRange(store);
			return true;

			bool SafeValue(ILInstruction expression) => expression.ResultType == StackType.Obj
				&& !expression.Descendants.Any(i => i is LdLoc load && load.Variable == variable
					|| i is StLoc assignment && assignment.Variable == variable
					|| i.HasDirectFlag(InstructionFlags.ControlFlow));
		}

		public void Run(Block block, int pos, StatementTransformContext context)
		{
			if (TransformRefTypes(block, pos, context))
				return;
			if (TransformHoistedConstructorArgumentNullGuard(block, pos, context))
				return;
			TransformThrowExpressionValueTypes(block, pos, context);
		}

		/// <summary>
		/// Handles NullCoalescingInstruction case 1: reference types.
		/// </summary>
		bool TransformRefTypes(Block block, int pos, StatementTransformContext context)
		{
			if (!(block.Instructions[pos] is StLoc stloc))
				return false;
			if (stloc.Variable.Kind != VariableKind.StackSlot)
				return false;
			if (!block.Instructions[pos + 1].MatchIfInstruction(out var condition, out var trueInst))
				return false;
			if (!(condition.MatchCompEquals(out var left, out var right) && left.MatchLdLoc(stloc.Variable) && right.MatchLdNull()))
				return false;
			trueInst = Block.Unwrap(trueInst);
			// stloc s(valueInst)
			// if (comp(ldloc s == ldnull)) {
			//		stloc s(fallbackInst)
			// }
			// =>
			// stloc s(if.notnull(valueInst, fallbackInst))
			if (trueInst.MatchStLoc(stloc.Variable, out var fallbackValue))
			{
				context.Step("NullCoalescingTransform: simple (reference types)", stloc);
				stloc.Value = new NullCoalescingInstruction(stloc.Variable.Type, NullCoalescingKind.Ref, stloc.Value, fallbackValue);
				block.Instructions.RemoveAt(pos + 1); // remove if instruction
				ILInlining.InlineOneIfPossible(block, pos, InliningOptions.None, context);
				return true;
			}
			// sometimes the compiler generates:
			// stloc s(valueInst)
			// if (comp(ldloc s == ldnull)) {
			//		stloc v(fallbackInst)
			//      stloc s(ldloc v)
			// }
			// v must be single-assign and single-use.
			if (trueInst is Block trueBlock && trueBlock.Instructions.Count == 2
				&& trueBlock.Instructions[0].MatchStLoc(out var temporary, out fallbackValue)
				&& temporary.IsSingleDefinition && temporary.LoadCount == 1
				&& trueBlock.Instructions[1].MatchStLoc(stloc.Variable, out var useOfTemporary)
				&& useOfTemporary.MatchLdLoc(temporary))
			{
				context.Step("NullCoalescingTransform: with temporary variable (reference types)", stloc);
				stloc.Value = new NullCoalescingInstruction(stloc.Variable.Type, NullCoalescingKind.Ref, stloc.Value, fallbackValue);
				block.Instructions.RemoveAt(pos + 1); // remove if instruction
				ILInlining.InlineOneIfPossible(block, pos, InliningOptions.None, context);
				return true;
			}
			// stloc obj(valueInst)
			// if (comp(ldloc obj == ldnull)) {
			//		throw(...)
			// }
			// =>
			// stloc obj(if.notnull(valueInst, throw(...)))
			if (context.Settings.ThrowExpressions && trueInst is Throw throwInst)
			{
				context.Step("NullCoalescingTransform (reference types + throw expression)", stloc);
				stloc.Value = new NullCoalescingInstruction(stloc.Variable.Type, NullCoalescingKind.Ref, stloc.Value, throwInst);
				block.Instructions.RemoveAt(pos + 1); // remove if instruction
				ILInlining.InlineOneIfPossible(block, pos, InliningOptions.None, context);
				return true;
			}
			return false;
		}

		/// <summary>
		/// When an argument of a chained constructor call contains a throwing null-check
		/// (e.g. <c>arg ?? throw ...</c>) and <c>arg</c> is evaluated more than once, the C#
		/// compiler hoists the null-check in front of the chained call:
		/// <code>
		///   if (comp.o(ldloc param == ldnull)) throw(...)
		///   call Base..ctor(..., ldlen(ldloc param), ..., ldloc param, ...)
		/// </code>
		/// The guard then blocks TransformFieldAndConstructorInitializers from lifting the chained
		/// call into a <c>: this(...)</c> / <c>: base(...)</c> clause. Replace the guard with
		/// <code>
		///   stloc temp(if.notnull(ldloc param, throw(...)))
		/// </code>
		/// redirecting the first following use of the parameter (the position the check was hoisted
		/// from) to <c>temp</c>, and leave moving the coalescing expression into the chained call to
		/// ILInlining, which does so only when that preserves the order of evaluation.
		/// Only reference-type chains (<c>base/this..ctor</c> calls, identified by IL offset via
		/// <see cref="ILInlining.IsInConstructorInitializer"/>) need this: value types chain via
		/// <c>this = new TSelf(...)</c>, which is an ordinary body statement, so a preceding
		/// guard statement is legal C# and can stay.
		/// </summary>
		bool TransformHoistedConstructorArgumentNullGuard(Block block, int pos, StatementTransformContext context)
		{
			// A throw-expression is the only way to express the folded form.
			if (!context.Settings.ThrowExpressions)
				return false;

			var function = block.Ancestors.OfType<ILFunction>().FirstOrDefault();
			if (function?.Method is not { IsConstructor: true, IsStatic: false })
				return false;

			// Match `if (comp(ldloc param == ldnull)) throw(...)` with no else branch.
			var guard = block.Instructions[pos];
			if (!IsArgumentNullGuard(guard, out var paramLoad, out var throwInst))
				return false;

			// Only a guard sitting in the initializer's argument evaluation, i.e. before the
			// chained constructor call, is necessarily compiler-hoisted.
			if (!ILInlining.IsInConstructorInitializer(function, guard))
				return false;

			// The compiler hoists the guard directly in front of the statement containing the
			// parameter use it was lifted out of, so that use must be order-safely reachable
			// within the immediately following statement.
			var paramLoadParent = paramLoad.Parent;
			var paramLoadChildIndex = paramLoad.ChildIndex;
			var throwInstParent = throwInst.Parent;
			var throwInstChildIndex = throwInst.ChildIndex;
			var expressionWithThrow = new NullCoalescingInstruction(paramLoad.Variable.Type, NullCoalescingKind.Ref, paramLoad, throwInst);
			var result = ILInlining.FindLoadInNext(block.Instructions[pos + 1], paramLoad.Variable, expressionWithThrow,
				InliningOptions.None);
			if (result.Type != ILInlining.FindResultType.Found || result.LoadInst is not LdLoc firstUse)
			{
				// reset the primary positions (see remarks on ILInstruction.Parent)
				var paramLoadSiblings = paramLoadParent.Children;
				paramLoadSiblings[paramLoadChildIndex] = paramLoad;
				var throwInstSiblings = throwInstParent.Children;
				throwInstSiblings[throwInstChildIndex] = throwInst;
				return false;
			}

			context.Step($"NullCoalescingTransform: fold hoisted null-guard of '{paramLoad.Variable.Name}' into argument of chained constructor call", guard);

			var temp = function.RegisterVariable(VariableKind.StackSlot, paramLoad.Variable.Type);
			firstUse.Variable = temp;
			var stloc = new StLoc(temp, expressionWithThrow);
			stloc.AddILRange(guard);
			block.Instructions[pos] = stloc;
			context.EndStep(stloc);
			ILInlining.InlineOneIfPossible(block, pos, InliningOptions.None, context);
			return true;
		}

		/// <summary>
		/// Matches a hoisted argument null-guard `if (comp(ldloc param == ldnull)) throw(...)`
		/// (no else branch). Only parameters qualify: nothing else is in scope before the
		/// constructor initializer.
		/// </summary>
		static bool IsArgumentNullGuard(ILInstruction inst, out LdLoc paramLoad, out Throw throwInst)
		{
			paramLoad = null;
			throwInst = null;
			if (!inst.MatchIfInstruction(out var condition, out var trueInst))
				return false;
			if (!(Block.Unwrap(trueInst) is Throw t))
				return false;
			if (!(condition.MatchCompEquals(out var left, out var right) && right.MatchLdNull() && left is LdLoc load))
				return false;
			if (load.Variable.Kind != VariableKind.Parameter)
				return false;
			paramLoad = load;
			throwInst = t;
			return true;
		}

		/// <summary>
		/// stloc v(value)
		/// if (logic.not(call get_HasValue(ldloca v))) throw(...)
		/// ... Call(arg1, arg2, call GetValueOrDefault(ldloca v), arg4) ...
		/// =>
		/// ... Call(arg1, arg2, if.notnull(value, throw(...)), arg4) ...
		/// </summary>
		bool TransformThrowExpressionValueTypes(Block block, int pos, StatementTransformContext context)
		{
			if (pos + 2 >= block.Instructions.Count)
				return false;
			if (!(block.Instructions[pos] is StLoc stloc))
				return false;
			ILVariable v = stloc.Variable;
			if (!(v.StoreCount == 1 && v.LoadCount == 0 && v.AddressCount == 2))
				return false;
			if (!block.Instructions[pos + 1].MatchIfInstruction(out var condition, out var trueInst))
				return false;
			if (!(Block.Unwrap(trueInst) is Throw throwInst))
				return false;
			if (!condition.MatchLogicNot(out var arg))
				return false;
			if (!(arg is CallInstruction call && NullableLiftingTransform.MatchHasValueCall(call, v)))
				return false;
			var throwInstParent = throwInst.Parent;
			var throwInstChildIndex = throwInst.ChildIndex;
			var underlyingType = NullableType.GetUnderlyingType(call.Method.DeclaringType);
			var nullCoalescingWithThrow = new NullCoalescingInstruction(
				underlyingType,
				NullCoalescingKind.NullableWithValueFallback,
				stloc.Value,
				throwInst);
			var result = ILInlining.FindLoadInNext(block.Instructions[pos + 2], v, nullCoalescingWithThrow, InliningOptions.None);
			if (result.Type == ILInlining.FindResultType.Found
				&& NullableLiftingTransform.MatchGetValueOrDefault(result.LoadInst.Parent, v))
			{
				context.Step("NullCoalescingTransform (value types + throw expression)", stloc);
				result.LoadInst.Parent.ReplaceWith(nullCoalescingWithThrow);
				block.Instructions.RemoveRange(pos, 2); // remove store(s) and if instruction
				return true;
			}
			else
			{
				// reset the primary position (see remarks on ILInstruction.Parent)
				stloc.Value = stloc.Value;
				var children = throwInstParent.Children;
				children[throwInstChildIndex] = throwInst;
				return false;
			}
		}
	}
}
