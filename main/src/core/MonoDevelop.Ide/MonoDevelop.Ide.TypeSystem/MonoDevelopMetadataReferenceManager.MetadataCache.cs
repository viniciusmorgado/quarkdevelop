using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace MonoDevelop.Ide.TypeSystem
{
	partial class MonoDevelopMetadataReferenceManager
	{
		class MetadataCache
		{
			const int InitialCapacity = 64;
			const int CapacityMultiplier = 2;

			readonly object _gate = new object ();

			// value is ValueSource so that how metadata is re-acquired back are different per entry. 
			readonly Dictionary<FileKey, RecoverableMetadataValueSource> _metadataCache = new Dictionary<FileKey, RecoverableMetadataValueSource> ();

			int _capacity = InitialCapacity;

			public bool TryGetMetadata (FileKey key, out AssemblyMetadata metadata)
			{
				lock (_gate) {
					return TryGetMetadata_NoLock (key, out metadata);
				}
			}

			public bool TryGetSource (FileKey key, out RecoverableMetadataValueSource source)
			{
				lock (_gate) {
					return _metadataCache.TryGetValue (key, out source);
				}
			}

			bool TryGetMetadata_NoLock (FileKey key, out AssemblyMetadata metadata)
			{
				if (_metadataCache.TryGetValue (key, out var metadataSource)) {
					metadata = metadataSource.GetValue ();
					return metadata != null;
				}

				metadata = null;
				return false;
			}

			public bool TryGetOrAddMetadata (FileKey key, RecoverableMetadataValueSource newMetadata, out AssemblyMetadata metadata)
			{
				lock (_gate) {
					if (TryGetMetadata_NoLock (key, out metadata)) {
						return false;
					}

					EnsureCapacity_NoLock ();

					metadata = newMetadata.GetValue ();
					Contract.ThrowIfNull (metadata);

					// don't use "Add" since key might already exist with already released metadata
					_metadataCache [key] = newMetadata;
					return true;
				}
			}

			void EnsureCapacity_NoLock ()
			{
				if (_metadataCache.Count < _capacity) {
					return;
				}

				// metadata doesn't exist anymore. delete it from cache
				var keysToRemove = _metadataCache.Where (kv => !kv.Value.HasValue).Select (kv => kv.Key).ToList ();
				foreach (var key in keysToRemove) {
					_metadataCache.Remove (key);
				}

				// cache is too small, increase it
				if (_metadataCache.Count >= _capacity) {
					_capacity *= CapacityMultiplier;
				}
			}

			public void ClearCache ()
			{
				lock (_gate) {
					_metadataCache.Clear ();
				}
			}
		}
	}
}
