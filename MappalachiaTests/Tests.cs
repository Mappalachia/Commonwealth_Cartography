using Mappalachia.Class;

namespace MappalachiaTests
{
	[TestClass]
	public class Tests
	{
		List<int> list = new List<int> { 0, 1, 2, 3, 4, 5 };

		[TestMethod]
		public void TestCyclicListAccessor_N0()
		{
			int n = 1;
			int expectedResult = 1;

			Assert.AreEqual(GeometryHelper.GetCyclicItem(list, n), expectedResult);
		}

		[TestMethod]
		public void TestCyclicListAccessor_NMinus1()
		{
			int n = -1;
			int expectedResult = 5;

			Assert.AreEqual(GeometryHelper.GetCyclicItem(list, n), expectedResult);
		}

		[TestMethod]
		public void TestCyclicListAccessor_NMinus6()
		{
			int n = -6;
			int expectedResult = 0;

			Assert.AreEqual(GeometryHelper.GetCyclicItem(list, n), expectedResult);
		}

		[TestMethod]
		public void TestCyclicListAccessor_N6()
		{
			int n = 6;
			int expectedResult = 0;

			Assert.AreEqual(GeometryHelper.GetCyclicItem(list, n), expectedResult);
		}

		[TestMethod]
		public void TestCyclicListAccessor_N13()
		{
			int n = 13;
			int expectedResult = 1;

			Assert.AreEqual(GeometryHelper.GetCyclicItem(list, n), expectedResult);
		}

		[TestMethod]
		public void TestCyclicListAccessor_NMinus13()
		{
			int n = -13;
			int expectedResult = 5;

			Assert.AreEqual(GeometryHelper.GetCyclicItem(list, n), expectedResult);
		}
	}
}