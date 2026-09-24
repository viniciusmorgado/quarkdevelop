using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Modern.Generators;

// Source generators of the shared framework, which the SDK passes to the compiler from the targeting pack: the
// Regex generator implements the partial method, the System.Text.Json generator the serializer context.
public static partial class Words
{
	[GeneratedRegex (@"\b\w+\b")]
	private static partial Regex Word ();

	public static int Count (string text) => Word ().Count (text);
}

public record Release (int Major, int Minor);

[JsonSerializable (typeof (Release))]
public partial class ReleaseContext : JsonSerializerContext
{
}

public static class Serialization
{
	public static string ToJson (Release release) => JsonSerializer.Serialize (release, ReleaseContext.Default.Release);
}
