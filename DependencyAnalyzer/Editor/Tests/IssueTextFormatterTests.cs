using DependencyAnalyzer.Editor.Controller.Issues;
using NUnit.Framework;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class IssueTextFormatterTests
    {
        [Test]
        public void FormatTitle_RemovesIssuePrefixAndUnderscores()
        {
            Assert.AreEqual("Missing Rigidbody", IssueTextFormatter.FormatTitle("Issue_Missing_Rigidbody"));
        }

        [Test]
        public void FormatDetail_RemovesDemoNoise()
        {
            Assert.AreEqual(
                "ConsoleErrorShader.shader",
                IssueTextFormatter.FormatDetail("Assets/DependencyAnalyzerDemo/IssueAssets/IssueConsoleErrorShader.shader"));
        }
    }
}
