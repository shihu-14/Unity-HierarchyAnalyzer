using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DependencyAnalyzer.Editor.UI.Controls;
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
        private const string IssuePanelStylePath = "Assets/DependencyAnalyzer/Editor/UI/Styles/IssuePanelStyle.uss";

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
            Assert.IsNotNull(view.Q<VisualElement>(className: "dependency-issue-location-list--object-child"));
            Assert.IsNull(view.Q<VisualElement>(className: "dependency-issue-location-list--issue-child"));
        }

        [Test]
        public void IssuePanelChevrons_UseHeaderDrawingSettingsForEveryGroupLevel()
        {
            var headerButton = new Button();
            ChevronIcon.SetIssuePanelButtonIcon(headerButton, true);
            var objectGroup = CreateObjectGroup(CreateLocation("target"));
            var group = CreateBrokenReferenceGroup(objectGroup);
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id, objectGroup.Id },
                null,
                null);

            var headerChevron = headerButton.Q<ChevronIcon>();
            var groupChevron = view.Q<ChevronIcon>(className: "dependency-issue-group-chevron");
            var objectChevron = view.Q<ChevronIcon>(className: "dependency-issue-object-group-chevron");

            Assert.IsNotNull(headerChevron);
            Assert.IsNotNull(groupChevron);
            Assert.IsNotNull(objectChevron);
            Assert.AreEqual(ChevronIcon.IssuePanelVerticalScale, headerChevron.VerticalScale);
            Assert.AreEqual(headerChevron.VerticalScale, groupChevron.VerticalScale);
            Assert.AreEqual(headerChevron.VerticalScale, objectChevron.VerticalScale);

            var styleText = File.ReadAllText(IssuePanelStylePath);
            var headerRule = ExtractStyleRule(styleText, ".dependency-issue-toggle-button");
            var groupRule = ExtractStyleRule(
                styleText,
                ".dependency-issue-group-chevron,\n.dependency-issue-object-group-chevron");
            StringAssert.Contains("width: 24px;", headerRule);
            StringAssert.Contains("height: 22px;", headerRule);
            StringAssert.Contains("width: 24px;", groupRule);
            StringAssert.Contains("height: 22px;", groupRule);
        }

        [Test]
        public void GroupCounts_AppearImmediatelyAfterTitlesWithoutFlexPush()
        {
            var objectGroup = CreateObjectGroup(CreateLocation("target"));
            var group = CreateBrokenReferenceGroup(objectGroup);
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id },
                null,
                null);
            var issueRow = view.Q<VisualElement>(className: "dependency-issue-group-row");
            var objectRow = view.Q<VisualElement>(className: "dependency-issue-object-group-row");
            var issueTitle = issueRow.Q<Label>(className: "dependency-issue-group-title");
            var issueCount = issueRow.Q<Label>(className: "dependency-issue-group-count");
            var objectTitle = objectRow.Q<Label>(className: "dependency-issue-object-group-title");
            var objectCount = objectRow.Q<Label>(className: "dependency-issue-object-group-count");

            Assert.AreEqual(issueRow.IndexOf(issueTitle) + 1, issueRow.IndexOf(issueCount));
            Assert.AreEqual(objectRow.IndexOf(objectTitle) + 1, objectRow.IndexOf(objectCount));

            var styleText = File.ReadAllText(IssuePanelStylePath);
            var titleRule = ExtractStyleRule(
                styleText,
                ".dependency-issue-group-title,\n.dependency-issue-object-group-title");
            var countRule = ExtractStyleRule(
                styleText,
                ".dependency-issue-group-count,\n.dependency-issue-object-group-count");
            StringAssert.Contains("flex-grow: 0;", titleRule);
            StringAssert.Contains("flex-shrink: 1;", titleRule);
            StringAssert.Contains("margin-left: 6px;", countRule);
            StringAssert.Contains("flex-shrink: 0;", countRule);
            StringAssert.DoesNotContain("flex-grow: 1;", countRule);
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
            Assert.IsNotNull(view.Q<VisualElement>(className: "dependency-issue-location-list--issue-child"));
            Assert.IsNull(view.Q<VisualElement>(className: "dependency-issue-location-list--object-child"));
            var locationRow = view.Q<VisualElement>(className: "dependency-issue-location-row");
            Assert.IsNotNull(locationRow);
            Assert.AreEqual(
                location.DisplayPath,
                locationRow.Q<Label>(className: "dependency-issue-location-label").text);
        }

        [Test]
        public void LocationRow_PlacesAccentObjectNameAndPathInOrderWithoutInlineBorder()
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
            var objectName = row.Q<Label>(className: "dependency-issue-location-object-name");
            var path = row.Q<Label>(className: "dependency-issue-location-label");

            Assert.IsNotNull(accent);
            Assert.IsNotNull(objectName);
            Assert.IsNotNull(path);
            Assert.AreEqual("MeshRenderer", objectName.text);
            Assert.AreEqual("Path: Main/Player/MeshRenderer/m_Material", path.text);
            Assert.AreEqual(path.text, row.tooltip);
            Assert.AreEqual(Color.cyan, accent.style.backgroundColor.value);
            Assert.AreEqual(row.IndexOf(accent) + 1, row.IndexOf(objectName));
            Assert.AreEqual(row.IndexOf(objectName) + 1, row.IndexOf(path));
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
                "No related node",
                row.Q<Label>(className: "dependency-issue-location-object-name").text);
            Assert.AreEqual(
                "Path: Main/Player/MeshRenderer/No related node",
                row.Q<Label>(className: "dependency-issue-location-label").text);
        }

        [Test]
        public void IssuePanelStyles_IndentHierarchyAndUseVerticalAccentWithoutRowBorder()
        {
            var styleText = File.ReadAllText(IssuePanelStylePath);
            var issueChildRule = ExtractStyleRule(
                styleText,
                ".dependency-issue-location-list--issue-child .dependency-issue-location-row");
            var objectChildRule = ExtractStyleRule(
                styleText,
                ".dependency-issue-location-list--object-child .dependency-issue-location-row");
            var markerRule = ExtractStyleRule(styleText, ".dependency-issue-location-accent");
            var rowRule = ExtractStyleRule(styleText, ".dependency-issue-location-row");

            StringAssert.Contains("padding-left: 32px;", issueChildRule);
            StringAssert.Contains("padding-left: 48px;", objectChildRule);
            StringAssert.Contains("width: 3px;", markerRule);
            StringAssert.Contains("height: 14px;", markerRule);
            StringAssert.Contains("margin-right: 5px;", markerRule);
            StringAssert.Contains("border-left-width: 0;", rowRule);
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
                new[] { "Main", "Player", "MeshRenderer" },
                string.IsNullOrEmpty(targetNodeId) ? "No related node" : "m_Material",
                string.IsNullOrEmpty(targetNodeId) ? "No related node" : "MeshRenderer",
                targetNodeId,
                Color.cyan);
        }

        private static string ExtractStyleRule(string styleText, string selector)
        {
            var signature = "\n" + selector + " {";
            var start = styleText.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "USS selector was not found: " + selector);
            start++;
            var end = styleText.IndexOf('}', start);
            Assert.Greater(end, start, "USS rule was not closed: " + selector);
            return styleText.Substring(start, end - start + 1);
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
