namespace Modern.Extensions;

// Extension members (C# 14) and field-backed properties (C# 14).
public static class StringExtensions
{
	extension(string text)
	{
		public int WordCount => text.Split (' ', StringSplitOptions.RemoveEmptyEntries).Length;

		public string Shout () => text.ToUpperInvariant () + "!";
	}
}

public sealed class Temperature
{
	public double Celsius {
		get => field;
		set => field = value < -273.15 ? throw new ArgumentOutOfRangeException (nameof (value)) : value;
	}
}
