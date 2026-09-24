using Modern.Extensions;
using Modern.Generators;
using Modern.Generics;
using Modern.Interop;
using Modern.Text;

// Top-level statements (C# 9). The other files use the C# 8 to 14 features that the editor highlights.
Person ada = new ("Ada", "Lovelace") { Born = 1815 };
Person older = ada with { Born = 1814 };
int[] numbers = [1, 2, 3];
int[] more = [.. numbers, 4, 5];
Point origin = (0, 0);

string[] lines = [
	Describe.Shape (new Circle (2)),
	Describe.List (numbers),
	Describe.List (more),
	$"{older.First} {older.Born} {numbers[^1]} {origin.X} {Describe.IsLetter ('m')} {Describe.HasValue (null)}",
	Samples.Interpolated ("MonoDevelop"),
	Samples.Braces (3),
	$"{Samples.Json.Length} {Samples.Utf8Length} {"a b c".WordCount} {"hi".Shout ()}",
	$"{Algorithms.Unit<Square> ().Area} {Algorithms.LengthOf (new Word ("modern"))} {Algorithms.Sum ([1, 2, 3])}",
	$"{Pointers.CallTwice (21)} {Pointers.SizeOf<long> ()} {new Temperature { Celsius = 21 }.Celsius} {Max (2, 3)}",
	$"{Words.Count ("modern C sharp")} {Serialization.ToJson (new Release (10, 0))}",
];
foreach (var line in lines)
	Console.WriteLine (line);
Console.WriteLine ("Modern C#: OK");
