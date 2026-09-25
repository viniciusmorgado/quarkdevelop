# Code moved out of the headless C# binding

`CSharpCompilerParameters.CreateMetadataReferenceResolver` used Roslyn workspace internals
(`IMetadataService`, `WorkspaceMetadataFileReferenceResolver`, `RelativePathResolver`) and
`IdeApp.TypeSystemService`. It was removed from the shared source (task T053) and must be
re-implemented in the GUI C# binding as the `CSharpCompilerParameters.MetadataReferenceResolverProvider`
hook (task T089). Original implementation (MonoDevelop 8.6):

```csharp
static MetadataReferenceResolver CreateMetadataReferenceResolver (IMetadataService metadataService, string projectDirectory, string outputDirectory)
		{
			ImmutableArray<string> assemblySearchPaths;
			if (projectDirectory != null && outputDirectory != null) {
				assemblySearchPaths = ImmutableArray.Create (projectDirectory, outputDirectory);
			} else if (projectDirectory != null) {
				assemblySearchPaths = ImmutableArray.Create (projectDirectory);
			} else if (outputDirectory != null) {
				assemblySearchPaths = ImmutableArray.Create (outputDirectory);
			} else {
				assemblySearchPaths = ImmutableArray<string>.Empty;
			}

			return new WorkspaceMetadataFileReferenceResolver (metadataService, new RelativePathResolver (assemblySearchPaths, baseDirectory: projectDirectory));
		}
```

`CSharpCompilerParameters.GlobalRuleSetProvider` replaces `IdeApp.TypeSystemService.RuleSetManager.GetGlobalRuleSet`.
When it is not set, the headless binding reads the same file (`RuleSet.global` in the profile's config folder, T135).
