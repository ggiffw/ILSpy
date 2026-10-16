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

using System.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ICSharpCode.Decompiler.CSharp;
using NUnit.Framework;
namespace ICSharpCode.Decompiler.Tests;
[TestFixture]
public class InlineArrayForeachTests
{
    static float Lookup(float[] array, Dictionary<float,float> map) { float sum=0;for(int i=0;i<array.Length;i++){float v=map[array[i]];sum+=v;sum-=v;}return sum; }
    static float Changed(float[] array, Dictionary<float,float> map, Func<float[]> callback) { float sum=0;for(int i=0;i<array.Length;i++){float v=map[array[i]];sum+=v;sum-=v;array=callback();}return sum; }
    static float IndexUsed(float[] array, Dictionary<float,float> map) { float sum=0;for(int i=0;i<array.Length;i++){float v=map[array[i]];sum+=v+i;sum-=v;}return sum; }
    static float Receiver(float[] array, Func<Dictionary<float,float>> callback) { float sum=0;for(int i=0;i<array.Length;i++){float v=callback()[array[i]];sum+=v;sum-=v;}return sum; }
    static float Duplicate(float[] array, Dictionary<float,float> map) { float sum=0;for(int i=0;i<array.Length;i++){float v=map[array[i]];sum+=v+array[i];sum-=v;}return sum; }
    static float Preamble(float[] array, Dictionary<float,float> map, Action callback) { float sum=0;for(int i=0;i<array.Length;i++){callback();float v=map[array[i]];sum+=v;sum-=v;}return sum; }
    static float ByRef(ref float[] array, Dictionary<float,float> map) { float sum=0;for(int i=0;i<array.Length;i++){float v=map[array[i]];sum+=v;sum-=v;}return sum; }
    static float Nonzero(float[] array, Dictionary<float,float> map) { float sum=0;for(int i=1;i<array.Length;i++){float v=map[array[i]];sum+=v;sum-=v;}return sum; }
    static float Collision(float[] array, Dictionary<float,float> map, int item, int item2) { float sum=item+item2;for(int i=0;i<array.Length;i++){float v=map[array[i]];sum+=v;sum-=v;}return sum; }
    static float Anonymous() { var array=new[]{new{Code=1},new{Code=2}};var map=array.ToDictionary(x=>x,_=>3f);float sum=0;for(int i=0;i<array.Length;i++){float v=map[array[i]];sum+=v;sum-=v;}return sum; }
    static void Nested(int[] array, Dictionary<int,List<int>> map, List<int> output) { for(int i=0;i<array.Length;i++) output.Add(map[array[i]].Count); }
    static void NestedChanged(int[] array, Dictionary<int,List<int>> map, List<int> output, Func<int[]> callback) { for(int i=0;i<array.Length;i++){output.Add(map[array[i]].Count);array=callback();} }
    static void NestedReceiver(int[] array, Dictionary<int,List<int>> map, Func<List<int>> callback) { for(int i=0;i<array.Length;i++) callback().Add(map[array[i]].Count); }
    static void NestedLookup(int[] array, Func<Dictionary<int,List<int>>> callback, List<int> output) { for(int i=0;i<array.Length;i++) output.Add(callback()[array[i]].Count); }
    static void NestedIndex(int[] array, Dictionary<int,List<int>> map, List<int> output) { for(int i=0;i<array.Length;i++){output.Add(map[array[i]].Count);output.Add(i);} }
    static void NestedPreamble(int[] array, Dictionary<int,List<int>> map, List<int> output, Action callback) { for(int i=0;i<array.Length;i++){callback();output.Add(map[array[i]].Count);} }
    static void NestedDuplicate(int[] array, Dictionary<int,List<int>> map, List<int> output) { for(int i=0;i<array.Length;i++) output.Add(map[array[i]].Count+array[i]); }
    [TestCase(nameof(Lookup), true)] [TestCase(nameof(Changed), false)]
    [TestCase(nameof(IndexUsed), false)] [TestCase(nameof(Receiver), false)]
    [TestCase(nameof(Duplicate), false)] [TestCase(nameof(Preamble), false)]
    [TestCase(nameof(ByRef), false)] [TestCase(nameof(Nonzero), false)] [TestCase(nameof(Collision), true)]
    [TestCase(nameof(Anonymous), true)]
    [TestCase(nameof(Nested), true)] [TestCase(nameof(NestedChanged), false)]
    [TestCase(nameof(NestedReceiver), false)] [TestCase(nameof(NestedLookup), false)] [TestCase(nameof(NestedIndex), false)]
    [TestCase(nameof(NestedPreamble), false)] [TestCase(nameof(NestedDuplicate), false)]
    public void ForeachFromAnInlineDictionaryKeyKeepsEvaluationAndArraySnapshots(string method, bool expected)
    {
        var dec=new CSharpDecompiler(typeof(InlineArrayForeachTests).Assembly.Location,new DecompilerSettings{ThrowOnAssemblyResolveErrors=false});
        var info=typeof(InlineArrayForeachTests).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!;
        var text=dec.DecompileAsString(System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(info.MetadataToken));
        Assert.That(text.Contains("foreach ("),Is.EqualTo(expected),text);
        if (method==nameof(Collision)) Assert.That(text.Contains("float item3 in"), Is.True, text);
        if (method==nameof(Anonymous)) Assert.That(text.Contains("foreach (var "), Is.True, text);
    }
}
