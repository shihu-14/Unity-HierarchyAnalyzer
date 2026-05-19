using System.IO;
using DependencyAnalyzer.Editor.Controller;
using DependencyAnalyzer.Editor.UI.GraphView;
using DependencyAnalyzer.Editor.Utils;
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
            UnityTempDirectoryGuard.EnsureProjectTempDirectoryExists();
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
            var zoomStepControl = new VisualElement { name = "zoom-step-control" };
            zoomStepControl.AddToClassList("dependency-zoom-step-control");
            var zoomStepSlider = new Slider("Zoom Step", 0.001f, 0.03f) { name = "zoom-step-slider", value = 0.004f };
            var zoomStepValueLabel = new Label("0.004") { name = "zoom-step-value-label" };
            zoomStepValueLabel.AddToClassList("dependency-zoom-step-value");
            var searchControl = new VisualElement { name = "search-control" };
            searchControl.AddToClassList("dependency-search-control");
            var searchField = new TextField("Search") { name = "search-field" };
            var searchPreviousButton = new Button { name = "search-previous-button", text = "Prev" };
            var searchNextButton = new Button { name = "search-next-button", text = "Next" };
            var searchCountLabel = new Label("0/0") { name = "search-count-label" };
            searchCountLabel.AddToClassList("dependency-search-count");
            var searchFilterToggle = new Toggle("Filter") { name = "search-filter-toggle" };
            var statusLabel = new Label("Ready") { name = "status-label" };

            zoomStepControl.Add(zoomStepSlider);
            zoomStepControl.Add(zoomStepValueLabel);
            searchControl.Add(searchField);
            searchControl.Add(searchPreviousButton);
            searchControl.Add(searchNextButton);
            searchControl.Add(searchCountLabel);
            searchControl.Add(searchFilterToggle);
            toolbar.Add(scanButton);
            toolbar.Add(cancelButton);
            toolbar.Add(zoomStepControl);
            toolbar.Add(searchControl);
            toolbar.Add(statusLabel);
            root.Add(toolbar);

            var graphContainer = new VisualElement { name = "graph-container" };
            graphContainer.AddToClassList("dependency-graph-container");
            root.Add(graphContainer);

            var issuePanel = new VisualElement { name = "issue-panel" };
            issuePanel.AddToClassList("dependency-issue-panel");
            var issueHeader = new VisualElement { name = "issue-panel-header" };
            issueHeader.AddToClassList("dependency-issue-header");
            var issueTitleLabel = new Label("Issues (0)") { name = "issue-title-label" };
            issueTitleLabel.AddToClassList("dependency-issue-title");
            var issueToggleButton = new Button { name = "issue-toggle-button", text = "Hide" };
            var issueList = new ScrollView { name = "issue-list" };
            issueList.AddToClassList("dependency-issue-list");
            issueHeader.Add(issueTitleLabel);
            issueHeader.Add(issueToggleButton);
            issuePanel.Add(issueHeader);
            issuePanel.Add(issueList);
            root.Add(issuePanel);
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
