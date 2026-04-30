using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Settings
{
    public static class AnalyzerSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/Dependency Analyzer", SettingsScope.Project, new HashSet<string>
            {
                "dependency",
                "analyzer",
                "exclude",
                "scan",
                "graph"
            })
            {
                label = "Dependency Analyzer",
                activateHandler = (_, rootElement) =>
                {
                    BuildSettingsUI(rootElement, AnalyzerSettings.LoadOrCreateRuntimeSettings());
                }
            };
        }

        private static void BuildSettingsUI(VisualElement rootElement, AnalyzerSettings settings)
        {
            rootElement.Clear();

            if (AnalyzerSettings.LoadSettingsAsset() == null)
            {
                var createAssetButton = new Button(() =>
                {
                    BuildSettingsUI(rootElement, AnalyzerSettings.GetOrCreateSettingsAsset());
                })
                {
                    text = "Create Shared Settings Asset"
                };
                rootElement.Add(createAssetButton);
            }

            var serializedSettings = new SerializedObject(settings);
            rootElement.Add(new PropertyField(serializedSettings.FindProperty("excludedFolderPaths"), "Excluded Folders"));
            rootElement.Add(new PropertyField(serializedSettings.FindProperty("excludedExtensions"), "Excluded Extensions"));
            rootElement.Add(new PropertyField(serializedSettings.FindProperty("scanYieldBatchSize"), "Scan Yield Batch Size"));
            rootElement.Add(new PropertyField(serializedSettings.FindProperty("initialExpansionDepth"), "Initial Expansion Depth"));
            rootElement.Add(new PropertyField(serializedSettings.FindProperty("zoomStep"), "Zoom Step"));
            rootElement.Bind(serializedSettings);
        }
    }
}
