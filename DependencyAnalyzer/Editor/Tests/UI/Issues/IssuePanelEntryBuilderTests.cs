using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.Controller.Issues;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.Issues;
using NUnit.Framework;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class IssuePanelEntryBuilderTests
    {
        [Test]
        public void Build_IncludesMissingReferenceRows()
        {
            var graph = new DependencyGraph();
            var missing = CreateNode("missing", "Scene/Object", "Missing Field", "Missing", DependencyNodeKind.MissingReference);
            graph.AddOrUpdateNode(missing);

            var entries = Build(graph);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(DependencyScanIssueSeverity.Warning, entries[0].Severity);
            Assert.AreEqual(missing.Id, entries[0].TargetNodeId);
            Assert.IsTrue(entries[0].HasRelatedNode);
        }

        [Test]
        public void Build_IncludesTypedMissingTargetsFromEdges()
        {
            var graph = new DependencyGraph();
            var source = CreateNode("source", "Scene/Object", "Object", "Object", DependencyNodeKind.SceneObject);
            var missing = CreateNode("missing-material", "Scene/Object", "material", "Material", DependencyNodeKind.Asset);
            missing.MarkMissingReferences();
            graph.AddOrUpdateNode(source);
            graph.AddOrUpdateNode(missing);
            graph.AddEdge(new DependencyEdge(
                source.Id,
                missing.Id,
                "m_Materials.Array.data[0]",
                DependencyReferenceKind.SerializedProperty,
                true));

            var entries = Build(graph);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(missing.Id, entries[0].TargetNodeId);
        }

        [Test]
        public void Build_KeepsScannerWarningWithoutRelatedNode()
        {
            var graph = new DependencyGraph();
            graph.AddIssue(new DependencyScanIssue(
                "SerializedPropertyScanner",
                string.Empty,
                "Scanner warning",
                DependencyScanIssueSeverity.Warning));

            var entries = Build(graph);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(IssuePanelEntryOrigin.Analyzer, entries[0].Origin);
            Assert.AreEqual(DependencyScanIssueSeverity.Warning, entries[0].Severity);
            Assert.IsFalse(entries[0].HasRelatedNode);
            Assert.IsEmpty(entries[0].TargetNodeId);
            StringAssert.Contains("No related node", entries[0].Title);
        }

        [Test]
        public void Build_KeepsScannerErrorWithoutRelatedNode()
        {
            var graph = new DependencyGraph();
            graph.AddIssue(new DependencyScanIssue(
                "ScannerOrchestrator",
                string.Empty,
                "Scanner error",
                DependencyScanIssueSeverity.Error));

            var entries = Build(graph);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(IssuePanelEntryOrigin.Analyzer, entries[0].Origin);
            Assert.AreEqual(DependencyScanIssueSeverity.Error, entries[0].Severity);
            Assert.IsFalse(entries[0].HasRelatedNode);
            Assert.IsEmpty(entries[0].TargetNodeId);
            StringAssert.Contains("No related node", entries[0].Title);
        }

        [Test]
        public void CountByOrigin_SeparatesConsoleAndAnalyzerIssues()
        {
            var graph = new DependencyGraph();
            var source = CreateNode("source", "Scene/Object", "Object", "Object", DependencyNodeKind.SceneObject);
            var missing = CreateNode("missing", "Scene/Object", "Missing Field", "Missing", DependencyNodeKind.MissingReference);
            graph.AddOrUpdateNode(source);
            graph.AddOrUpdateNode(missing);
            graph.AddIssue(new DependencyScanIssue(
                "Unity Console",
                source.Path,
                "Console error",
                DependencyScanIssueSeverity.Error));
            graph.AddIssue(new DependencyScanIssue(
                "Unity Console",
                string.Empty,
                "Unlinked Console warning",
                DependencyScanIssueSeverity.Warning));
            graph.AddIssue(new DependencyScanIssue(
                "SerializedPropertyScanner",
                string.Empty,
                "Scanner warning",
                DependencyScanIssueSeverity.Warning));
            graph.AddIssue(new DependencyScanIssue(
                "ScannerOrchestrator",
                string.Empty,
                "Scanner error",
                DependencyScanIssueSeverity.Error));
            graph.AddIssue(new DependencyScanIssue(
                "Unity Console Reader",
                string.Empty,
                "Reader warning",
                DependencyScanIssueSeverity.Warning));

            var counts = IssuePanelEntryBuilder.CountByOrigin(Build(graph));

            Assert.AreEqual(1, counts.ConsoleErrors);
            Assert.AreEqual(1, counts.ConsoleWarnings);
            Assert.AreEqual(1, counts.AnalyzerErrors);
            Assert.AreEqual(3, counts.AnalyzerWarnings);
            Assert.AreEqual("Issues | Console E: 1 W: 1 | Analyzer E: 1 W: 3", counts.DisplayText);
        }

        [Test]
        public void CountByOrigin_DoesNotAddOccurrencesToConsoleRowCount()
        {
            var graph = new DependencyGraph();
            graph.AddIssue(new DependencyScanIssue(
                "Unity Console",
                string.Empty,
                "Collapsed warning",
                DependencyScanIssueSeverity.Warning,
                string.Empty,
                0,
                0,
                string.Empty,
                0,
                7));

            var counts = IssuePanelEntryBuilder.CountByOrigin(Build(graph));

            Assert.AreEqual(1, counts.ConsoleWarnings);
            Assert.AreEqual(0, counts.AnalyzerWarnings);
        }

        [Test]
        public void CountByOrigin_EmptyConsoleKeepsAnalyzerCounts()
        {
            var graph = new DependencyGraph();
            var missing = CreateNode("missing", "Scene/Object", "Missing Field", "Missing", DependencyNodeKind.MissingReference);
            graph.AddOrUpdateNode(missing);
            graph.AddIssue(new DependencyScanIssue(
                "SerializedPropertyScanner",
                string.Empty,
                "Scanner warning",
                DependencyScanIssueSeverity.Warning));

            var counts = IssuePanelEntryBuilder.CountByOrigin(Build(graph));

            Assert.AreEqual(0, counts.ConsoleErrors);
            Assert.AreEqual(0, counts.ConsoleWarnings);
            Assert.AreEqual(0, counts.AnalyzerErrors);
            Assert.AreEqual(2, counts.AnalyzerWarnings);
        }

        private static List<IssuePanelEntry> Build(DependencyGraph graph)
        {
            return IssuePanelEntryBuilder.Build(graph, IssueTargetResolver.FindIssueEntryTargetNodeId);
        }

        private static DependencyNode CreateNode(string id, string path, string name, string type, DependencyNodeKind kind)
        {
            return new DependencyNode(
                id,
                default,
                path,
                name,
                type,
                type,
                Array.Empty<string>(),
                "DefaultAsset Icon",
                kind);
        }
    }
}
