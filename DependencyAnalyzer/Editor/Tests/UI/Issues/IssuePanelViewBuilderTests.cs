using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.Issues;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class IssuePanelViewBuilderTests
    {
        [Test]
        public void OccurrenceRow_PlacesNodeIconBeforeSeverityIcon()
        {
            var nodeIconTexture = new Texture2D(1, 1);
            try
            {
                var group = CreateGroup(CreateOccurrence("node", nodeIconTexture));

                var view = IssuePanelViewBuilder.CreateGroupView(group, true, null, null);
                var row = view.Q<VisualElement>(className: "dependency-issue-occurrence-row");
                var nodeIcon = row.Q<Image>(className: "dependency-issue-node-icon");
                var severityIcon = row.Q<Image>(className: "dependency-issue-severity-icon");

                Assert.IsNotNull(nodeIcon);
                Assert.IsNotNull(severityIcon);
                Assert.Less(row.IndexOf(nodeIcon), row.IndexOf(severityIcon));
            }
            finally
            {
                Object.DestroyImmediate(nodeIconTexture);
            }
        }

        [Test]
        public void NoRelatedNodeRow_IsDisabled()
        {
            var group = CreateGroup(CreateOccurrence(string.Empty, null));
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                true,
                null,
                null);
            var row = view.Q<VisualElement>(className: "dependency-issue-occurrence-row");

            Assert.IsFalse(row.enabledSelf);
            Assert.AreEqual("No related node", row.Q<Label>(className: "dependency-issue-occurrence-label").text);
        }

        [Test]
        public void RelatedOccurrenceRow_IsEnabled()
        {
            var group = CreateGroup(CreateOccurrence("target", null));
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                true,
                null,
                null);
            var row = view.Q<VisualElement>(className: "dependency-issue-occurrence-row");

            Assert.IsTrue(row.enabledSelf);
        }

        [Test]
        public void CollapsedGroup_HidesAffectedLocations()
        {
            var group = CreateGroup(CreateOccurrence("target", null));
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                false,
                null,
                null);

            Assert.IsNotNull(view.Q<VisualElement>(className: "dependency-issue-group-row"));
            Assert.IsNull(view.Q<VisualElement>(className: "dependency-issue-location-list"));
        }

        private static IssueGroup CreateGroup(IssueOccurrence occurrence)
        {
            return new IssueGroup(
                "group",
                "Missing Reference: Material",
                DependencyScanIssueSeverity.Warning,
                IssueOrigin.Analyzer,
                null,
                new[] { occurrence });
        }

        private static IssueOccurrence CreateOccurrence(string targetNodeId, Texture nodeIcon)
        {
            return new IssueOccurrence(
                "Missing Reference/Material",
                "Missing Reference/Material",
                "Missing Reference: Material",
                "Missing serialized reference",
                DependencyScanIssueSeverity.Warning,
                targetNodeId,
                nodeIcon,
                Color.gray,
                null,
                IssueOrigin.Analyzer,
                new IssueLocation(null, string.IsNullOrEmpty(targetNodeId) ? "No related node" : "m_Material"),
                1);
        }

    }
}
