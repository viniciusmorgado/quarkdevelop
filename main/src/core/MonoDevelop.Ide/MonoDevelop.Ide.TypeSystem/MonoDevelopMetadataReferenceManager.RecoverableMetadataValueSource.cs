//
// MetadataReferenceCache.RecoverableMetadataValueSource.cs
//
// Author:
//       Marius Ungureanu <maungu@microsoft.com>
//
// Copyright (c) 2018 Microsoft Inc.
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
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;

namespace MonoDevelop.Ide.TypeSystem
{
	partial class MonoDevelopMetadataReferenceManager
	{
		// Roslyn 5.9 removed ValueSource<T>; this is a plain weakly-held value recoverable from temporary storage.
		class RecoverableMetadataValueSource
		{
			readonly WeakReference<AssemblyMetadata> _weakValue;
			readonly List<ITemporaryStorageStreamHandle> _storages;

			public RecoverableMetadataValueSource (AssemblyMetadata value, List<ITemporaryStorageStreamHandle> storages)
			{
				Contract.ThrowIfFalse (storages.Count > 0);

				_weakValue = new WeakReference<AssemblyMetadata> (value);
				_storages = storages;
			}

			public IReadOnlyList<ITemporaryStorageStreamHandle> GetStorages ()
			{
				return _storages;
			}

			public bool HasValue => _weakValue.TryGetTarget (out _);

			public AssemblyMetadata GetValue ()
			{
				if (_weakValue.TryGetTarget (out var value)) {
					return value;
				}

				return RecoverMetadata ();
			}

			AssemblyMetadata RecoverMetadata ()
			{
				var moduleBuilder = ImmutableArray.CreateBuilder<ModuleMetadata> (_storages.Count);

				foreach (var storage in _storages) {
					moduleBuilder.Add (CreateModuleMetadata (storage));
				}

				var metadata = AssemblyMetadata.Create (moduleBuilder.ToImmutable ());
				_weakValue.SetTarget (metadata);

				return metadata;
			}

			public bool TryGetValue (out AssemblyMetadata value)
			{
				return _weakValue.TryGetTarget (out value);
			}
		}
	}
}
