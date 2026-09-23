//
// ResXStronglyTypedBuilderTests.cs
//
// Copyright (c) 2026 MonoDevelop contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CSharp;
using MonoDevelop.Ide.CustomTools;
using NUnit.Framework;

namespace MonoDevelop.Ide.Gtk3.Tests
{
	/// <summary>
	/// ResXStronglyTypedBuilder replaces ResXResourceReader and StronglyTypedResourceBuilder (not available
	/// on .NET 10 for Linux) in the ResX custom tools.
	/// </summary>
	[TestFixture]
	public class ResXStronglyTypedBuilderTests
	{
		const string ResX = @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
  <resheader name=""resmimetype""><value>text/microsoft-resx</value></resheader>
  <data name=""Greeting"" xml:space=""preserve""><value>Hello &lt;world&gt;</value></data>
  <data name=""Two words"" xml:space=""preserve""><value>two</value></data>
  <data name=""Data"" type=""System.Byte[], mscorlib""><value>AQID</value></data>
  <data name=""Icon"" type=""System.Resources.ResXFileRef, System.Windows.Forms""><value>icon.png;System.Drawing.Bitmap, System.Drawing</value></data>
  <data name=""Text"" type=""System.Resources.ResXFileRef, System.Windows.Forms""><value>text.txt;System.String, mscorlib;utf-8</value></data>
  <data name="">>$this.Name"" xml:space=""preserve""><value>Form1</value></data>
  <metadata name=""Meta""><value>ignored</value></metadata>
</root>";

		static readonly string [] ReadNames = { "Greeting", "Two words", "Data", "Icon", "Text" };
		static readonly string [] UnmatchableNames = { "ResourceManager", "a b", "a_b", "1abc" };
		static readonly string [] PropertyNames = { "ResourceManager", "Culture", "_class", "Fine" };

		string dir;

		[SetUp]
		public void SetUp ()
		{
			dir = Path.Combine (Path.GetTempPath (), "resx-builder-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (dir);
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (dir, true);
		}

		string WriteResX ()
		{
			var path = Path.Combine (dir, "Strings.resx");
			File.WriteAllText (path, ResX);
			return path;
		}

		[Test]
		public void ReadReturnsResourceTypesAndStringValues ()
		{
			var entries = ResXStronglyTypedBuilder.Read (WriteResX ()).ToDictionary (e => e.Name);

			Assert.That (entries.Keys, Is.EquivalentTo (ReadNames));
			Assert.AreEqual ("Hello <world>", entries ["Greeting"].Value);
			Assert.IsTrue (entries ["Greeting"].IsString);
			Assert.AreEqual ("System.Byte[]", entries ["Data"].TypeName);
			Assert.AreEqual ("System.Drawing.Bitmap", entries ["Icon"].TypeName);
			Assert.IsTrue (entries ["Text"].IsString);
		}

		[TestCase ("System.Byte[], mscorlib, Version=4.0.0.0", "System.Byte[]")]
		[TestCase ("System.Collections.Generic.List`1[[System.String, mscorlib]], mscorlib", "System.Collections.Generic.List`1[[System.String, mscorlib]]")]
		[TestCase ("System.Int32", "System.Int32")]
		public void StripAssemblyNameKeepsGenericArguments (string typeName, string expected)
		{
			Assert.AreEqual (expected, ResXStronglyTypedBuilder.StripAssemblyName (typeName));
		}

		[Test]
		public void InvalidAndCollidingNamesAreUnmatchable ()
		{
			var entries = new [] {
				new ResXEntry ("ResourceManager", "System.String", "x"),
				new ResXEntry ("a b", "System.String", "x"),
				new ResXEntry ("a_b", "System.String", "x"),
				new ResXEntry ("1abc", "System.String", "x"),
				new ResXEntry ("class", "System.String", "x"),
				new ResXEntry ("Fine", "System.String", "x"),
			};
			var ccu = ResXStronglyTypedBuilder.Create (entries, "Strings", "Test", "Test", new CSharpCodeProvider (), true, out var unmatchable);

			Assert.That (unmatchable, Is.EquivalentTo (UnmatchableNames));
			var properties = ccu.Namespaces [0].Types [0].Members.OfType<CodeMemberProperty> ().Select (p => p.Name);
			Assert.That (properties, Is.EquivalentTo (PropertyNames));
		}

		/// <summary>
		/// ResXFileCodeGenerator patches the resource ID and the PCL GetTypeInfo call into the
		/// "new ResourceManager (id, typeof (T).Assembly)" expression of the ResourceManager property.
		/// </summary>
		[Test]
		public void ResourceManagerPropertyHasTheShapeTheGeneratorPatches ()
		{
			var ccu = ResXStronglyTypedBuilder.Create (Array.Empty<ResXEntry> (), "Strings", "Gen", "Res", new CSharpCodeProvider (), true, out _);

			var init = (CodeObjectCreateExpression)ccu.Namespaces [0].Types [0]
				.Members.OfType<CodeMemberProperty> ().Single (t => t.Name == "ResourceManager")
				.GetStatements.OfType<CodeConditionStatement> ().Single ()
				.TrueStatements.OfType<CodeVariableDeclarationStatement> ().Single ()
				.InitExpression;
			Assert.AreEqual ("Res.Strings", ((CodePrimitiveExpression)init.Parameters [0]).Value);
			Assert.IsInstanceOf<CodePropertyReferenceExpression> (init.Parameters [1]);
		}

		[TestCase (true)]
		[TestCase (false)]
		public void GeneratedClassCompilesAndReadsTheEmbeddedResources (bool internalClass)
		{
			var provider = new CSharpCodeProvider ();
			var entries = ResXStronglyTypedBuilder.Read (WriteResX ()).Where (e => e.Name != "Icon");
			var ccu = ResXStronglyTypedBuilder.Create (entries, "Strings", "Gen", "Res", provider, internalClass, out var unmatchable);
			Assert.IsEmpty (unmatchable);

			var code = new StringWriter ();
			provider.GenerateCodeFromCompileUnit (ccu, code, new CodeGeneratorOptions ());
			StringAssert.Contains (internalClass ? "internal class Strings" : "public class Strings", code.ToString ());
			StringAssert.Contains ("Hello &lt;world&gt;", code.ToString ());

			var resources = new MemoryStream ();
			using (var writer = new ResourceWriter (resources)) {
				writer.AddResource ("Greeting", "Hello <world>");
				writer.AddResource ("Two words", "two");
				writer.AddResource ("Data", new byte [] { 1, 2, 3 });
				writer.AddResource ("Text", "text");
				writer.Generate ();
				resources = new MemoryStream (resources.ToArray ());
			}

			var references = ((string)AppContext.GetData ("TRUSTED_PLATFORM_ASSEMBLIES")).Split (Path.PathSeparator)
				.Select (p => MetadataReference.CreateFromFile (p));
			var compilation = CSharpCompilation.Create ("Generated", new [] { CSharpSyntaxTree.ParseText (code.ToString ()) }, references,
				new CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary));
			var assemblyStream = new MemoryStream ();
			var emit = compilation.Emit (assemblyStream, manifestResources: new [] {
				new ResourceDescription ("Res.Strings.resources", () => resources, true)
			});
			Assert.IsTrue (emit.Success, string.Join ("\n", emit.Diagnostics));

			var type = Assembly.Load (assemblyStream.ToArray ()).GetType ("Gen.Strings");
			object Get (string name) => type.GetProperty (name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue (null);
			Assert.AreEqual ("Hello <world>", Get ("Greeting"));
			Assert.AreEqual ("two", Get ("Two_words"));
			Assert.AreEqual (new byte [] { 1, 2, 3 }, Get ("Data"));
			Assert.AreEqual ("text", Get ("Text"));
		}
	}
}
