namespace Modern.Generics;

// Static abstract interface members (C# 11), default interface members (C# 8), the notnull constraint (C# 8),
// scoped parameters (C# 11), native-sized integers (C# 9) and the allows ref struct anti-constraint (C# 13).
public interface IShape<TSelf> where TSelf : IShape<TSelf>
{
	static abstract TSelf Unit { get; }

	double Area { get; }

	string Describe () => $"area {Area}";
}

public readonly record struct Square (double Side) : IShape<Square>
{
	public static Square Unit => new (1);

	public double Area => Side * Side;
}

public interface IMeasured
{
	int Length { get; }
}

public ref struct Word (ReadOnlySpan<char> text) : IMeasured
{
	readonly ReadOnlySpan<char> chars = text;

	public readonly int Length => chars.Length;
}

public static class Algorithms
{
	public static T Unit<T> () where T : IShape<T> => T.Unit;

	public static int LengthOf<T> (T value) where T : IMeasured, allows ref struct => value.Length;

	public static string Key<TKey> (TKey key) where TKey : notnull => key.ToString () ?? "";

	public static int Sum (scoped ReadOnlySpan<int> values)
	{
		int total = 0;
		foreach (var value in values)
			total += value;
		return total;
	}

	public static nint Offset (nint start, nuint count) => start + (nint)count;
}
