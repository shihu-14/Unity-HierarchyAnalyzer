using System;
using DependencyAnalyzer.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Utils
{
    public static class IconUtility
    {
        public static string GetIconContentName(Type type)
        {
            if (type == null)
            {
                return "DefaultAsset Icon";
            }

            if (typeof(GameObject).IsAssignableFrom(type))
            {
                return "GameObject Icon";
            }

            if (typeof(Component).IsAssignableFrom(type))
            {
                return "cs Script Icon";
            }

            if (typeof(Material).IsAssignableFrom(type))
            {
                return "Material Icon";
            }

            if (typeof(Texture).IsAssignableFrom(type))
            {
                return "Texture Icon";
            }

            if (typeof(AudioClip).IsAssignableFrom(type))
            {
                return "AudioClip Icon";
            }

            if (typeof(Mesh).IsAssignableFrom(type))
            {
                return "Mesh Icon";
            }

            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                return "ScriptableObject Icon";
            }

            if (type.Name == "MonoScript")
            {
                return "cs Script Icon";
            }

            return "DefaultAsset Icon";
        }

        public static Texture GetIcon(DependencyNodeData node)
        {
            if (node == null)
            {
                return EditorGUIUtility.IconContent("DefaultAsset Icon").image;
            }

            if (!string.IsNullOrEmpty(node.Path)
                && node.Path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                && !node.Path.Contains("::"))
            {
                var cachedIcon = AssetDatabase.GetCachedIcon(node.Path);
                if (cachedIcon != null)
                {
                    return cachedIcon;
                }
            }

            var content = EditorGUIUtility.IconContent(node.IconContentName);
            return content != null && content.image != null
                ? content.image
                : EditorGUIUtility.IconContent("DefaultAsset Icon").image;
        }

        public static Texture GetWarningIcon()
        {
            return EditorGUIUtility.IconContent("console.warnicon.sml").image;
        }

        public static string GetNodeTypeClass(DependencyNodeData node)
        {
            if (node == null)
            {
                return "dependency-node--default";
            }

            if (node.Kind == DependencyNodeKind.MissingReference)
            {
                return "dependency-node--missing";
            }

            var typeName = node.TypeName;
            if (typeName.Contains("Prefab") || node.Path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return "dependency-node--prefab";
            }

            if (typeName.Contains("Material"))
            {
                return "dependency-node--material";
            }

            if (typeName.Contains("Texture"))
            {
                return "dependency-node--texture";
            }

            if (typeName.Contains("AudioClip"))
            {
                return "dependency-node--audio";
            }

            if (typeName.Contains("Mesh"))
            {
                return "dependency-node--mesh";
            }

            if (typeName.Contains("ScriptableObject"))
            {
                return "dependency-node--scriptable-object";
            }

            if (node.Kind == DependencyNodeKind.SceneObject || node.Kind == DependencyNodeKind.Component)
            {
                return "dependency-node--scene";
            }

            return "dependency-node--default";
        }
    }
}
