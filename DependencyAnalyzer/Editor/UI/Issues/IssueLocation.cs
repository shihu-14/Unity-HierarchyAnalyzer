using System.Collections.Generic;
using System.Linq;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal sealed class IssueLocation
    {
        private readonly List<string> parentSegments;

        public IssueLocation(IEnumerable<string> parentSegments, string label)
        {
            this.parentSegments = parentSegments == null
                ? new List<string>()
                : parentSegments
                    .Where(segment => !string.IsNullOrWhiteSpace(segment))
                    .Select(segment => segment.Trim())
                    .ToList();
            Label = string.IsNullOrWhiteSpace(label) ? "Unknown location" : label.Trim();
        }

        public IReadOnlyList<string> ParentSegments => parentSegments;
        public string Label { get; }
        public bool HasHierarchy => parentSegments.Count > 0;
        public string SortKey => string.Join("/", parentSegments) + "\u001f" + Label;
    }
}
