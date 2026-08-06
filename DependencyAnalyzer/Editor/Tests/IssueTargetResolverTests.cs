using System;
using DependencyAnalyzer.Editor.Controller.Issues;
using DependencyAnalyzer.Editor.Core;
using NUnit.Framework;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class IssueTargetResolverTests
    {
        [Test]
        public void FindIssueEntryTargetNodeId_PrefersIssueSourceEdge()
        {
            var graph = new DependencyGraph();
            var source = CreateNode("script", "Assets/Foo.cs", "Foo.cs", "Script", DependencyNodeKind.Asset);
            var issue = new DependencyScanIssue("Unity Console", "Assets/Foo.cs", "Assets/Foo.cs(8,13): error CS1003", DependencyScanIssueSeverity.Error);
            var issueNode = new DependencyNode(
                "issue:error:foo",
                default,
                "Assets/Foo.cs",
                "Foo.cs",
                "Script",
                "Script",
                Array.Empty<string>(),
                "cs Script Icon",
                DependencyNodeKind.Asset,
                0,
                issue.Severity,
                issue.Message);

            graph.AddOrUpdateNode(source);
            graph.AddOrUpdateNode(issueNode);
            graph.AddEdge(new DependencyEdge(source.Id, issueNode.Id, "Error Issue", DependencyReferenceKind.Issue));

            Assert.AreEqual(source.Id, IssueTargetResolver.FindIssueEntryTargetNodeId(graph, issue));
        }

        [Test]
        public void IssuePanelEntryBuilder_IncludesMissingReferenceRows()
        {
            var graph = new DependencyGraph();
            var missing = CreateNode("missing", "Scene/Object", "Missing Field", "Missing", DependencyNodeKind.MissingReference);
            graph.AddOrUpdateNode(missing);

            var entries = IssuePanelEntryBuilder.Build(graph);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(DependencyScanIssueSeverity.Warning, entries[0].Severity);
            Assert.AreEqual(missing.Id, entries[0].TargetNodeId);
        }

        [Test]
        public void IssuePanelEntryBuilder_IncludesTypedMissingTargetsFromEdges()
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

            var entries = IssuePanelEntryBuilder.Build(graph);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(missing.Id, entries[0].TargetNodeId);
        }

        [Test]
        public void IssuePanelEntryBuilder_CountsConsoleAndAnalyzerIssuesSeparately()
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
                source.Path,
                "Scanner warning",
                DependencyScanIssueSeverity.Warning));
            graph.AddIssue(new DependencyScanIssue(
                "ScannerOrchestrator",
                source.Path,
                "Scanner error",
                DependencyScanIssueSeverity.Error));
            graph.AddIssue(new DependencyScanIssue(
                "Unity Console Reader",
                string.Empty,
                "Reader warning",
                DependencyScanIssueSeverity.Warning));

            var counts = IssuePanelEntryBuilder.CountByOrigin(IssuePanelEntryBuilder.Build(graph));

            Assert.AreEqual(1, counts.ConsoleErrors);
            Assert.AreEqual(1, counts.ConsoleWarnings);
            Assert.AreEqual(1, counts.AnalyzerErrors);
            Assert.AreEqual(3, counts.AnalyzerWarnings);
            Assert.AreEqual("Issues | Console E: 1 W: 1 | Analyzer E: 1 W: 3", counts.DisplayText);
        }

        [Test]
        public void IssuePanelEntryBuilder_DoesNotAddOccurrencesToConsoleRowCount()
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

            var counts = IssuePanelEntryBuilder.CountByOrigin(IssuePanelEntryBuilder.Build(graph));

            Assert.AreEqual(1, counts.ConsoleWarnings);
            Assert.AreEqual(0, counts.AnalyzerWarnings);
        }

        [Test]
        public void IssuePanelEntryBuilder_EmptyConsoleKeepsAnalyzerCounts()
        {
            var graph = new DependencyGraph();
            var source = CreateNode("source", "Scene/Object", "Object", "Object", DependencyNodeKind.SceneObject);
            var missing = CreateNode("missing", "Scene/Object", "Missing Field", "Missing", DependencyNodeKind.MissingReference);
            graph.AddOrUpdateNode(source);
            graph.AddOrUpdateNode(missing);
            graph.AddIssue(new DependencyScanIssue(
                "SerializedPropertyScanner",
                source.Path,
                "Scanner warning",
                DependencyScanIssueSeverity.Warning));

            var counts = IssuePanelEntryBuilder.CountByOrigin(IssuePanelEntryBuilder.Build(graph));

            Assert.AreEqual(0, counts.ConsoleErrors);
            Assert.AreEqual(0, counts.ConsoleWarnings);
            Assert.AreEqual(0, counts.AnalyzerErrors);
            Assert.AreEqual(2, counts.AnalyzerWarnings);
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
