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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.TypeSystem;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using NUnit.Framework;

namespace ICSharpCode.Decompiler.Tests.Output;

[TestFixture]
public class GeneratedIdentifierOutputTests
{
	static string Print(AstNode node, bool navigation, bool locations = false)
	{
		using var text = new StringWriter();
		TokenWriter writer = navigation ? new TextTokenWriter(new PlainTextOutput(text), new DecompilerSettings()) : new TextWriterTokenWriter(text);
		if (locations)
			writer = TokenWriter.WrapInWriterThatSetsLocationsInAST(writer);
		node.AcceptVisitor(new CSharpOutputVisitor(writer, FormattingOptionsFactory.CreateAllman()));
		return text.ToString();
	}

	static Assembly Compile(string text)
	{
		var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
		var compilation = CSharpCompilation.Create("Identifiers_" + Guid.NewGuid().ToString("N"), new[] { CSharpSyntaxTree.ParseText(text) }, references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));
		using var stream = new MemoryStream();
		var result = compilation.Emit(stream);
		Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)) + Environment.NewLine + text);
		return Assembly.Load(stream.ToArray());
	}

	[TestCase("<State>d__1")]
	[TestCase("<>1__state")]
	[TestCase("9name")]
	[TestCase("has space")]
	[TestCase("a\\b")]
	[TestCase("a|b")]
	[TestCase("\u0301start")]
	[TestCase("a\0b")]
	[TestCase("a\u200Db")]
	[TestCase("a\uD800b")]
	[TestCase("a\uD83D\uDE00b")]
	public void DefaultWritersEmitLegalIdentifiers(string name)
	{
		var tree = new TypeDeclaration { Name = name, Modifiers = Modifiers.Public };
		string plain = Print(tree, false);
		Assert.That(CSharpSyntaxTree.ParseText(plain).GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error), Is.Empty, plain);
		Assert.That(Print(tree, true), Is.EqualTo(plain));
		Assert.That(tree.Name, Is.EqualTo(name), "Printing must not rename the AST.");
		Compile(plain);
	}

	[TestCase("Ordinary")]
	[TestCase("class")]
	[TestCase("a\u0301")]
	[TestCase("\u2160name")]
	[TestCase("a\u203Fb")]
	public void ValidIdentifiersKeepTheirIdentity(string name)
	{
		var tree = new TypeDeclaration { Name = name, Modifiers = Modifiers.Public };
		string text = Print(tree, false);
		var type = Compile(text).GetTypes().Single();
		Assert.That(type.Name, Is.EqualTo(name));
		Assert.That(Print(tree, true), Is.EqualTo(text));
	}

	[Test]
	public void EncodedNamesDoNotCaptureExistingNamesOrOtherEncodings()
	{
		var tree = new ICSharpCode.Decompiler.CSharp.Syntax.SyntaxTree();
		foreach (string name in new[] { "<A>", "_003CA_003E", "_003CA>", "_003CA_003E_1", "9A", "_9A" })
			tree.Members.Add(new TypeDeclaration { Name = name, Modifiers = Modifiers.Public });
		string first = Print(tree, false);
		Assert.That(Compile(first).GetTypes().Length, Is.EqualTo(6));
		Assert.That(Print(tree, true), Is.EqualTo(first));
		Assert.That(Print(tree, false), Is.EqualTo(first));
		var names = CSharpSyntaxTree.ParseText(first).GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Select(n => n.Identifier.ValueText).ToArray();
		foreach (var member in tree.Members.ToArray())
		{
			member.Remove();
			tree.Members.InsertAfter(null, member);
		}
		var reversed = CSharpSyntaxTree.ParseText(Print(tree, false)).GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Select(n => n.Identifier.ValueText).ToArray();
		Assert.That(reversed, Is.EquivalentTo(names));
	}

	[Test]
	public void PrintedLocationsIncludeTheEncodedToken()
	{
		var tree = new TypeDeclaration { Name = "<State>d__1", Modifiers = Modifiers.Public };
		var annotation = new object();
		tree.NameToken.AddAnnotation(annotation);
		string text = Print(tree, false, true);
		var token = CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Single().Identifier;
		Assert.That(tree.NameToken.EndLocation.Column - tree.NameToken.StartLocation.Column, Is.EqualTo(token.Text.Length));
		Assert.That(tree.NameToken.Annotation<object>(), Is.SameAs(annotation));
		Assert.That(tree.Name, Is.EqualTo("<State>d__1"));
	}

	static string Fixture()
	{
		var assembly = new PersistedAssemblyBuilder(new AssemblyName("Generated_" + Guid.NewGuid().ToString("N")), typeof(object).Assembly);
		var module = assembly.DefineDynamicModule("Main");
		var outer = module.DefineType("Container", TypeAttributes.Public);
		var state = outer.DefineNestedType("<Step>d__0", TypeAttributes.NestedPrivate | TypeAttributes.Sealed);
		state.SetCustomAttribute(new CustomAttributeBuilder(typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes), Array.Empty<object>()));
		var field = state.DefineField("<>1__state", typeof(int), FieldAttributes.Public);
		var collision = state.DefineField("_003C_003E1__state", typeof(int), FieldAttributes.Public);
		var ctor = state.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
		var il = ctor.GetILGenerator();
		il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
		il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4, 41); il.Emit(OpCodes.Stfld, field);
		il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4, 7); il.Emit(OpCodes.Stfld, collision); il.Emit(OpCodes.Ret);
		var other = outer.DefineNestedType("_003CStep_003Ed__0", TypeAttributes.NestedPrivate | TypeAttributes.Sealed);
		other.DefineDefaultConstructor(MethodAttributes.Public);
		var method = outer.DefineMethod("Create", MethodAttributes.Public | MethodAttributes.Static, typeof(object), Type.EmptyTypes);
		method.SetCustomAttribute(new CustomAttributeBuilder(typeof(IteratorStateMachineAttribute).GetConstructor(new[] { typeof(Type) }), new object[] { state }));
		il = method.GetILGenerator(); il.Emit(OpCodes.Newobj, ctor); il.Emit(OpCodes.Ret);
		var read = outer.DefineMethod("Read", MethodAttributes.Public | MethodAttributes.Static, typeof(int), Type.EmptyTypes);
		il = read.GetILGenerator();
		il.Emit(OpCodes.Newobj, ctor); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ldc_I4, 10); il.Emit(OpCodes.Mul);
		il.Emit(OpCodes.Newobj, ctor); il.Emit(OpCodes.Ldfld, collision); il.Emit(OpCodes.Add); il.Emit(OpCodes.Ret);
		state.CreateType(); other.CreateType(); outer.CreateType();
		string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, assembly.GetName().Name + ".dll");
		assembly.Save(path);
		return path;
	}

	[TestCase(false)]
	[TestCase(true)]
	public void IteratorAttributeConstructorsAndFieldReferencesKeepTheirBindings(bool navigation)
	{
		string path = Fixture();
		var original = Assembly.LoadFile(path).GetType("Container");
		var decompiler = new CSharpDecompiler(path, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
		var tree = decompiler.DecompileType(new FullTypeName("Container"));
		string text = Print(tree, navigation);
		var restored = Compile(text).GetType("Container");
		Assert.That(restored.GetMethod("Read").Invoke(null, null), Is.EqualTo(original.GetMethod("Read").Invoke(null, null)));
		Assert.That(restored.GetMethod("Read").Invoke(null, null), Is.EqualTo(417));
		object value = restored.GetMethod("Create").Invoke(null, null);
		var attribute = restored.GetMethod("Create").GetCustomAttribute<IteratorStateMachineAttribute>();
		Assert.That(attribute, Is.Not.Null);
		Assert.That(attribute.StateMachineType, Is.EqualTo(value.GetType()));
		Assert.That(attribute.StateMachineType.GetCustomAttribute<CompilerGeneratedAttribute>(), Is.Not.Null);
		Assert.That(restored.GetNestedTypes(BindingFlags.NonPublic).Length, Is.EqualTo(2));
		Assert.That(tree.Descendants.OfType<Identifier>().Any(i => i.Name == "<Step>d__0"), Is.True);
		Assert.That(Print(tree, navigation), Is.EqualTo(text));
	}
	[TestCase(false, "field")]
	[TestCase(true, "field")]
	[TestCase(false, "attribute")]
	[TestCase(true, "attribute")]
	public void CompiledWrongBindingIsRejected(bool navigation, string variant)
	{
		var decompiler = new CSharpDecompiler(Fixture(), new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
		string text = Print(decompiler.DecompileType(new FullTypeName("Container")), navigation);
		var restored = Compile(text).GetType("Container");
		var state = restored.GetMethod("Create").GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
		string field = state.GetFields().Single(f => (int)f.GetValue(Activator.CreateInstance(state)) == 41).Name;
		string otherField = state.GetFields().Single(f => f.Name != field).Name;
		string otherType = restored.GetNestedTypes(BindingFlags.NonPublic).Single(t => t != state).Name;
		string wrong = variant == "field" ? text.Replace("." + field, "." + otherField)
			: text.Replace("typeof(" + state.Name + ")", "typeof(" + otherType + ")");
		Assert.That(wrong, Is.Not.EqualTo(text));
		var broken = Compile(wrong).GetType("Container");
		Assert.Throws<AssertionException>(() => {
			Assert.That(broken.GetMethod("Read").Invoke(null, null), Is.EqualTo(417));
			Assert.That(broken.GetMethod("Create").GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType,
				Is.EqualTo(broken.GetMethod("Create").Invoke(null, null).GetType()));
		});
	}

}
