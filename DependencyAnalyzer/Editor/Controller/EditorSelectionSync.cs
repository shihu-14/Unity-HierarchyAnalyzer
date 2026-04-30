using DependencyAnalyzer.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Controller
{
    public sealed class EditorSelectionSync
    {
        public bool PingAndSelect(DependencyNodeData node)
        {
            if (node == null || node.Kind == DependencyNodeKind.MissingReference)
            {
                return false;
            }

            var target = ResolveObject(node);
            if (target == null)
            {
                return false;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
            return true;
        }

        private static Object ResolveObject(DependencyNodeData node)
        {
            var target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(node.GlobalObjectId);
            if (target != null)
            {
                return target;
            }

            if (node.InstanceId != 0)
            {
                target = EditorUtility.InstanceIDToObject(node.InstanceId);
                if (target != null)
                {
                    return target;
                }
            }

            if (!string.IsNullOrEmpty(node.Path) && !node.Path.Contains("::"))
            {
                return AssetDatabase.LoadMainAssetAtPath(node.Path);
            }

            return null;
        }
    }
}
