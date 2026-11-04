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

public static class NativeArrayIndices
{
	public static int ReadSigned(int[] array, long index)
	{
		return array[(nint)index];
	}

	public static int ReadUnsigned(int[] array, ulong index)
	{
		return array[(nint)index];
	}

	public static int ReadNativeUnsigned(int[] array, UIntPtr index)
	{
		return array[(nint)(nuint)index];
	}

	public static int ReadUInt32(int[] array, uint index)
	{
		return array[index];
	}

	public static int ReadCheckedUnsigned(int[] array, ulong index)
	{
		return array[index];
	}

	public static void StoreSigned(int[] array, long index, int value)
	{
		array[(nint)index] = value;
	}

	public static void StoreUnsigned(int[] array, ulong index, int value)
	{
		array[(nint)index] = value;
	}

	public static ref int AddressSigned(int[] array, long index)
	{
		return ref array[(nint)index];
	}

	public static ref int AddressUnsigned(int[] array, ulong index)
	{
		return ref array[(nint)index];
	}

	public static void StoreWithCallback(int[] array, ulong index, Func<int> rhs)
	{
		array[(nint)index] = rhs();
	}
}
