using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace ICSharpCode.Decompiler.Tests.TestCases.Correctness
{
	class FloatingPointArithmetic
	{
		public static int Main(string[] args)
		{
			Issue999();
			Issue1656();
			Issue1794();
			FloatingNegation();
			return 0;
		}

		static void Issue999()
		{
			for (float i = -10f; i <= 10f; i += 0.01f)
				Console.WriteLine("{1:R}: {0:R}", M(i), i);
		}

		static float M(float v)
		{
			return 0.99f * v + 0.01f;
		}

		static void Issue1656()
		{
			double primary = 'B';
			CxAssert((++primary) == 'C');
			CxAssert((--primary) == 'B');
			CxAssert((primary++) == 'B');
			CxAssert((primary--) == 'C');
		}

		static void Issue1794()
		{
			Console.WriteLine("CastUnsignedToFloat:");
			Console.WriteLine(CastUnsignedToFloat(9007199791611905).ToString("r"));
			Console.WriteLine("CastUnsignedToDouble:");
			Console.WriteLine(CastUnsignedToDouble(9007199791611905).ToString("r"));
			Console.WriteLine("CastUnsignedToFloatViaDouble:");
			Console.WriteLine(CastUnsignedToFloatViaDouble(9007199791611905).ToString("r"));

			Console.WriteLine("CastSignedToFloat:");
			Console.WriteLine(CastSignedToFloat(9007199791611905).ToString("r"));
			Console.WriteLine("ImplicitCastSignedToFloat:");
			Console.WriteLine(ImplicitCastSignedToFloat(9007199791611905).ToString("r"));
			Console.WriteLine("CastSignedToDouble:");
			Console.WriteLine(CastSignedToDouble(9007199791611905).ToString("r"));
			Console.WriteLine("CastSignedToFloatViaDouble:");
			Console.WriteLine(CastSignedToFloatViaDouble(9007199791611905).ToString("r"));
		}

		static float CastUnsignedToFloat(ulong val)
		{
			return (float)val;
		}

		static double CastUnsignedToDouble(ulong val)
		{
			return (double)val;
		}

		static float CastUnsignedToFloatViaDouble(ulong val)
		{
			// The double-rounding can increase the rounding error
			return (float)(double)val;
		}

		static float CastSignedToFloat(long val)
		{
			return (float)val;
		}

		static double CastSignedToDouble(long val)
		{
			return (double)val;
		}

		static float CastSignedToFloatViaDouble(long val)
		{
			// The double-rounding can increase the rounding error
			return (float)(double)val;
		}

		static float ImplicitCastSignedToFloat(long val)
		{
			return val;
		}

		static void CxAssert(bool v)
		{
			if (!v)
			{
				throw new InvalidOperationException();
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static float NegateSingle(float value) { return -value; }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static double NegateDouble(double value) { return -value; }
		static float SubtractSingle(float value) { return 0f - value; }
		static double SubtractDouble(double value) { return 0d - value; }
		static float NegateTwiceSingle(float value) { return -(-value); }
		static double NegateTwiceDouble(double value) { return -(-value); }
		static float? NegateNullableSingle(float? value) { return -value; }
		static double? NegateNullableDouble(double? value) { return -value; }
		static float NegateConverted(double value) { return -(float)value; }
		static double ConvertNegate(float value) { return -(double)value; }
		static float NegateSelected(bool select, float a, float b) { return -(select ? a : b); }
		static float AddNegatedSingle(float a, float b) { return -a + b; }
		static float SubtractNegatedSingle(float a, float b) { return a - -b; }
		static double AddNegatedDouble(double a, double b) { return -a + b; }
		static double SubtractNegatedDouble(double a, double b) { return a - -b; }
		static int trace;
		static float ReadSingle(int tag, int fault)
		{
			trace = trace * 10 + tag;
			if (tag == fault)
				throw new InvalidOperationException();
			return tag == 1 ? 1.5f : -2.25f;
		}
		static float NegateCalls(int fault) { return -(ReadSingle(1, fault) + ReadSingle(2, fault)); }
		static float SubtractCalls(int fault) { return -ReadSingle(1, fault) - ReadSingle(2, fault); }
		static string SingleBits(float value) { return BitConverter.ToInt32(BitConverter.GetBytes(value), 0).ToString("X8"); }
		static string DoubleBits(double value) { return BitConverter.DoubleToInt64Bits(value).ToString("X16"); }
		static void FloatingNegation()
		{
			int[] singles = { 0, int.MinValue, 1, int.MinValue + 1, 0x007FFFFF, 0x00800000, 0x3F800000, unchecked((int)0xBF800000), 0x7F800000, unchecked((int)0xFF800000), 0x7FC01234, unchecked((int)0xFFC05678), 0x7FA01234, unchecked((int)0xFFA05678) };
			foreach (int bits in singles)
			{
				float value = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
				Console.WriteLine("single:{0}:{1}:{2}:{3}:{4}:{5}", bits.ToString("X8"), SingleBits(NegateSingle(value)), SingleBits(SubtractSingle(value)), SingleBits(NegateTwiceSingle(value)), SingleBits(NegateNullableSingle(value).Value), DoubleBits(ConvertNegate(value)));
				Console.WriteLine("select:{0}:{1}", SingleBits(NegateSelected(true, value, 0f)), SingleBits(NegateSelected(false, 0f, value)));
				foreach (int otherBits in new[] { 0, int.MinValue, 0x7FC01234, unchecked((int)0xFFC05678) })
				{
					float other = BitConverter.ToSingle(BitConverter.GetBytes(otherBits), 0);
					Console.WriteLine("single-pair:{0}:{1}", SingleBits(AddNegatedSingle(value, other)), SingleBits(SubtractNegatedSingle(value, other)));
				}
			}
			long[] doubles = { 0L, long.MinValue, 1L, long.MinValue + 1, 0x000FFFFFFFFFFFFFL, 0x0010000000000000L, 0x3FF0000000000000L, unchecked((long)0xBFF0000000000000), 0x7FF0000000000000L, unchecked((long)0xFFF0000000000000), 0x7FF8123456789ABCL, unchecked((long)0xFFF856789ABC1234), 0x7FF0123456789ABCL, unchecked((long)0xFFF056789ABC1234) };
			foreach (long bits in doubles)
			{
				double value = BitConverter.Int64BitsToDouble(bits);
				Console.WriteLine("double:{0}:{1}:{2}:{3}:{4}:{5}", bits.ToString("X16"), DoubleBits(NegateDouble(value)), DoubleBits(SubtractDouble(value)), DoubleBits(NegateTwiceDouble(value)), DoubleBits(NegateNullableDouble(value).Value), SingleBits(NegateConverted(value)));
				foreach (long otherBits in new[] { 0L, long.MinValue, 0x7FF8123456789ABCL, unchecked((long)0xFFF856789ABC1234) })
				{
					double other = BitConverter.Int64BitsToDouble(otherBits);
					Console.WriteLine("double-pair:{0}:{1}", DoubleBits(AddNegatedDouble(value, other)), DoubleBits(SubtractNegatedDouble(value, other)));
				}
			}
			Console.WriteLine("null:{0}:{1}", NegateNullableSingle(null).HasValue, NegateNullableDouble(null).HasValue);
			for (int fault = 0; fault <= 2; fault++)
			{
				trace = 0;
				try
				{ Console.WriteLine("calls:{0}:{1}", SingleBits(NegateCalls(fault)), trace); }
				catch (InvalidOperationException) { Console.WriteLine("calls:throw:{0}", trace); }
				trace = 0;
				try
				{ Console.WriteLine("subtract:{0}:{1}", SingleBits(SubtractCalls(fault)), trace); }
				catch (InvalidOperationException) { Console.WriteLine("subtract:throw:{0}", trace); }
			}
			Expression<Func<float, float>> negateTree = value => -value;
			Expression<Func<float, float>> subtractTree = value => 0f - value;
			Expression<Func<float?, float?>> nullableTree = value => -value;
			Expression<Func<float, float>> checkedTree = value => checked(-value);
			Console.WriteLine("trees:{0}:{1}:{2}:{3}", negateTree.Body.NodeType, subtractTree.Body.NodeType, nullableTree.Body.NodeType, checkedTree.Body.NodeType);
			var negateDelegate = negateTree.Compile();
			var subtractDelegate = subtractTree.Compile();
			var nullableDelegate = nullableTree.Compile();
			var checkedDelegate = checkedTree.Compile();
			foreach (int bits in singles)
			{
				float value = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
				Console.WriteLine("tree-value:{0}:{1}:{2}:{3}", SingleBits(negateDelegate(value)), SingleBits(subtractDelegate(value)), SingleBits(nullableDelegate(value).Value), SingleBits(checkedDelegate(value)));
			}
			Console.WriteLine("tree-null:{0}", nullableDelegate(null).HasValue);
			Expression<Func<double, double>> negateDoubleTree = value => -value;
			Expression<Func<double, double>> subtractDoubleTree = value => 0d - value;
			Expression<Func<double?, double?>> nullableDoubleTree = value => -value;
			Console.WriteLine("double-trees:{0}:{1}:{2}", negateDoubleTree.Body.NodeType, subtractDoubleTree.Body.NodeType, nullableDoubleTree.Body.NodeType);
			var negateDoubleDelegate = negateDoubleTree.Compile();
			var subtractDoubleDelegate = subtractDoubleTree.Compile();
			var nullableDoubleDelegate = nullableDoubleTree.Compile();
			foreach (long bits in doubles)
			{
				double value = BitConverter.Int64BitsToDouble(bits);
				Console.WriteLine("double-tree-value:{0}:{1}:{2}", DoubleBits(negateDoubleDelegate(value)), DoubleBits(subtractDoubleDelegate(value)), DoubleBits(nullableDoubleDelegate(value).Value));
			}
			Console.WriteLine("double-tree-null:{0}", nullableDoubleDelegate(null).HasValue);
			var parameter = Expression.Parameter(typeof(float), "value");
			var explicitlyCheckedTree = Expression.Lambda<Func<float, float>>(Expression.NegateChecked(parameter), parameter);
			Console.WriteLine("explicitly-checked-tree:{0}:{1}", explicitlyCheckedTree.Body.NodeType, SingleBits(explicitlyCheckedTree.Compile()(0f)));
		}
	}
}
