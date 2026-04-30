using System;
using System.Collections.Generic;

namespace DependencyAnalyzer.Editor.Core
{
    public sealed class DependencyCache
    {
        private readonly Dictionary<string, DependencyNodeData> nodeLookup = new Dictionary<string, DependencyNodeData>();

        public int Count => nodeLookup.Count;

        public void Clear()
        {
            nodeLookup.Clear();
        }

        public bool TryGetNode(string nodeId, out DependencyNodeData node)
        {
            return nodeLookup.TryGetValue(nodeId, out node);
        }

        public DependencyNodeData Store(DependencyNodeData node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            DependencyNodeData existing;
            if (nodeLookup.TryGetValue(node.Id, out existing))
            {
                if (node.HasMissingReferences)
                {
                    existing.MarkMissingReferences();
                }

                return existing;
            }

            nodeLookup.Add(node.Id, node);
            return node;
        }
    }
}
