using System;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.GraphView;
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
            missing.MarkAsMissingTarget(MissingTargetKind.MissingScript);
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
            CollectionAssert.AreEqual(new[] { "Main", "Player" }, location.ParentSegments);
            Assert.AreEqual("Missing Component [1]", location.Label);
            Assert.AreEqual(source.DisplayName, location.SourceObjectName);
            Assert.IsFalse(location.HasMissingObjectType);
            Assert.AreEqual(DependencyNodeStyleResolver.GetNodeAccentColor(source), location.AccentColor);
            Assert.AreEqual("Path: Main/Player/Missing Component [1]", location.DisplayPath);
        }

        [Test]
        public void Build_GroupsBrokenReferencesByObjectTypeUnderOneRoot()
        {
            var graph = new DependencyGraph();
            AddMissingReference(graph, "material", "Material", "m_Material");
            AddMissingReference(graph, "texture", "Texture2D", "m_Texture");
            AddMissingReference(graph, "mesh", "Mesh", "m_Mesh");
            AddMissingReference(graph, "script", "Script", "scriptReference");
            AddMissingReference(graph, "generic", "Missing Reference", "objectReference");
            AddMissingReference(graph, "other", "ReferenceFixtureAsset", "customReference");

            var model = ProjectIssuePanelBuilder.Build(graph);
            var group = model.Groups.Single();

            Assert.AreEqual(ProjectIssueType.BrokenMissingReference, group.Type);
            Assert.AreEqual("Broken Missing Reference", group.Title);
            Assert.AreEqual(6, group.Count);
            CollectionAssert.AreEquivalent(
                new[] { "Material", "Texture", "Mesh", "Script", "Object", "ReferenceFixtureAsset" },
                group.ObjectGroups.Select(objectGroup => objectGroup.ObjectType));
            Assert.IsTrue(group.ObjectGroups.All(objectGroup => objectGroup.Count == 1));
            AssertBrokenLocationUsesTypeColor(group, "Material", "Material");
            AssertBrokenLocationUsesTypeColor(group, "Texture", "Texture");
            AssertBrokenLocationUsesTypeColor(group, "Mesh", "Mesh");
            AssertBrokenLocationUsesTypeColor(group, "Script", "Script", "cs Script Icon");
            var objectGroup = group.ObjectGroups.Single(candidate => candidate.ObjectType == "Object");
            Assert.AreEqual("issue:broken-missing-reference:type:object", objectGroup.Id);
            var objectLocation = objectGroup.Locations.Single();
            Assert.AreEqual("Object", objectLocation.MissingObjectType);
            Assert.AreEqual(
                DependencyNodeStyleResolver.GetTypeAccentColor("Object"),
                objectLocation.AccentColor);
            Assert.IsFalse(group.ObjectGroups.Any(candidate => candidate.ObjectType == "Object Reference"));
            var materialLocation = group.ObjectGroups
                .Single(objectGroup => objectGroup.ObjectType == "Material")
                .Locations.Single();
            Assert.AreEqual("material", materialLocation.SourceObjectName);
            Assert.AreEqual("Path: Main/Root/material/Component/m_Material", materialLocation.DisplayPath);
        }

        [Test]
        public void Build_TreatsSerializedScriptReferenceAsBrokenReference()
        {
            var graph = new DependencyGraph();
            AddMissingReference(graph, "script-reference", "Script", "scriptReference");

            var group = ProjectIssuePanelBuilder.Build(graph).Groups.Single();

            Assert.AreEqual(ProjectIssueType.BrokenMissingReference, group.Type);
            Assert.AreEqual("Script", group.ObjectGroups.Single().ObjectType);
            var location = group.ObjectGroups.Single().Locations.Single();
            Assert.AreEqual("Script", location.MissingObjectType);
            Assert.AreEqual(
                DependencyNodeStyleResolver.GetNodeAccentColor(
                    CreateNode("style-script", string.Empty, "Script", "Script", DependencyNodeKind.Asset, "cs Script Icon")),
                location.AccentColor);
        }

        [TestCase("Object")]
        [TestCase("Object Reference")]
        [TestCase("Missing")]
        [TestCase("Missing Reference")]
        public void Build_NormalizesGenericMissingTypesToObject(string typeName)
        {
            var graph = new DependencyGraph();
            AddMissingReference(graph, "generic", typeName, "objectReference");

            var objectGroup = ProjectIssuePanelBuilder.Build(graph)
                .Groups.Single()
                .ObjectGroups.Single();
            var location = objectGroup.Locations.Single();

            Assert.AreEqual("Object", objectGroup.ObjectType);
            Assert.AreEqual("issue:broken-missing-reference:type:object", objectGroup.Id);
            Assert.AreEqual("Object", location.MissingObjectType);
            Assert.AreEqual(
                DependencyNodeStyleResolver.GetTypeAccentColor("Object"),
                location.AccentColor);
        }

        [Test]
        public void Build_UsesObjectWhenMissingTargetMetadataIsUnavailable()
        {
            var graph = new DependencyGraph();
            var source = CreateNode(
                "source",
                "Assets/Scenes/Main.unity::Root/Component",
                "Component",
                "Component",
                DependencyNodeKind.Component);
            graph.AddOrUpdateNode(source);
            graph.AddEdge(new DependencyEdge(
                source.Id,
                "unresolved-missing-target",
                "objectReference",
                DependencyReferenceKind.SerializedProperty,
                true));

            var objectGroup = ProjectIssuePanelBuilder.Build(graph)
                .Groups.Single()
                .ObjectGroups.Single();

            Assert.AreEqual("Object", objectGroup.ObjectType);
            Assert.AreEqual("issue:broken-missing-reference:type:object", objectGroup.Id);
            Assert.AreEqual(
                DependencyNodeStyleResolver.GetTypeAccentColor("Object"),
                objectGroup.Locations.Single().AccentColor);
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
                missing.MarkAsMissingTarget(MissingTargetKind.MissingScript);
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
            Assert.AreEqual("No related node", location.SourceObjectName);
            Assert.AreEqual("Material", location.MissingObjectType);
            Assert.AreEqual(
                DependencyNodeStyleResolver.GetNodeAccentColor(
                    CreateNode("style-material", string.Empty, "Material", "Material", DependencyNodeKind.Asset)),
                location.AccentColor);
            Assert.AreEqual("No related node", location.Label);
        }

        private static void AssertBrokenLocationUsesTypeColor(
            ProjectIssueGroup group,
            string objectType,
            string styleTypeName,
            string iconContentName = "DefaultAsset Icon")
        {
            var location = group.ObjectGroups
                .Single(objectGroup => objectGroup.ObjectType == objectType)
                .Locations.Single();
            var styleNode = CreateNode(
                "style-" + objectType,
                string.Empty,
                objectType,
                styleTypeName,
                DependencyNodeKind.Asset,
                iconContentName);

            Assert.AreEqual(objectType, location.MissingObjectType);
            Assert.AreEqual(DependencyNodeStyleResolver.GetNodeAccentColor(styleNode), location.AccentColor);
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
            missing.MarkAsMissingTarget(MissingTargetKind.BrokenReference);
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
            DependencyNodeKind kind,
            string iconContentName = "DefaultAsset Icon")
        {
            return new DependencyNode(
                id,
                default,
                path,
                displayName,
                typeName,
                typeName,
                Array.Empty<string>(),
                iconContentName,
                kind);
        }
    }
}
