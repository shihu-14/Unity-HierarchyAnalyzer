using System.Collections.Generic;
using System.Linq;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal enum ProjectIssueType
    {
        MissingScript,
        BrokenMissingReference
    }

    internal sealed class ProjectIssueGroup
    {
        private readonly List<ProjectIssueLocation> locations;
        private readonly List<ProjectIssueObjectGroup> objectGroups;

        public ProjectIssueGroup(
            string id,
            ProjectIssueType type,
            string title,
            IEnumerable<ProjectIssueLocation> locations,
            IEnumerable<ProjectIssueObjectGroup> objectGroups)
        {
            Id = id ?? string.Empty;
            Type = type;
            Title = title ?? string.Empty;
            this.locations = locations == null
                ? new List<ProjectIssueLocation>()
                : locations.ToList();
            this.objectGroups = objectGroups == null
                ? new List<ProjectIssueObjectGroup>()
                : objectGroups.ToList();
        }

        public string Id { get; }
        public ProjectIssueType Type { get; }
        public string Title { get; }
        public IReadOnlyList<ProjectIssueLocation> Locations => locations;
        public IReadOnlyList<ProjectIssueObjectGroup> ObjectGroups => objectGroups;
        public int Count => locations.Count + objectGroups.Sum(group => group.Count);
    }
}
