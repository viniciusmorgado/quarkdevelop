namespace Modern.Text;

// Raw string literals (C# 11), interpolated raw strings with one and two dollar signs, UTF-8 literals (C# 11).
public static class Samples
{
	public const string Json = """
		{ "name": "MonoDevelop", "empty": "" }
		""";

	public static string Interpolated (string name) => $"""
		Hello, "{name}"!
		""";

	public static string Braces (int count) => $$"""
		{ "count": {{count}} }
		""";

	public static ReadOnlySpan<byte> Utf8 => "MonoDevelop"u8;

	public static int Utf8Length => Utf8.Length;
}
