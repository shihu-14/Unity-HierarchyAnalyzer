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

            if (typeof(Camera).IsAssignableFrom(type))
            {
                return "Camera Icon";
            }

            if (typeof(Canvas).IsAssignableFrom(type))
            {
                return "Canvas Icon";
            }

            if (typeof(Light).IsAssignableFrom(type))
            {
                return "Light Icon";
            }

            if (typeof(AudioSource).IsAssignableFrom(type))
            {
                return "AudioSource Icon";
            }

            if (typeof(Component).IsAssignableFrom(type))
            {
                return typeof(MonoBehaviour).IsAssignableFrom(type) ? "cs Script Icon" : type.Name + " Icon";
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

        public static Texture GetIssueIcon(DependencyScanIssueSeverity severity)
        {
            switch (severity)
            {
                case DependencyScanIssueSeverity.Error:
                    return EditorGUIUtility.IconContent("console.erroricon.sml").image;
                case DependencyScanIssueSeverity.Info:
                    return EditorGUIUtility.IconContent("console.infoicon.sml").image;
                default:
                    return GetWarningIcon();
            }
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

            if (node.Kind == DependencyNodeKind.Issue)
            {
                return GetIssueNodeTypeClass(node);
            }

            var typeName = node.TypeName;
            if (typeName.Contains("Directional Light") || typeName == "Light")
            {
                return "dependency-node--light";
            }

            if (typeName.Contains("Camera"))
            {
                return "dependency-node--camera";
            }

            if (typeName.Contains("Canvas"))
            {
                return "dependency-node--canvas";
            }

            if (string.Equals(node.IconContentName, "cs Script Icon", StringComparison.Ordinal))
            {
                return "dependency-node--csharp";
            }

            if (typeName.Contains("AudioSource"))
            {
                return "dependency-node--audio";
            }

            if (typeName.Contains("Animator") || typeName.Contains("Animation"))
            {
                return "dependency-node--animator";
            }

            if (typeName.Contains("MonoScript") || node.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return "dependency-node--csharp";
            }

            if (node.Kind == DependencyNodeKind.Component)
            {
                return "dependency-node--component";
            }

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

            if (node.Kind == DependencyNodeKind.SceneObject)
            {
                return "dependency-node--object";
            }

            return "dependency-node--default";
        }

        public static Color GetNodeAccentColor(DependencyNodeData node)
        {
            var className = GetNodeTypeClass(node);
            switch (className)
            {
                case "dependency-node--prefab":
                    return new Color(0.32f, 0.65f, 1f);
                case "dependency-node--material":
                    return new Color(1f, 0.54f, 0.24f);
                case "dependency-node--texture":
                    return new Color(0.10f, 0.82f, 1f);
                case "dependency-node--audio":
                    return new Color(0.69f, 0.47f, 0.78f);
                case "dependency-node--animator":
                    return new Color(1f, 0.30f, 0.43f);
                case "dependency-node--mesh":
                    return new Color(0.84f, 0.87f, 0.90f);
                case "dependency-node--scriptable-object":
                    return new Color(0.56f, 0.42f, 1f);
                case "dependency-node--object":
                case "dependency-node--scene":
                    return new Color(0.46f, 0.66f, 0.77f);
                case "dependency-node--component":
                    return new Color(0f, 0.76f, 0.54f);
                case "dependency-node--csharp":
                    return new Color(0.41f, 0.72f, 0.47f);
                case "dependency-node--camera":
                    return new Color(0.61f, 0.55f, 1f);
                case "dependency-node--canvas":
                    return new Color(0.89f, 0.41f, 0.68f);
                case "dependency-node--light":
                    return new Color(0.85f, 0.78f, 0.40f);
                case "dependency-node--missing":
                    return new Color(0.87f, 0.39f, 0.39f);
                case "dependency-node--issue-error":
                    return new Color(0.93f, 0.31f, 0.31f);
                case "dependency-node--issue-warning":
                    return new Color(0.95f, 0.69f, 0.27f);
                case "dependency-node--issue-info":
                    return new Color(0.39f, 0.67f, 0.95f);
                default:
                    return new Color(0.56f, 0.63f, 0.70f);
            }
        }

        private static string GetIssueNodeTypeClass(DependencyNodeData node)
        {
            if (node.TypeName.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "dependency-node--issue-error";
            }

            if (node.TypeName.IndexOf("Info", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "dependency-node--issue-info";
            }

            return "dependency-node--issue-warning";
        }
    }
}
