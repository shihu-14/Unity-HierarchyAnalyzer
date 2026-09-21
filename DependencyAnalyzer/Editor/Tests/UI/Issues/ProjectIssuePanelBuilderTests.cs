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
        public void Build_UnifiesMissingComponentAndSerializedScriptUnderScript()
        {
            var graph = new DependencyGraph();
            var componentOwner = CreateNode(
                "component-owner",
                "Assets/Scenes/Main.unity::Player",
                "Player",
                "Object",
                DependencyNodeKind.SceneObject);
            var missingComponent = CreateNode(
                "missing-component",
                componentOwner.Path,
                "Missing MonoBehaviour",
                "Script",
                DependencyNodeKind.Component,
                "cs Script Icon",
                "UnityEngine.MonoBehaviour");
            missingComponent.MarkAsMissingTarget(MissingTargetKind.MissingScript);
            graph.AddOrUpdateNode(componentOwner);
            graph.AddOrUpdateNode(missingComponent);
            graph.AddEdge(new DependencyEdge(
                componentOwner.Id,
                missingComponent.Id,
                "Missing Component [1]",
                DependencyReferenceKind.Component,
                true));

            var serializedOwner = CreateNode(
                "serialized-owner",
                "Assets/Scenes/Main.unity::Enemy/EnemyController",
                "EnemyController (Script)",
                "Script",
                DependencyNodeKind.Component,
                "cs Script Icon");
            graph.AddOrUpdateNode(serializedOwner);
            AddMissingReference(
                graph,
                serializedOwner,
                "serialized-script",
                "Script",
                "scriptReference",
                "cs Script Icon",
                "UnityEditor.MonoScript");

            var model = ProjectIssuePanelBuilder.Build(graph);
            var group = model.Groups.Single();

            Assert.AreEqual("issue:type:script", group.Id);
            Assert.AreEqual("Script", group.ObjectType);
            Assert.AreEqual(2, group.Count);
            Assert.AreEqual(2, model.WarningCount);
            Assert.AreSame(UnityEditor.EditorGUIUtility.IconContent("cs Script Icon").image, group.Icon);
            Assert.AreEqual(
                DependencyNodeStyleResolver.GetTypeAccentColor("Script"),
                group.AccentColor);
            CollectionAssert.AreEquivalent(
                new[] { "Missing Component [1]", "scriptReference" },
                group.Locations.Select(location => location.Label));
            Assert.AreEqual(
                componentOwner.Id,
                group.Locations.Single(location => location.Label.Contains("Missing Component")).TargetNodeId);
        }

        [Test]
        public void Build_GroupsMissingEdgesDirectlyByCanonicalObjectType()
        {
            var graph = new DependencyGraph();
            AddMissingReference(graph, "material", "Material", "m_Material", "Material Icon");
            AddMissingReference(graph, "texture", "Texture2D", "m_Texture", "Texture Icon");
            AddMissingReference(graph, "mesh", "Mesh", "m_Mesh", "Mesh Icon");
            AddMissingReference(graph, "audio", "AudioClip", "m_Clip", "AudioClip Icon");
            AddMissingReference(
                graph,
                "game-object",
                "GameObject",
                "target",
                "GameObject Icon",
                "UnityEngine.GameObject");
            AddMissingReference(
                graph,
                "object-reference",
                "Object",
                "reference",
                "DefaultAsset Icon",
                "UnityEngine.Object");
            AddMissingReference(graph, "unknown", "Missing Reference", "unknownReference");

            var model = ProjectIssuePanelBuilder.Build(graph);

            CollectionAssert.AreEqual(
                new[]
                {
                    "AudioClip",
                    "GameObject",
                    "Material",
                    "Mesh",
                    "Object Reference",
                    "Texture",
                    "Unknown Reference"
                },
                model.Groups.Select(group => group.ObjectType));
            Assert.IsTrue(model.Groups.All(group => group.Count == 1));
            Assert.AreEqual(7, model.WarningCount);
            Assert.AreEqual(
                "issue:type:gameobject",
                model.Groups.Single(group => group.ObjectType == "GameObject").Id);
            Assert.AreEqual(
                "issue:type:object-reference",
                model.Groups.Single(group => group.ObjectType == "Object Reference").Id);
            Assert.AreEqual(
                "issue:type:unknown-reference",
                model.Groups.Single(group => group.ObjectType == "Unknown Reference").Id);
        }

        [TestCase("Texture2D", "Texture")]
        [TestCase("Texture3D", "Texture")]
        [TestCase("Cubemap", "Texture")]
        [TestCase("MonoScript", "Script")]
        [TestCase("MonoBehaviour", "Script")]
        [TestCase("RuntimeAnimatorController", "Animator")]
        [TestCase("AnimatorController", "Animator")]
        public void Build_NormalizesKnownObjectTypeAliases(string typeName, string expectedType)
        {
            var graph = new DependencyGraph();
            AddMissingReference(graph, "missing", typeName, "reference");

            Assert.AreEqual(expectedType, ProjectIssuePanelBuilder.Build(graph).Groups.Single().ObjectType);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Unknown")]
        [TestCase("Missing")]
        [TestCase("Missing Reference")]
        public void Build_UsesUnknownReferenceWhenTypeMetadataIsUnavailable(string typeName)
        {
            var graph = new DependencyGraph();
            AddMissingReference(graph, "unknown", typeName, "reference");

            var group = ProjectIssuePanelBuilder.Build(graph).Groups.Single();

            Assert.AreEqual("Unknown Reference", group.ObjectType);
            Assert.AreEqual(
                DependencyNodeStyleResolver.GetTypeAccentColor("Unknown Reference"),
                group.AccentColor);
        }

        [Test]
        public void Build_UsesUnknownReferenceWhenMissingTargetNodeIsUnavailable()
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

            var group = ProjectIssuePanelBuilder.Build(graph).Groups.Single();

            Assert.AreEqual("Unknown Reference", group.ObjectType);
            Assert.IsNotNull(group.Icon);
        }

        [Test]
        public void Build_DoesNotCreateGroupsForNoneOrAnalyzerDiagnostics()
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
        public void Build_WarningCountEqualsAllMissingEdgeLocations()
        {
            var graph = new DependencyGraph();
            for (var i = 0; i < 2; i++)
            {
                AddMissingReference(
                    graph,
                    "script-" + i,
                    "Script",
                    "scriptReference" + i,
                    "cs Script Icon");
            }

            for (var i = 0; i < 5; i++)
            {
                AddMissingReference(
                    graph,
                    "broken-" + i,
                    i % 2 == 0 ? "Material" : "Mesh",
                    "reference" + i);
            }

            var model = ProjectIssuePanelBuilder.Build(graph);

            Assert.AreEqual(7, model.WarningCount);
            Assert.AreEqual(7, model.Groups.Sum(group => group.Locations.Count));
            Assert.AreEqual(2, model.Groups.Single(group => group.ObjectType == "Script").Count);
            Assert.AreEqual(3, model.Groups.Single(group => group.ObjectType == "Material").Count);
            Assert.AreEqual(2, model.Groups.Single(group => group.ObjectType == "Mesh").Count);
        }

        [Test]
        public void Build_PreservesDistinctSerializedPropertyPaths()
        {
            var graph = new DependencyGraph();
            var source = CreateNode(
                "source",
                "Assets/Scenes/Main.unity::Object/Fixture",
                "Fixture",
                "Fixture",
                DependencyNodeKind.Component);
            graph.AddOrUpdateNode(source);
            AddMissingReference(graph, source, "first", "Material", "materials.Array.data[0]");
            AddMissingReference(graph, source, "second", "Material", "materials.Array.data[1]");

            var locations = ProjectIssuePanelBuilder.Build(graph).Groups.Single().Locations;

            Assert.AreEqual(2, locations.Count);
            CollectionAssert.AreEquivalent(
                new[] { "materials.Array.data[0]", "materials.Array.data[1]" },
                locations.Select(location => location.Label));
        }

        [Test]
        public void Build_KeepsMissingEdgeWithoutSourceAsDisabledLocation()
        {
            var graph = new DependencyGraph();
            var missing = CreateNode(
                "missing",
                "Scene::Missing",
                "missing",
                "Material",
                DependencyNodeKind.Asset,
                "Material Icon");
            missing.MarkAsMissingTarget(MissingTargetKind.BrokenReference);
            graph.AddOrUpdateNode(missing);
            graph.AddEdge(new DependencyEdge(
                "unresolved-source",
                missing.Id,
                "m_Material",
                DependencyReferenceKind.SerializedProperty,
                true));

            var location = ProjectIssuePanelBuilder.Build(graph).Groups.Single().Locations.Single();

            Assert.IsFalse(location.HasRelatedNode);
            Assert.AreEqual("No related node", location.SourceObjectName);
            Assert.AreEqual("No related node", location.Label);
        }

        [Test]
        public void Build_UsesTargetMetadataForGroupColorAndIcon()
        {
            var graph = new DependencyGraph();
            AddMissingReference(graph, "material", "Material", "m_Material", "Material Icon");

            var group = ProjectIssuePanelBuilder.Build(graph).Groups.Single();

            Assert.AreEqual(
                DependencyNodeStyleResolver.GetTypeAccentColor("Material"),
                group.AccentColor);
            Assert.AreSame(
                UnityEditor.EditorGUIUtility.IconContent("Material Icon").image,
                group.Icon);
        }

        private static void AddMissingReference(
            DependencyGraph graph,
            string id,
            string typeName,
            string memberName,
            string iconContentName = "DefaultAsset Icon",
            string namespaceQualifiedTypeName = null)
        {
            var source = CreateNode(
                "source-" + id,
                "Assets/Scenes/Main.unity::Root/" + id + "/Component",
                id,
                "Component",
                DependencyNodeKind.Component);
            graph.AddOrUpdateNode(source);
            AddMissingReference(
                graph,
                source,
                id,
                typeName,
                memberName,
                iconContentName,
                namespaceQualifiedTypeName);
        }

        private static void AddMissingReference(
            DependencyGraph graph,
            DependencyNode source,
            string id,
            string typeName,
            string memberName,
            string iconContentName = "DefaultAsset Icon",
            string namespaceQualifiedTypeName = null)
        {
            var missing = CreateNode(
                "missing-" + id,
                source.Path,
                memberName,
                typeName,
                DependencyNodeKind.Asset,
                iconContentName,
                namespaceQualifiedTypeName);
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
            string iconContentName = "DefaultAsset Icon",
            string namespaceQualifiedTypeName = null)
        {
            return new DependencyNode(
                id,
                default,
                path,
                displayName,
                typeName,
                namespaceQualifiedTypeName ?? typeName,
                Array.Empty<string>(),
                iconContentName,
                kind);
        }
    }
}
