using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DependencyAnalyzer.Editor.Controller.Issues;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Scanners;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.UI.Controls;
using DependencyAnalyzer.Editor.UI.GraphView;
using DependencyAnalyzer.Editor.UI.Issues;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Controller
{
    public sealed partial class DependencyGraphController : IDisposable
    {
        private const float DefaultIssuePanelHeight = 148f;
        private const float CollapsedIssuePanelHeight = 36f;
        private const float MinimumIssuePanelHeight = 64f;
        private const float MaximumIssuePanelHeight = 460f;

        private readonly DependencyGraphView graphView;
        private readonly VisualElement root;
        private readonly ScannerOrchestrator scannerOrchestrator;
        private readonly DependencyNodeCache cache;
        private readonly EditorSelectionSync selectionSync;
        private readonly Button loadButton;
        private readonly Label loadProgressLabel;
        private readonly Slider zoomStepSlider;
        private readonly VisualElement searchControl;
        private readonly TextField searchField;
        private readonly SearchIconElement searchIcon;
        private readonly VisualElement searchSuggestionList;
        private readonly Button searchPreviousButton;
        private readonly Button searchNextButton;
        private readonly Label searchCountLabel;
        private readonly Toggle searchFilterToggle;
        private readonly Label statusLabel;
        private readonly VisualElement issuePanel;
        private readonly VisualElement issueResizeHandle;
        private readonly ScrollView issueList;
        private readonly Label issueTitleLabel;
        private readonly Button issueWarningFilterButton;
        private readonly Button issueErrorFilterButton;
        private readonly Button issueToggleButton;
        private Label issueWarningCountLabel;
        private Label issueErrorCountLabel;

        private CancellationTokenSource scanCancellation;
        private DependencyGraph currentGraph;
        private bool disposed;
        private bool hierarchyRefreshQueued;
        private bool suppressNextSelectionFocus;
        private bool issueListVisible = true;
        private bool issueWarningsVisible = true;
        private bool issueErrorsVisible = true;
        private bool hasCompletedLoad;
        private bool isLoading;
        private bool isResizingIssuePanel;
        private bool isSearchFieldFocused;
        private float issuePanelHeight = DefaultIssuePanelHeight;
        private float issueResizeStartMouseY;
        private float issueResizeStartHeight;
        private int suppressedSelectionInstanceId;

        public DependencyGraphController(VisualElement root, DependencyGraphView graphView)
        {
            this.root = root;
            this.graphView = graphView;
            scannerOrchestrator = new ScannerOrchestrator();
            cache = new DependencyNodeCache();
            selectionSync = new EditorSelectionSync();

            loadButton = root.Q<Button>("load-button") ?? root.Q<Button>("scan-button");
            loadProgressLabel = root.Q<Label>("load-progress-label");
            zoomStepSlider = root.Q<Slider>("zoom-step-slider");
            searchControl = root.Q<VisualElement>("search-control");
            searchField = root.Q<TextField>("search-field");
            searchIcon = EnsureSearchIcon(root.Q<VisualElement>("search-field-wrap"));
            searchSuggestionList = root.Q<VisualElement>("search-suggestion-list");
            searchPreviousButton = root.Q<Button>("search-previous-button");
            searchNextButton = root.Q<Button>("search-next-button");
            searchCountLabel = root.Q<Label>("search-count-label");
            searchFilterToggle = root.Q<Toggle>("search-filter-toggle");
            statusLabel = root.Q<Label>("status-label");
            issuePanel = root.Q<VisualElement>("issue-panel");
            issueResizeHandle = root.Q<VisualElement>("issue-resize-handle");
            issueList = root.Q<ScrollView>("issue-list");
            issueTitleLabel = root.Q<Label>("issue-title-label");
            issueWarningFilterButton = root.Q<Button>("issue-warning-filter-button");
            issueErrorFilterButton = root.Q<Button>("issue-error-filter-button");
            issueToggleButton = root.Q<Button>("issue-toggle-button");

            if (loadButton != null)
            {
                loadButton.clicked += HandleLoadClicked;
                UpdateLoadButtonText();
            }

            HideLoadProgress();

            if (searchField != null)
            {
                searchField.RegisterValueChangedCallback(HandleSearchChanged);
                searchField.RegisterCallback<KeyDownEvent>(HandleSearchKeyDown, TrickleDown.TrickleDown);
                searchField.RegisterCallback<FocusInEvent>(HandleSearchFocusIn);
                searchField.RegisterCallback<FocusOutEvent>(HandleSearchFocusOut);
            }

            if (searchIcon != null)
            {
                searchIcon.RegisterCallback<MouseDownEvent>(HandleSearchIconMouseDown);
                UpdateSearchIconVisibility();
            }

            if (searchPreviousButton != null)
            {
                SetSearchArrowIcon(searchPreviousButton, true);
                searchPreviousButton.tooltip = "Previous";
                searchPreviousButton.clicked += HandleSearchPreviousClicked;
            }

            if (searchNextButton != null)
            {
                SetSearchArrowIcon(searchNextButton, false);
                searchNextButton.tooltip = "Next";
                searchNextButton.clicked += HandleSearchNextClicked;
            }

            if (searchFilterToggle != null)
            {
                searchFilterToggle.RegisterValueChangedCallback(HandleSearchFilterChanged);
            }

            issueWarningCountLabel = ConfigureIssueFilterButton(
                issueWarningFilterButton,
                DependencyScanIssueSeverity.Warning,
                "Toggle all Console and Analyzer warnings",
                ToggleIssueWarnings);
            issueErrorCountLabel = ConfigureIssueFilterButton(
                issueErrorFilterButton,
                DependencyScanIssueSeverity.Error,
                "Toggle all Console and Analyzer errors",
                ToggleIssueErrors);

            if (issueToggleButton != null)
            {
                issueToggleButton.AddToClassList("dependency-issue-toggle-button");
                issueToggleButton.clicked += ToggleIssueList;
                UpdateIssueToggleIcon();
            }

            if (issueResizeHandle != null)
            {
                issueResizeHandle.RegisterCallback<MouseDownEvent>(HandleIssueResizeMouseDown);
                issueResizeHandle.RegisterCallback<MouseMoveEvent>(HandleIssueResizeMouseMove);
                issueResizeHandle.RegisterCallback<MouseUpEvent>(HandleIssueResizeMouseUp);
            }

            var settings = AnalyzerSettings.LoadOrCreateRuntimeSettings();
            InitializeZoomFields(settings);
            ApplyGraphSettings(settings);
            graphView.NodeSelected += HandleNodeSelected;
            Selection.selectionChanged += HandleEditorSelectionChanged;
            EditorApplication.delayCall += RequestInitialScan;
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
            root.RegisterCallback<KeyDownEvent>(HandleGlobalKeyDown, TrickleDown.TrickleDown);
            root.RegisterCallback<MouseDownEvent>(HandleRootMouseDown, TrickleDown.TrickleDown);
            UpdateSearchState(new DependencyGraphView.SearchResultState(-1, 0));
            PopulateIssuePanel(null);
            ApplyIssuePanelHeight();
            SetStatus("Ready");
        }

        public void Dispose()
        {
            disposed = true;
            EditorApplication.delayCall -= RequestInitialScan;
            EditorApplication.delayCall -= RunQueuedHierarchyScan;
            EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
            Selection.selectionChanged -= HandleEditorSelectionChanged;
            graphView.NodeSelected -= HandleNodeSelected;

            if (loadButton != null)
            {
                loadButton.clicked -= HandleLoadClicked;
            }

            if (zoomStepSlider != null)
            {
                zoomStepSlider.UnregisterValueChangedCallback(HandleZoomChanged);
            }

            if (searchField != null)
            {
                searchField.UnregisterValueChangedCallback(HandleSearchChanged);
                searchField.UnregisterCallback<KeyDownEvent>(HandleSearchKeyDown, TrickleDown.TrickleDown);
                searchField.UnregisterCallback<FocusInEvent>(HandleSearchFocusIn);
                searchField.UnregisterCallback<FocusOutEvent>(HandleSearchFocusOut);
            }

            if (searchIcon != null)
            {
                searchIcon.UnregisterCallback<MouseDownEvent>(HandleSearchIconMouseDown);
            }

            if (searchPreviousButton != null)
            {
                searchPreviousButton.clicked -= HandleSearchPreviousClicked;
            }

            if (searchNextButton != null)
            {
                searchNextButton.clicked -= HandleSearchNextClicked;
            }

            if (searchFilterToggle != null)
            {
                searchFilterToggle.UnregisterValueChangedCallback(HandleSearchFilterChanged);
            }

            if (issueWarningFilterButton != null)
            {
                issueWarningFilterButton.clicked -= ToggleIssueWarnings;
            }

            if (issueErrorFilterButton != null)
            {
                issueErrorFilterButton.clicked -= ToggleIssueErrors;
            }

            if (issueToggleButton != null)
            {
                issueToggleButton.clicked -= ToggleIssueList;
            }

            if (issueResizeHandle != null)
            {
                issueResizeHandle.UnregisterCallback<MouseDownEvent>(HandleIssueResizeMouseDown);
                issueResizeHandle.UnregisterCallback<MouseMoveEvent>(HandleIssueResizeMouseMove);
                issueResizeHandle.UnregisterCallback<MouseUpEvent>(HandleIssueResizeMouseUp);
            }

            root?.UnregisterCallback<KeyDownEvent>(HandleGlobalKeyDown, TrickleDown.TrickleDown);
            root?.UnregisterCallback<MouseDownEvent>(HandleRootMouseDown, TrickleDown.TrickleDown);

            CancelActiveScan();
        }

        private void RequestInitialScan()
        {
            if (disposed)
            {
                return;
            }

            _ = ScanAsync();
        }

        private void HandleLoadClicked()
        {
            _ = ScanAsync();
        }

        private void HandleHierarchyChanged()
        {
            if (disposed || currentGraph == null || hierarchyRefreshQueued)
            {
                return;
            }

            hierarchyRefreshQueued = true;
            EditorApplication.delayCall += RunQueuedHierarchyScan;
        }

        private void RunQueuedHierarchyScan()
        {
            EditorApplication.delayCall -= RunQueuedHierarchyScan;
            hierarchyRefreshQueued = false;
            if (disposed)
            {
                return;
            }

            _ = ScanAsync();
        }

        private async Task ScanAsync()
        {
            if (scanCancellation != null)
            {
                return;
            }

            var activeCancellation = new CancellationTokenSource();
            scanCancellation = activeCancellation;

            var token = activeCancellation.Token;
            cache.Clear();
            isLoading = true;
            SetLoadControlsEnabled(false);
            SetLoadProgress(0f);
            SetStatus(string.Empty);

            try
            {
                var settings = AnalyzerSettings.LoadOrCreateRuntimeSettings();
                ApplyGraphSettings(settings);
                var progress = new Progress<ScanProgress>(scanProgress => HandleScanProgress(scanProgress, activeCancellation));
                currentGraph = await scannerOrchestrator.ScanAsync(settings, cache, progress, token);
                graphView.Populate(currentGraph, settings.InitialExpansionDepth);
                UpdateSearchState(graphView.SetSearch(GetSearchQuery(), IsSearchFilterEnabled(), false));
                PopulateIssuePanel(currentGraph);
                hasCompletedLoad = true;
                UpdateLoadButtonText();
                SetStatus("Completed: " + currentGraph.Nodes.Count + " nodes, "
                    + currentGraph.Edges.Count + " edges, "
                    + IssuePanelEntryBuilder.Count(currentGraph, IssueTargetResolver.FindIssueEntryTargetNodeId) + " issues");
            }
            catch (OperationCanceledException)
            {
                SetStatus("Canceled");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("Failed: " + exception.Message);
            }
            finally
            {
                if (scanCancellation == activeCancellation)
                {
                    activeCancellation.Dispose();
                    scanCancellation = null;
                }

                isLoading = false;
                HideLoadProgress();
                SetLoadControlsEnabled(true);
            }
        }

        private void HandleNodeSelected(DependencyNode node)
        {
            var target = selectionSync.ResolveObject(node);
            if (target == null)
            {
                suppressNextSelectionFocus = false;
                suppressedSelectionInstanceId = 0;
                return;
            }

            suppressNextSelectionFocus = true;
            suppressedSelectionInstanceId = target.GetInstanceID();
            selectionSync.PingAndSelect(target);
        }

        private void HandleEditorSelectionChanged()
        {
            if (disposed || currentGraph == null)
            {
                return;
            }

            var selectedObject = Selection.activeObject;
            if (selectedObject == null)
            {
                return;
            }

            if (suppressNextSelectionFocus)
            {
                if (selectedObject.GetInstanceID() == suppressedSelectionInstanceId)
                {
                    suppressNextSelectionFocus = false;
                    suppressedSelectionInstanceId = 0;
                    return;
                }

                suppressNextSelectionFocus = false;
                suppressedSelectionInstanceId = 0;
            }

            graphView.FocusNodeByInstanceId(selectedObject.GetInstanceID());
        }

    }
}
