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

            EnsureColorPalette(rootVisualElement);

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

            var loadButton = new Button { name = "load-button", text = "Load" };
            var loadProgressLabel = new Label { name = "load-progress-label" };
            loadProgressLabel.AddToClassList("dependency-load-progress-label");
            var zoomStepControl = new VisualElement { name = "zoom-step-control" };
            zoomStepControl.AddToClassList("dependency-zoom-step-control");
            var zoomScaleLabel = new Label("Zoom Scale") { name = "zoom-scale-label" };
            zoomScaleLabel.AddToClassList("dependency-zoom-scale-label");
            var zoomStepSlider = new Slider { name = "zoom-step-slider", lowValue = 0.001f, highValue = 0.03f, value = 0.004f };
            var searchControl = new VisualElement { name = "search-control" };
            searchControl.AddToClassList("dependency-search-control");
            var searchFieldWrap = new VisualElement { name = "search-field-wrap" };
            searchFieldWrap.AddToClassList("dependency-search-field-wrap");
            var searchField = new TextField { name = "search-field" };
            var searchSuggestionList = new VisualElement { name = "search-suggestion-list" };
            searchSuggestionList.AddToClassList("dependency-search-suggestion-list");
            var searchPreviousButton = new Button { name = "search-previous-button" };
            searchPreviousButton.AddToClassList("dependency-search-arrow-button");
            var searchNextButton = new Button { name = "search-next-button" };
            searchNextButton.AddToClassList("dependency-search-arrow-button");
            var searchCountLabel = new Label("0 / 0") { name = "search-count-label" };
            searchCountLabel.AddToClassList("dependency-search-count");
            var searchDivider = new VisualElement { name = "search-divider" };
            searchDivider.AddToClassList("dependency-search-divider");

            zoomStepControl.Add(zoomScaleLabel);
            zoomStepControl.Add(zoomStepSlider);
            searchFieldWrap.Add(searchField);
            searchFieldWrap.Add(searchSuggestionList);
            searchControl.Add(searchFieldWrap);
            searchControl.Add(searchCountLabel);
            searchControl.Add(searchDivider);
            searchControl.Add(searchPreviousButton);
            searchControl.Add(searchNextButton);
            toolbar.Add(loadButton);
            toolbar.Add(loadProgressLabel);
            toolbar.Add(zoomStepControl);
            toolbar.Add(searchControl);
            root.Add(toolbar);

            var graphContainer = new VisualElement { name = "graph-container" };
            graphContainer.AddToClassList("dependency-graph-container");
            root.Add(graphContainer);

            var issuePanel = new VisualElement { name = "issue-panel" };
            issuePanel.AddToClassList("dependency-issue-panel");
            var issueResizeHandle = new VisualElement { name = "issue-resize-handle" };
            issueResizeHandle.AddToClassList("dependency-issue-resize-handle");
            var issueHeader = new VisualElement { name = "issue-panel-header" };
            issueHeader.AddToClassList("dependency-issue-header");
            var issueTitleLabel = new Label("Issues (0)") { name = "issue-title-label" };
            issueTitleLabel.AddToClassList("dependency-issue-title");
            var issueToggleButton = new Button { name = "issue-toggle-button" };
            var issueList = new ScrollView { name = "issue-list" };
            issueList.AddToClassList("dependency-issue-list");
            issueHeader.Add(issueTitleLabel);
            issueHeader.Add(issueToggleButton);
            issuePanel.Add(issueResizeHandle);
            issuePanel.Add(issueHeader);
            issuePanel.Add(issueList);
            root.Add(issuePanel);
        }

        private static void EnsureColorPalette(VisualElement root)
        {
            var toolbar = root.Q<VisualElement>("dependency-toolbar");
            if (toolbar == null)
            {
                return;
            }

            var existing = root.Q<VisualElement>("color-palette-preview");
            if (existing != null)
            {
                existing.RemoveFromHierarchy();
            }

            var palette = new VisualElement { name = "color-palette-preview" };
            palette.AddToClassList("dependency-color-palette-preview");
            AddColorSwatch(palette, "blue", "Blue #52A7FF");
            AddColorSwatch(palette, "sky", "Sky #6BB8D6");
            AddColorSwatch(palette, "teal", "Teal #45C7AE");
            AddColorSwatch(palette, "green", "Green #69B779");
            AddColorSwatch(palette, "lime", "Lime #9ED384");
            AddColorSwatch(palette, "yellow", "Yellow #D9C766");
            AddColorSwatch(palette, "amber", "Amber #F2B046");
            AddColorSwatch(palette, "orange", "Orange #F28B60");
            AddColorSwatch(palette, "red", "Red #EC4E4E");
            AddColorSwatch(palette, "pink", "Pink #E268AD");
            AddColorSwatch(palette, "purple", "Purple #B178C6");
            AddColorSwatch(palette, "violet", "Violet #7D8CFF");
            AddColorSwatch(palette, "brown", "Brown #C48267");
            AddColorSwatch(palette, "neutral", "Neutral #A69B8E");
            toolbar.Add(palette);
        }

        private static void AddColorSwatch(VisualElement palette, string className, string tooltip)
        {
            var swatch = new VisualElement();
            swatch.tooltip = tooltip;
            swatch.AddToClassList("dependency-color-swatch");
            swatch.AddToClassList("dependency-color-swatch--" + className);
            palette.Add(swatch);
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
