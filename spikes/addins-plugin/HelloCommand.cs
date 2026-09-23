namespace SamplePlugin
{
	public class HelloCommand : AddinsSpike.ICommand
	{
		public string Run () => "hello from an add-in loaded on " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
	}
}
