using System.Collections.Generic;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.Issues;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Controller
{
    public sealed partial class DependencyGraphController
    {
        private ProjectIssuePanelModel PopulateIssuePanel(DependencyGraph graphData)
        {
            var model = ProjectIssuePanelBuilder.Build(graphData);
            if (issueList == null)
            {
                return model;
            }

            if (issueTitleLabel != null)
            {
                issueTitleLabel.text = "Issues";
                issueTitleLabel.tooltip = "Broken project references";
            }

            IssuePanelViewBuilder.ConfigureWarningStatus(issueWarningIcon, issueWarningCountLabel, model.WarningCount);
            issueList.contentContainer.Clear();
            if (model.Groups.Count == 0)
            {
                var empty = new Label("No issues");
                empty.AddToClassList("dependency-issue-empty");
                issueList.Add(empty);
                return model;
            }

            AddIssueGroups(model.Groups);
            return model;
        }

        private void AddIssueGroups(IReadOnlyList<ProjectIssueGroup> groups)
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
                    expandedIssueGroupIds,
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
