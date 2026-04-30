using System;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Utils;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using GraphNode = UnityEditor.Experimental.GraphView.Node;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed class CustomNodeView : GraphNode
    {
        public CustomNodeView(DependencyNodeData data)
        {
            Data = data;
            viewDataKey = data.Id;
            title = data.DisplayName;
            capabilities &= ~Capabilities.Deletable;

            AddToClassList("dependency-node");
            AddToClassList(IconUtility.GetNodeTypeClass(data));

            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = string.Empty;
            OutputPort.portName = string.Empty;
            inputContainer.Add(InputPort);
            outputContainer.Add(OutputPort);

            BuildTitleContent();
            tooltip = BuildTooltip(data);

            RegisterCallback<MouseDownEvent>(HandleMouseDown);
            RefreshExpandedState();
            RefreshPorts();
        }

        public DependencyNodeData Data { get; }
        public Port InputPort { get; }
        public Port OutputPort { get; }
        public event Action<DependencyNodeData> NodeSelected;

        private void BuildTitleContent()
        {
            titleContainer.Clear();

            var body = new VisualElement();
            body.AddToClassList("dependency-node-body");

            var icon = new Image { image = IconUtility.GetIcon(Data) };
            icon.AddToClassList("dependency-node-icon");
            body.Add(icon);

            var nameLabel = new Label(Data.DisplayName);
            nameLabel.AddToClassList("dependency-node-name");
            body.Add(nameLabel);

            var badgeContainer = new VisualElement();
            badgeContainer.AddToClassList("dependency-node-badges");

            if (Data.HasMissingReferences || Data.Kind == DependencyNodeKind.MissingReference)
            {
                var warningIcon = new Image { image = IconUtility.GetWarningIcon() };
                warningIcon.AddToClassList("dependency-node-warning");
                badgeContainer.Add(warningIcon);
            }

            var dependencyBadge = new Label(Data.DependencyCount.ToString());
            dependencyBadge.tooltip = "Dependencies";
            dependencyBadge.AddToClassList("dependency-node-badge");
            badgeContainer.Add(dependencyBadge);

            var usedByBadge = new Label(Data.UsedByCount.ToString());
            usedByBadge.tooltip = "Used By";
            usedByBadge.AddToClassList("dependency-node-badge");
            badgeContainer.Add(usedByBadge);

            body.Add(badgeContainer);
            titleContainer.Add(body);
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
        }
    }
}
