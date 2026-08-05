using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.Controller.Issues;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Scanners;
using DependencyAnalyzer.Editor.Scanners.Issues;
using NUnit.Framework;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class ConsoleIssueScannerTests
    {
        [Test]
        public void AddConsoleIssues_PreservesMetadataAndLinksContextNode()
        {
            var graph = new DependencyGraphData();
            var source = CreateNode("source", "Scene/Source", 123);
            graph.AddOrUpdateNode(source);
            var entry = CreateEntry(
                "Console error",
                "Assets/Foo.cs",
                "Foo.Trace()",
                12,
                7,
                1,
                123,
                10,
                0,
                3);

            ConsoleIssueScanner.AddConsoleIssues(
                graph,
                new DependencyCache(),
                new FixedConsoleLogReader(ConsoleLogReadResult.Success(new[] { entry })));

            Assert.AreEqual(1, graph.Issues.Count);
            var issue = graph.Issues[0];
            Assert.AreEqual(source.Path, issue.SubjectPath);
            Assert.AreEqual(DependencyScanIssueSeverity.Error, issue.Severity);
            Assert.AreEqual("Console error", issue.Message);
            Assert.AreEqual("Assets/Foo.cs", issue.FilePath);
            Assert.AreEqual(12, issue.Line);
            Assert.AreEqual(7, issue.Column);
            Assert.AreEqual("Foo.Trace()", issue.StackTrace);
            Assert.AreEqual(123, issue.InstanceId);
            Assert.AreEqual(3, issue.OccurrenceCount);
        }

        [Test]
        public void AddConsoleIssues_KeepsIssueWithoutRelatedNode()
        {
            var graph = new DependencyGraphData();
            var entry = CreateEntry("Standalone warning", string.Empty, string.Empty, 0, 0, 512, 0, 20, 0, 1);

            ConsoleIssueScanner.AddConsoleIssues(
                graph,
                new DependencyCache(),
                new FixedConsoleLogReader(ConsoleLogReadResult.Success(new[] { entry })));

            Assert.AreEqual(1, graph.Issues.Count);
            Assert.IsEmpty(graph.Issues[0].SubjectPath);
            var panelEntries = IssuePanelEntryBuilder.Build(graph);
            Assert.AreEqual(1, panelEntries.Count);
            Assert.IsEmpty(panelEntries[0].TargetNodeId);
            StringAssert.Contains("No related node", panelEntries[0].Title);
        }

        [Test]
        public void AddConsoleIssues_DeduplicatesSameConsoleRow()
        {
            var graph = new DependencyGraphData();
            var first = CreateEntry("Repeated warning", string.Empty, string.Empty, 0, 0, 512, 0, 30, 0, 1);
            var duplicate = CreateEntry("Repeated warning", string.Empty, string.Empty, 0, 0, 512, 0, 30, 1, 1);

            ConsoleIssueScanner.AddConsoleIssues(
                graph,
                new DependencyCache(),
                new FixedConsoleLogReader(ConsoleLogReadResult.Success(new[] { first, duplicate })));

            Assert.AreEqual(1, graph.Issues.Count);
        }

        [Test]
        public void AddConsoleIssues_KeepsIdenticalTextFromDifferentConsoleRows()
        {
            var graph = new DependencyGraphData();
            var first = CreateEntry("Repeated warning", string.Empty, string.Empty, 0, 0, 512, 0, 40, 0, 1);
            var second = CreateEntry("Repeated warning", string.Empty, string.Empty, 0, 0, 512, 0, 41, 1, 1);

            ConsoleIssueScanner.AddConsoleIssues(
                graph,
                new DependencyCache(),
                new FixedConsoleLogReader(ConsoleLogReadResult.Success(new[] { first, second })));

            Assert.AreEqual(2, graph.Issues.Count);
        }

        [Test]
        public void AddConsoleIssues_EmptyCurrentSnapshotDoesNotRetainPreviousIssues()
        {
            var previousGraph = new DependencyGraphData();
            var entry = CreateEntry("Old warning", string.Empty, string.Empty, 0, 0, 512, 0, 50, 0, 1);
            ConsoleIssueScanner.AddConsoleIssues(
                previousGraph,
                new DependencyCache(),
                new FixedConsoleLogReader(ConsoleLogReadResult.Success(new[] { entry })));

            var currentGraph = new DependencyGraphData();
            ConsoleIssueScanner.AddConsoleIssues(
                currentGraph,
                new DependencyCache(),
                new FixedConsoleLogReader(ConsoleLogReadResult.Success(Array.Empty<ConsoleLogEntry>())));

            Assert.AreEqual(1, previousGraph.Issues.Count);
            Assert.AreEqual(0, currentGraph.Issues.Count);
        }

        [Test]
        public void AddConsoleIssues_UsesOnlyReaderSnapshot()
        {
            var graph = new DependencyGraphData();

            ConsoleIssueScanner.AddConsoleIssues(
                graph,
                new DependencyCache(),
                new FixedConsoleLogReader(ConsoleLogReadResult.Success(Array.Empty<ConsoleLogEntry>())));

            Assert.AreEqual(0, graph.Issues.Count, "Past Editor.log content must not be used as a fallback.");
        }

        [Test]
        public void AddConsoleIssues_ReportsReaderFailureWithoutRelatedNode()
        {
            var graph = new DependencyGraphData();

            ConsoleIssueScanner.AddConsoleIssues(
                graph,
                new DependencyCache(),
                new FixedConsoleLogReader(ConsoleLogReadResult.Failure("Internal API changed.")));

            Assert.AreEqual(1, graph.Issues.Count);
            Assert.AreEqual("Unity Console Reader", graph.Issues[0].ScannerName);
            StringAssert.Contains("Internal API changed", graph.Issues[0].Message);
            var panelEntries = IssuePanelEntryBuilder.Build(graph);
            Assert.AreEqual(1, panelEntries.Count);
            Assert.IsEmpty(panelEntries[0].TargetNodeId);
        }

        private static ConsoleLogEntry CreateEntry(
            string message,
            string file,
            string stackTrace,
            int line,
            int column,
            int mode,
            int instanceId,
            int globalLineIndex,
            int rowIndex,
            int occurrenceCount)
        {
            return new ConsoleLogEntry(
                message,
                file,
                stackTrace,
                line,
                column,
                mode,
                instanceId,
                0,
                globalLineIndex,
                rowIndex,
                occurrenceCount);
        }

        private static DependencyNodeData CreateNode(string id, string path, int instanceId)
        {
            return new DependencyNodeData(
                id,
                default,
                path,
                "Source",
                "GameObject",
                "UnityEngine.GameObject",
                0L,
                Array.Empty<string>(),
                "GameObject Icon",
                DependencyNodeKind.SceneObject,
                instanceId);
        }

        private sealed class FixedConsoleLogReader : IConsoleLogReader
        {
            private readonly ConsoleLogReadResult result;

            public FixedConsoleLogReader(ConsoleLogReadResult result)
            {
                this.result = result;
            }

            public ConsoleLogReadResult Read()
            {
                return result;
            }
        }
    }
}
