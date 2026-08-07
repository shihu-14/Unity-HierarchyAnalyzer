using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.UI.Controls;
using DependencyAnalyzer.Editor.UI.Icons;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal static class IssuePanelViewBuilder
    {
        private const float BaseLocationIndent = 12f;
        private const float LocationIndentStep = 14f;

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
                container.Add(CreateLocationList(group.Locations, focusNode));
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

            var chevron = new ChevronIcon(isExpanded, 0.45f);
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

            var chevron = new ChevronIcon(isExpanded, 0.42f);
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
                container.Add(CreateLocationList(group.Locations, focusNode));
            }

            return container;
        }

        private static VisualElement CreateLocationList(
            IReadOnlyList<ProjectIssueLocation> locations,
            Action<string> focusNode)
        {
            var list = new VisualElement();
            list.AddToClassList("dependency-issue-location-list");
            var root = new LocationBranch(string.Empty);
            var flatLocations = new List<ProjectIssueLocation>();

            for (var i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (!location.HasHierarchy)
                {
                    flatLocations.Add(location);
                    continue;
                }

                var branch = root;
                for (var segmentIndex = 0; segmentIndex < location.ParentSegments.Count; segmentIndex++)
                {
                    branch = branch.GetOrAddChild(location.ParentSegments[segmentIndex]);
                }

                branch.Locations.Add(location);
            }

            for (var childIndex = 0; childIndex < root.Children.Count; childIndex++)
            {
                AddLocationBranch(list, root.Children[childIndex], 0, focusNode);
            }

            for (var i = 0; i < flatLocations.Count; i++)
            {
                list.Add(CreateLocationRow(flatLocations[i], 0, focusNode));
            }

            return list;
        }

        private static void AddLocationBranch(
            VisualElement list,
            LocationBranch branch,
            int depth,
            Action<string> focusNode)
        {
            var branchRow = new Label(branch.Label);
            branchRow.AddToClassList("dependency-issue-location-branch");
            branchRow.style.paddingLeft = BaseLocationIndent + (depth * LocationIndentStep);
            list.Add(branchRow);

            for (var childIndex = 0; childIndex < branch.Children.Count; childIndex++)
            {
                AddLocationBranch(list, branch.Children[childIndex], depth + 1, focusNode);
            }

            for (var locationIndex = 0; locationIndex < branch.Locations.Count; locationIndex++)
            {
                list.Add(CreateLocationRow(branch.Locations[locationIndex], depth + 1, focusNode));
            }
        }

        private static VisualElement CreateLocationRow(
            ProjectIssueLocation location,
            int depth,
            Action<string> focusNode)
        {
            var row = location.HasRelatedNode
                ? new Button(() => focusNode?.Invoke(location.TargetNodeId))
                : new Button();
            row.text = string.Empty;
            row.AddToClassList("dependency-issue-location-row");
            row.style.paddingLeft = BaseLocationIndent + (depth * LocationIndentStep);
            row.tooltip = location.DisplayPath;

            var accent = new VisualElement();
            accent.AddToClassList("dependency-issue-location-accent");
            accent.style.backgroundColor = location.AccentColor;
            row.Add(accent);

            var label = new Label(location.Label);
            label.AddToClassList("dependency-issue-location-label");
            row.Add(label);

            if (!location.HasRelatedNode)
            {
                row.AddToClassList("dependency-issue-location-row--disabled");
                row.SetEnabled(false);
            }

            return row;
        }

        private sealed class LocationBranch
        {
            private readonly Dictionary<string, LocationBranch> childLookup =
                new Dictionary<string, LocationBranch>(StringComparer.OrdinalIgnoreCase);

            public LocationBranch(string label)
            {
                Label = label ?? string.Empty;
            }

            public string Label { get; }
            public List<LocationBranch> Children { get; } = new List<LocationBranch>();
            public List<ProjectIssueLocation> Locations { get; } = new List<ProjectIssueLocation>();

            public LocationBranch GetOrAddChild(string label)
            {
                if (childLookup.TryGetValue(label, out var child))
                {
                    return child;
                }

                child = new LocationBranch(label);
                childLookup.Add(label, child);
                Children.Add(child);
                return child;
            }
        }
    }
}
