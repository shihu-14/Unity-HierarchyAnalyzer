using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Controller.Issues;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.UI.Controls;
using DependencyAnalyzer.Editor.UI.GraphView;
using DependencyAnalyzer.Editor.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Controller
{
    public sealed partial class DependencyGraphController
    {
        private void PopulateIssuePanel(DependencyGraphData graphData)
        {
            if (issueList == null)
            {
                return;
            }

            var entries = IssuePanelEntryBuilder.Build(graphData);
            var errorEntries = entries
                .Where(entry => entry.Severity == DependencyScanIssueSeverity.Error)
                .ToList();
            var warningEntries = entries
                .Where(entry => entry.Severity == DependencyScanIssueSeverity.Warning)
                .ToList();
            var counts = IssuePanelEntryBuilder.CountByOrigin(entries);

            if (issueTitleLabel != null)
            {
                issueTitleLabel.text = counts.DisplayText;
                issueTitleLabel.tooltip = "Console counts current Console rows. Analyzer counts Missing References and scanner issues.";
            }

            UpdateIssueFilterButtons(counts.ErrorCount, counts.WarningCount);
            issueList.contentContainer.Clear();
            if (entries.Count == 0)
            {
                var empty = new Label("No issues");
                empty.AddToClassList("dependency-issue-empty");
                issueList.Add(empty);
                return;
            }

            if (issueErrorsVisible)
            {
                AddIssueRows(errorEntries);
            }

            if (issueWarningsVisible)
            {
                AddIssueRows(warningEntries);
            }
        }

        private void AddIssueRows(IReadOnlyList<IssuePanelEntry> entries)
        {
            if (issueList == null || entries == null || entries.Count == 0)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                issueList.Add(CreateIssueRow(entries[i]));
            }
        }

        private VisualElement CreateIssueRow(IssuePanelEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("dependency-issue-row");
            row.AddToClassList(IssuePanelEntryBuilder.GetSeverityClass(entry.Severity));
            if (string.IsNullOrEmpty(entry.TargetNodeId))
            {
                row.AddToClassList("dependency-issue-row--disabled");
            }
            else
            {
                row.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    graphView.FocusNode(entry.TargetNodeId, true);
                    evt.PreventDefault();
                    evt.StopPropagation();
                });
            }

            row.tooltip = entry.Detail;
            row.style.borderLeftColor = new StyleColor(entry.NodeColor);
            var severityIcon = new Image { image = IconUtility.GetIssueIcon(entry.Severity) };
            severityIcon.AddToClassList("dependency-issue-severity-icon");

            var nodeIcon = new Image { image = entry.NodeIcon };
            nodeIcon.AddToClassList("dependency-issue-node-icon");

            var text = new VisualElement();
            text.AddToClassList("dependency-issue-text");

            var main = new Label(entry.Title);
            main.AddToClassList("dependency-issue-main");
            var detail = new Label(entry.Detail);
            detail.AddToClassList("dependency-issue-detail");
            text.Add(main);
            text.Add(detail);

            row.Add(severityIcon);
            if (entry.NodeIcon != null)
            {
                row.Add(nodeIcon);
            }
            row.Add(text);
            return row;
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

            var icon = new Image { image = IconUtility.GetIssueIcon(severity) };
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
