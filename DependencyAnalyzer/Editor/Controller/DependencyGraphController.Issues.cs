using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Controller.Issues;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.Icons;
using DependencyAnalyzer.Editor.UI.Issues;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Controller
{
    public sealed partial class DependencyGraphController
    {
        private void PopulateIssuePanel(DependencyGraph graphData)
        {
            if (issueList == null)
            {
                return;
            }

            var groups = IssueGroupBuilder.Build(graphData, IssueTargetResolver.FindIssueEntryTargetNodeId);
            var errorGroups = groups
                .Where(group => group.Severity == DependencyScanIssueSeverity.Error)
                .ToList();
            var warningGroups = groups
                .Where(group => group.Severity == DependencyScanIssueSeverity.Warning)
                .ToList();
            var summary = IssueGroupBuilder.Summarize(groups);

            if (issueTitleLabel != null)
            {
                issueTitleLabel.text = "Issues";
                issueTitleLabel.tooltip = "Detected root issues";
            }

            if (issueTotalCountLabel != null)
            {
                issueTotalCountLabel.text = summary.TotalCount.ToString();
                issueTotalCountLabel.tooltip = "Root issue count. A collapsed Console row counts as one issue.";
            }

            UpdateIssueFilterButtons(summary.ErrorCount, summary.WarningCount);
            issueList.contentContainer.Clear();
            if (groups.Count == 0)
            {
                var empty = new Label("No issues");
                empty.AddToClassList("dependency-issue-empty");
                issueList.Add(empty);
                return;
            }

            if (issueErrorsVisible)
            {
                AddIssueGroups(errorGroups);
            }

            if (issueWarningsVisible)
            {
                AddIssueGroups(warningGroups);
            }
        }

        private void AddIssueGroups(IReadOnlyList<IssueGroup> groups)
        {
            if (issueList == null || groups == null || groups.Count == 0)
            {
                return;
            }

            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                issueList.Add(IssuePanelViewBuilder.CreateGroupView(
                    group,
                    expandedIssueGroupIds.Contains(group.Id),
                    ToggleIssueGroup,
                    FocusIssueNode));
            }
        }

        private void ToggleIssueGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return;
            }

            if (!expandedIssueGroupIds.Add(groupId))
            {
                expandedIssueGroupIds.Remove(groupId);
            }

            PopulateIssuePanel(currentGraph);
        }

        private void FocusIssueNode(string targetNodeId)
        {
            if (string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            graphView.FocusNode(targetNodeId, true);
        }

        private static Label ConfigureIssueFilterButton(
            Button button,
            DependencyScanIssueSeverity severity,
            string tooltip,
            Action clicked)
        {
            if (button == null)
            {
                return null;
            }

            button.text = string.Empty;
            button.tooltip = tooltip;
            button.clicked += clicked;
            button.Clear();

            var icon = new Image { image = DependencyIconProvider.GetIssueIcon(severity) };
            icon.AddToClassList("dependency-issue-filter-icon");
            var count = new Label("0");
            count.AddToClassList("dependency-issue-filter-count");
            button.Add(icon);
            button.Add(count);
            return count;
        }

        private void ToggleIssueWarnings()
        {
            issueWarningsVisible = !issueWarningsVisible;
            PopulateIssuePanel(currentGraph);
        }

        private void ToggleIssueErrors()
        {
            issueErrorsVisible = !issueErrorsVisible;
            PopulateIssuePanel(currentGraph);
        }

        private void UpdateIssueFilterButtons(int errorCount, int warningCount)
        {
            if (issueWarningCountLabel != null)
            {
                issueWarningCountLabel.text = warningCount.ToString();
            }

            if (issueErrorCountLabel != null)
            {
                issueErrorCountLabel.text = errorCount.ToString();
            }

            SetIssueFilterButtonState(issueWarningFilterButton, issueWarningsVisible);
            SetIssueFilterButtonState(issueErrorFilterButton, issueErrorsVisible);
        }

        private static void SetIssueFilterButtonState(Button button, bool visible)
        {
            if (button == null)
            {
                return;
            }

            if (visible)
            {
                button.RemoveFromClassList("dependency-issue-filter-button--off");
            }
            else
            {
                button.AddToClassList("dependency-issue-filter-button--off");
            }
        }

        private void ToggleIssueList()
        {
            SetIssueListVisible(!issueListVisible);
        }

        private void SetIssueListVisible(bool visible)
        {
            issueListVisible = visible;
            if (issueList != null)
            {
                issueList.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            ApplyIssuePanelHeight();

            if (issueToggleButton != null)
            {
                UpdateIssueToggleIcon();
            }
        }

        private void UpdateIssueToggleIcon()
        {
            if (issueToggleButton == null)
            {
                return;
            }

            SetChevronButtonIcon(issueToggleButton, !issueListVisible, 0.59f);
            issueToggleButton.tooltip = issueListVisible ? "Hide issues" : "Show issues";
        }

        private void HandleIssueResizeMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || issuePanel == null || !issueListVisible)
            {
                return;
            }

            isResizingIssuePanel = true;
            issueResizeStartMouseY = evt.mousePosition.y;
            issueResizeStartHeight = GetCurrentIssuePanelHeight();
            MouseCaptureController.CaptureMouse(issueResizeHandle);
            evt.StopPropagation();
        }

        private void HandleIssueResizeMouseMove(MouseMoveEvent evt)
        {
            if (!isResizingIssuePanel || issuePanel == null)
            {
                return;
            }

            var deltaY = evt.mousePosition.y - issueResizeStartMouseY;
            issuePanelHeight = Mathf.Clamp(issueResizeStartHeight - deltaY, MinimumIssuePanelHeight, GetMaximumIssuePanelHeight());
            ApplyIssuePanelHeight();
            evt.StopPropagation();
        }

        private void HandleIssueResizeMouseUp(MouseUpEvent evt)
        {
            if (!isResizingIssuePanel)
            {
                return;
            }

            isResizingIssuePanel = false;
            if (issueResizeHandle != null && MouseCaptureController.HasMouseCapture(issueResizeHandle))
            {
                MouseCaptureController.ReleaseMouse(issueResizeHandle);
            }

            evt.StopPropagation();
        }

        private void ApplyIssuePanelHeight()
        {
            if (issuePanel == null)
            {
                return;
            }

            issuePanel.style.height = issueListVisible
                ? Mathf.Clamp(issuePanelHeight, MinimumIssuePanelHeight, GetMaximumIssuePanelHeight())
                : CollapsedIssuePanelHeight;
        }

        private float GetMaximumIssuePanelHeight()
        {
            var rootHeight = root == null ? 0f : root.resolvedStyle.height;
            if (float.IsNaN(rootHeight) || rootHeight <= 0f)
            {
                return MaximumIssuePanelHeight;
            }

            return Mathf.Clamp(rootHeight * 0.68f, MinimumIssuePanelHeight, MaximumIssuePanelHeight);
        }

        private float GetCurrentIssuePanelHeight()
        {
            if (issuePanel == null)
            {
                return issuePanelHeight;
            }

            var currentHeight = issuePanel.resolvedStyle.height;
            return float.IsNaN(currentHeight) || currentHeight <= 0f
                ? issuePanelHeight
                : currentHeight;
        }

    }
}
