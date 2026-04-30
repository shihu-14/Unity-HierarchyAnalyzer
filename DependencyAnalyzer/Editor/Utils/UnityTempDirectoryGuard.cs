using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Utils
{
    [InitializeOnLoad]
    public static class UnityTempDirectoryGuard
    {
        static UnityTempDirectoryGuard()
        {
            EnsureProjectTempDirectoryExists();
            EditorApplication.delayCall += EnsureProjectTempDirectoryExists;
        }

        public static void EnsureProjectTempDirectoryExists()
        {
            try
            {
                var assetsDirectory = new DirectoryInfo(Application.dataPath);
                var projectRoot = assetsDirectory.Parent;
                if (projectRoot == null)
                {
                    return;
                }

                var tempPath = Path.Combine(projectRoot.FullName, "Temp");
                if (!Directory.Exists(tempPath))
                {
                    Directory.CreateDirectory(tempPath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Dependency Analyzer could not ensure the Unity Temp directory exists: " + exception.Message);
            }
        }
    }
}
