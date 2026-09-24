namespace Modern.Shapes;

// Switch expressions (C# 8), relational and logical patterns (C# 9) and list patterns (C# 11).
public static class Describe
{
	public static string Shape (Shape shape) => shape switch {
		Circle { Radius: > 0 and < 10 } circle => $"small circle {circle.Radius}",
		Circle => "large circle",
		Rectangle (var width, var height) when width == height => "square",
		Rectangle rectangle => $"rectangle {rectangle.Width}x{rectangle.Height}",
		_ => "unknown",
	};

	public static string List (int[] values) => values switch {
		[] => "empty",
		[1, .., 3] => "one to three",
		[var first, .. var rest] => $"starts with {first}, {rest.Length} more",
	};

	public static bool IsLetter (char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

	public static bool HasValue (object? value) => value is not null;
}
