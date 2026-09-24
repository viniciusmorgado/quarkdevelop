using Xunit;

namespace XUnitSample
{
	public class CalculatorTests
	{
		[Fact]
		public void AdditionPasses ()
		{
			Assert.Equal (2, 1 + 1);
		}

		[Fact]
		public void AdditionFails ()
		{
			Assert.Equal (3, 1 + 1);
		}
	}
}
