using System;
using System.Collections.Generic;
using System.Reflection;
using DependencyAnalyzer.Editor.UI.Issues;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class IssuePanelViewBuilderTests
    {
        private const string GraphWindowUxmlPath = "Assets/DependencyAnalyzer/Editor/UI/Styles/GraphWindow.uxml";

        [Test]
        public void BrokenReferenceView_CreatesIssueTypeObjectTypeAndLocationHierarchy()
        {
            var objectGroup = CreateObjectGroup(CreateLocation("target"));
            var group = CreateBrokenReferenceGroup(objectGroup);
            var expanded = new HashSet<string> { group.Id, objectGroup.Id };

            var view = IssuePanelViewBuilder.CreateGroupView(group, expanded, null, null);

            Assert.IsNotNull(view.Q<VisualElement>(className: "dependency-issue-group-row"));
            Assert.IsNotNull(view.Q<VisualElement>(className: "dependency-issue-object-group-row"));
            Assert.IsNotNull(view.Q<VisualElement>(className: "dependency-issue-location-row"));
        }

        [Test]
        public void LocationRow_ShowsDisplayPathWithoutSegmentBranches()
        {
            var location = CreateLocation("target");
            var objectGroup = CreateObjectGroup(location);
            var group = CreateBrokenReferenceGroup(objectGroup);
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id, objectGroup.Id },
                null,
                null);

            var label = view.Q<Label>(className: "dependency-issue-location-label");

            Assert.AreEqual(location.DisplayPath, label.text);
            Assert.IsNull(view.Q<VisualElement>(className: "dependency-issue-location-branch"));
            Assert.AreEqual(1, view.Query<VisualElement>(className: "dependency-issue-location-row").ToList().Count);
        }

        [Test]
        public void MissingScriptView_ShowsLocationsWithoutObjectTypeGroup()
        {
            var location = CreateLocation("target");
            var group = new ProjectIssueGroup(
                "issue:missing-script",
                ProjectIssueType.MissingScript,
                "Missing Script",
                new[] { location },
                null);

            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id },
                null,
                null);

            Assert.IsNull(view.Q<VisualElement>(className: "dependency-issue-object-group-row"));
            var locationRow = view.Q<VisualElement>(className: "dependency-issue-location-row");
            Assert.IsNotNull(locationRow);
            Assert.AreEqual(
                location.DisplayPath,
                locationRow.Q<Label>(className: "dependency-issue-location-label").text);
        }

        [Test]
        public void LocationRow_PlacesAccentSquareImmediatelyBeforePathWithoutInlineBorder()
        {
            var objectGroup = CreateObjectGroup(CreateLocation("target"));
            var group = CreateBrokenReferenceGroup(objectGroup);
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id, objectGroup.Id },
                null,
                null);
            var row = view.Q<VisualElement>(className: "dependency-issue-location-row");
            var accent = row.Q<VisualElement>(className: "dependency-issue-location-accent");
            var label = row.Q<Label>(className: "dependency-issue-location-label");

            Assert.IsNotNull(accent);
            Assert.IsNotNull(label);
            Assert.AreEqual("Scene: Main/Player/MeshRenderer/m_Material", label.text);
            Assert.AreEqual(Color.cyan, accent.style.backgroundColor.value);
            Assert.AreEqual(row.IndexOf(accent) + 1, row.IndexOf(label));
            Assert.AreEqual(StyleKeyword.Null, row.style.borderLeftColor.keyword);
        }

        [Test]
        public void LocationRow_ClickInvokesFocusCallbackWithTargetNodeId()
        {
            var objectGroup = CreateObjectGroup(CreateLocation("target-node"));
            var group = CreateBrokenReferenceGroup(objectGroup);
            var focusedNodeId = string.Empty;
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id, objectGroup.Id },
                null,
                nodeId => focusedNodeId = nodeId);
            var row = view.Q<Button>(className: "dependency-issue-location-row");

            SimulateClick(row);

            Assert.AreEqual("target-node", focusedNodeId);
        }

        [Test]
        public void LocationWithoutRelatedNode_IsDisabled()
        {
            var objectGroup = CreateObjectGroup(CreateLocation(string.Empty));
            var group = CreateBrokenReferenceGroup(objectGroup);
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id, objectGroup.Id },
                null,
                null);
            var row = view.Q<VisualElement>(className: "dependency-issue-location-row");

            Assert.IsFalse(row.enabledSelf);
            Assert.AreEqual(
                "Scene: Main/Player/MeshRenderer/No related node",
                row.Q<Label>(className: "dependency-issue-location-label").text);
        }

        [Test]
        public void CollapsedObjectType_HidesLocations()
        {
            var objectGroup = CreateObjectGroup(CreateLocation("target"));
            var group = CreateBrokenReferenceGroup(objectGroup);
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id },
                null,
                null);

            Assert.IsNotNull(view.Q<VisualElement>(className: "dependency-issue-object-group-row"));
            Assert.IsNull(view.Q<VisualElement>(className: "dependency-issue-location-row"));
        }

        [Test]
        public void ConfigureWarningStatus_ShowsWarningCountWithoutButtonBehavior()
        {
            var icon = new Image();
            var count = new Label();

            IssuePanelViewBuilder.ConfigureWarningStatus(icon, count, 7);

            Assert.IsNotNull(icon.image);
            Assert.AreEqual("7", count.text);
        }

        [Test]
        public void GraphWindowLayout_HasWarningStatusWithoutTotalOrSeverityFilters()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GraphWindowUxmlPath);
            Assert.IsNotNull(visualTree);
            var root = visualTree.CloneTree();
            var status = root.Q<VisualElement>("issue-warning-status");

            Assert.IsNotNull(status);
            Assert.IsFalse(status is Button);
            Assert.IsNotNull(root.Q<Image>("issue-warning-icon"));
            Assert.IsNotNull(root.Q<Label>("issue-warning-count-label"));
            Assert.IsNull(root.Q<Label>("issue-total-count-label"));
            Assert.IsNull(root.Q<Button>("issue-error-filter-button"));
            Assert.IsNull(root.Q<Button>("issue-warning-filter-button"));
        }

        private static ProjectIssueGroup CreateBrokenReferenceGroup(ProjectIssueObjectGroup objectGroup)
        {
            return new ProjectIssueGroup(
                "issue:broken-missing-reference",
                ProjectIssueType.BrokenMissingReference,
                "Broken Missing Reference",
                null,
                new[] { objectGroup });
        }

        private static ProjectIssueObjectGroup CreateObjectGroup(ProjectIssueLocation location)
        {
            return new ProjectIssueObjectGroup(
                "issue:broken-missing-reference:type:material",
                "Material",
                null,
                new[] { location });
        }

        private static ProjectIssueLocation CreateLocation(string targetNodeId)
        {
            return new ProjectIssueLocation(
                new[] { "Scene: Main", "Player", "MeshRenderer" },
                string.IsNullOrEmpty(targetNodeId) ? "No related node" : "m_Material",
                targetNodeId,
                Color.cyan);
        }

        private static void SimulateClick(Button button)
        {
            var method = typeof(Clickable).GetMethod(
                "SimulateSingleClick",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Clickable.SimulateSingleClick was not available in this Unity version.");
            var parameters = method.GetParameters();
            var arguments = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                arguments[i] = parameters[i].ParameterType.IsValueType
                    ? Activator.CreateInstance(parameters[i].ParameterType)
                    : null;
            }

            method.Invoke(button.clickable, arguments);
        }
    }
}
