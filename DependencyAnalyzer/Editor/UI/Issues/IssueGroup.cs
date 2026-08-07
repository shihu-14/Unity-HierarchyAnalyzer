using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using UnityEngine;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal sealed class IssueGroup
    {
        private readonly List<IssueOccurrence> occurrences;

        public IssueGroup(
            string id,
            string title,
            DependencyScanIssueSeverity severity,
            IssueOrigin origin,
            Texture icon,
            IEnumerable<IssueOccurrence> occurrences)
        {
            Id = id ?? string.Empty;
            Title = string.IsNullOrEmpty(title) ? "Issue" : title;
            Severity = severity;
            Origin = origin;
            Icon = icon;
            this.occurrences = occurrences == null
                ? new List<IssueOccurrence>()
                : occurrences.OrderBy(occurrence => occurrence.Location.SortKey, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public string Id { get; }
        public string Title { get; }
        public DependencyScanIssueSeverity Severity { get; }
        public IssueOrigin Origin { get; }
        public Texture Icon { get; }
        public IReadOnlyList<IssueOccurrence> Occurrences => occurrences;
        public int OccurrenceCount => occurrences.Count;
        public string Tooltip => Origin == IssueOrigin.Console
            ? "Source: Unity Console"
            : "Source: Analyzer";
    }
}
