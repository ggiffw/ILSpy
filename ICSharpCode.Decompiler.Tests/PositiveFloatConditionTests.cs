using System.Linq;
using ExpressionType = System.Linq.Expressions.ExpressionType;
using System.Reflection;

using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.TypeSystem;

using NUnit.Framework;

namespace ICSharpCode.Decompiler.Tests
{
	[TestFixture]
	public class PositiveFloatConditionTests
	{
		[TestCase("single", true)]
		[TestCase("double", true)]
		[TestCase("unknown", false)]
		[TestCase("lifted", false)]
		[TestCase("integer", false)]
		[TestCase("no-call", false)]
		[TestCase("equality", false)]
		[TestCase("user-defined", false)]
		public void PositiveOrderedFloatConditionRetainsOriginalComparison(string kind, bool expected)
		{
			var decompiler = new CSharpDecompiler(typeof(object).Assembly.Location, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
			var boolean = decompiler.TypeSystem.FindType(KnownTypeCode.Boolean);
			var number = decompiler.TypeSystem.FindType(kind == "double" ? KnownTypeCode.Double : kind == "integer" ? KnownTypeCode.Int32 : KnownTypeCode.Single);
			Expression left = kind == "no-call" ? new IdentifierExpression("value") : new InvocationExpression(new IdentifierExpression("Measure"));
			left.AddAnnotation(new ResolveResult(number));
			var right = new PrimitiveExpression(0f);
			right.AddAnnotation(new ResolveResult(number));
			var comparison = new BinaryOperatorExpression(left, kind == "equality" ? BinaryOperatorType.Equality : BinaryOperatorType.GreaterThanOrEqual, right);
			if (kind != "unknown")
				comparison.AddAnnotation(new OperatorResolveResult(boolean, ExpressionType.GreaterThanOrEqual,
					kind == "user-defined" ? decompiler.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().First(m => m.Name == "Equals") : null,
					kind == "lifted", new[] { new ResolveResult(number), new ResolveResult(number) }));
			var condition = new UnaryOperatorExpression(UnaryOperatorType.Not, comparison);
			condition.AddAnnotation(new OperatorResolveResult(boolean, ExpressionType.Not, new ResolveResult(boolean)));
			var match = typeof(PatternStatementTransform).GetMethod("PositiveBooleanCondition", BindingFlags.Static | BindingFlags.NonPublic)!;
			var result = match.Invoke(null, match.GetParameters().Length == 2 ? new object[] { condition, false } : new object[] { condition, false, true });
			Assert.That(result, expected ? Is.SameAs(comparison) : Is.Null);
			Assert.That(comparison.Operator, Is.EqualTo(kind == "equality" ? BinaryOperatorType.Equality : BinaryOperatorType.GreaterThanOrEqual));
		}
	}
}
