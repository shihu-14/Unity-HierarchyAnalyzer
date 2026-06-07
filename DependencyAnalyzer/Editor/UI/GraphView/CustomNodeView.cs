using System;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed class CustomNodeView : VisualElement
    {
        public const float NodeWidth = 240f;
        public const float NodeHeight = 72f;

        private const float DragThreshold = 3f;
        private const float MinimumNodeScale = 0.58f;
        private const float MaxExtraWidth = 76f;
        private const float MaxExtraHeight = 24f;
        private const float NameFontSize = 12f;
        private const float TypeFontSize = 10f;
        private const float BadgeFontSize = 10f;
        private const float ParentJumpFontSize = 11f;
        private const float ActionButtonBaseSize = 16f;
        private const float ActionButtonMinSize = 12f;
        private const float ActionButtonFontSize = 24f;
        private const float PlusButtonFontSize = 20f;
        private const float MenuButtonBaseSize = 18f;
        private const float MenuButtonMinSize = 13.5f;
        private const float MenuButtonFontSize = 7.875f;
        private const float MenuButtonMinFontSize = 5.9f;
        private const float StackStepOffset = 6f;
        private const float MiddleStackDepth = 0.85f;
        private const float BackStackDepth = 2f;
        private const float StackBorderOverlap = 1f;

        private readonly Func<float> zoomProvider;
        private readonly bool canToggleChildren;
        private readonly bool isExpanded;
        private readonly bool hasMenuChildren;
        private readonly bool isMenuExpanded;
        private readonly bool hasParent;
        private readonly bool hasPropagatedMissingReference;
        private readonly float nodeScale;
        private readonly float nodeWidth;
        private readonly float nodeHeight;
        private Vector2 graphPosition;
        private Vector2 dragStartMousePosition;
        private Vector2 dragStartGraphPosition;
        private bool isDragging;
        private bool hasDragged;

        public CustomNodeView(
            string viewId,
            DependencyNodeData data,
            bool hasHiddenChildren,
            bool canToggleChildren,
            bool isExpanded,
            bool hasMenuChildren,
            bool isMenuExpanded,
            bool hasParent,
            bool hasPropagatedMissingReference,
            float sizeScale,
            Func<float> zoomProvider)
        {
            ViewId = viewId;
            Data = data;
            HasHiddenChildren = hasHiddenChildren;
            this.canToggleChildren = canToggleChildren;
            this.isExpanded = isExpanded;
            this.hasMenuChildren = hasMenuChildren;
            this.isMenuExpanded = isMenuExpanded;
            this.hasParent = hasParent;
            this.hasPropagatedMissingReference = hasPropagatedMissingReference;
            this.zoomProvider = zoomProvider;
            nodeScale = Mathf.Clamp(sizeScale, MinimumNodeScale, 1f);
            var preferredSize = GetPreferredSize(data, sizeScale);
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
        public float HiddenStackOffset => HasHiddenChildren ? GetHiddenStackOffset(nodeScale) : 0f;
        public bool IsExpanded => isExpanded;
        public event Action<DependencyNodeData> NodeSelected;
        public event Action<CustomNodeView, Vector2> NodeMoved;
        public event Action<CustomNodeView> ToggleRequested;
        public event Action<CustomNodeView> MenuToggleRequested;
        public event Action<CustomNodeView> ParentJumpRequested;

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
            return GetPreferredSize(data, 1f);
        }

        public static Vector2 GetPreferredSize(DependencyNodeData data, float sizeScale)
        {
            var score = GetInfluenceScore(data);
            var ratio = Mathf.Clamp01(score / 18f);
            var scale = Mathf.Clamp(sizeScale, MinimumNodeScale, 1f);
            return new Vector2(
                Mathf.Round((NodeWidth + Mathf.Round(MaxExtraWidth * ratio)) * scale),
                Mathf.Round((NodeHeight + Mathf.Round(MaxExtraHeight * ratio)) * scale));
        }

        public static float GetHiddenStackOffset(float sizeScale)
        {
            return GetStackOffset(sizeScale, BackStackDepth);
        }

        private void BuildContent()
        {
            if (HasHiddenChildren)
            {
                AddStackShadow("dependency-node-stack-shadow--back", GetHiddenStackOffset(nodeScale));
                AddStackShadow("dependency-node-stack-shadow--middle", GetMiddleStackOffset(nodeScale));
            }

            var accent = new VisualElement();
            accent.AddToClassList("dependency-node-accent");
            Add(accent);

            var header = new VisualElement();
            header.AddToClassList("dependency-node-header");
            header.style.height = nodeHeight;
            header.style.paddingLeft = Mathf.Round(Mathf.Clamp(13f * nodeScale, 6f, 13f));
            header.style.paddingRight = Mathf.Round(Mathf.Clamp(7f * nodeScale, 4f, 7f));
            header.style.paddingTop = Mathf.Round(Mathf.Clamp(6f * nodeScale, 2f, 6f));

            var icon = new Image { image = IconUtility.GetIcon(Data) };
            icon.AddToClassList("dependency-node-icon");
            var iconSize = Mathf.Round(Mathf.Clamp(24f * nodeScale, 15f, 24f));
            icon.style.width = iconSize;
            icon.style.height = iconSize;
            icon.style.marginRight = Mathf.Round(Mathf.Clamp(8f * nodeScale, 4f, 8f));
            header.Add(icon);

            var issueMarker = CreateIssueMarker();
            if (issueMarker != null)
            {
                header.Add(issueMarker);
            }

            var titleStack = new VisualElement();
            titleStack.AddToClassList("dependency-node-title-stack");
            titleStack.style.flexShrink = 1f;

            var nameLabel = new Label(Data.DisplayName);
            nameLabel.AddToClassList("dependency-node-name");
            var nameFontSize = NameFontSize;
            nameLabel.style.fontSize = nameFontSize;
            nameLabel.style.height = Mathf.Round(nameFontSize + 4f);
            titleStack.Add(nameLabel);

            var typeLabel = new Label(Data.TypeName);
            typeLabel.AddToClassList("dependency-node-type");
            var typeFontSize = TypeFontSize;
            typeLabel.style.fontSize = typeFontSize;
            typeLabel.style.height = Mathf.Round(typeFontSize + 3f);
            typeLabel.style.marginTop = Mathf.Round(Mathf.Clamp(2f * nodeScale, 0f, 2f));
            titleStack.Add(typeLabel);

            header.Add(titleStack);

            var badgeContainer = new VisualElement();
            badgeContainer.AddToClassList("dependency-node-badges");
            badgeContainer.style.marginLeft = Mathf.Round(Mathf.Clamp(6f * nodeScale, 2f, 6f));
            badgeContainer.style.flexShrink = 0f;

            if (hasPropagatedMissingReference)
            {
                var warningIcon = new Image { image = IconUtility.GetWarningIcon() };
                warningIcon.AddToClassList("dependency-node-warning");
                var warningSize = Mathf.Round(Mathf.Clamp(16f * nodeScale, 11f, 16f));
                warningIcon.style.width = warningSize;
                warningIcon.style.height = warningSize;
                warningIcon.style.marginLeft = Mathf.Round(Mathf.Clamp(4f * nodeScale, 2f, 4f));
                warningIcon.tooltip = "Hidden child contains a missing reference";
                badgeContainer.Add(warningIcon);
            }

            badgeContainer.Add(CreateBadge(Data.DependencyCount.ToString(), "Dependencies", nodeScale));
            badgeContainer.Add(CreateBadge(Data.UsedByCount.ToString(), "Used By", nodeScale));
            if (canToggleChildren && hasMenuChildren)
            {
                badgeContainer.Add(CreateControlStack());
            }
            else if (canToggleChildren)
            {
                badgeContainer.Add(CreateToggleButton());
            }
            else if (hasMenuChildren)
            {
                badgeContainer.Add(CreateMenuToggleButton());
            }

            header.Add(badgeContainer);

            Add(header);

            if (hasParent)
            {
                Add(CreateParentJumpButton());
            }
        }

        private Image CreateIssueMarker()
        {
            if (!ShouldShowIssueMarker())
            {
                return null;
            }

            var severity = Data.IssueSeverity.HasValue ? Data.IssueSeverity.Value : DependencyScanIssueSeverity.Warning;
            var marker = new Image { image = IconUtility.GetIssueIcon(severity) };
            marker.AddToClassList("dependency-node-issue-marker");
            var markerSize = Mathf.Round(Mathf.Clamp(20f * nodeScale, 14f, 20f));
            marker.style.width = markerSize;
            marker.style.height = markerSize;
            marker.style.marginRight = Mathf.Round(Mathf.Clamp(5f * nodeScale, 2f, 5f));
            marker.tooltip = BuildIssueMarkerTooltip(severity);
            return marker;
        }

        private bool ShouldShowIssueMarker()
        {
            return Data.HasIssue
                || Data.HasMissingReferences
                || Data.Kind == DependencyNodeKind.MissingReference;
        }

        private string BuildIssueMarkerTooltip(DependencyScanIssueSeverity severity)
        {
            if (Data.HasIssue)
            {
                return severity + ": " + (string.IsNullOrEmpty(Data.IssueMessage) ? "Issue" : Data.IssueMessage);
            }

            return hasPropagatedMissingReference
                ? "Hidden child contains a missing reference"
                : "Missing reference";
        }

        private void AddStackShadow(string layerClass, float offset)
        {
            offset = Mathf.Max(1f, offset);
            var overlap = GetStackBorderOverlap(nodeScale);
            var overlappedRight = nodeWidth - overlap;
            var overlappedBottom = nodeHeight - overlap;
            var extent = offset + overlap;

            AddStackShadowPart(
                layerClass,
                "dependency-node-stack-shadow--right",
                overlappedRight,
                offset,
                extent,
                nodeHeight);
            AddStackShadowPart(
                layerClass,
                "dependency-node-stack-shadow--bottom",
                offset,
                overlappedBottom,
                nodeWidth,
                extent);
            AddStackShadowPart(
                layerClass,
                "dependency-node-stack-shadow--corner",
                overlappedRight,
                overlappedBottom,
                extent,
                extent);
        }

        private static float GetMiddleStackOffset(float sizeScale)
        {
            return GetStackOffset(sizeScale, MiddleStackDepth);
        }

        private static float GetStackOffset(float sizeScale, float depth)
        {
            var scale = Mathf.Clamp(sizeScale, MinimumNodeScale, 1f);
            return Mathf.Max(1f, Mathf.Round(StackStepOffset * depth * scale));
        }

        private static float GetStackBorderOverlap(float sizeScale)
        {
            var scale = Mathf.Clamp(sizeScale, MinimumNodeScale, 1f);
            return Mathf.Max(1f, Mathf.Round(StackBorderOverlap * scale));
        }

        private void AddStackShadowPart(string layerClass, string partClass, float left, float top, float width, float height)
        {
            var part = new VisualElement();
            part.pickingMode = PickingMode.Ignore;
            part.AddToClassList("dependency-node-stack-shadow");
            part.AddToClassList(layerClass);
            part.AddToClassList(partClass);
            part.style.left = left;
            part.style.top = top;
            part.style.width = width;
            part.style.height = height;
            Add(part);
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
            return CreateBadge(text, tooltipText, 1f);
        }

        private static Label CreateBadge(string text, string tooltipText, float scale)
        {
            var badge = new Label(text);
            badge.tooltip = tooltipText;
            badge.AddToClassList("dependency-node-badge");
            badge.style.minWidth = Mathf.Round(Mathf.Clamp(20f * scale, 14f, 20f));
            badge.style.height = Mathf.Round(Mathf.Clamp(17f * scale, 13f, 17f));
            badge.style.paddingLeft = Mathf.Round(Mathf.Clamp(5f * scale, 2f, 5f));
            badge.style.paddingRight = Mathf.Round(Mathf.Clamp(5f * scale, 2f, 5f));
            badge.style.marginLeft = Mathf.Round(Mathf.Clamp(4f * scale, 2f, 4f));
            badge.style.fontSize = BadgeFontSize;
            return badge;
        }

        private VisualElement CreateControlStack()
        {
            var stack = new VisualElement();
            stack.AddToClassList("dependency-node-control-stack");
            stack.style.marginLeft = Mathf.Round(Mathf.Clamp(4f * nodeScale, 2f, 4f));

            var toggleButton = CreateToggleButton();
            toggleButton.style.marginLeft = 0;
            toggleButton.style.marginBottom = Mathf.Round(Mathf.Clamp(1f * nodeScale, 0f, 1f));
            stack.Add(toggleButton);

            var menuButton = CreateMenuToggleButton();
            menuButton.style.marginLeft = 0;
            menuButton.style.marginTop = Mathf.Round(Mathf.Clamp(1f * nodeScale, 0f, 1f));
            stack.Add(menuButton);

            return stack;
        }

        private Label CreateToggleButton()
        {
            var button = new Label(isExpanded ? "-" : "+");
            var buttonSize = Mathf.Round(Mathf.Clamp(ActionButtonBaseSize * nodeScale, ActionButtonMinSize, ActionButtonBaseSize));
            button.tooltip = isExpanded ? "Collapse children" : "Expand children";
            button.AddToClassList("dependency-node-toggle");
            button.style.minWidth = buttonSize;
            button.style.width = buttonSize;
            button.style.height = buttonSize;
            button.style.marginLeft = Mathf.Round(Mathf.Clamp(4f * nodeScale, 2f, 4f));
            var fontSize = isExpanded ? ActionButtonFontSize : PlusButtonFontSize;
            button.style.fontSize = Mathf.Round(Mathf.Clamp(fontSize * nodeScale, ActionButtonMinSize, fontSize));
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    ToggleRequested?.Invoke(this);
                }

                evt.StopPropagation();
            });
            return button;
        }

        private Label CreateMenuToggleButton()
        {
            var button = new Label("•••");
            var buttonSize = Mathf.Round(Mathf.Clamp(MenuButtonBaseSize * nodeScale, MenuButtonMinSize, MenuButtonBaseSize));
            button.tooltip = isMenuExpanded ? "Hide inspector references" : "Show inspector references";
            button.AddToClassList("dependency-node-menu-toggle");
            if (isMenuExpanded)
            {
                button.AddToClassList("dependency-node-menu-toggle--expanded");
            }

            button.style.minWidth = buttonSize;
            button.style.width = buttonSize;
            button.style.height = buttonSize;
            button.style.marginLeft = Mathf.Round(Mathf.Clamp(4f * nodeScale, 2f, 4f));
            button.style.fontSize = Mathf.Round(Mathf.Clamp(MenuButtonFontSize * nodeScale, MenuButtonMinFontSize, MenuButtonFontSize));
            button.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    MenuToggleRequested?.Invoke(this);
                }

                evt.StopPropagation();
            });
            return button;
        }

        private Label CreateParentJumpButton()
        {
            var button = new Label("<");
            button.tooltip = "Jump to parent node";
            button.AddToClassList("dependency-node-parent-jump");
            button.style.top = Mathf.Round(Mathf.Clamp(34f * nodeScale, 20f, 34f));
            button.style.width = Mathf.Round(Mathf.Clamp(20f * nodeScale, 15f, 20f));
            button.style.height = Mathf.Round(Mathf.Clamp(24f * nodeScale, 16f, 24f));
            button.style.fontSize = ParentJumpFontSize;
            button.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    ParentJumpRequested?.Invoke(this);
                }

                evt.StopPropagation();
            });
            return button;
        }

        private static string BuildTooltip(DependencyNodeData data)
        {
            var tooltip = "Path: " + data.Path
                + "\nType: " + data.NamespaceQualifiedTypeName
                + "\nFile Size: " + FormatBytes(data.FileSizeBytes)
                + "\nAsset Labels: " + data.LabelsText
                + "\nDependencies: " + data.DependencyCount
                + "\nUsed By: " + data.UsedByCount;
            if (data.HasIssue)
            {
                tooltip += "\nIssue: " + data.IssueSeverity.Value
                    + " - " + (string.IsNullOrEmpty(data.IssueMessage) ? "Issue" : data.IssueMessage);
            }

            return tooltip;
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
