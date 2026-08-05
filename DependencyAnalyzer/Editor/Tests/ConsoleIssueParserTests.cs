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
        public void TryGetSeverity_DetectsUnityScriptingWarningMode()
        {
            var entry = new ConsoleLogEntry("Warning without keyword classification", string.Empty, string.Empty, 0, 512, 0);

            Assert.IsTrue(ConsoleIssueParser.TryGetSeverity(entry, out var severity));
            Assert.AreEqual(DependencyScanIssueSeverity.Warning, severity);
        }

        [Test]
        public void TryGetSeverity_DetectsUnityScriptingErrorMode()
        {
            var entry = new ConsoleLogEntry("Failure without error keyword", string.Empty, string.Empty, 0, 256, 0);

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
