using System;
using System.IO;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.GraphView;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class DependencyNodeViewTests
    {
        [Test]
        public void Tooltip_KeepsReferenceCounts()
        {
            var node = CreateNode(DependencyNodeKind.Asset, Array.Empty<string>());
            node.SetReferenceCounts(3, 4);

            var view = CreateView(node);

            StringAssert.Contains("Dependencies: 3", view.tooltip);
            StringAssert.Contains("Used By: 4", view.tooltip);
        }

        [Test]
        public void Tooltip_OmitsAssetLabelsWhenAssetHasNoNonEmptyLabels()
        {
            var node = CreateNode(DependencyNodeKind.Asset, new[] { null, string.Empty, " " });

            var view = CreateView(node);

            StringAssert.DoesNotContain("Asset Labels", view.tooltip);
            StringAssert.DoesNotContain("(none)", view.tooltip);
        }

        [Test]
        public void Tooltip_ShowsOnlyNonEmptyLabelsForAsset()
        {
            var node = CreateNode(DependencyNodeKind.Asset, new[] { string.Empty, " Gameplay ", "Reviewed" });

            var view = CreateView(node);

            StringAssert.Contains("Asset Labels: Gameplay, Reviewed", view.tooltip);
        }

        [Test]
        public void Tooltip_OmitsAssetLabelsForNonAssetNode()
        {
            var node = CreateNode(DependencyNodeKind.Component, new[] { "ShouldNotAppear" });

            var view = CreateView(node);

            StringAssert.DoesNotContain("Asset Labels", view.tooltip);
            StringAssert.DoesNotContain("ShouldNotAppear", view.tooltip);
        }

        [Test]
        public void NodeContent_DoesNotCreateDependencyOrUsedByBadges()
        {
            var node = CreateNode(DependencyNodeKind.Asset, Array.Empty<string>());
            node.SetReferenceCounts(8, 9);

            var view = CreateView(node);

            Assert.IsNull(view.Q<Label>(className: "dependency-node-badge"));
            Assert.IsNull(view.Q(className: "dependency-node-badges"));
            StringAssert.Contains("Dependencies: 8", view.tooltip);
            StringAssert.Contains("Used By: 9", view.tooltip);
        }

        [Test]
        public void MissingTarget_DoesNotCreateWarningMarker()
        {
            var node = CreateNode(DependencyNodeKind.Asset, Array.Empty<string>());
            node.MarkAsMissingTarget(MissingTargetKind.BrokenReference);

            var view = CreateView(node);

            Assert.IsNull(view.Q<Image>(className: "dependency-node-issue-marker"));
            Assert.IsTrue(view.ClassListContains(DependencyNodeStyleResolver.MissingTargetClass));
        }

        [Test]
        public void DirectMissingSource_CreatesWarningMarkerWithoutMissingTargetStyle()
        {
            var node = CreateNode(DependencyNodeKind.Component, Array.Empty<string>());
            node.MarkMissingReferences();

            var view = CreateView(node);

            Assert.NotNull(view.Q<Image>(className: "dependency-node-issue-marker"));
            Assert.IsFalse(view.ClassListContains(DependencyNodeStyleResolver.MissingTargetClass));
        }

        [TestCase("Script", "cs Script Icon", "dependency-node--csharp")]
        [TestCase("Material", "Material Icon", "dependency-node--material")]
        [TestCase("Texture2D", "Texture Icon", "dependency-node--texture")]
        [TestCase("Mesh", "Mesh Icon", "dependency-node--mesh")]
        [TestCase("Object Reference", "DefaultAsset Icon", "dependency-node--default")]
        public void MissingTarget_KeepsObjectTypeClass(
            string typeName,
            string iconContentName,
            string expectedTypeClass)
        {
            var node = CreateNode(
                DependencyNodeKind.Asset,
                Array.Empty<string>(),
                typeName,
                iconContentName);
            node.MarkAsMissingTarget(MissingTargetKind.BrokenReference);

            var view = CreateView(node);

            Assert.IsTrue(view.ClassListContains(expectedTypeClass));
            Assert.IsTrue(view.ClassListContains(DependencyNodeStyleResolver.MissingTargetClass));
            Assert.AreEqual(DependencyNodeStyleResolver.MissingTargetOpacity, view.TargetOpacity);
        }

        [Test]
        public void MissingScript_KeepsScriptTypeClassAndOpacity()
        {
            var node = CreateNode(
                DependencyNodeKind.Component,
                Array.Empty<string>(),
                "Script",
                "cs Script Icon");
            node.MarkAsMissingTarget(MissingTargetKind.MissingScript);

            var view = CreateView(node);

            Assert.IsTrue(view.ClassListContains("dependency-node--csharp"));
            Assert.IsTrue(view.ClassListContains(DependencyNodeStyleResolver.MissingTargetClass));
            Assert.AreEqual(0.45f, view.TargetOpacity);
            Assert.AreEqual(0.45f, view.style.opacity.value);
        }

        [Test]
        public void NodeOpacity_UsesResolverAsSingleSourceOfTruth()
        {
            var missingNode = CreateNode(DependencyNodeKind.Asset, Array.Empty<string>());
            missingNode.MarkAsMissingTarget(MissingTargetKind.BrokenReference);
            var missingView = CreateView(missingNode);
            var normalView = CreateView(CreateNode(DependencyNodeKind.Asset, Array.Empty<string>()));
            var styleText = File.ReadAllText(
                "Assets/DependencyAnalyzer/Editor/UI/Styles/NodeStyle.uss").Replace("\r\n", "\n");

            Assert.AreEqual(0.45f, DependencyNodeStyleResolver.MissingTargetOpacity);
            Assert.AreEqual(0.45f, missingView.TargetOpacity);
            Assert.AreEqual(0.45f, missingView.style.opacity.value);
            Assert.AreEqual(1f, normalView.TargetOpacity);
            Assert.AreEqual(1f, normalView.style.opacity.value);
            StringAssert.DoesNotContain(".dependency-node--missing-target", styleText);
        }

        private static DependencyNode CreateNode(
            DependencyNodeKind kind,
            string[] labels,
            string typeName = null,
            string iconContentName = "DefaultAsset Icon")
        {
            return new DependencyNode(
                "node",
                default,
                kind == DependencyNodeKind.Asset ? "Assets/Test.asset" : "Scene/Object/Component",
                "Test Node",
                typeName ?? kind.ToString(),
                typeName == null ? "Test." + kind : typeName,
                labels,
                iconContentName,
                kind);
        }

        private static DependencyNodeView CreateView(DependencyNode node)
        {
            return new DependencyNodeView(
                "view",
                node,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                DependencyScanIssueSeverity.Warning,
                string.Empty,
                1f,
                () => 1f);
        }
    }
}
