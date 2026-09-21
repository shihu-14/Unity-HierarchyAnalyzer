using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.UI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal sealed class IssuePanelView : IDisposable
    {
        private const float DefaultIssuePanelHeight = 148f;
        private const float CollapsedIssuePanelHeight = 36f;
        private const float MinimumIssuePanelHeight = 64f;
        private const float MaximumIssuePanelHeight = 460f;
        private readonly VisualElement root;
        private readonly VisualElement issuePanel;
        private readonly VisualElement issueResizeHandle;
        private readonly ScrollView issueList;
        private readonly Label issueTitleLabel;
        private readonly Image issueWarningIcon;
        private readonly Label issueWarningCountLabel;
        private readonly Button issueToggleButton;
        private readonly HashSet<string> expandedIssueGroupIds = new HashSet<string>(StringComparer.Ordinal);
        private ProjectIssuePanelModel model;
        private bool issueListVisible = true;
        private bool isResizingIssuePanel;
        private float issuePanelHeight = DefaultIssuePanelHeight;
        private float issueResizeStartMouseY;
        private float issueResizeStartHeight;

        public IssuePanelView(VisualElement root)
        {
            this.root = root;
            issuePanel = root.Q("issue-panel");
            issueResizeHandle = root.Q("issue-resize-handle");
            issueList = root.Q<ScrollView>("issue-list");
            issueTitleLabel = root.Q<Label>("issue-title-label");
            issueWarningIcon = root.Q<Image>("issue-warning-icon");
            issueWarningCountLabel = root.Q<Label>("issue-warning-count-label");
            issueToggleButton = root.Q<Button>("issue-toggle-button");
            if (issueToggleButton != null)
            {
                issueToggleButton.AddToClassList("dependency-issue-toggle-button");
                issueToggleButton.clicked += ToggleIssueList;
                UpdateIssueToggleIcon();
            }

            if (issueResizeHandle != null)
            {
                issueResizeHandle.RegisterCallback<MouseDownEvent>(HandleIssueResizeMouseDown);
                issueResizeHandle.RegisterCallback<MouseMoveEvent>(HandleIssueResizeMouseMove);
                issueResizeHandle.RegisterCallback<MouseUpEvent>(HandleIssueResizeMouseUp);
            }

            SetModel(new ProjectIssuePanelModel(null));
            ApplyIssuePanelHeight();
        }

        public event Action<string> NodeFocusRequested;

        public void Dispose()
        {
            if (issueToggleButton != null)
            {
                issueToggleButton.clicked -= ToggleIssueList;
            }

            if (issueResizeHandle != null)
            {
                issueResizeHandle.UnregisterCallback<MouseDownEvent>(HandleIssueResizeMouseDown);
                issueResizeHandle.UnregisterCallback<MouseMoveEvent>(HandleIssueResizeMouseMove);
                issueResizeHandle.UnregisterCallback<MouseUpEvent>(HandleIssueResizeMouseUp);
                if (MouseCaptureController.HasMouseCapture(issueResizeHandle))
                {
                    MouseCaptureController.ReleaseMouse(issueResizeHandle);
                }
            }

            isResizingIssuePanel = false;
            issueList?.Clear();
            NodeFocusRequested = null;
        }

        public void SetModel(ProjectIssuePanelModel model)
        {
            this.model = model;
            if (issueList == null)
            {
                return;
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
                return;
            }

            AddIssueGroups(model.Groups);
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

            SetModel(model);
        }

        private void FocusIssueNode(string targetNodeId)
        {
            if (string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            NodeFocusRequested?.Invoke(targetNodeId);
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

            ChevronIcon.SetIssuePanelButtonIcon(issueToggleButton, !issueListVisible);
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
