using System;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.Issues;
using NUnit.Framework;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class ProjectIssuePanelBuilderTests
    {
        [Test]
        public void Build_ClassifiesMissingScriptWithDirectLocationAndSourceTarget()
        {
            var graph = new DependencyGraph();
            var source = CreateNode("owner", "Assets/Scenes/Main.unity::Player", "Player", "Object", DependencyNodeKind.SceneObject);
            var missing = CreateNode("missing-script", source.Path, "Missing MonoBehaviour", "Script", DependencyNodeKind.Component);
            graph.AddOrUpdateNode(source);
            graph.AddOrUpdateNode(missing);
            graph.AddEdge(new DependencyEdge(
                source.Id,
                missing.Id,
                "Missing Component [1]",
                DependencyReferenceKind.Component,
                true));

            var model = ProjectIssuePanelBuilder.Build(graph);
            var group = model.Groups.Single();
            var location = group.Locations.Single();

            Assert.AreEqual(ProjectIssueType.MissingScript, group.Type);
            Assert.AreEqual("Missing Script", group.Title);
            Assert.AreEqual(1, group.Count);
            Assert.IsEmpty(group.ObjectGroups);
            Assert.AreEqual(source.Id, location.TargetNodeId);
            CollectionAssert.AreEqual(new[] { "Scene: Main", "Player" }, location.ParentSegments);
            Assert.AreEqual("Missing Component [1]", location.Label);
        }

        [Test]
        public void Build_GroupsBrokenReferencesByObjectTypeUnderOneRoot()
        {
            var graph = new DependencyGraph();
            AddMissingReference(graph, "material", "Material", "m_Material");
            AddMissingReference(graph, "texture", "Texture2D", "m_Texture");
            AddMissingReference(graph, "mesh", "Mesh", "m_Mesh");
            AddMissingReference(graph, "other", "ReferenceFixtureAsset", "customReference");

            var model = ProjectIssuePanelBuilder.Build(graph);
            var group = model.Groups.Single();

            Assert.AreEqual(ProjectIssueType.BrokenMissingReference, group.Type);
            Assert.AreEqual("Broken Missing Reference", group.Title);
            Assert.AreEqual(4, group.Count);
            CollectionAssert.AreEquivalent(
                new[] { "Material", "Texture", "Mesh", "ReferenceFixtureAsset" },
                group.ObjectGroups.Select(objectGroup => objectGroup.ObjectType));
            Assert.IsTrue(group.ObjectGroups.All(objectGroup => objectGroup.Count == 1));
        }

        [Test]
        public void Build_TreatsSerializedScriptReferenceAsBrokenReference()
        {
            var graph = new DependencyGraph();
            AddMissingReference(graph, "script-reference", "Script", "scriptReference");

            var group = ProjectIssuePanelBuilder.Build(graph).Groups.Single();

            Assert.AreEqual(ProjectIssueType.BrokenMissingReference, group.Type);
            Assert.AreEqual("Script", group.ObjectGroups.Single().ObjectType);
        }

        [Test]
        public void Build_DoesNotCreateIssueForNoneOrAnalyzerDiagnostics()
        {
            var graph = new DependencyGraph();
            graph.AddIssue(new DependencyScanIssue(
                "SerializedPropertyScanner",
                "Scene::Object",
                "Failed to inspect component",
                DependencyScanIssueSeverity.Warning));
            graph.AddIssue(new DependencyScanIssue(
                "Unity Console",
                string.Empty,
                "Console error",
                DependencyScanIssueSeverity.Error));

            var model = ProjectIssuePanelBuilder.Build(graph);

            Assert.AreEqual(0, model.WarningCount);
            Assert.IsEmpty(model.Groups);
        }

        [Test]
        public void Build_CountsMissingScriptAndBrokenReferenceLocations()
        {
            var graph = new DependencyGraph();
            for (var i = 0; i < 2; i++)
            {
                var source = CreateNode("script-owner-" + i, "Scene::ScriptOwner" + i, "ScriptOwner" + i, "Object", DependencyNodeKind.SceneObject);
                var missing = CreateNode("missing-script-" + i, source.Path, "Missing MonoBehaviour", "Script", DependencyNodeKind.Component);
                graph.AddOrUpdateNode(source);
                graph.AddOrUpdateNode(missing);
                graph.AddEdge(new DependencyEdge(source.Id, missing.Id, "Missing Component [0]", DependencyReferenceKind.Component, true));
            }

            for (var i = 0; i < 5; i++)
            {
                AddMissingReference(graph, "broken-" + i, i % 2 == 0 ? "Material" : "Mesh", "reference" + i);
            }

            var model = ProjectIssuePanelBuilder.Build(graph);

            Assert.AreEqual(7, model.WarningCount);
            Assert.AreEqual(2, model.Groups.Single(group => group.Type == ProjectIssueType.MissingScript).Count);
            Assert.AreEqual(5, model.Groups.Single(group => group.Type == ProjectIssueType.BrokenMissingReference).Count);
        }

        [Test]
        public void Build_PreservesDistinctSerializedPropertyPaths()
        {
            var graph = new DependencyGraph();
            var source = CreateNode("source", "Scene::Object/Fixture", "Fixture", "Fixture", DependencyNodeKind.Component);
            graph.AddOrUpdateNode(source);
            AddMissingReference(graph, source, "first", "Material", "materials.Array.data[0]");
            AddMissingReference(graph, source, "second", "Material", "materials.Array.data[1]");

            var locations = ProjectIssuePanelBuilder.Build(graph)
                .Groups.Single()
                .ObjectGroups.Single()
                .Locations;

            Assert.AreEqual(2, locations.Count);
            CollectionAssert.AreEquivalent(
                new[] { "materials.Array.data[0]", "materials.Array.data[1]" },
                locations.Select(location => location.Label));
        }

        [Test]
        public void Build_KeepsBrokenEdgeWithoutSourceAsDisabledLocation()
        {
            var graph = new DependencyGraph();
            var missing = CreateNode("missing", "Scene::Missing", "missing", "Material", DependencyNodeKind.Asset);
            graph.AddOrUpdateNode(missing);
            graph.AddEdge(new DependencyEdge("unresolved-source", missing.Id, "m_Material", DependencyReferenceKind.SerializedProperty, true));

            var location = ProjectIssuePanelBuilder.Build(graph)
                .Groups.Single()
                .ObjectGroups.Single()
                .Locations.Single();

            Assert.IsFalse(location.HasRelatedNode);
            Assert.AreEqual("No related node", location.Label);
        }

        private static void AddMissingReference(
            DependencyGraph graph,
            string id,
            string typeName,
            string memberName)
        {
            var source = CreateNode(
                "source-" + id,
                "Assets/Scenes/Main.unity::Root/" + id + "/Component",
                id,
                "Component",
                DependencyNodeKind.Component);
            graph.AddOrUpdateNode(source);
            AddMissingReference(graph, source, id, typeName, memberName);
        }

        private static void AddMissingReference(
            DependencyGraph graph,
            DependencyNode source,
            string id,
            string typeName,
            string memberName)
        {
            var missing = CreateNode("missing-" + id, source.Path, memberName, typeName, DependencyNodeKind.Asset);
            missing.MarkMissingReferences();
            graph.AddOrUpdateNode(missing);
            graph.AddEdge(new DependencyEdge(
                source.Id,
                missing.Id,
                memberName,
                DependencyReferenceKind.SerializedProperty,
                true));
        }

        private static DependencyNode CreateNode(
            string id,
            string path,
            string displayName,
            string typeName,
            DependencyNodeKind kind)
        {
            return new DependencyNode(
                id,
                default,
                path,
                displayName,
                typeName,
                typeName,
                Array.Empty<string>(),
                "DefaultAsset Icon",
                kind);
        }
    }
}
