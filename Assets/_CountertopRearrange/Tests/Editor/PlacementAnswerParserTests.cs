using NUnit.Framework;

namespace CounterTopRearrange.Tests
{
    public class PlacementAnswerParserTests
    {
        [Test]
        public void CleanJson_ReturnsPoint()
        {
            float x;
            float y;
            bool ok = PlacementAnswerParser.TryParse("{\"x\": 0.42, \"y\": 0.61}", out x, out y);

            Assert.IsTrue(ok);
            Assert.AreEqual(0.42f, x, 1e-6f);
            Assert.AreEqual(0.61f, y, 1e-6f);
        }

        [Test]
        public void FencedJson_ReturnsPoint()
        {
            float x;
            float y;
            bool ok = PlacementAnswerParser.TryParse(
                "```json\n{\"x\":0.5,\"y\":0.5,\"reason\":\"clear spot\"}\n```", out x, out y);

            Assert.IsTrue(ok);
            Assert.AreEqual(0.5f, x, 1e-6f);
            Assert.AreEqual(0.5f, y, 1e-6f);
        }

        [Test]
        public void ProseWrappedJson_ReturnsPoint()
        {
            float x;
            float y;
            bool ok = PlacementAnswerParser.TryParse(
                "The best spot is {\"x\": 0.3, \"y\": 0.7} because it is clear.", out x, out y);

            Assert.IsTrue(ok);
            Assert.AreEqual(0.3f, x, 1e-6f);
            Assert.AreEqual(0.7f, y, 1e-6f);
        }

        [Test]
        public void MissingKey_ReturnsFalse()
        {
            float x;
            float y;
            Assert.IsFalse(PlacementAnswerParser.TryParse("{\"x\": 0.5}", out x, out y));
        }

        [Test]
        public void OutOfRange_ReturnsFalse()
        {
            float x;
            float y;
            Assert.IsFalse(PlacementAnswerParser.TryParse("{\"x\": 1.5, \"y\": 0.5}", out x, out y));
            Assert.IsFalse(PlacementAnswerParser.TryParse("{\"x\": 0.5, \"y\": -0.1}", out x, out y));
        }

        [Test]
        public void NonJson_ReturnsFalse()
        {
            float x;
            float y;
            Assert.IsFalse(PlacementAnswerParser.TryParse("no JSON here", out x, out y));
            Assert.IsFalse(PlacementAnswerParser.TryParse("", out x, out y));
            Assert.IsFalse(PlacementAnswerParser.TryParse(null, out x, out y));
        }
    }
}
