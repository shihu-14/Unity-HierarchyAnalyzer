using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal sealed class ProjectIssueObjectGroup
    {
        private readonly List<ProjectIssueLocation> locations;

        public ProjectIssueObjectGroup(
            string id,
            string objectType,
            Texture icon,
            IEnumerable<ProjectIssueLocation> locations)
        {
            Id = id ?? string.Empty;
            ObjectType = string.IsNullOrWhiteSpace(objectType) ? "Object Reference" : objectType;
            Icon = icon;
            this.locations = locations == null
                ? new List<ProjectIssueLocation>()
                : locations.OrderBy(location => location.SortKey, System.StringComparer.OrdinalIgnoreCase).ToList();
        }

        public string Id { get; }
        public string ObjectType { get; }
        public Texture Icon { get; }
        public IReadOnlyList<ProjectIssueLocation> Locations => locations;
        public int Count => locations.Count;
    }
}
