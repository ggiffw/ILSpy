.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89)
  .ver 4:0:0:0
}
.assembly NativeArrayIndices { }
.module NativeArrayIndices.dll

.class public abstract sealed auto ansi beforefieldinit NativeArrayIndices
    extends [mscorlib]System.Object
{
  .method public hidebysig static int32 ReadSigned(int32[] 'array', int64 index) cil managed
  {
    .maxstack 2
    ldarg.0
    ldarg.1
    conv.i
    ldelem.i4
    ret
  }
  .method public hidebysig static int32 ReadUnsigned(int32[] 'array', uint64 index) cil managed
  {
    .maxstack 2
    ldarg.0
    ldarg.1
    conv.i
    ldelem.i4
    ret
  }
  .method public hidebysig static int32 ReadNativeUnsigned(int32[] 'array', native unsigned int index) cil managed
  {
    .maxstack 2
    ldarg.0
    ldarg.1
    ldelem.i4
    ret
  }
  .method public hidebysig static int32 ReadUInt32(int32[] 'array', uint32 index) cil managed
  {
    .maxstack 2
    ldarg.0
    ldarg.1
    conv.u
    ldelem.i4
    ret
  }
  .method public hidebysig static int32 ReadCheckedUnsigned(int32[] 'array', uint64 index) cil managed
  {
    .maxstack 2
    ldarg.0
    ldarg.1
    conv.ovf.i.un
    ldelem.i4
    ret
  }
  .method public hidebysig static void StoreSigned(int32[] 'array', int64 index, int32 'value') cil managed
  {
    .maxstack 3
    ldarg.0
    ldarg.1
    conv.i
    ldarg.2
    stelem.i4
    ret
  }
  .method public hidebysig static void StoreUnsigned(int32[] 'array', uint64 index, int32 'value') cil managed
  {
    .maxstack 3
    ldarg.0
    ldarg.1
    conv.i
    ldarg.2
    stelem.i4
    ret
  }
  .method public hidebysig static int32& AddressSigned(int32[] 'array', int64 index) cil managed
  {
    .maxstack 2
    ldarg.0
    ldarg.1
    conv.i
    ldelema [mscorlib]System.Int32
    ret
  }
  .method public hidebysig static int32& AddressUnsigned(int32[] 'array', uint64 index) cil managed
  {
    .maxstack 2
    ldarg.0
    ldarg.1
    conv.i
    ldelema [mscorlib]System.Int32
    ret
  }
  .method public hidebysig static void StoreWithCallback(int32[] 'array', uint64 index,
      class [mscorlib]System.Func`1<int32> rhs) cil managed
  {
    .maxstack 3
    ldarg.0
    ldarg.1
    conv.i
    ldarg.2
    callvirt instance !0 class [mscorlib]System.Func`1<int32>::Invoke()
    stelem.i4
    ret
  }
}
