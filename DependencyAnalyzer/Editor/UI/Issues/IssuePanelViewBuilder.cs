using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.UI.Controls;
using DependencyAnalyzer.Editor.UI.Icons;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal static class IssuePanelViewBuilder
    {
        private const string IssueChildLocationListClass =
            "dependency-issue-location-list--issue-child";
        private const string ObjectChildLocationListClass =
            "dependency-issue-location-list--object-child";

        public static void ConfigureWarningStatus(Image icon, Label count, int warningCount)
        {
            if (icon != null)
            {
                icon.image = DependencyIconProvider.GetWarningIcon();
                icon.tooltip = "Broken project references";
            }

            if (count != null)
            {
                count.text = warningCount.ToString();
                count.tooltip = "Missing scripts and broken serialized references";
            }
        }

        public static VisualElement CreateGroupView(
            ProjectIssueGroup group,
            ISet<string> expandedGroupIds,
            Action<string> toggleGroup,
            Action<string> focusNode)
        {
            var container = new VisualElement();
            container.AddToClassList("dependency-issue-group");
            var isExpanded = expandedGroupIds != null && expandedGroupIds.Contains(group.Id);
            container.Add(CreateIssueTypeRow(group, isExpanded, toggleGroup));
            if (!isExpanded)
            {
                return container;
            }

            if (group.Type == ProjectIssueType.MissingScript)
            {
                container.Add(CreateLocationList(
                    group.Locations,
                    focusNode,
                    IssueChildLocationListClass));
                return container;
            }

            var objectGroupList = new VisualElement();
            objectGroupList.AddToClassList("dependency-issue-object-group-list");
            for (var i = 0; i < group.ObjectGroups.Count; i++)
            {
                var objectGroup = group.ObjectGroups[i];
                var objectGroupExpanded = expandedGroupIds != null && expandedGroupIds.Contains(objectGroup.Id);
                objectGroupList.Add(CreateObjectGroupView(objectGroup, objectGroupExpanded, toggleGroup, focusNode));
            }

            container.Add(objectGroupList);
            return container;
        }

        private static VisualElement CreateIssueTypeRow(
            ProjectIssueGroup group,
            bool isExpanded,
            Action<string> toggleGroup)
        {
            var row = new Button(() => toggleGroup?.Invoke(group.Id));
            row.text = string.Empty;
            row.AddToClassList("dependency-issue-group-row");
            row.tooltip = group.Title;

            var chevron = ChevronIcon.CreateIssuePanel(isExpanded);
            chevron.AddToClassList("dependency-issue-group-chevron");
            row.Add(chevron);

            var warningIcon = new Image { image = DependencyIconProvider.GetWarningIcon() };
            warningIcon.AddToClassList("dependency-issue-group-warning-icon");
            row.Add(warningIcon);

            var title = new Label(group.Title);
            title.AddToClassList("dependency-issue-group-title");
            row.Add(title);

            var count = new Label("(" + group.Count + ")");
            count.AddToClassList("dependency-issue-group-count");
            row.Add(count);
            return row;
        }

        private static VisualElement CreateObjectGroupView(
            ProjectIssueObjectGroup group,
            bool isExpanded,
            Action<string> toggleGroup,
            Action<string> focusNode)
        {
            var container = new VisualElement();
            container.AddToClassList("dependency-issue-object-group");

            var row = new Button(() => toggleGroup?.Invoke(group.Id));
            row.text = string.Empty;
            row.AddToClassList("dependency-issue-object-group-row");
            row.tooltip = group.ObjectType;

            var chevron = ChevronIcon.CreateIssuePanel(isExpanded);
            chevron.AddToClassList("dependency-issue-object-group-chevron");
            row.Add(chevron);

            if (group.Icon != null)
            {
                var icon = new Image { image = group.Icon };
                icon.AddToClassList("dependency-issue-object-group-icon");
                row.Add(icon);
            }

            var label = new Label(group.ObjectType);
            label.AddToClassList("dependency-issue-object-group-title");
            row.Add(label);

            var count = new Label("(" + group.Count + ")");
            count.AddToClassList("dependency-issue-object-group-count");
            row.Add(count);
            container.Add(row);

            if (isExpanded)
            {
                container.Add(CreateLocationList(
                    group.Locations,
                    focusNode,
                    ObjectChildLocationListClass));
            }

            return container;
        }

        private static VisualElement CreateLocationList(
            IReadOnlyList<ProjectIssueLocation> locations,
            Action<string> focusNode,
            string hierarchyClass)
        {
            var list = new VisualElement();
            list.AddToClassList("dependency-issue-location-list");
            list.AddToClassList(hierarchyClass);
            for (var i = 0; i < locations.Count; i++)
            {
                list.Add(CreateLocationRow(locations[i], focusNode));
            }

            return list;
        }

        private static VisualElement CreateLocationRow(
            ProjectIssueLocation location,
            Action<string> focusNode)
        {
            var row = location.HasRelatedNode
                ? new Button(() => focusNode?.Invoke(location.TargetNodeId))
                : new Button();
            row.text = string.Empty;
            row.AddToClassList("dependency-issue-location-row");
            row.tooltip = location.DisplayPath;

            var accent = new VisualElement();
            accent.AddToClassList("dependency-issue-location-accent");
            accent.style.backgroundColor = location.AccentColor;
            row.Add(accent);

            if (location.HasMissingObjectType)
            {
                var missingObjectType = new Label(location.MissingObjectType);
                missingObjectType.AddToClassList("dependency-issue-location-type");
                row.Add(missingObjectType);
            }

            var sourceObjectName = new Label(location.SourceObjectName);
            sourceObjectName.AddToClassList("dependency-issue-location-source-name");
            row.Add(sourceObjectName);

            var path = new Label(location.DisplayPath);
            path.AddToClassList("dependency-issue-location-label");
            row.Add(path);

            if (!location.HasRelatedNode)
            {
                row.AddToClassList("dependency-issue-location-row--disabled");
                row.SetEnabled(false);
            }

            return row;
        }
    }
}
