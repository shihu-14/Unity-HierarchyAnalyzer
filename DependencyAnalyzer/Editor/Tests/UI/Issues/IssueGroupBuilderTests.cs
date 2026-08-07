using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Controller.Issues;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.Issues;
using NUnit.Framework;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class IssueGroupBuilderTests
    {
        [Test]
        public void Build_GroupsSameMissingMaterialAcrossThreeNodes()
        {
            var graph = new DependencyGraph();
            AddMissingReference(graph, "player", "Assets/Scenes/Main.unity::Player/MeshRenderer", "material-player", "Material");
            AddMissingReference(graph, "enemy", "Assets/Scenes/Main.unity::Enemy/MeshRenderer", "material-enemy", "Material");
            AddMissingReference(graph, "ground", "Assets/Scenes/Main.unity::Ground/MeshRenderer", "material-ground", "Material");

            var groups = Build(graph);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual("Missing Reference: Material", groups[0].Title);
            Assert.AreEqual(3, groups[0].OccurrenceCount);
        }

        [Test]
        public void Build_SeparatesMissingMaterialAndMissingMesh()
        {
            var graph = new DependencyGraph();
            AddMissingReference(graph, "material-source", "Scene::Player/MeshRenderer", "material", "Material");
            AddMissingReference(graph, "mesh-source", "Scene::Enemy/MeshFilter", "mesh", "Mesh");

            var groups = Build(graph);

            Assert.AreEqual(2, groups.Count);
            CollectionAssert.AreEquivalent(
                new[] { "Missing Reference: Material", "Missing Reference: Mesh" },
                groups.Select(group => group.Title));
        }

        [Test]
        public void Build_SeparatesWarningAndError()
        {
            var graph = new DependencyGraph();
            graph.AddIssue(CreateIssue("Scanner", "Same problem", DependencyScanIssueSeverity.Warning));
            graph.AddIssue(CreateIssue("Scanner", "Same problem", DependencyScanIssueSeverity.Error));

            var groups = Build(graph);

            Assert.AreEqual(2, groups.Count);
            CollectionAssert.AreEquivalent(
                new[] { DependencyScanIssueSeverity.Warning, DependencyScanIssueSeverity.Error },
                groups.Select(group => group.Severity));
        }

        [Test]
        public void Build_SeparatesConsoleAndAnalyzerOrigins()
        {
            var graph = new DependencyGraph();
            graph.AddIssue(CreateIssue("Unity Console", "Same problem", DependencyScanIssueSeverity.Warning));
            graph.AddIssue(CreateIssue("SerializedPropertyScanner", "Same problem", DependencyScanIssueSeverity.Warning));

            var groups = Build(graph);

            Assert.AreEqual(2, groups.Count);
            CollectionAssert.AreEquivalent(
                new[] { IssueOrigin.Console, IssueOrigin.Analyzer },
                groups.Select(group => group.Origin));
        }

        [Test]
        public void Build_DoesNotCountPropagatedMarkersAsOccurrences()
        {
            var graph = new DependencyGraph();
            var root = CreateNode("root", "Scene::Root", "Root", "GameObject", DependencyNodeKind.SceneObject);
            var source = CreateNode("source", "Scene::Root/Child/MeshRenderer", "MeshRenderer", "MeshRenderer", DependencyNodeKind.Component);
            graph.AddOrUpdateNode(root);
            graph.AddOrUpdateNode(source);
            graph.AddEdge(new DependencyEdge(root.Id, source.Id, "Child", DependencyReferenceKind.Hierarchy));
            AddMissingReference(graph, source, "missing", "Material");
            root.MarkMissingReferences();

            var groups = Build(graph);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(1, groups[0].OccurrenceCount);
        }

        [Test]
        public void Build_PreservesEveryAffectedLocation()
        {
            var graph = new DependencyGraph();
            AddMissingReference(graph, "player", "Assets/Scenes/Main.unity::Player/MeshRenderer", "material-player", "Material");
            AddMissingReference(graph, "enemy", "Assets/Scenes/Main.unity::Enemy/MeshRenderer", "material-enemy", "Material");
            AddMissingReference(graph, "ground", "Assets/Scenes/Main.unity::Ground/MeshRenderer", "material-ground", "Material");

            var group = Build(graph).Single();

            Assert.AreEqual(3, group.Occurrences.Count);
            CollectionAssert.AreEquivalent(
                new[] { "Player", "Enemy", "Ground" },
                group.Occurrences.Select(occurrence => occurrence.Location.ParentSegments[1]));
            Assert.IsTrue(group.Occurrences.All(occurrence => occurrence.Location.Label == "m_Materials.Array.data[0]"));
        }

        [Test]
        public void Build_KeepsIssueWithoutRelatedNode()
        {
            var graph = new DependencyGraph();
            graph.AddIssue(CreateIssue("ScannerOrchestrator", "Scanner failed", DependencyScanIssueSeverity.Error));

            var groups = Build(graph);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(1, groups[0].OccurrenceCount);
            Assert.IsFalse(groups[0].Occurrences[0].HasRelatedNode);
            Assert.AreEqual("No related node", groups[0].Occurrences[0].Location.Label);
        }

        [Test]
        public void Summarize_CountsRootOccurrencesBySeverity()
        {
            var graph = new DependencyGraph();
            AddMissingReference(graph, "source", "Scene::Object/MeshRenderer", "material", "Material");
            graph.AddIssue(CreateIssue("Unity Console", "Console error", DependencyScanIssueSeverity.Error));
            graph.AddIssue(CreateIssue("SerializedPropertyScanner", "Scanner warning", DependencyScanIssueSeverity.Warning));

            var summary = IssueGroupBuilder.Summarize(Build(graph));

            Assert.AreEqual(3, summary.TotalCount);
            Assert.AreEqual(1, summary.ErrorCount);
            Assert.AreEqual(2, summary.WarningCount);
            Assert.AreEqual(1, summary.ConsoleErrors);
            Assert.AreEqual(0, summary.ConsoleWarnings);
            Assert.AreEqual(0, summary.AnalyzerErrors);
            Assert.AreEqual(2, summary.AnalyzerWarnings);
        }

        [Test]
        public void Summarize_CollapsedConsoleRowCountsOnceAndKeepsReportedOccurrences()
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

            var groups = Build(graph);
            var summary = IssueGroupBuilder.Summarize(groups);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(1, groups[0].OccurrenceCount);
            Assert.AreEqual(7, groups[0].Occurrences[0].ReportedOccurrenceCount);
            StringAssert.Contains("Occurrences: 7", groups[0].Occurrences[0].Detail);
            Assert.AreEqual(1, summary.TotalCount);
            Assert.AreEqual(1, summary.ConsoleWarnings);
        }

        [Test]
        public void Build_NormalizesCompilerFileLocationsWithoutMergingDifferentCauses()
        {
            var graph = new DependencyGraph();
            graph.AddIssue(CreateConsoleCompilerIssue("Assets/A.cs", 10, "error CS1003: Syntax error"));
            graph.AddIssue(CreateConsoleCompilerIssue("Assets/B.cs", 20, "error CS1003: Syntax error"));
            graph.AddIssue(CreateConsoleCompilerIssue("Assets/C.cs", 30, "error CS0103: Unknown name"));

            var groups = Build(graph);

            Assert.AreEqual(2, groups.Count);
            Assert.AreEqual(2, groups.Single(group => group.Title.Contains("CS1003")).OccurrenceCount);
            Assert.AreEqual(1, groups.Single(group => group.Title.Contains("CS0103")).OccurrenceCount);
        }

        [Test]
        public void Build_CreatesSceneHierarchyLocation()
        {
            var graph = new DependencyGraph();
            AddMissingReference(
                graph,
                "weapon",
                "Assets/Scenes/Main.unity::Player/Weapon/MeshRenderer",
                "material",
                "Material");

            var location = Build(graph).Single().Occurrences.Single().Location;

            CollectionAssert.AreEqual(
                new[] { "Scene: Main", "Player", "Weapon", "MeshRenderer" },
                location.ParentSegments);
            Assert.AreEqual("m_Materials.Array.data[0]", location.Label);
        }

        private static List<IssueGroup> Build(DependencyGraph graph)
        {
            return IssueGroupBuilder.Build(graph, IssueTargetResolver.FindIssueEntryTargetNodeId);
        }

        private static DependencyScanIssue CreateIssue(
            string scanner,
            string message,
            DependencyScanIssueSeverity severity)
        {
            return new DependencyScanIssue(scanner, string.Empty, message, severity);
        }

        private static DependencyScanIssue CreateConsoleCompilerIssue(
            string filePath,
            int line,
            string message)
        {
            return new DependencyScanIssue(
                "Unity Console",
                string.Empty,
                filePath + "(" + line + ",1): " + message,
                DependencyScanIssueSeverity.Error,
                filePath,
                line,
                1,
                string.Empty,
                0,
                1);
        }

        private static void AddMissingReference(
            DependencyGraph graph,
            string sourceId,
            string sourcePath,
            string missingId,
            string missingType)
        {
            var source = CreateNode(
                sourceId,
                sourcePath,
                sourceId,
                "MeshRenderer",
                DependencyNodeKind.Component);
            graph.AddOrUpdateNode(source);
            AddMissingReference(graph, source, missingId, missingType);
        }

        private static void AddMissingReference(
            DependencyGraph graph,
            DependencyNode source,
            string missingId,
            string missingType)
        {
            var missing = CreateNode(
                missingId,
                source.Path,
                "m_Materials.Array.data[0]",
                missingType,
                DependencyNodeKind.Asset);
            missing.MarkMissingReferences();
            graph.AddOrUpdateNode(missing);
            graph.AddEdge(new DependencyEdge(
                source.Id,
                missing.Id,
                "m_Materials.Array.data[0]",
                DependencyReferenceKind.SerializedProperty,
                true));
        }

        private static DependencyNode CreateNode(
            string id,
            string path,
            string name,
            string type,
            DependencyNodeKind kind)
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
