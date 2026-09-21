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
        private const string GraphWindowUxmlPath =
            "Assets/DependencyAnalyzer/Editor/UI/Styles/GraphWindow.uxml";
        private const string IssuePanelStylePath =
            "Assets/DependencyAnalyzer/Editor/UI/Styles/IssuePanelStyle.uss";

        [Test]
        public void ObjectTypeView_CreatesOneGroupLevelWithDirectLocations()
        {
            var group = CreateGroup(CreateLocation("target"));

            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id },
                null,
                null);

            Assert.IsNotNull(view.Q<VisualElement>(className: "dependency-issue-group-row"));
            Assert.IsNotNull(view.Q<VisualElement>(className: "dependency-issue-location-row"));
            Assert.IsNotNull(view.Q<VisualElement>(className: "dependency-issue-location-list--group-child"));
            Assert.IsNull(view.Q<VisualElement>(className: "dependency-issue-object-group-row"));
            Assert.IsNull(view.Q<VisualElement>(className: "dependency-issue-location-branch"));
        }

        [Test]
        public void IssuePanelChevrons_UseSharedHeaderDrawingSettings()
        {
            var headerButton = new Button();
            ChevronIcon.SetIssuePanelButtonIcon(headerButton, true);
            var group = CreateGroup(CreateLocation("target"));
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id },
                null,
                null);

            var headerChevron = headerButton.Q<ChevronIcon>();
            var groupChevron = view.Q<ChevronIcon>(className: "dependency-issue-group-chevron");

            Assert.IsNotNull(headerChevron);
            Assert.IsNotNull(groupChevron);
            Assert.AreEqual(ChevronIcon.IssuePanelVerticalScale, headerChevron.VerticalScale);
            Assert.AreEqual(headerChevron.VerticalScale, groupChevron.VerticalScale);
            Assert.AreEqual(ChevronIcon.IssuePanelStrokeColor, headerChevron.StrokeColor);
            Assert.AreEqual(headerChevron.StrokeColor, groupChevron.StrokeColor);

            var styleText = File.ReadAllText(IssuePanelStylePath);
            var headerRule = ExtractStyleRule(styleText, ".dependency-issue-toggle-button");
            var groupRule = ExtractStyleRule(styleText, ".dependency-issue-group-chevron");
            StringAssert.Contains("width: 24px;", headerRule);
            StringAssert.Contains("height: 22px;", headerRule);
            StringAssert.Contains("width: 24px;", groupRule);
            StringAssert.Contains("height: 22px;", groupRule);
        }

        [Test]
        public void GroupRow_PlacesChevronColorIconTypeAndCountInOrder()
        {
            var group = CreateGroup(CreateLocation("target"));
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id },
                null,
                null);
            var row = view.Q<VisualElement>(className: "dependency-issue-group-row");
            var chevron = row.Q<ChevronIcon>(className: "dependency-issue-group-chevron");
            var accent = row.Q<VisualElement>(className: "dependency-issue-group-accent");
            var icon = row.Q<Image>(className: "dependency-issue-group-icon");
            var title = row.Q<Label>(className: "dependency-issue-group-title");
            var count = row.Q<Label>(className: "dependency-issue-group-count");

            Assert.AreEqual("Material", title.text);
            Assert.AreEqual("(1)", count.text);
            Assert.AreEqual(Color.cyan, accent.style.backgroundColor.value);
            Assert.AreSame(group.Icon, icon.image);
            Assert.AreEqual(row.IndexOf(chevron) + 1, row.IndexOf(accent));
            Assert.AreEqual(row.IndexOf(accent) + 1, row.IndexOf(icon));
            Assert.AreEqual(row.IndexOf(icon) + 1, row.IndexOf(title));
            Assert.AreEqual(row.IndexOf(title) + 1, row.IndexOf(count));
        }

        [Test]
        public void LocationRow_ShowsSourceAndFullPathWithoutRepeatedTypeOrAccent()
        {
            var location = CreateLocation("target");
            var group = CreateGroup(location);
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id },
                null,
                null);
            var row = view.Q<VisualElement>(className: "dependency-issue-location-row");
            var sourceName = row.Q<Label>(className: "dependency-issue-location-source-name");
            var path = row.Q<Label>(className: "dependency-issue-location-label");

            Assert.AreEqual("MeshRenderer", sourceName.text);
            Assert.AreEqual(location.DisplayPath, path.text);
            Assert.AreEqual("Path: Main/Player/MeshRenderer/m_Material", path.text);
            Assert.AreEqual(path.text, row.tooltip);
            Assert.AreEqual(row.IndexOf(sourceName) + 1, row.IndexOf(path));
            Assert.IsNull(row.Q<Label>(className: "dependency-issue-location-type"));
            Assert.IsNull(row.Q<VisualElement>(className: "dependency-issue-location-accent"));
            Assert.AreEqual(StyleKeyword.Null, row.style.borderLeftColor.keyword);
        }

        [Test]
        public void LocationRow_ClickInvokesFocusCallbackWithTargetNodeId()
        {
            var group = CreateGroup(CreateLocation("target-node"));
            var focusedNodeId = string.Empty;
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id },
                null,
                nodeId => focusedNodeId = nodeId);

            SimulateClick(view.Q<Button>(className: "dependency-issue-location-row"));

            Assert.AreEqual("target-node", focusedNodeId);
        }

        [Test]
        public void LocationWithoutRelatedNode_IsDisabled()
        {
            var group = CreateGroup(CreateLocation(string.Empty));
            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string> { group.Id },
                null,
                null);
            var row = view.Q<VisualElement>(className: "dependency-issue-location-row");

            Assert.IsFalse(row.enabledSelf);
            Assert.AreEqual(
                "No related node",
                row.Q<Label>(className: "dependency-issue-location-source-name").text);
            Assert.AreEqual(
                "Path: Main/Player/MeshRenderer/No related node",
                row.Q<Label>(className: "dependency-issue-location-label").text);
        }

        [Test]
        public void IssuePanelStyles_IndentLocationsAndUseGroupAccentWithoutRowBorder()
        {
            var styleText = File.ReadAllText(IssuePanelStylePath);
            var locationListRule = ExtractStyleRule(
                styleText,
                ".dependency-issue-location-list--group-child .dependency-issue-location-row");
            var markerRule = ExtractStyleRule(styleText, ".dependency-issue-group-accent");
            var rowRule = ExtractStyleRule(styleText, ".dependency-issue-location-row");

            StringAssert.Contains("padding-left: 32px;", locationListRule);
            StringAssert.Contains("width: 6px;", markerRule);
            StringAssert.Contains("min-width: 6px;", markerRule);
            StringAssert.Contains("height: 18px;", markerRule);
            StringAssert.Contains("margin-right: 5px;", markerRule);
            StringAssert.Contains("border-left-width: 0;", rowRule);
            StringAssert.DoesNotContain(".dependency-issue-location-accent", styleText);
            StringAssert.DoesNotContain(".dependency-issue-location-type", styleText);
        }

        [Test]
        public void CollapsedObjectType_HidesLocations()
        {
            var group = CreateGroup(CreateLocation("target"));

            var view = IssuePanelViewBuilder.CreateGroupView(
                group,
                new HashSet<string>(),
                null,
                null);

            Assert.IsNotNull(view.Q<VisualElement>(className: "dependency-issue-group-row"));
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

        private static ProjectIssueGroup CreateGroup(ProjectIssueLocation location)
        {
            return new ProjectIssueGroup(
                "issue:type:material",
                "Material",
                EditorGUIUtility.IconContent("Material Icon").image,
                Color.cyan,
                new[] { location });
        }

        private static ProjectIssueLocation CreateLocation(string targetNodeId)
        {
            return new ProjectIssueLocation(
                new[] { "Main", "Player", "MeshRenderer" },
                string.IsNullOrEmpty(targetNodeId) ? "No related node" : "m_Material",
                string.IsNullOrEmpty(targetNodeId) ? "No related node" : "MeshRenderer",
                targetNodeId);
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
