//
// StartupHook.cs
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

using Microsoft.Build.Locator;

/// <summary>
/// .NET startup hook for test hosts (DOTNET_STARTUP_HOOKS, set by the generated .runsettings).
/// MonoDevelop compiles against Microsoft.Build without shipping it (ADR 0008); test discovery
/// reflects over types that implement MSBuild interfaces before any test code runs, so the SDK's
/// MSBuild must be registered before the test host loads the test assemblies.
/// </summary>
internal sealed class StartupHook
{
	public static void Initialize ()
	{
		// Only the test host needs the hook: child processes (dotnet msbuild, the MSBuild builder,
		// mdtool) register MSBuild themselves, and dotnet msbuild aborts when the hook runs in it.
		System.Environment.SetEnvironmentVariable ("DOTNET_STARTUP_HOOKS", null);
		if (!MSBuildLocator.IsRegistered)
			MSBuildLocator.RegisterDefaults ();
	}
}
