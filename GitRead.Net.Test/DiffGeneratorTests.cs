using NUnit.Framework;

namespace GitRead.Net.Test
{
    [TestFixture]
    public class DiffGeneratorTests
    {
        [Test]
        public void TestOverlap()
        {
            string text1 = @"
                az
                az
                ab
                ac";
            string text2 = @"
                az
                ab
                ac";
            (int added, int deleted) = DiffGenerator.GetLinesChanged(text1, text2);
            Assert.AreEqual(0, added);
            Assert.AreEqual(1, deleted);
        }
    }
}
