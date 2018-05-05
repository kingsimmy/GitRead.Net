using System.Collections.Generic;
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

        [Test]
        public void TestDiff01()
        {
            Dictionary<string, string> files = TestUtils.ExtractZippedFiles("Files01");
            (int added, int deleted) = DiffGenerator.GetLinesChanged(files["JsonSerializerTest_4793c7b.cs"], files["JsonSerializerTest_2368a8e.cs"]);
            Assert.AreEqual(23, added);
            Assert.AreEqual(0, deleted);
        }
    }
}