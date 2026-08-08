using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed partial class DependencyGraphView
    {

        private Vector2 LayoutNodes(IReadOnlyList<RenderNode> roots, IReadOnlyDictionary<string, Rect> previousNodeRects)
        {
            var nextRow = 0f;

            for (var i = 0; i < roots.Count; i++)
            {
                PlaceNode(roots[i], ref nextRow);
            }

            for (var i = 0; i < roots.Count; i++)
            {
                CollectRenderNodes(roots[i]);
            }

            MarkNearestVisibleIssues();

            var occupiedRects = new List<Rect>();
            foreach (var pair in previousNodeRects)
            {
                if (manuallyMovedViewIds.Contains(pair.Key) && renderNodeByViewId.ContainsKey(pair.Key))
                {
                    occupiedRects.Add(Inflate(pair.Value, NodeCollisionPadding));
                }
            }
            var maxX = CanvasPadding;
            var maxY = CanvasPadding;

            for (var i = 0; i < renderNodes.Count; i++)
            {
                var renderNode = renderNodes[i];
                renderNode.Size = DependencyNodeView.GetPreferredSize(renderNode.Node, renderNode.SizeScale);
                var layoutPosition = new Vector2(CanvasPadding + renderNode.Depth * ColumnSpacing, CanvasPadding + renderNode.Row);
                var position = layoutPosition;

                if (manuallyMovedViewIds.Contains(renderNode.ViewId)
                    && previousNodeRects.TryGetValue(renderNode.ViewId, out var previousRect))
                {
                    position = previousRect.position;
                }
                else if (renderNode.Parent != null && nodeRects.TryGetValue(renderNode.Parent.ViewId, out var parentRect))
                {
                    var preferredPosition = new Vector2(
                        parentRect.xMax + ChildHorizontalOffset,
                        CanvasPadding + renderNode.Row);
                    position = FindOpenPosition(preferredPosition, renderNode.Size, occupiedRects);
                }
                else
                {
                    position = FindOpenPosition(layoutPosition, renderNode.Size, occupiedRects);
                }

                var nodeView = new DependencyNodeView(
                    renderNode.ViewId,
                    renderNode.Node,
                    renderNode.HasHiddenChildren,
                    renderNode.CanToggleChildren,
                    renderNode.IsExpanded,
                    renderNode.HasMenuChildren,
                    renderNode.IsMenuExpanded,
                    renderNode.Parent != null,
                    renderNode.HasPropagatedMissingReference,
                    renderNode.HasPropagatedIssue,
                    renderNode.PropagatedIssueSeverity,
                    renderNode.PropagatedIssueMessage,
                    renderNode.SizeScale,
                    () => zoom);
                nodeView.SetGraphPosition(position);
                nodeView.NodeSelected += HandleNodeSelected;
                nodeView.NodeMoved += HandleNodeMoved;
                nodeView.ToggleRequested += HandleNodeToggleRequested;
                nodeView.MenuToggleRequested += HandleMenuToggleRequested;
                nodeView.ParentJumpRequested += HandleParentJumpRequested;
                var isSearchCurrent = IsCurrentSearchNode(renderNode.NodeId);
                if (isSearchCurrent)
                {
                    AddSearchPulseHighlight(nodeView, true);
                }

                if (string.Equals(editorSelectionNodeId, renderNode.NodeId, StringComparison.Ordinal))
                {
                    AddEditorSelectionPulseHighlight(nodeView);
                }

                nodeViews.Add(renderNode.ViewId, nodeView);
                var rect = nodeView.GetGraphRect();
                nodeRects.Add(renderNode.ViewId, rect);
                if (!previousNodeRects.ContainsKey(renderNode.ViewId))
                {
                    occupiedRects.Add(Inflate(rect, NodeCollisionPadding));
                }

                maxX = Mathf.Max(maxX, rect.xMax);
                maxY = Mathf.Max(maxY, rect.yMax);
                nodeLayer.Add(nodeView);
            }

            return new Vector2(
                Mathf.Max(CanvasPadding * 2f + ColumnSpacing, maxX + CanvasPadding),
                Mathf.Max(CanvasPadding * 2f + RowSpacing, maxY + CanvasPadding));
        }

        private static Vector2 FindOpenPosition(Vector2 preferredPosition, Vector2 nodeSize, IReadOnlyList<Rect> occupiedRects)
        {
            var candidate = new Rect(
                Mathf.Max(CanvasPadding, preferredPosition.x),
                Mathf.Max(CanvasPadding, preferredPosition.y),
                nodeSize.x,
                nodeSize.y);
            var verticalStep = nodeSize.y + NodeCollisionPadding * 2f;
            var horizontalStep = ColumnSpacing * 0.55f;

            for (var attempt = 0; attempt < 240; attempt++)
            {
                var paddedCandidate = Inflate(candidate, NodeCollisionPadding);
                var overlaps = false;
                for (var i = 0; i < occupiedRects.Count; i++)
                {
                    if (occupiedRects[i].Overlaps(paddedCandidate))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    return candidate.position;
                }

                candidate.y += verticalStep;
                if (attempt > 0 && attempt % 24 == 0)
                {
                    candidate.x += horizontalStep;
                    candidate.y = CanvasPadding;
                }
            }

            return candidate.position;
        }

        private static Rect Inflate(Rect rect, float padding)
        {
            return new Rect(
                rect.xMin - padding,
                rect.yMin - padding,
                rect.width + padding * 2f,
                rect.height + padding * 2f);
        }

        private float PlaceNode(RenderNode node, ref float nextRow)
        {
            var childRows = new List<float>();
            for (var i = 0; i < node.Children.Count; i++)
            {
                childRows.Add(PlaceNode(node.Children[i], ref nextRow));
            }

            if (childRows.Count == 0)
            {
                node.Row = nextRow;
                nextRow += node.Size.y + GetNodeGap(node);
            }
            else
            {
                node.Row = (childRows[0] + childRows[childRows.Count - 1]) * 0.5f;
            }

            return node.Row;
        }

        private static float GetNodeGap(RenderNode node)
        {
            return node != null && node.Depth >= 3 ? DeepNodeGap : ShallowNodeGap;
        }

        private void CollectRenderNodes(RenderNode root)
        {
            renderNodes.Add(root);
            renderNodeByViewId[root.ViewId] = root;
            if (!renderNodesByNodeId.TryGetValue(root.NodeId, out var nodeInstances))
            {
                nodeInstances = new List<RenderNode>();
                renderNodesByNodeId.Add(root.NodeId, nodeInstances);
            }

            nodeInstances.Add(root);
            for (var i = 0; i < root.Children.Count; i++)
            {
                CollectRenderNodes(root.Children[i]);
            }
        }

        private void SetCanvasSize(float width, float height)
        {
            contentLayer.style.width = Mathf.Max(1f, width);
            contentLayer.style.height = Mathf.Max(1f, height);
            edgeLayer.style.width = Mathf.Max(1f, width);
            edgeLayer.style.height = Mathf.Max(1f, height);
            animationLayer.style.width = Mathf.Max(1f, width);
            animationLayer.style.height = Mathf.Max(1f, height);
            nodeLayer.style.width = Mathf.Max(1f, width);
            nodeLayer.style.height = Mathf.Max(1f, height);
        }

        private void ResetViewTransform()
        {
            pan = new Vector2(24f, 24f);
            zoom = Mathf.Clamp(1f, minZoom, maxZoom);
        }

        private void ApplyTransform()
        {
            contentLayer.style.left = pan.x;
            contentLayer.style.top = pan.y;
            contentLayer.transform.position = Vector3.zero;
            contentLayer.transform.scale = new Vector3(zoom, zoom, 1f);
            miniMap.MarkDirtyRepaint();
        }

        private Vector2 GetViewportSize()
        {
            var width = resolvedStyle.width;
            var height = resolvedStyle.height;
            if (float.IsNaN(width) || width <= 0f)
            {
                width = layout.width;
            }

            if (float.IsNaN(height) || height <= 0f)
            {
                height = layout.height;
            }

            return new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        }
    }
}
