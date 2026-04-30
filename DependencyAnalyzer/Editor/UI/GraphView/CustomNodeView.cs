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

        private Vector2 graphPosition;

        public CustomNodeView(DependencyNodeData data, bool hasHiddenChildren)
        {
            Data = data;
            HasHiddenChildren = hasHiddenChildren;
            pickingMode = PickingMode.Position;
            style.position = Position.Absolute;
            style.width = NodeWidth;
            style.height = NodeHeight;

            AddToClassList("dependency-node");
            AddToClassList(IconUtility.GetNodeTypeClass(data));

            BuildContent();
            tooltip = BuildTooltip(data);

            RegisterCallback<MouseDownEvent>(HandleMouseDown);
        }

        public DependencyNodeData Data { get; }
        public bool HasHiddenChildren { get; }
        public event Action<DependencyNodeData> NodeSelected;

        public void SetGraphPosition(Vector2 position)
        {
            graphPosition = position;
            style.left = position.x;
            style.top = position.y;
        }

        public Rect GetGraphRect()
        {
            return new Rect(graphPosition.x, graphPosition.y, NodeWidth, NodeHeight);
        }

        private void BuildContent()
        {
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
            Add(pathLabel);
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

            NodeSelected?.Invoke(Data);
            evt.StopPropagation();
        }
    }
}
