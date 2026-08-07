using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Scanners;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class AnalyzerDiagnosticReporterTests
    {
        [TestCase(DependencyScanIssueSeverity.Info, LogType.Log)]
        [TestCase(DependencyScanIssueSeverity.Warning, LogType.Warning)]
        [TestCase(DependencyScanIssueSeverity.Error, LogType.Error)]
        public void Report_UsesDiagnosticSeverityAndPreservesContext(
            DependencyScanIssueSeverity severity,
            LogType expectedLogType)
        {
            const string expected = "[Dependency Analyzer Diagnostic] Test Scanner [Scene::Object]: Failed to inspect component";
            LogAssert.Expect(expectedLogType, expected);

            AnalyzerDiagnosticReporter.Report(new[]
            {
                new DependencyScanIssue(
                    "Test Scanner",
                    "Scene::Object",
                    "Failed to inspect component",
                    severity)
            });
        }

        [Test]
        public void Report_DeduplicatesIdenticalDiagnosticsWithinSnapshot()
        {
            const string expected = "[Dependency Analyzer Diagnostic] Test Scanner [Scene::Object]: Repeated failure";
            var diagnostic = new DependencyScanIssue(
                "Test Scanner",
                "Scene::Object",
                "Repeated failure",
                DependencyScanIssueSeverity.Warning);
            LogAssert.Expect(LogType.Warning, expected);

            AnalyzerDiagnosticReporter.Report(new[] { diagnostic, diagnostic });
        }
    }
}
