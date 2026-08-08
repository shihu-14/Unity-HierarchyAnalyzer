using System;
using DependencyAnalyzer.Editor.Core;
using UnityEngine;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    internal static class DependencyNodeStyleResolver
    {
        internal const string MissingTargetClass = "dependency-node--missing-target";
        internal const float MissingTargetOpacity = 0.60f;

        public static string GetNodeTypeClass(DependencyNode node)
        {
            if (node == null)
            {
                return "dependency-node--default";
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

        public static Color GetNodeAccentColor(DependencyNode node)
        {
            return GetAccentColorForClass(GetNodeTypeClass(node));
        }

        internal static float GetNodeOpacity(DependencyNode node)
        {
            return node != null && node.IsMissingTarget ? MissingTargetOpacity : 1f;
        }

        internal static Color GetTypeAccentColor(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)
                || string.Equals(typeName, "Object Reference", StringComparison.OrdinalIgnoreCase))
            {
                return GetAccentColorForClass("dependency-node--default");
            }

            if (string.Equals(typeName, "Script", StringComparison.OrdinalIgnoreCase)
                || typeName.IndexOf("MonoScript", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GetAccentColorForClass("dependency-node--csharp");
            }

            if (typeName.IndexOf("Prefab", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GetAccentColorForClass("dependency-node--prefab");
            }

            if (typeName.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GetAccentColorForClass("dependency-node--material");
            }

            if (typeName.IndexOf("Texture", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GetAccentColorForClass("dependency-node--texture");
            }

            if (typeName.IndexOf("Audio", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GetAccentColorForClass("dependency-node--audio");
            }

            if (typeName.IndexOf("Animator", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Animation", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GetAccentColorForClass("dependency-node--animator");
            }

            if (typeName.IndexOf("Mesh", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GetAccentColorForClass("dependency-node--mesh");
            }

            if (typeName.IndexOf("ScriptableObject", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GetAccentColorForClass("dependency-node--scriptable-object");
            }

            if (typeName.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GetAccentColorForClass("dependency-node--camera");
            }

            if (typeName.IndexOf("Canvas", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GetAccentColorForClass("dependency-node--canvas");
            }

            if (typeName.IndexOf("Light", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GetAccentColorForClass("dependency-node--light");
            }

            if (string.Equals(typeName, "Object", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "GameObject", StringComparison.OrdinalIgnoreCase))
            {
                return GetAccentColorForClass("dependency-node--object");
            }

            return GetAccentColorForClass("dependency-node--default");
        }

        private static Color GetAccentColorForClass(string className)
        {
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

        private static string GetIssueNodeTypeClass(DependencyNode node)
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
