namespace Modern.Interop;

// Function pointers (C# 9) and the unmanaged constraint (C# 7.3).
public static unsafe class Pointers
{
	static int Twice (int value) => value * 2;

	public static int CallTwice (int value)
	{
		delegate* managed<int, int> twice = &Twice;
		return twice (value);
	}

	public static nint Address (delegate* unmanaged<int, int> function) => (nint)function;

	public static int SizeOf<T> () where T : unmanaged => sizeof (T);
}
