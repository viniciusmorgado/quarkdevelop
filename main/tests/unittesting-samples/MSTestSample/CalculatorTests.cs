using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestSample
{
	[TestClass]
	public class CalculatorTests
	{
		[TestMethod]
		public void AdditionPasses ()
		{
			Assert.AreEqual (2, 1 + 1);
		}

		[TestMethod]
		public void AdditionFails ()
		{
			Assert.AreEqual (3, 1 + 1);
		}
	}
}
