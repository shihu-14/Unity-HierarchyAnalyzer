using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal sealed class ProjectIssueGroup
    {
        private readonly List<ProjectIssueLocation> locations;

        public ProjectIssueGroup(
            string id,
            string objectType,
            Texture icon,
            Color accentColor,
            IEnumerable<ProjectIssueLocation> locations)
        {
            Id = id ?? string.Empty;
            ObjectType = string.IsNullOrWhiteSpace(objectType) ? "Unknown Reference" : objectType.Trim();
            Icon = icon;
            AccentColor = accentColor;
            this.locations = locations == null
                ? new List<ProjectIssueLocation>()
                : locations.OrderBy(location => location.SortKey, System.StringComparer.OrdinalIgnoreCase).ToList();
        }

        public string Id { get; }
        public string ObjectType { get; }
        public Texture Icon { get; }
        public Color AccentColor { get; }
        public IReadOnlyList<ProjectIssueLocation> Locations => locations;
        public int Count => locations.Count;
    }
}
