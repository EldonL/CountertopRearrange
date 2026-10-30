using NUnit.Framework;

namespace CounterTopRearrange.Tests
{
    public class SpawnablePromptTests
    {
        [Test]
        public void BuildQuestion_NamesSelectedObject()
        {
            string question = SpawnablePrompt.BuildQuestion("red cube");

            Assert.IsTrue(question.Contains("red cube"), "Question must name the selected object.");
            Assert.IsFalse(question.Contains("book"), "Question must not fall back to book.");
        }

        [Test]
        public void BuildQuestion_KeepsPlacementContract()
        {
            string question = SpawnablePrompt.BuildQuestion("cone");

            Assert.IsTrue(question.Contains("\"x\""));
            Assert.IsTrue(question.Contains("\"y\""));
            Assert.IsTrue(question.Contains("counter"));
        }

        [Test]
        public void BuildQuestion_BlankName_FallsBackToBook()
        {
            Assert.IsTrue(SpawnablePrompt.BuildQuestion(null).Contains("book"));
            Assert.IsTrue(SpawnablePrompt.BuildQuestion("").Contains("book"));
            Assert.IsTrue(SpawnablePrompt.BuildQuestion("   ").Contains("book"));
        }

        [Test]
        public void BuildQuestion_TrimsName()
        {
            string question = SpawnablePrompt.BuildQuestion("  sphere  ");

            Assert.IsTrue(question.Contains("a sphere on"));
        }
    }
}
