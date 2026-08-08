using System;
using DependencyAnalyzer.Editor.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DependencyAnalyzer.Editor.Controller
{
    public sealed class EditorSelectionSync
    {
        private readonly Action<Object> selectObject;
        private readonly Action<Object> pingObject;

        public EditorSelectionSync()
            : this(target => Selection.activeObject = target, EditorGUIUtility.PingObject)
        {
        }

        internal EditorSelectionSync(Action<Object> selectObject, Action<Object> pingObject)
        {
            this.selectObject = selectObject ?? throw new ArgumentNullException(nameof(selectObject));
            this.pingObject = pingObject ?? throw new ArgumentNullException(nameof(pingObject));
        }

        public bool PingAndSelect(DependencyNode node)
        {
            return PingAndSelect(ResolveObject(node));
        }

        public bool PingAndSelect(Object inspectorTarget)
        {
            if (inspectorTarget == null)
            {
                return false;
            }

            var hierarchyOrProjectTarget = inspectorTarget is Component component
                ? component.gameObject
                : inspectorTarget;
            selectObject(inspectorTarget);
            pingObject(hierarchyOrProjectTarget);
            return true;
        }

        public Object ResolveObject(DependencyNode node)
        {
            if (node == null || node.IsMissingTarget)
            {
                return null;
            }

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
