//
// MetadataReferenceCache.cs
//
// Author:
//       David Karlaš <david.karlas@xamarin.com>
//
// Copyright (c) 2015 Xamarin, Inc (http://www.xamarin.com)
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
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using MonoDevelop.Core;
using System.Threading;
using System.Reflection;
using System.Globalization;
using MonoDevelop.Ide.TypeSystem.MetadataReferences;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using MonoDevelop.Core.Assemblies;
using Microsoft.CodeAnalysis.Host;
using System.Reflection.PortableExecutable;
using System.Diagnostics;

namespace MonoDevelop.Ide.TypeSystem
{
	// Roslyn 5.9: ITemporaryStorageService/ITemporaryStreamStorage were replaced by ITemporaryStorageServiceInternal
	// and stream handles; the metadata is copied to memory-mapped temporary storage and read back through an
	// UnmanagedMemoryStream owned by the module metadata (as VisualStudioMetadataReferenceManager does).
	partial class MonoDevelopMetadataReferenceManager : IWorkspaceService
	{
		readonly MetadataCache _metadataCache;
		readonly MetadataReferenceCache _metadataReferenceCache;
		readonly ITemporaryStorageServiceInternal _temporaryStorageService;

		internal MonoDevelopMetadataReferenceManager (ITemporaryStorageServiceInternal temporaryStorageService)
		{
			_metadataCache = new MetadataCache ();
			_metadataReferenceCache = new MetadataReferenceCache ();

			_temporaryStorageService = temporaryStorageService;
			Debug.Assert (temporaryStorageService != null);
		}

		/// <exception cref="IOException"/>
		/// <exception cref="BadImageFormatException" />
		internal Metadata GetMetadata (string fullPath, DateTime snapshotTimestamp)
		{
			var key = new FileKey (fullPath, snapshotTimestamp);
			// check existing metadata
			if (_metadataCache.TryGetMetadata (key, out var metadata)) {
				return metadata;
			}

			// use temporary storage
			var storages = new List<ITemporaryStorageStreamHandle> ();
			var newMetadata = CreateAssemblyMetadataFromTemporaryStorage (key, storages);

			// don't dispose assembly metadata since it shares module metadata
			if (!_metadataCache.TryGetOrAddMetadata (key, new RecoverableMetadataValueSource (newMetadata, storages), out metadata)) {
				newMetadata.Dispose ();
			}

			return metadata;
		}

		internal IReadOnlyList<ITemporaryStorageStreamHandle> GetStorages (string fullPath, DateTime snapshotTimestamp)
		{
			var key = new FileKey (fullPath, snapshotTimestamp);
			// check existing metadata
			if (_metadataCache.TryGetSource (key, out var source)) {
				return source.GetStorages ();
			}

			return null;
		}

		/// <exception cref="IOException"/>
		/// <exception cref="BadImageFormatException" />
		AssemblyMetadata CreateAssemblyMetadataFromTemporaryStorage (FileKey fileKey, List<ITemporaryStorageStreamHandle> storages)
		{
			var moduleMetadata = CreateModuleMetadataFromTemporaryStorage (fileKey, storages);
			return CreateAssemblyMetadata (fileKey, moduleMetadata, storages, CreateModuleMetadataFromTemporaryStorage);
		}

		ModuleMetadata CreateModuleMetadataFromTemporaryStorage (FileKey moduleFileKey, List<ITemporaryStorageStreamHandle> storages)
		{
			var handle = WriteMetadataToTemporaryStorage (moduleFileKey);
			var metadata = CreateModuleMetadata (handle);

			// hold onto storage if requested
			if (storages != null) {
				storages.Add (handle);
			}

			return metadata;
		}

		ITemporaryStorageStreamHandle WriteMetadataToTemporaryStorage (FileKey moduleFileKey)
		{
			using (var copyStream = new MemoryStream ()) {
				// open a file and let it go as soon as possible
				using (var fileStream = new FileStream (moduleFileKey.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
					var headers = new PEHeaders (fileStream);

					var offset = headers.MetadataStartOffset;
					var size = headers.MetadataSize;

					// given metadata contains no metadata info.
					// throw bad image format exception so that we can show right diagnostic to user.
					if (size <= 0) {
						throw new BadImageFormatException ();
					}

					StreamCopy (fileStream, copyStream, offset, size);
				}

				// copy over the data to temp storage and let pooled stream go
				copyStream.Position = 0;
				return _temporaryStorageService.WriteToTemporaryStorage (copyStream, CancellationToken.None);
			}
		}

		/// <summary>
		/// Creates module metadata that owns the stream read back from the temporary storage.
		/// </summary>
		internal static unsafe ModuleMetadata CreateModuleMetadata (ITemporaryStorageStreamHandle handle)
		{
			var stream = handle.ReadFromTemporaryStorage ();
			if (stream is UnmanagedMemoryStream unmanagedStream) {
				// The stream is kept alive as long as the metadata through the onDispose callback.
				return ModuleMetadata.CreateFromMetadata ((IntPtr)unmanagedStream.PositionPointer, (int)unmanagedStream.Length, unmanagedStream.Dispose);
			}

			// Not a memory-mapped stream: copy the metadata into native memory owned by the metadata.
			using (stream) {
				var length = (int)stream.Length;
				var pointer = Marshal.AllocHGlobal (length);
				try {
					using (var target = new UnmanagedMemoryStream ((byte*)pointer, length, length, FileAccess.Write))
						stream.CopyTo (target);
				} catch {
					Marshal.FreeHGlobal (pointer);
					throw;
				}
				return ModuleMetadata.CreateFromMetadata (pointer, length, () => Marshal.FreeHGlobal (pointer));
			}
		}

		void StreamCopy (Stream source, Stream destination, int start, int length)
		{
			source.Position = start;

			var buffer = new byte [Math.Min (length, 81920)];

			var read = 0;
			var left = length;
			while (left > 0 && (read = source.Read (buffer, 0, Math.Min (left, buffer.Length))) != 0) {
				destination.Write (buffer, 0, read);
				left -= read;
			}
		}

		/// <exception cref="IOException"/>
		/// <exception cref="BadImageFormatException" />
		AssemblyMetadata CreateAssemblyMetadata (
		   FileKey fileKey, ModuleMetadata manifestModule, List<ITemporaryStorageStreamHandle> storages,
		   Func<FileKey, List<ITemporaryStorageStreamHandle>, ModuleMetadata> moduleMetadataFactory)
		{
			var moduleBuilder = ImmutableArray.CreateBuilder<ModuleMetadata> ();

			string assemblyDir = null;
			foreach (string moduleName in manifestModule.GetModuleNames ()) {
				if (moduleBuilder.Count == 0) {
					moduleBuilder.Add (manifestModule);
					assemblyDir = Path.GetDirectoryName (fileKey.FullPath);
				}

				var moduleFileKey = FileKey.Create (Path.Combine (assemblyDir, moduleName));
				var metadata = moduleMetadataFactory (moduleFileKey, storages);

				moduleBuilder.Add (metadata);
			}

			if (moduleBuilder.Count == 0) {
				moduleBuilder.Add (manifestModule);
			}

			return AssemblyMetadata.Create (
				moduleBuilder.ToImmutable ());
		}

		public PortableExecutableReference GetOrCreateMetadataReferenceSnapshot (string filePath, MetadataReferenceProperties properties)
		{
			return _metadataReferenceCache.GetOrCreate (this, filePath, properties).CurrentSnapshot;
		}

		internal MonoDevelopMetadataReference GetOrCreateMetadataReference (string filePath, MetadataReferenceProperties properties)
		{
			return _metadataReferenceCache.GetOrCreate (this, filePath, properties);
		}

		public void ClearCache ()
		{
			// Clear the reference cache before the metadata cache
			// as the FileWatcher updates can technically trigger while the metadata cache
			// is being cleared, avoiding unnecessary work and possible items not being invalidated.
			_metadataReferenceCache.ClearCache ();
			_metadataCache.ClearCache();
		}
	}
}
