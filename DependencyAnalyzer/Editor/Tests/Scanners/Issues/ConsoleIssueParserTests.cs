using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Scanners.Issues;
using NUnit.Framework;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class ConsoleIssueParserTests
    {
        [Test]
        public void TryGetSeverity_FallsBackToCompilerErrorTextWhenModeIsUnknown()
        {
            var entry = new ConsoleLogEntry("Assets/Foo.cs(8,13): error CS1003: Syntax error", string.Empty, string.Empty, 0, 0, 0);

            Assert.IsTrue(ConsoleIssueParser.TryGetSeverity(entry, out var severity));
            Assert.AreEqual(DependencyScanIssueSeverity.Error, severity);
        }

        [Test]
        public void TryGetSeverity_FallsBackToCompilerWarningTextWhenModeIsUnknown()
        {
            var entry = new ConsoleLogEntry("Assets/Foo.cs(8,13): warning CS0168: Variable is never used", string.Empty, string.Empty, 0, 0, 0);

            Assert.IsTrue(ConsoleIssueParser.TryGetSeverity(entry, out var severity));
            Assert.AreEqual(DependencyScanIssueSeverity.Warning, severity);
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

        [TestCase(1 << 0, TestName = "TryGetSeverity_ErrorMode")]
        [TestCase(1 << 1, TestName = "TryGetSeverity_AssertMode")]
        [TestCase(1 << 4, TestName = "TryGetSeverity_FatalMode")]
        [TestCase(1 << 6, TestName = "TryGetSeverity_AssetImportErrorMode")]
        [TestCase(1 << 8, TestName = "TryGetSeverity_ScriptingErrorMode")]
        [TestCase(1 << 11, TestName = "TryGetSeverity_ScriptCompileErrorMode")]
        [TestCase(1 << 17, TestName = "TryGetSeverity_ScriptingExceptionMode")]
        [TestCase(1 << 20, TestName = "TryGetSeverity_GraphCompileErrorMode")]
        [TestCase(1 << 21, TestName = "TryGetSeverity_ScriptingAssertionMode")]
        public void TryGetSeverity_DetectsUnityConsoleErrorModes(int mode)
        {
            var entry = new ConsoleLogEntry("Mode-only entry", string.Empty, string.Empty, 0, mode, 0);

            Assert.IsTrue(ConsoleIssueParser.TryGetSeverity(entry, out var severity));
            Assert.AreEqual(DependencyScanIssueSeverity.Error, severity);
        }

        [TestCase(1 << 7, TestName = "TryGetSeverity_AssetImportWarningMode")]
        [TestCase(1 << 9, TestName = "TryGetSeverity_ScriptingWarningMode")]
        [TestCase(1 << 12, TestName = "TryGetSeverity_ScriptCompileWarningMode")]
        public void TryGetSeverity_DetectsUnityConsoleWarningModes(int mode)
        {
            var entry = new ConsoleLogEntry("Mode-only entry", string.Empty, string.Empty, 0, mode, 0);

            Assert.IsTrue(ConsoleIssueParser.TryGetSeverity(entry, out var severity));
            Assert.AreEqual(DependencyScanIssueSeverity.Warning, severity);
        }

        [Test]
        public void TryGetSeverity_DoesNotTreatVisualScriptingErrorBitAsError()
        {
            var entry = new ConsoleLogEntry("Mode-only entry", string.Empty, string.Empty, 0, 1 << 22, 0);

            Assert.IsFalse(ConsoleIssueParser.TryGetSeverity(entry, out _));
        }

        [Test]
        public void TryGetSeverity_DoesNotTreatRegularLogAsIssue()
        {
            var entry = new ConsoleLogEntry("Regular log", string.Empty, string.Empty, 0, 1 << 2, 0);

            Assert.IsFalse(ConsoleIssueParser.TryGetSeverity(entry, out _));
        }

        [Test]
        public void TryGetSeverity_DoesNotUseWarningTextFallbackForLogMode()
        {
            var entry = new ConsoleLogEntry("This regular log contains warning text", string.Empty, string.Empty, 0, 1 << 2, 0);

            Assert.IsFalse(ConsoleIssueParser.TryGetSeverity(entry, out _));
        }

        [Test]
        public void TryGetSeverity_DoesNotUseErrorTextFallbackForScriptingLogMode()
        {
            var entry = new ConsoleLogEntry("This scripting log contains error text", string.Empty, string.Empty, 0, 1 << 10, 0);

            Assert.IsFalse(ConsoleIssueParser.TryGetSeverity(entry, out _));
        }

        [Test]
        public void ExtractLineNumber_ReadsUnityPathLine()
        {
            const string path = "Assets/Foo.cs";

            Assert.AreEqual(8, ConsoleIssueParser.ExtractLineNumber("Assets/Foo.cs(8,13): warning CS0168", path));
        }
    }
}
