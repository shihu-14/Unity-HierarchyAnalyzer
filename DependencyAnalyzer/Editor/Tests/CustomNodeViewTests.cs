using System;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.GraphView;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class CustomNodeViewTests
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

        private static DependencyNodeData CreateNode(DependencyNodeKind kind, string[] labels)
        {
            return new DependencyNodeData(
                "node",
                default,
                kind == DependencyNodeKind.Asset ? "Assets/Test.asset" : "Scene/Object/Component",
                "Test Node",
                kind.ToString(),
                "Test." + kind,
                labels,
                "DefaultAsset Icon",
                kind);
        }

        private static CustomNodeView CreateView(DependencyNodeData node)
        {
            return new CustomNodeView(
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
