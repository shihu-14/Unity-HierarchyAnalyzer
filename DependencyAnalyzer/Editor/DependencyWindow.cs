using System.IO;
using DependencyAnalyzer.Editor.Controller;
using DependencyAnalyzer.Editor.UI.GraphView;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor
{
    public sealed class DependencyWindow : EditorWindow
    {
        private DependencyGraphController controller;
        private DependencyGraphView graphView;

        [MenuItem("Tools/Dependency Analyzer/Open Graph")]
        public static void Open()
        {
            var window = GetWindow<DependencyWindow>();
            window.titleContent = new GUIContent("Dependency Graph");
            window.minSize = new Vector2(720f, 480f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("dependency-window");
            AddStyleSheetToRoot("NodeStyle.uss");
            AddStyleSheetToRoot("EdgeStyle.uss");

            var visualTree = LoadEditorAsset<VisualTreeAsset>("GraphWindow.uxml");
            if (visualTree != null)
            {
                visualTree.CloneTree(rootVisualElement);
            }
            else
            {
                BuildFallbackLayout(rootVisualElement);
            }

            var graphContainer = rootVisualElement.Q<VisualElement>("graph-container");
            if (graphContainer == null)
            {
                graphContainer = rootVisualElement;
            }

            graphView = new DependencyGraphView();
            graphView.StretchToParentSize();
            graphContainer.Add(graphView);

            controller?.Dispose();
            controller = new DependencyGraphController(rootVisualElement, graphView);
        }

        private void OnDisable()
        {
            controller?.Dispose();
            controller = null;
        }

        private static void BuildFallbackLayout(VisualElement root)
        {
            var toolbar = new VisualElement { name = "dependency-toolbar" };
            toolbar.AddToClassList("dependency-toolbar");

            var scanButton = new Button { name = "scan-button", text = "Scan" };
            var cancelButton = new Button { name = "cancel-button", text = "Cancel" };
            var depthField = new IntegerField("Depth") { name = "depth-field", value = 3 };
            var statusLabel = new Label("Ready") { name = "status-label" };

            toolbar.Add(scanButton);
            toolbar.Add(cancelButton);
            toolbar.Add(depthField);
            toolbar.Add(statusLabel);
            root.Add(toolbar);

            var graphContainer = new VisualElement { name = "graph-container" };
            graphContainer.AddToClassList("dependency-graph-container");
            root.Add(graphContainer);
        }

        private void AddStyleSheetToRoot(string fileName)
        {
            var styleSheet = LoadEditorAsset<StyleSheet>(fileName);
            if (styleSheet != null && !rootVisualElement.styleSheets.Contains(styleSheet))
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
        }

        private static T LoadEditorAsset<T>(string fileName) where T : Object
        {
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var guids = AssetDatabase.FindAssets(nameWithoutExtension);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (Path.GetFileName(path) == fileName)
                {
                    return AssetDatabase.LoadAssetAtPath<T>(path);
                }
            }

            return null;
        }
    }
}
