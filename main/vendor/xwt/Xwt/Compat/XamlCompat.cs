//
// XamlCompat.cs
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

// Minimal stand-ins for the System.Xaml (WPF) types Xwt annotates its widgets with. System.Xaml is
// not part of .NET on Linux; the attributes only matter to XAML loaders, which the IDE does not use.
// ValueSerializerAttribute itself ships with .NET (System.ObjectModel).
// (MonoDevelop local patch, see UPSTREAM.md.)

using System;
using System.ComponentModel;

namespace System.Windows.Markup
{
	[AttributeUsage (AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	internal sealed class ContentPropertyAttribute : Attribute
	{
		public ContentPropertyAttribute (string name)
		{
			Name = name;
		}

		public string Name { get; }
	}

	internal interface IValueSerializerContext : ITypeDescriptorContext
	{
	}

	internal abstract class ValueSerializer
	{
		public virtual bool CanConvertToString (object value, IValueSerializerContext context) => false;

		public virtual bool CanConvertFromString (string value, IValueSerializerContext context) => false;

		public virtual string ConvertToString (object value, IValueSerializerContext context) =>
			throw new NotSupportedException ();

		public virtual object ConvertFromString (string value, IValueSerializerContext context) =>
			throw new NotSupportedException ();
	}
}
