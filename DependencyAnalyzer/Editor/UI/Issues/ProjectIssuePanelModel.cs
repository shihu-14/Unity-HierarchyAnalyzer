using System.Collections.Generic;
using System.Linq;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal sealed class ProjectIssuePanelModel
    {
        private readonly List<ProjectIssueGroup> groups;

        public ProjectIssuePanelModel(IEnumerable<ProjectIssueGroup> groups)
        {
            this.groups = groups == null
                ? new List<ProjectIssueGroup>()
                : groups.Where(group => group != null && group.Count > 0).ToList();
        }

        public IReadOnlyList<ProjectIssueGroup> Groups => groups;
        public int WarningCount => groups.Sum(group => group.Count);
    }
}
