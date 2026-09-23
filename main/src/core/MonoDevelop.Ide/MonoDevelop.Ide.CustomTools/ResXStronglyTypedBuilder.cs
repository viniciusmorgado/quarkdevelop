//
// ResXStronglyTypedBuilder.cs
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Xml;

namespace MonoDevelop.Ide.CustomTools
{
	/// <summary>
	/// A resource of a .resx file: its name, the full name of its type (without assembly) and, for strings,
	/// its value.
	/// </summary>
	sealed class ResXEntry
	{
		public ResXEntry (string name, string typeName, string value)
		{
			Name = name;
			TypeName = typeName;
			Value = value;
		}

		public string Name { get; }
		public string TypeName { get; }
		public string Value { get; }
		public bool IsString => TypeName == "System.String";
	}

	/// <summary>
	/// Replaces System.Windows.Forms' ResXResourceReader and System.Design's StronglyTypedResourceBuilder,
	/// which do not exist on .NET 10 for Linux. The .resx file is read as XML (resource values are not
	/// deserialized: the generated class only needs their types), and the generated CodeDOM has the shape
	/// of StronglyTypedResourceBuilder's output, which ResXFileCodeGenerator patches.
	/// </summary>
	static class ResXStronglyTypedBuilder
	{
		const int DocCommentMaxValueLength = 512;
		const string ResourceManagerField = "resourceMan";
		const string CultureField = "resourceCulture";
		const string ResourceManagerProperty = "ResourceManager";
		const string CultureProperty = "Culture";
		const string GeneratorName = "MonoDevelop.Ide.CustomTools.ResXFileCodeGenerator";

		static readonly char [] invalidIdentifierChars = {
			' ', '\u00A0', '.', ',', ';', '|', '~', '@', '#', '%', '^', '&', '*', '+', '-', '/', '\\', '<', '>',
			'?', '[', ']', '(', ')', '{', '}', '"', '\'', ':', '!', '='
		};

		public static List<ResXEntry> Read (string path)
		{
			var doc = new XmlDocument { XmlResolver = null };
			using (var reader = XmlReader.Create (path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
				doc.Load (reader);

			var entries = new List<ResXEntry> ();
			foreach (XmlElement data in doc.DocumentElement.SelectNodes ("data")) {
				var name = data.GetAttribute ("name");
				// ">>name.Type" style entries are designer metadata, not resources
				if (string.IsNullOrEmpty (name) || name.StartsWith (">>", StringComparison.Ordinal))
					continue;
				var type = data.GetAttribute ("type");
				var value = data.SelectSingleNode ("value")?.InnerText ?? string.Empty;
				if (type.Length == 0) {
					entries.Add (new ResXEntry (name, data.HasAttribute ("mimetype") ? "System.Object" : "System.String", value));
					continue;
				}
				var typeName = StripAssemblyName (type);
				if (typeName == "System.Resources.ResXFileRef") {
					// value: "path;type[;encoding]"
					var parts = value.Split (';');
					typeName = parts.Length > 1 ? StripAssemblyName (parts [1].Trim ()) : "System.Object";
					value = null;
				} else if (typeName == "System.Resources.ResXNullRef") {
					typeName = "System.Object";
				}
				entries.Add (new ResXEntry (name, typeName, typeName == "System.String" ? value : null));
			}
			return entries;
		}

		/// <summary>
		/// "System.Byte[], mscorlib, Version=..." → "System.Byte[]"; commas inside generic arguments are kept.
		/// </summary>
		internal static string StripAssemblyName (string typeName)
		{
			int depth = 0;
			for (int i = 0; i < typeName.Length; i++) {
				switch (typeName [i]) {
				case '[': depth++; break;
				case ']': depth--; break;
				case ',':
					if (depth == 0)
						return typeName.Substring (0, i).Trim ();
					break;
				}
			}
			return typeName.Trim ();
		}

		/// <summary>
		/// Returns a valid identifier for a resource name, or null.
		/// </summary>
		internal static string VerifyResourceName (string name, CodeDomProvider provider)
		{
			var chars = name.ToCharArray ();
			for (int i = 0; i < chars.Length; i++) {
				if (Array.IndexOf (invalidIdentifierChars, chars [i]) >= 0)
					chars [i] = '_';
			}
			var identifier = provider.CreateValidIdentifier (new string (chars));
			return provider.IsValidIdentifier (identifier) ? identifier : null;
		}

		public static CodeCompileUnit Create (IEnumerable<ResXEntry> resources, string baseName, string generatedCodeNamespace,
			string resourcesNamespace, CodeDomProvider provider, bool internalClass, out string [] unmatchable)
		{
			var className = VerifyResourceName (baseName, provider);
			if (className == null)
				throw new ArgumentException ($"'{baseName}' is not a valid class name", nameof (baseName));

			var comparer = (provider.LanguageOptions & LanguageOptions.CaseInsensitive) != 0
				? StringComparer.OrdinalIgnoreCase
				: StringComparer.Ordinal;
			var reserved = new HashSet<string> (comparer) { ResourceManagerProperty, CultureProperty, ResourceManagerField, CultureField, className };

			var invalid = new List<string> ();
			var byIdentifier = new Dictionary<string, List<ResXEntry>> (comparer);
			foreach (var resource in resources) {
				var identifier = VerifyResourceName (resource.Name, provider);
				if (identifier == null || reserved.Contains (identifier)) {
					invalid.Add (resource.Name);
					continue;
				}
				if (!byIdentifier.TryGetValue (identifier, out var list))
					byIdentifier [identifier] = list = new List<ResXEntry> ();
				list.Add (resource);
			}

			var ccu = new CodeCompileUnit ();
			ccu.ReferencedAssemblies.Add ("System.dll");
			var ns = new CodeNamespace (generatedCodeNamespace);
			ns.Imports.Add (new CodeNamespaceImport ("System"));
			ccu.Namespaces.Add (ns);

			var type = new CodeTypeDeclaration (className) {
				IsClass = true,
				TypeAttributes = internalClass ? System.Reflection.TypeAttributes.NotPublic : System.Reflection.TypeAttributes.Public
			};
			AddSummary (type.Comments, "A strongly-typed resource class, for looking up localized strings, etc.");
			type.CustomAttributes.Add (new CodeAttributeDeclaration (new CodeTypeReference (typeof (GeneratedCodeAttribute)),
				new CodeAttributeArgument (new CodePrimitiveExpression (GeneratorName)),
				new CodeAttributeArgument (new CodePrimitiveExpression (typeof (ResXStronglyTypedBuilder).Assembly.GetName ().Version.ToString ()))));
			type.CustomAttributes.Add (new CodeAttributeDeclaration (new CodeTypeReference (typeof (DebuggerNonUserCodeAttribute))));
			type.CustomAttributes.Add (new CodeAttributeDeclaration (new CodeTypeReference (typeof (CompilerGeneratedAttribute))));
			ns.Types.Add (type);

			var memberAttributes = (internalClass ? MemberAttributes.Assembly : MemberAttributes.Public) | MemberAttributes.Static;
			var editorBrowsable = new CodeAttributeDeclaration (new CodeTypeReference (typeof (EditorBrowsableAttribute)),
				new CodeAttributeArgument (new CodeFieldReferenceExpression (new CodeTypeReferenceExpression (typeof (EditorBrowsableState)), nameof (EditorBrowsableState.Advanced))));

			type.Members.Add (new CodeMemberField (typeof (ResourceManager), ResourceManagerField) { Attributes = MemberAttributes.Private | MemberAttributes.Static });
			type.Members.Add (new CodeMemberField (typeof (CultureInfo), CultureField) { Attributes = MemberAttributes.Private | MemberAttributes.Static });

			// internal, also for public classes, as StronglyTypedResourceBuilder does
			var ctor = new CodeConstructor { Attributes = MemberAttributes.Assembly };
			type.Members.Add (ctor);

			// ResourceManager: if (object.ReferenceEquals (resourceMan, null)) { var temp = new ResourceManager ("name", typeof (T).Assembly); resourceMan = temp; }
			var managerName = string.IsNullOrEmpty (resourcesNamespace) ? baseName : resourcesNamespace + "." + baseName;
			var managerField = new CodeFieldReferenceExpression (null, ResourceManagerField);
			var manager = new CodeMemberProperty {
				Name = ResourceManagerProperty,
				Type = new CodeTypeReference (typeof (ResourceManager)),
				Attributes = memberAttributes,
				HasGet = true
			};
			AddSummary (manager.Comments, "Returns the cached ResourceManager instance used by this class.");
			manager.CustomAttributes.Add (editorBrowsable);
			manager.GetStatements.Add (new CodeConditionStatement (
				new CodeMethodInvokeExpression (new CodeTypeReferenceExpression (typeof (object)), nameof (ReferenceEquals), managerField, new CodePrimitiveExpression (null)),
				new CodeVariableDeclarationStatement (typeof (ResourceManager), "temp", new CodeObjectCreateExpression (typeof (ResourceManager),
					new CodePrimitiveExpression (managerName),
					new CodePropertyReferenceExpression (new CodeTypeOfExpression (new CodeTypeReference (className)), "Assembly"))),
				new CodeAssignStatement (managerField, new CodeVariableReferenceExpression ("temp"))));
			manager.GetStatements.Add (new CodeMethodReturnStatement (managerField));
			type.Members.Add (manager);

			var cultureField = new CodeFieldReferenceExpression (null, CultureField);
			var culture = new CodeMemberProperty {
				Name = CultureProperty,
				Type = new CodeTypeReference (typeof (CultureInfo)),
				Attributes = memberAttributes,
				HasGet = true,
				HasSet = true
			};
			AddSummary (culture.Comments, "Overrides the current thread's CurrentUICulture property for all resource lookups using this strongly typed resource class.");
			culture.CustomAttributes.Add (editorBrowsable);
			culture.GetStatements.Add (new CodeMethodReturnStatement (cultureField));
			culture.SetStatements.Add (new CodeAssignStatement (cultureField, new CodePropertySetValueReferenceExpression ()));
			type.Members.Add (culture);

			var managerProperty = new CodePropertyReferenceExpression (null, ResourceManagerProperty);
			foreach (var pair in byIdentifier.OrderBy (p => p.Key, StringComparer.Ordinal)) {
				if (pair.Value.Count > 1) {
					invalid.AddRange (pair.Value.Select (r => r.Name));
					continue;
				}
				type.Members.Add (CreateProperty (pair.Key, pair.Value [0], managerProperty, cultureField, memberAttributes));
			}

			unmatchable = invalid.ToArray ();
			return ccu;
		}

		static CodeMemberProperty CreateProperty (string identifier, ResXEntry resource, CodeExpression manager, CodeExpression culture, MemberAttributes attributes)
		{
			var property = new CodeMemberProperty { Name = identifier, Attributes = attributes, HasGet = true };
			var name = new CodePrimitiveExpression (resource.Name);

			if (resource.IsString) {
				property.Type = new CodeTypeReference (typeof (string));
				var value = resource.Value ?? string.Empty;
				if (value.Length > DocCommentMaxValueLength)
					value = string.Concat (value.AsSpan (0, DocCommentMaxValueLength), " [rest of string was truncated]");
				AddSummary (property.Comments, "Looks up a localized string similar to " + System.Security.SecurityElement.Escape (value) + ".");
				property.GetStatements.Add (new CodeMethodReturnStatement (new CodeMethodInvokeExpression (manager, nameof (ResourceManager.GetString), name, culture)));
				return property;
			}

			if (resource.TypeName == "System.IO.MemoryStream" || resource.TypeName == "System.IO.UnmanagedMemoryStream") {
				property.Type = new CodeTypeReference (typeof (UnmanagedMemoryStream));
				AddSummary (property.Comments, "Looks up a localized resource of type System.IO.UnmanagedMemoryStream similar to System.IO.MemoryStream.");
				property.GetStatements.Add (new CodeMethodReturnStatement (new CodeMethodInvokeExpression (manager, nameof (ResourceManager.GetStream), name, culture)));
				return property;
			}

			property.Type = new CodeTypeReference (resource.TypeName, CodeTypeReferenceOptions.GlobalReference);
			AddSummary (property.Comments, "Looks up a localized resource of type " + resource.TypeName + ".");
			property.GetStatements.Add (new CodeVariableDeclarationStatement (typeof (object), "obj",
				new CodeMethodInvokeExpression (manager, nameof (ResourceManager.GetObject), name, culture)));
			property.GetStatements.Add (new CodeMethodReturnStatement (new CodeCastExpression (property.Type, new CodeVariableReferenceExpression ("obj"))));
			return property;
		}

		static void AddSummary (CodeCommentStatementCollection comments, string text)
		{
			comments.Add (new CodeCommentStatement ("<summary>", true));
			comments.Add (new CodeCommentStatement ("  " + text, true));
			comments.Add (new CodeCommentStatement ("</summary>", true));
		}
	}
}
