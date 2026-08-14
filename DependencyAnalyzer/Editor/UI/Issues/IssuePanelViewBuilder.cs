using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.UI.Controls;
using DependencyAnalyzer.Editor.UI.Icons;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal static class IssuePanelViewBuilder
    {
        private const string GroupChildLocationListClass =
            "dependency-issue-location-list--group-child";

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
                count.tooltip = "Missing references grouped by Object type";
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
            container.Add(CreateObjectTypeRow(group, isExpanded, toggleGroup));
            if (!isExpanded)
            {
                return container;
            }

            container.Add(CreateLocationList(
                group.Locations,
                focusNode,
                GroupChildLocationListClass));
            return container;
        }

        private static VisualElement CreateObjectTypeRow(
            ProjectIssueGroup group,
            bool isExpanded,
            Action<string> toggleGroup)
        {
            var row = new Button(() => toggleGroup?.Invoke(group.Id));
            row.text = string.Empty;
            row.AddToClassList("dependency-issue-group-row");
            row.tooltip = group.ObjectType;

            var chevron = ChevronIcon.CreateIssuePanel(isExpanded);
            chevron.AddToClassList("dependency-issue-group-chevron");
            row.Add(chevron);

            var accent = new VisualElement();
            accent.AddToClassList("dependency-issue-group-accent");
            accent.style.backgroundColor = group.AccentColor;
            row.Add(accent);

            if (group.Icon != null)
            {
                var icon = new Image { image = group.Icon };
                icon.AddToClassList("dependency-issue-group-icon");
                row.Add(icon);
            }

            var title = new Label(group.ObjectType);
            title.AddToClassList("dependency-issue-group-title");
            row.Add(title);

            var count = new Label("(" + group.Count + ")");
            count.AddToClassList("dependency-issue-group-count");
            row.Add(count);
            return row;
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
