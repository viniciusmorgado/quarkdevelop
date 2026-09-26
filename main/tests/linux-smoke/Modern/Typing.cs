namespace Modern;

// The GUI smoke test types into this file (MD_SMOKE_TYPING): Return at the end of the statement in the if block, and
// after the parenthesis that opens the argument list, must put the caret at the indentation of the next statement or
// argument, and Tab must then stay on the new line.
public static class Typing
{
	public static string[] Forecast (bool development)
	{
		if (development) {
			Console.WriteLine ("development");
		}
		return Enumerable.Range (1, 3).Select (index =>
			string.Join
			(
				", ",
				index,
				index * 2
			))
			.ToArray ();
	}
}
