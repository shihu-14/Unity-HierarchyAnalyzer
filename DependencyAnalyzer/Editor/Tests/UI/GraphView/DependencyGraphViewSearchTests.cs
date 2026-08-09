using System;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.GraphView;
using NUnit.Framework;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class DependencyGraphViewSearchTests
    {
        [TestCase("Audio Source", "Audio Source")]
        [TestCase("Audio Source Cluster", "Audio Source")]
        [TestCase("My Audio Source Object", "Audio Source")]
        [TestCase("Audio Source", "audio source")]
        [TestCase("Audio   Source", "Audio   Source")]
        [TestCase("Audio Source", "  Audio Source  ")]
        public void SetSearch_MatchesContinuousDisplayNameSubstring(string displayName, string query)
        {
            var graphView = CreateGraphView(CreateNode("candidate", displayName));

            var state = graphView.SetSearch(query, false, false);

            Assert.AreEqual(1, state.Total);
            Assert.AreEqual("candidate", state.Suggestions[0].NodeId);
        }

        [TestCase("Source Audio", "Audio Source")]
        [TestCase("Audio Something Source", "Audio Source")]
        [TestCase("Audio Source", "Audio   Source")]
        public void SetSearch_DoesNotMatchNonContinuousDisplayName(string displayName, string query)
        {
            var graphView = CreateGraphView(CreateNode("candidate", displayName));

            var state = graphView.SetSearch(query, false, false);

            Assert.AreEqual(0, state.Total);
        }

        [Test]
        public void SetSearch_DoesNotMatchPathTypeNamespaceLabelsKindOrMissingState()
        {
            var pathOnly = CreateNode("path", "Weapon", path: "Scene/Audio Source/Weapon");
            var typeOnly = CreateNode(
                "type",
                "Player",
                typeName: "Audio Source",
                namespaceQualifiedTypeName: "Example.Audio Source");
            var labelOnly = CreateNode("label", "Character", labels: new[] { "Audio Source" });
            var kindOnly = CreateNode("kind", "Graph Entry", kind: DependencyNodeKind.Asset);
            var missingOnly = CreateNode("missing", "Broken Reference");
            missingOnly.MarkAsMissingTarget(MissingTargetKind.BrokenReference);
            var graphView = CreateGraphView(pathOnly, typeOnly, labelOnly, kindOnly, missingOnly);

            Assert.AreEqual(0, graphView.SetSearch("Audio Source", false, false).Total);
            Assert.AreEqual(0, graphView.SetSearch("Asset", false, false).Total);
            Assert.AreEqual(0, graphView.SetSearch("missing:true", false, false).Total);
        }

        [Test]
        public void SetSearch_TreatsScopedSyntaxAsLiteralDisplayNameText()
        {
            var typeMetadataOnly = CreateNode("metadata", "Player", typeName: "Audio");
            var literalName = CreateNode("literal", "type:Audio Controller");
            var graphView = CreateGraphView(typeMetadataOnly, literalName);

            var state = graphView.SetSearch("type:Audio", false, false);

            Assert.AreEqual(1, state.Total);
            Assert.AreEqual(literalName.Id, state.Suggestions[0].NodeId);
        }

        [Test]
        public void SearchNavigation_RemainsCyclicForContinuousNameMatches()
        {
            var first = CreateNode("first", "Audio Source");
            var second = CreateNode("second", "Audio Source Cluster");
            var third = CreateNode("third", "My Audio Source");
            var graphView = CreateGraphView(first, second, third);

            var initial = graphView.SetSearch("Audio Source", false, false);
            var next = graphView.FocusNextSearchResult(false);
            var previous = graphView.FocusNextSearchResult(true);
            var wrappedPrevious = graphView.FocusNextSearchResult(true);

            Assert.AreEqual(3, initial.Total);
            Assert.AreEqual(0, initial.CurrentIndex);
            Assert.AreEqual(1, next.CurrentIndex);
            Assert.AreEqual(0, previous.CurrentIndex);
            Assert.AreEqual(2, wrappedPrevious.CurrentIndex);
        }

        private static DependencyGraphView CreateGraphView(params DependencyNode[] candidates)
        {
            var graph = new DependencyGraph();
            var root = new DependencyNode(
                "root",
                default,
                "Scene::Hierarchy Root",
                "Hierarchy Root",
                "GameObject",
                "UnityEngine.GameObject",
                Array.Empty<string>(),
                "GameObject Icon",
                DependencyNodeKind.SceneObject,
                1001);
            graph.AddOrUpdateNode(root);
            for (var i = 0; i < candidates.Length; i++)
            {
                graph.AddOrUpdateNode(candidates[i]);
                graph.AddEdge(new DependencyEdge(
                    root.Id,
                    candidates[i].Id,
                    string.Empty,
                    DependencyReferenceKind.Component));
            }

            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 4);
            return graphView;
        }

        private static DependencyNode CreateNode(
            string id,
            string displayName,
            string path = null,
            string typeName = "FixtureComponent",
            string namespaceQualifiedTypeName = "Example.FixtureComponent",
            string[] labels = null,
            DependencyNodeKind kind = DependencyNodeKind.Component)
        {
            return new DependencyNode(
                id,
                default,
                path ?? "Scene::" + displayName,
                displayName,
                typeName,
                namespaceQualifiedTypeName,
                labels ?? Array.Empty<string>(),
                "cs Script Icon",
                kind);
        }
    }
}
