using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Scanners.Issues;
using NUnit.Framework;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class ConsoleIssueParserTests
    {
        [Test]
        public void TryGetSeverity_DetectsCompilerErrorText()
        {
            var entry = new ConsoleLogEntry("Assets/Foo.cs(8,13): error CS1003: Syntax error", string.Empty, string.Empty, 0, 0, 0);

            Assert.IsTrue(ConsoleIssueParser.TryGetSeverity(entry, out var severity));
            Assert.AreEqual(DependencyScanIssueSeverity.Error, severity);
        }

        [Test]
        public void ExtractLineNumber_ReadsUnityPathLine()
        {
            const string path = "Assets/Foo.cs";

            Assert.AreEqual(8, ConsoleIssueParser.ExtractLineNumber("Assets/Foo.cs(8,13): warning CS0168", path));
        }
    }
}
