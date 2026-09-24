using NUnit.Framework;

namespace NUnitSample
{
	public class CalculatorTests
	{
		[Test]
		public void AdditionPasses ()
		{
			Assert.That (1 + 1, Is.EqualTo (2));
		}

		[Test]
		public void AdditionFails ()
		{
			Assert.That (1 + 1, Is.EqualTo (3));
		}
	}
}
