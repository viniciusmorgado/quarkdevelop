namespace Modern.Shapes;

// File-scoped namespace (C# 10), records (C# 9), record structs (C# 10), required members (C# 11),
// primary constructors of classes (C# 12) and file-local types (C# 11).
public record Person (string First, string Last)
{
	public required int Born { get; init; }
}

public readonly record struct Vector (double X, double Y);

public abstract record Shape;

public sealed record Circle (double Radius) : Shape;

public sealed record Rectangle (double Width, double Height) : Shape;

public class Counter (int start)
{
	int current = start;

	public int Next () => ++current;

	public static string Kind => Hidden.Name;
}

file sealed class Hidden
{
	public const string Name = nameof (Hidden);
}
