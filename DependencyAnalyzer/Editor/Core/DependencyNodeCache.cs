using System;
using System.Collections.Generic;

namespace DependencyAnalyzer.Editor.Core
{
    public sealed class DependencyNodeCache
    {
        private readonly Dictionary<string, DependencyNode> nodeLookup = new Dictionary<string, DependencyNode>();

        public int Count => nodeLookup.Count;

        public void Clear()
        {
            nodeLookup.Clear();
        }

        public bool TryGetNode(string nodeId, out DependencyNode node)
        {
            return nodeLookup.TryGetValue(nodeId, out node);
        }

        public DependencyNode Store(DependencyNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            DependencyNode existing;
            if (nodeLookup.TryGetValue(node.Id, out existing))
            {
                if (node.HasMissingReferences)
                {
                    existing.MarkMissingReferences();
                }

                if (node.MissingTargetState != MissingTargetKind.None)
                {
                    existing.MarkAsMissingTarget(node.MissingTargetState);
                }

                return existing;
            }

            nodeLookup.Add(node.Id, node);
            return node;
        }
    }
}
