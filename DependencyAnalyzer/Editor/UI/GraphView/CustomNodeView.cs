using System;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed class CustomNodeView : VisualElement
    {
        public const float NodeWidth = 260f;
        public const float NodeHeight = 92f;

        private const float DragThreshold = 3f;
        private const float MaxExtraWidth = 98f;
        private const float MaxExtraHeight = 42f;

        private readonly Func<float> zoomProvider;
        private readonly float nodeWidth;
        private readonly float nodeHeight;
        private Vector2 graphPosition;
        private Vector2 dragStartMousePosition;
        private Vector2 dragStartGraphPosition;
        private bool isDragging;
        private bool hasDragged;

        public CustomNodeView(string viewId, DependencyNodeData data, bool hasHiddenChildren, Func<float> zoomProvider)
        {
            ViewId = viewId;
            Data = data;
            HasHiddenChildren = hasHiddenChildren;
            this.zoomProvider = zoomProvider;
            var preferredSize = GetPreferredSize(data);
            nodeWidth = preferredSize.x;
            nodeHeight = preferredSize.y;
            pickingMode = PickingMode.Position;
            style.position = Position.Absolute;
            style.width = nodeWidth;
            style.height = nodeHeight;

            AddToClassList("dependency-node");
            AddToClassList(IconUtility.GetNodeTypeClass(data));
            AddToClassList(GetImpactClass(data));
            if (HasHiddenChildren)
            {
                AddToClassList("dependency-node--expandable");
            }

            BuildContent();
            tooltip = BuildTooltip(data);

            RegisterCallback<MouseDownEvent>(HandleMouseDown);
            RegisterCallback<MouseMoveEvent>(HandleMouseMove);
            RegisterCallback<MouseUpEvent>(HandleMouseUp);
        }

        public string ViewId { get; }
        public DependencyNodeData Data { get; }
        public bool HasHiddenChildren { get; }
        public event Action<DependencyNodeData> NodeSelected;
        public event Action<CustomNodeView, Vector2> NodeMoved;

        public void SetGraphPosition(Vector2 position)
        {
            graphPosition = position;
            style.left = position.x;
            style.top = position.y;
        }

        public Rect GetGraphRect()
        {
            return new Rect(graphPosition.x, graphPosition.y, nodeWidth, nodeHeight);
        }

        public static Vector2 GetPreferredSize(DependencyNodeData data)
        {
            var score = GetInfluenceScore(data);
            var ratio = Mathf.Clamp01(score / 18f);
            return new Vector2(
                NodeWidth + Mathf.Round(MaxExtraWidth * ratio),
                NodeHeight + Mathf.Round(MaxExtraHeight * ratio));
        }

        private void BuildContent()
        {
            if (HasHiddenChildren)
            {
                var backShadow = new VisualElement();
                backShadow.AddToClassList("dependency-node-stack-shadow");
                backShadow.AddToClassList("dependency-node-stack-shadow--back");
                backShadow.style.width = nodeWidth;
                backShadow.style.height = nodeHeight;
                Add(backShadow);

                var middleShadow = new VisualElement();
                middleShadow.AddToClassList("dependency-node-stack-shadow");
                middleShadow.AddToClassList("dependency-node-stack-shadow--middle");
                middleShadow.style.width = nodeWidth;
                middleShadow.style.height = nodeHeight;
                Add(middleShadow);
            }

            var accent = new VisualElement();
            accent.AddToClassList("dependency-node-accent");
            Add(accent);

            var header = new VisualElement();
            header.AddToClassList("dependency-node-header");

            var icon = new Image { image = IconUtility.GetIcon(Data) };
            icon.AddToClassList("dependency-node-icon");
            header.Add(icon);

            var titleStack = new VisualElement();
            titleStack.AddToClassList("dependency-node-title-stack");

            var nameLabel = new Label(Data.DisplayName);
            nameLabel.AddToClassList("dependency-node-name");
            titleStack.Add(nameLabel);

            var typeLabel = new Label(Data.TypeName);
            typeLabel.AddToClassList("dependency-node-type");
            titleStack.Add(typeLabel);

            header.Add(titleStack);

            var badgeContainer = new VisualElement();
            badgeContainer.AddToClassList("dependency-node-badges");

            if (Data.HasMissingReferences || Data.Kind == DependencyNodeKind.MissingReference)
            {
                var warningIcon = new Image { image = IconUtility.GetWarningIcon() };
                warningIcon.AddToClassList("dependency-node-warning");
                badgeContainer.Add(warningIcon);
            }

            badgeContainer.Add(CreateBadge(Data.DependencyCount.ToString(), "Dependencies"));
            badgeContainer.Add(CreateBadge(Data.UsedByCount.ToString(), "Used By"));
            if (HasHiddenChildren)
            {
                badgeContainer.Add(CreateBadge("+", "Click to expand children"));
            }

            header.Add(badgeContainer);

            Add(header);

            var pathLabel = new Label(Data.Path);
            pathLabel.AddToClassList("dependency-node-path");
            pathLabel.style.height = Mathf.Max(32f, nodeHeight - 60f);
            Add(pathLabel);
        }

        private static int GetInfluenceScore(DependencyNodeData data)
        {
            if (data == null)
            {
                return 0;
            }

            return data.DependencyCount + data.UsedByCount * 2;
        }

        private static string GetImpactClass(DependencyNodeData data)
        {
            var score = GetInfluenceScore(data);
            if (score >= 12)
            {
                return "dependency-node--impact-high";
            }

            if (score >= 5)
            {
                return "dependency-node--impact-medium";
            }

            return "dependency-node--impact-low";
        }

        private static Label CreateBadge(string text, string tooltipText)
        {
            var badge = new Label(text);
            badge.tooltip = tooltipText;
            badge.AddToClassList("dependency-node-badge");
            return badge;
        }

        private static string BuildTooltip(DependencyNodeData data)
        {
            return "Path: " + data.Path
                + "\nType: " + data.NamespaceQualifiedTypeName
                + "\nFile Size: " + FormatBytes(data.FileSizeBytes)
                + "\nAsset Labels: " + data.LabelsText
                + "\nDependencies: " + data.DependencyCount
                + "\nUsed By: " + data.UsedByCount;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024L)
            {
                return bytes + " B";
            }

            if (bytes < 1024L * 1024L)
            {
                return (bytes / 1024f).ToString("0.##") + " KB";
            }

            return (bytes / 1024f / 1024f).ToString("0.##") + " MB";
        }

        private void HandleMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            isDragging = true;
            hasDragged = false;
            dragStartMousePosition = evt.mousePosition;
            dragStartGraphPosition = graphPosition;
            MouseCaptureController.CaptureMouse(this);
            BringToFront();
            evt.StopPropagation();
        }

        private void HandleMouseMove(MouseMoveEvent evt)
        {
            if (!isDragging)
            {
                return;
            }

            var zoom = Mathf.Max(0.01f, zoomProvider == null ? 1f : zoomProvider());
            var panelDelta = evt.mousePosition - dragStartMousePosition;
            if (!hasDragged && panelDelta.sqrMagnitude >= DragThreshold * DragThreshold)
            {
                hasDragged = true;
            }

            var nextPosition = dragStartGraphPosition + panelDelta / zoom;
            NodeMoved?.Invoke(this, nextPosition);
            evt.StopPropagation();
        }

        private void HandleMouseUp(MouseUpEvent evt)
        {
            if (!isDragging)
            {
                return;
            }

            var shouldSelect = !hasDragged && evt.button == 0;
            StopDragging(shouldSelect);
            evt.StopPropagation();
        }

        private void StopDragging(bool selectNode)
        {
            if (!isDragging)
            {
                return;
            }

            isDragging = false;
            if (MouseCaptureController.HasMouseCapture(this))
            {
                MouseCaptureController.ReleaseMouse(this);
            }

            if (selectNode)
            {
                NodeSelected?.Invoke(Data);
            }
        }
    }
}
