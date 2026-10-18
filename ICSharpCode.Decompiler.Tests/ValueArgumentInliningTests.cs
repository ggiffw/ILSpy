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
using System.Reflection;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.Transforms;
using ICSharpCode.Decompiler.TypeSystem;
using NUnit.Framework;
namespace ICSharpCode.Decompiler.Tests;
[TestFixture]
public class ValueArgumentInliningTests
{
    public struct Value {
        public float X, Y;
        public static Value operator +(Value a, Value b) => new Value { X=a.X+b.X, Y=a.Y+b.Y };
    }
    public class Sink {
        public void Accept(int key, Value value) { }
        public void AcceptKey(Value key, Value value) { }
        public void AcceptRef(int key, ref Value value) { }
        public void AcceptObject(int key, object value) { }
    }
    public struct ValueSink { public void Accept(int key, Value value) { } }
    static Value Make() => default;
    static int Earlier() => throw new InvalidOperationException();
    static int Change(ref int key) => ++key;
    static Sink Receiver() => new Sink();
    static void Fill(out Value value) { value=default; }
    static Value[] ChangeArray(ref Value value) { value=default; return new Value[1]; }
    static int ChangeIndex(ref Value value) { value=default; return 0; }
    static void Generic<T>(int key, T value) { }
    static int[] ArrayInput() => new int[1];
    static int IndexInput() => 0;
    static void OperatorResult(Sink sink,int key,Value a,Value b) { Value tmp=a+b; sink.Accept(key,tmp); }
    static void ArrayResult(Sink sink,int key,Value[] array,int index) { Value tmp=array[index]; sink.Accept(key,tmp); }
    static void ArrayKey(Sink sink,Value[] array,int index,Value value) { Value tmp=array[index]; sink.AcceptKey(tmp,value); }
    static void Copy(Sink sink,int key,Value value) { Value tmp=value; sink.Accept(key,tmp); }
    static void GenericResult(int key,Value a,Value b) { Value tmp=a+b; Generic(key,tmp); }
    static void Chain(Dictionary<Value,Value> map,Value[] array,Value[] normals,int index,Value value) {
        Value saved=value; Value key=array[index]; Value tmp=saved+normals[index]; map[key]=tmp;
    }
    static void OutChain(Dictionary<Value,Value> map,Value[] array,Value[] normals,int index) {
        Fill(out Value value); Value saved=value; Value key=array[index]; Value tmp=saved+normals[index]; map[key]=tmp;
    }
    static void OutArrayCallback(Dictionary<Value,Value> map,Value[] normals,int index) {
        Fill(out Value value); Value saved=value; Value key=ChangeArray(ref value)[index]; Value tmp=saved+normals[index]; map[key]=tmp;
    }
    static void OutIndexCallback(Dictionary<Value,Value> map,Value[] array,Value[] normals,int index) {
        Fill(out Value value); Value saved=value; Value key=array[ChangeIndex(ref value)]; Value tmp=saved+normals[index]; map[key]=tmp;
    }
    static void EarlierThrows(Sink sink,Value a,Value b) { Value tmp=a+b; sink.Accept(Earlier(),tmp); }
    static void EarlierChanges(Sink sink,int key,Value a,Value b) { Value tmp=a+b; sink.Accept(Change(ref key),tmp); }
    static void EffectfulReceiver(int key,Value a,Value b) { Value tmp=a+b; Receiver().Accept(key,tmp); }
    static void ReferenceEscape(Sink sink,int key,Value a,Value b) { Value tmp=a+b; sink.AcceptRef(key,ref tmp); }
    static void Reused(Sink sink,int key,Value a,Value b) { Value tmp=a+b; sink.Accept(key,tmp); sink.Accept(key,tmp); }
    static void ValueReceiver(ValueSink sink,int key,Value a,Value b) { Value tmp=a+b; sink.Accept(key,tmp); }
    static void UnknownProducer(Sink sink,int key) { Value tmp=Make(); sink.Accept(key,tmp); }
    static void Boxing(Sink sink,int key,Value a,Value b) { Value tmp=a+b; sink.AcceptObject(key,tmp); }
    static void TupleThenCall((Value, Value) pair, Sink sink) {
        Value first=pair.Item1; Value second=pair.Item2; int key=Earlier(); sink.Accept(key,first); sink.Accept(key,second);
    }
    static void TupleThenConstant((Value, Value) pair, Sink sink) {
        Value first=pair.Item1; Value second=pair.Item2; int key=42; sink.Accept(key,first); sink.Accept(key,second);
    }
    static void TupleThenField((Value, Value) pair, Sink sink, Value value) {
        Value first=pair.Item1; Value second=pair.Item2; float x=value.X; sink.Accept(0,first); sink.Accept(1,second); Console.WriteLine(x);
    }
    static void TupleIncomplete((Value, Value, Value) pair, Sink sink) {
        Value first=pair.Item1; Value second=pair.Item2; int key=Earlier(); sink.Accept(key,first); sink.Accept(key,second); sink.Accept(key,pair.Item3);
    }
    static void TupleReversed((Value, Value) pair, Sink sink) {
        Value second=pair.Item2; Value first=pair.Item1; int key=Earlier(); sink.Accept(key,first); sink.Accept(key,second);
    }
    static void TupleDuplicate((Value, Value) pair, Sink sink) {
        Value first=pair.Item1; Value second=pair.Item1; int key=Earlier(); sink.Accept(key,first); sink.Accept(key,second);
    }
    static void TupleMutation((Value, Value) pair, Sink sink) {
        Value first=pair.Item1; pair.Item2=Make(); Value second=pair.Item2; int key=Earlier(); sink.Accept(key,first); sink.Accept(key,second);
    }
    static void TupleEscape((Value, Value) pair, Sink sink) {
        Value first=pair.Item1; Value second=pair.Item2; int key=Earlier(); Console.WriteLine(pair); sink.Accept(key,first); sink.Accept(key,second);
    }
    static (Value, Value) sharedTuple;
    public class AliasSink { public Value Value; public void Accept(int key,Value value) { Value=value; } }
    static int TupleKey() => 42;
    static Value TupleOutput { set { sharedTuple.Item2=value; } }
    static void TupleAliasedSetter(ref (Value, Value) pair, AliasSink sink) {
        TupleOutput=pair.Item1; Value second=pair.Item2; int key=TupleKey(); sink.Accept(key,second);
    }
    public static float RunAliasedTupleFixture() {
        sharedTuple=(new Value {X=1},new Value {X=2}); var sink=new AliasSink(); TupleAliasedSetter(ref sharedTuple,sink); return sink.Value.X;
    }
    [Test]
    public void TupleBoundaryDoesNotAbsorbByRefPropertyAssignments()
    {
        var dec=new CSharpDecompiler(typeof(ValueArgumentInliningTests).Assembly.Location,new DecompilerSettings {ThrowOnAssemblyResolveErrors=false});
        var method=typeof(ValueArgumentInliningTests).GetMethod(nameof(TupleAliasedSetter),BindingFlags.Static|BindingFlags.NonPublic)!;
        Assert.That(method,Is.Not.Null);
        var tree=dec.Decompile(System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(method.MetadataToken));
        Assert.That(tree.Descendants.OfType<TupleExpression>(),Is.Empty,tree.ToString());
        Assert.That(tree.Descendants.OfType<ParenthesizedVariableDesignation>(),Is.Empty,tree.ToString());
    }
    static void EnumeratorBeforeInitializer(IEnumerable<(Value, Value)> input,Sink sink) {
        using var enumerator=input.GetEnumerator();
        Value saved=Make();
        while(enumerator.MoveNext()) {
            var pair=enumerator.Current; Value first=pair.Item1; Value second=pair.Item2; int key=Earlier();
            sink.Accept(key,first); sink.Accept(key,second); sink.Accept(key,saved);
        }
    }
    [Test]
    public void DeconstructionDoesNotMoveAnInitializerAcrossEnumeratorAcquisition()
    {
        var dec=new CSharpDecompiler(typeof(ValueArgumentInliningTests).Assembly.Location,new DecompilerSettings {ThrowOnAssemblyResolveErrors=false});
        var method=typeof(ValueArgumentInliningTests).GetMethod(nameof(EnumeratorBeforeInitializer),BindingFlags.Static|BindingFlags.NonPublic)!;
        Assert.That(method,Is.Not.Null);
        var tree=dec.Decompile(System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(method.MetadataToken));
        Assert.That(tree.Descendants.OfType<ParenthesizedVariableDesignation>().Count(),Is.EqualTo(1),tree.ToString());
        Assert.That(tree.Descendants.OfType<ForeachStatement>(),Is.Empty,tree.ToString());
        Assert.That(tree.Descendants.OfType<WhileStatement>().Count(),Is.EqualTo(1),tree.ToString());
        var calls=tree.Descendants.OfType<InvocationExpression>().Select(i=>i.ToString()).ToArray();
        Assert.That(System.Array.FindIndex(calls,c=>c.EndsWith("GetEnumerator ()")),Is.LessThan(System.Array.FindIndex(calls,c=>c=="Make ()")),tree.ToString());
        Assert.That(System.Array.FindIndex(calls,c=>c=="Make ()"),Is.LessThan(System.Array.FindIndex(calls,c=>c.EndsWith("MoveNext ()"))),tree.ToString());
    }
    [TestCase(nameof(TupleThenCall),true)] [TestCase(nameof(TupleThenConstant),true)] [TestCase(nameof(TupleThenField),true)]
    [TestCase(nameof(TupleIncomplete),false)] [TestCase(nameof(TupleReversed),false)] [TestCase(nameof(TupleDuplicate),false)]
    [TestCase(nameof(TupleMutation),false)] [TestCase(nameof(TupleEscape),false)]
    public void CompleteTupleReadsCanEndBeforeAnUnrelatedAssignment(string name,bool expected)
    {
        var dec=new CSharpDecompiler(typeof(ValueArgumentInliningTests).Assembly.Location,new DecompilerSettings {ThrowOnAssemblyResolveErrors=false});
        var method=typeof(ValueArgumentInliningTests).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)!;
        Assert.That(method,Is.Not.Null);
        var tree=dec.Decompile(System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(method.MetadataToken));
        Assert.That(tree.Descendants.OfType<ParenthesizedVariableDesignation>().Any(),Is.EqualTo(expected),tree.ToString());
    }
    [TestCase(nameof(OperatorResult),true)] [TestCase(nameof(ArrayResult),true)] [TestCase(nameof(ArrayKey),true)]
    [TestCase(nameof(Copy),true)] [TestCase(nameof(GenericResult),true)] [TestCase(nameof(Chain),true)]
    [TestCase(nameof(EarlierThrows),false)] [TestCase(nameof(EarlierChanges),false)] [TestCase(nameof(EffectfulReceiver),false)]
    [TestCase(nameof(ReferenceEscape),false)] [TestCase(nameof(Reused),false)] [TestCase(nameof(ValueReceiver),false)]
    [TestCase(nameof(UnknownProducer),false)] [TestCase(nameof(Boxing),false)]
    [TestCase(nameof(OutChain),true)] [TestCase(nameof(OutArrayCallback),false)] [TestCase(nameof(OutIndexCallback),false)]
    public void InlineOnlySingleUseExactValueArgumentsWithVerifiedEvaluationOrder(string name,bool expected)
    {
        var dec=new CSharpDecompiler(typeof(ValueArgumentInliningTests).Assembly.Location,new DecompilerSettings {ThrowOnAssemblyResolveErrors=false});
        var method=typeof(ValueArgumentInliningTests).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)!;
        Assert.That(method,Is.Not.Null);
        var tree=dec.Decompile(System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(method.MetadataToken));
        var locals=tree.Descendants.OfType<VariableDeclarationStatement>().Count(d=>d.Type.ToString()==nameof(Value));
        Assert.That(locals==0,Is.EqualTo(expected),tree.ToString());
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
    public void AddressedLocalCopyCrossesOnlyPlainReadOnlyArrayAccess(int invalid)
    {
        var dec=new CSharpDecompiler(typeof(ValueArgumentInliningTests).Assembly.Location,new DecompilerSettings {ThrowOnAssemblyResolveErrors=false});
        var integer=dec.TypeSystem.FindType(KnownTypeCode.Int32);
        var identity=dec.TypeSystem.FindType(KnownTypeCode.Object).GetMethods().First(m=>m.Name=="GetHashCode");
        var container=new BlockContainer();var block=new Block();container.Blocks.Add(block);
        var function=new ILFunction(identity,0,new GenericContext(),container,ILFunctionKind.TopLevelFunction);
        var source=function.RegisterVariable(VariableKind.Local,integer,"source");
        var pointer=function.RegisterVariable(VariableKind.Local,new ByReferenceType(integer),"pointer");
        var array=function.RegisterVariable(VariableKind.Parameter,new ArrayType(dec.TypeSystem,integer),"array");
        block.Instructions.Add(new StLoc(pointer,new LdLoca(source)));
        typeof(ILInstruction).GetMethod("AddRef",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(function,null);
        Assert.That(source.AddressCount,Is.EqualTo(1));
        var definition=dec.TypeSystem.MainModule.TypeDefinitions.Single(t=>t.FullName==typeof(ValueArgumentInliningTests).FullName);
        var address=new LdElema(integer,new LdLoc(array),new LdcI4(0));
        var read=new LdObj(address,integer);
        if(invalid==1)read.IsVolatile=true;
        if(invalid==2)read.UnalignedPrefix=1;
        if(invalid==3)read.Target=new LdLoc(pointer);
        if(invalid==4)address.Array=new Call(definition.GetMethods().Single(m=>m.Name==nameof(ArrayInput)));
        if(invalid==5)address.Indices[0]=new Call(definition.GetMethods().Single(m=>m.Name==nameof(IndexInput)));
        if(invalid==6)address.WithSystemIndex=true;
        ILInstruction moving=invalid==7 ? new Call(definition.GetMethods().Single(m=>m.Name==nameof(IndexInput))) : new LdLoc(source);
        var proof=typeof(ILInlining).GetMethod("IsSafeForInlineOver",BindingFlags.NonPublic|BindingFlags.Static);
        Assert.That(proof,Is.Not.Null);
        Assert.That((bool)proof!.Invoke(null,new object[]{read,moving})!,Is.EqualTo(invalid==0));
    }
}
