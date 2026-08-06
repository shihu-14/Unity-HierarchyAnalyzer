using System;
using DependencyAnalyzer.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public static class DependencyIconProvider
    {
        private const string WarningIssueIconPath = "Assets/DependencyAnalyzer/Editor/UI/Icons/issue-warning.png";
        private const string ErrorIssueIconPath = "Assets/DependencyAnalyzer/Editor/UI/Icons/issue-error.png";

        private static Texture warningIssueIcon;
        private static Texture errorIssueIcon;

        public static Texture GetIcon(DependencyNode node)
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
            return GetCustomIssueIcon(WarningIssueIconPath, ref warningIssueIcon, "console.warnicon.sml");
        }

        public static Texture GetIssueIcon(DependencyScanIssueSeverity severity)
        {
            switch (severity)
            {
                case DependencyScanIssueSeverity.Error:
                    return GetCustomIssueIcon(ErrorIssueIconPath, ref errorIssueIcon, "console.erroricon.sml");
                case DependencyScanIssueSeverity.Info:
                    return EditorGUIUtility.IconContent("console.infoicon.sml").image;
                default:
                    return GetWarningIcon();
            }
        }

        private static Texture GetCustomIssueIcon(string assetPath, ref Texture cachedIcon, string fallbackIconName)
        {
            if (cachedIcon != null)
            {
                return cachedIcon;
            }

            cachedIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            return cachedIcon != null
                ? cachedIcon
                : EditorGUIUtility.IconContent(fallbackIconName).image;
        }
    }
}
