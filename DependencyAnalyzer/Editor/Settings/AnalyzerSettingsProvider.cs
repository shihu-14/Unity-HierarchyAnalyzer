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
                    var settings = AnalyzerSettings.GetOrCreateSettings();
                    var serializedSettings = new SerializedObject(settings);

                    rootElement.Add(new PropertyField(serializedSettings.FindProperty("excludedFolderPaths"), "Excluded Folders"));
                    rootElement.Add(new PropertyField(serializedSettings.FindProperty("excludedExtensions"), "Excluded Extensions"));
                    rootElement.Add(new PropertyField(serializedSettings.FindProperty("scanYieldBatchSize"), "Scan Yield Batch Size"));
                    rootElement.Add(new PropertyField(serializedSettings.FindProperty("initialExpansionDepth"), "Initial Expansion Depth"));
                    rootElement.Bind(serializedSettings);
                }
            };
        }
    }
}
