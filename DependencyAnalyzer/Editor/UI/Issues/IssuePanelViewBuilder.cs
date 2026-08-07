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

        public static VisualElement CreateGroupView(
            IssueGroup group,
            bool isExpanded,
            Action<string> toggleGroup,
            Action<string> focusNode)
        {
            var container = new VisualElement();
            container.AddToClassList("dependency-issue-group");
            container.Add(CreateGroupRow(group, isExpanded, toggleGroup));
            if (isExpanded)
            {
                container.Add(CreateLocationList(group, focusNode));
            }

            return container;
        }

        private static VisualElement CreateGroupRow(
            IssueGroup group,
            bool isExpanded,
            Action<string> toggleGroup)
        {
            var row = new Button(() => toggleGroup?.Invoke(group.Id));
            row.text = string.Empty;
            row.AddToClassList("dependency-issue-group-row");
            row.AddToClassList(IssueGroupBuilder.GetSeverityClass(group.Severity));
            row.tooltip = group.Tooltip;

            var chevron = new ChevronIcon(isExpanded, 0.45f);
            chevron.AddToClassList("dependency-issue-group-chevron");
            row.Add(chevron);

            if (group.Icon != null)
            {
                var groupIcon = new Image { image = group.Icon };
                groupIcon.AddToClassList("dependency-issue-group-type-icon");
                row.Add(groupIcon);
            }

            var severityIcon = new Image { image = DependencyIconProvider.GetIssueIcon(group.Severity) };
            severityIcon.AddToClassList("dependency-issue-group-severity-icon");
            row.Add(severityIcon);

            var title = new Label(group.Title);
            title.AddToClassList("dependency-issue-group-title");
            row.Add(title);

            var count = new Label("×" + group.OccurrenceCount);
            count.AddToClassList("dependency-issue-group-count");
            row.Add(count);
            return row;
        }

        private static VisualElement CreateLocationList(IssueGroup group, Action<string> focusNode)
        {
            var list = new VisualElement();
            list.AddToClassList("dependency-issue-location-list");
            var root = new LocationBranch(string.Empty);
            var flatOccurrences = new List<IssueOccurrence>();

            for (var i = 0; i < group.Occurrences.Count; i++)
            {
                var occurrence = group.Occurrences[i];
                if (!occurrence.Location.HasHierarchy)
                {
                    flatOccurrences.Add(occurrence);
                    continue;
                }

                var branch = root;
                for (var segmentIndex = 0; segmentIndex < occurrence.Location.ParentSegments.Count; segmentIndex++)
                {
                    branch = branch.GetOrAddChild(occurrence.Location.ParentSegments[segmentIndex]);
                }

                branch.Occurrences.Add(occurrence);
            }

            for (var childIndex = 0; childIndex < root.Children.Count; childIndex++)
            {
                AddLocationBranch(list, root.Children[childIndex], 0, focusNode);
            }

            for (var i = 0; i < flatOccurrences.Count; i++)
            {
                list.Add(CreateOccurrenceRow(flatOccurrences[i], 0, focusNode));
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

            for (var occurrenceIndex = 0; occurrenceIndex < branch.Occurrences.Count; occurrenceIndex++)
            {
                list.Add(CreateOccurrenceRow(branch.Occurrences[occurrenceIndex], depth + 1, focusNode));
            }
        }

        private static VisualElement CreateOccurrenceRow(
            IssueOccurrence occurrence,
            int depth,
            Action<string> focusNode)
        {
            var row = occurrence.HasRelatedNode
                ? new Button(() => focusNode?.Invoke(occurrence.TargetNodeId))
                : new Button();
            row.text = string.Empty;
            row.AddToClassList("dependency-issue-occurrence-row");
            row.AddToClassList(IssueGroupBuilder.GetSeverityClass(occurrence.Severity));
            row.style.paddingLeft = BaseLocationIndent + (depth * LocationIndentStep);
            row.style.borderLeftColor = occurrence.NodeColor;
            row.tooltip = occurrence.Detail;

            if (occurrence.NodeIcon != null)
            {
                var nodeIcon = new Image { image = occurrence.NodeIcon };
                nodeIcon.AddToClassList("dependency-issue-node-icon");
                row.Add(nodeIcon);
            }

            var severityIcon = new Image { image = DependencyIconProvider.GetIssueIcon(occurrence.Severity) };
            severityIcon.AddToClassList("dependency-issue-severity-icon");
            row.Add(severityIcon);

            var label = new Label(occurrence.HasRelatedNode ? occurrence.Location.Label : "No related node");
            label.AddToClassList("dependency-issue-occurrence-label");
            row.Add(label);

            if (!occurrence.HasRelatedNode)
            {
                row.AddToClassList("dependency-issue-row--disabled");
                row.SetEnabled(false);
                return row;
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
            public List<IssueOccurrence> Occurrences { get; } = new List<IssueOccurrence>();

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
