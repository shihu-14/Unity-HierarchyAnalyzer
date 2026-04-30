using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UnityGraphView = UnityEditor.Experimental.GraphView.GraphView;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed class DependencyGraphView : UnityGraphView
    {
        private readonly Dictionary<string, CustomNodeView> nodeViews = new Dictionary<string, CustomNodeView>();
        private readonly HashSet<string> expandedNodeIds = new HashSet<string>();
        private DependencyGraphData graph;
        private int initialDepth = 3;

        public DependencyGraphView()
        {
            AddToClassList("dependency-graph-view");
            style.flexGrow = 1f;
            SetupZoom(0.05f, 2.0f);
            AddManipulator(new ContentDragger());
            AddManipulator(new SelectionDragger());
            AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
        }

        public event Action<DependencyNodeData> NodeSelected;

        public void Populate(DependencyGraphData graphData, int depth)
        {
            graph = graphData;
            initialDepth = Mathf.Clamp(depth, 3, 4);
            Render();
        }

        public void ExpandNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || graph == null)
            {
                return;
            }

            if (expandedNodeIds.Add(nodeId))
            {
                Render();
            }
        }

        private void Render()
        {
            ClearGraphElements();
            nodeViews.Clear();

            if (graph == null || graph.Nodes.Count == 0)
            {
                return;
            }

            var depthByNodeId = new Dictionary<string, int>();
            var visibleNodeIds = BuildVisibleNodeSet(depthByNodeId);
            var visibleNodes = graph.Nodes
                .Where(node => visibleNodeIds.Contains(node.Id))
                .OrderBy(node => depthByNodeId[node.Id])
                .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            LayoutNodes(visibleNodes, depthByNodeId);
            LayoutEdges(visibleNodeIds);

            schedule.Execute(() => FrameAll()).ExecuteLater(100);
        }

        private void ClearGraphElements()
        {
            foreach (var element in graphElements.ToList())
            {
                RemoveElement(element);
            }
        }

        private HashSet<string> BuildVisibleNodeSet(Dictionary<string, int> depthByNodeId)
        {
            var visible = new HashSet<string>();
            var queue = new Queue<QueuedNode>();
            var roots = graph.Nodes
                .Where(node => !graph.GetIncomingEdges(node.Id).Any())
                .OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (roots.Count == 0)
            {
                roots.AddRange(graph.Nodes.OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase));
            }

            for (var i = 0; i < roots.Count; i++)
            {
                queue.Enqueue(new QueuedNode(roots[i].Id, 0));
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (depthByNodeId.TryGetValue(current.NodeId, out var existingDepth) && existingDepth <= current.Depth)
                {
                    continue;
                }

                depthByNodeId[current.NodeId] = current.Depth;
                visible.Add(current.NodeId);

                if (!graph.TryGetNode(current.NodeId, out var node))
                {
                    continue;
                }

                var isExpanded = expandedNodeIds.Contains(current.NodeId);
                if (!isExpanded && (current.Depth >= initialDepth || node.IsHeavyLeafType))
                {
                    continue;
                }

                foreach (var edge in graph.GetOutgoingEdges(current.NodeId))
                {
                    queue.Enqueue(new QueuedNode(edge.TargetNodeId, current.Depth + 1));
                }
            }

            return visible;
        }

        private void LayoutNodes(IReadOnlyList<DependencyNodeData> visibleNodes, Dictionary<string, int> depthByNodeId)
        {
            var rowByDepth = new Dictionary<int, int>();
            for (var i = 0; i < visibleNodes.Count; i++)
            {
                var node = visibleNodes[i];
                var depth = depthByNodeId[node.Id];
                rowByDepth.TryGetValue(depth, out var row);
                rowByDepth[depth] = row + 1;

                var nodeView = new CustomNodeView(node);
                nodeView.NodeSelected += HandleNodeSelected;
                nodeViews.Add(node.Id, nodeView);
                AddElement(nodeView);
                nodeView.SetPosition(new Rect(40f + depth * 300f, 40f + row * 118f, 230f, 82f));
            }
        }

        private void LayoutEdges(HashSet<string> visibleNodeIds)
        {
            foreach (var edge in graph.Edges)
            {
                if (!visibleNodeIds.Contains(edge.SourceNodeId)
                    || !visibleNodeIds.Contains(edge.TargetNodeId)
                    || !nodeViews.TryGetValue(edge.SourceNodeId, out var source)
                    || !nodeViews.TryGetValue(edge.TargetNodeId, out var target))
                {
                    continue;
                }

                var edgeView = new CustomEdgeView(edge)
                {
                    output = source.OutputPort,
                    input = target.InputPort
                };
                edgeView.output.Connect(edgeView);
                edgeView.input.Connect(edgeView);
                AddElement(edgeView);
            }
        }

        private void HandleNodeSelected(DependencyNodeData node)
        {
            NodeSelected?.Invoke(node);
        }

        private readonly struct QueuedNode
        {
            public QueuedNode(string nodeId, int depth)
            {
                NodeId = nodeId;
                Depth = depth;
            }

            public string NodeId { get; }
            public int Depth { get; }
        }
    }
}
