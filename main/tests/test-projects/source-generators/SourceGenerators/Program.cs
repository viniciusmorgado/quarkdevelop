// Each partial member below is implemented by a source generator of the shared framework, which the SDK passes
// to the compiler as an Analyzer item from the targeting pack (Microsoft.NETCore.App.Ref/analyzers/dotnet/cs).
// Without the generators the editor reports CS8795 (partial member without implementation) and CS0534.
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

var point = new Point (1, 2);
string json = System.Text.Json.JsonSerializer.Serialize (point, PointContext.Default.Point);
Console.WriteLine ($"{Patterns.Digits ().IsMatch ("a1")} {json} {Native.GetPid () > 0}");

record Point (int X, int Y);

[JsonSerializable (typeof (Point))]
partial class PointContext : JsonSerializerContext
{
}

static partial class Patterns
{
	[GeneratedRegex ("[0-9]+")]
	public static partial Regex Digits ();
}

static partial class Native
{
	[LibraryImport ("libc", EntryPoint = "getpid")]
	internal static partial int GetPid ();
}
