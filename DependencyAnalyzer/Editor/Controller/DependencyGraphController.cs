using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Scanners;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.UI.GraphView;
using DependencyAnalyzer.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Controller
{
    public sealed class DependencyGraphController : IDisposable
    {
        private const float DefaultIssuePanelHeight = 148f;
        private const float CollapsedIssuePanelHeight = 36f;
        private const float MinimumIssuePanelHeight = 64f;
        private const float MaximumIssuePanelHeight = 460f;

        private readonly DependencyGraphView graphView;
        private readonly VisualElement root;
        private readonly ScannerOrchestrator scannerOrchestrator;
        private readonly DependencyCache cache;
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
        private DependencyGraphData currentGraph;
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
            cache = new DependencyCache();
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
                searchPreviousButton.tooltip = "Previous result";
                searchPreviousButton.clicked += HandleSearchPreviousClicked;
            }

            if (searchNextButton != null)
            {
                SetSearchArrowIcon(searchNextButton, false);
                searchNextButton.tooltip = "Next result";
                searchNextButton.clicked += HandleSearchNextClicked;
            }

            if (searchFilterToggle != null)
            {
                searchFilterToggle.RegisterValueChangedCallback(HandleSearchFilterChanged);
            }

            issueWarningCountLabel = ConfigureIssueFilterButton(
                issueWarningFilterButton,
                DependencyScanIssueSeverity.Warning,
                "Toggle warnings",
                ToggleIssueWarnings);
            issueErrorCountLabel = ConfigureIssueFilterButton(
                issueErrorFilterButton,
                DependencyScanIssueSeverity.Error,
                "Toggle errors",
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
                    + CountIssueEntries(currentGraph) + " issues");
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

        private void ApplyGraphSettings(AnalyzerSettings settings)
        {
            var step = zoomStepSlider == null ? settings.ZoomStep : zoomStepSlider.value;

            step = Mathf.Clamp(step, 0.001f, 0.03f);
            SetZoomSliderValue(step);
            graphView.ConfigureZoom(AnalyzerSettings.DefaultZoomMin, AnalyzerSettings.DefaultZoomMax, step);
        }

        private void HandleScanProgress(ScanProgress progress, CancellationTokenSource activeCancellation)
        {
            if (!isLoading || !ReferenceEquals(scanCancellation, activeCancellation))
            {
                return;
            }

            SetLoadProgress(progress.Total <= 0 ? 0f : progress.Ratio);
        }

        private void InitializeZoomFields(AnalyzerSettings settings)
        {
            SetZoomSliderValue(settings.ZoomStep);
            if (zoomStepSlider != null)
            {
                zoomStepSlider.RegisterValueChangedCallback(HandleZoomChanged);
            }
        }

        private void HandleZoomChanged(ChangeEvent<float> evt)
        {
            ApplyGraphSettings(AnalyzerSettings.LoadOrCreateRuntimeSettings());
        }

        private void SetZoomSliderValue(float value)
        {
            if (zoomStepSlider == null)
            {
                return;
            }

            zoomStepSlider.SetValueWithoutNotify(value);
        }

        private void HandleSearchChanged(ChangeEvent<string> evt)
        {
            UpdateSearchState(graphView.SetSearch(evt.newValue, IsSearchFilterEnabled(), true));
            UpdateSearchIconVisibility();
        }

        private void HandleSearchFocusIn(FocusInEvent evt)
        {
            isSearchFieldFocused = true;
            UpdateSearchIconVisibility();
        }

        private void HandleSearchFocusOut(FocusOutEvent evt)
        {
            isSearchFieldFocused = false;
            UpdateSearchIconVisibility();
        }

        private void HandleSearchIconMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            searchField?.Focus();
            evt.PreventDefault();
            evt.StopPropagation();
        }

        private void HandleSearchFilterChanged(ChangeEvent<bool> evt)
        {
            UpdateSearchState(graphView.SetSearch(GetSearchQuery(), evt.newValue, true));
        }

        private void HandleSearchPreviousClicked()
        {
            UpdateSearchState(graphView.FocusNextSearchResult(true));
        }

        private void HandleSearchNextClicked()
        {
            UpdateSearchState(graphView.FocusNextSearchResult(false));
        }

        private void HandleSearchKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                UpdateSearchState(graphView.FocusNextSearchResult((evt.modifiers & EventModifiers.Shift) != 0));
                evt.PreventDefault();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Escape)
            {
                if (searchField != null)
                {
                    searchField.value = string.Empty;
                }

                HideSearchSuggestions();
                evt.PreventDefault();
                evt.StopPropagation();
            }
        }

        private void HandleGlobalKeyDown(KeyDownEvent evt)
        {
            var isFindShortcut = evt.keyCode == KeyCode.F
                && ((evt.modifiers & EventModifiers.Command) != 0 || (evt.modifiers & EventModifiers.Control) != 0);
            if (!isFindShortcut || searchField == null)
            {
                return;
            }

            searchField.Focus();
            evt.PreventDefault();
            evt.StopPropagation();
        }

        private string GetSearchQuery()
        {
            return searchField == null ? string.Empty : searchField.value;
        }

        private bool IsSearchFilterEnabled()
        {
            return searchFilterToggle != null && searchFilterToggle.value;
        }

        private static SearchIconElement EnsureSearchIcon(VisualElement searchFieldWrap)
        {
            if (searchFieldWrap == null)
            {
                return null;
            }

            var existing = searchFieldWrap.Q<SearchIconElement>("search-icon");
            if (existing != null)
            {
                return existing;
            }

            var icon = new SearchIconElement { name = "search-icon" };
            icon.AddToClassList("dependency-search-icon");
            icon.tooltip = "Focus search";
            searchFieldWrap.Add(icon);
            icon.BringToFront();
            return icon;
        }

        private void UpdateSearchIconVisibility()
        {
            if (searchIcon == null)
            {
                return;
            }

            searchIcon.style.display = !isSearchFieldFocused && string.IsNullOrEmpty(GetSearchQuery())
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void UpdateSearchState(DependencyGraphView.SearchResultState state)
        {
            if (searchCountLabel != null)
            {
                searchCountLabel.text = state.DisplayIndex + " / " + state.Total;
            }

            var hasResults = state.Total > 0;
            searchPreviousButton?.SetEnabled(hasResults);
            searchNextButton?.SetEnabled(hasResults);
            UpdateSearchSuggestions(state.Suggestions);
        }

        private void UpdateSearchSuggestions(IReadOnlyList<DependencyGraphView.SearchSuggestion> suggestions)
        {
            if (searchSuggestionList == null)
            {
                return;
            }

            searchSuggestionList.Clear();
            if (string.IsNullOrWhiteSpace(GetSearchQuery()) || suggestions == null || suggestions.Count == 0)
            {
                HideSearchSuggestions();
                return;
            }

            for (var i = 0; i < suggestions.Count; i++)
            {
                var suggestion = suggestions[i];
                var row = new VisualElement();
                row.AddToClassList("dependency-search-suggestion-row");
                row.tooltip = suggestion.Path;
                row.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    SelectSearchSuggestion(suggestion);
                    evt.PreventDefault();
                    evt.StopPropagation();
                });

                var title = new Label(suggestion.DisplayName);
                title.AddToClassList("dependency-search-suggestion-title");
                var detail = new Label(suggestion.Detail);
                detail.AddToClassList("dependency-search-suggestion-detail");
                row.Add(title);
                row.Add(detail);
                searchSuggestionList.Add(row);
            }

            searchSuggestionList.style.display = DisplayStyle.Flex;
        }

        private void SelectSearchSuggestion(DependencyGraphView.SearchSuggestion suggestion)
        {
            if (searchField == null)
            {
                return;
            }

            searchField.SetValueWithoutNotify(suggestion.DisplayName);
            UpdateSearchState(graphView.SetSearch(suggestion.DisplayName, IsSearchFilterEnabled(), false));
            graphView.FocusNode(suggestion.NodeId, true);
            HideSearchSuggestions();
            searchField.Focus();
        }

        private void HideSearchSuggestions()
        {
            if (searchSuggestionList != null)
            {
                searchSuggestionList.style.display = DisplayStyle.None;
            }
        }

        private void HandleNodeSelected(DependencyNodeData node)
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

        private void CancelActiveScan()
        {
            if (scanCancellation == null || scanCancellation.IsCancellationRequested)
            {
                return;
            }

            scanCancellation.Cancel();
        }

        private void SetLoadControlsEnabled(bool enabled)
        {
            loadButton?.SetEnabled(enabled);
        }

        private void UpdateLoadButtonText()
        {
            if (loadButton != null)
            {
                loadButton.text = hasCompletedLoad ? "Reload" : "Load";
            }
        }

        private void SetLoadProgress(float ratio)
        {
            if (loadProgressLabel == null)
            {
                return;
            }

            var completed = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(ratio) * 100f), 0, 100);
            loadProgressLabel.text = completed + "/100";
            loadProgressLabel.style.display = DisplayStyle.Flex;
        }

        private void HideLoadProgress()
        {
            if (loadProgressLabel != null)
            {
                loadProgressLabel.text = string.Empty;
                loadProgressLabel.style.display = DisplayStyle.Flex;
            }
        }

        private static void SetSearchArrowIcon(Button button, bool pointsUp)
        {
            SetChevronButtonIcon(button, pointsUp, 0.45f);
        }

        private static void SetChevronButtonIcon(Button button, bool pointsUp, float verticalScale)
        {
            button.text = string.Empty;
            button.Clear();
            var icon = new ChevronIcon(pointsUp, verticalScale);
            icon.StretchToParentSize();
            button.Add(icon);
        }

        private sealed class SearchIconElement : VisualElement
        {
            public SearchIconElement()
            {
                pickingMode = PickingMode.Position;
                generateVisualContent += DrawSearchIcon;
            }

            private void DrawSearchIcon(MeshGenerationContext context)
            {
                var rect = contentRect;
                if (rect.width <= 0f || rect.height <= 0f)
                {
                    return;
                }

                var painter = context.painter2D;
                painter.strokeColor = Color.white;
                painter.lineWidth = 1.15f;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;

                var center = new Vector2(rect.x + rect.width * 0.43f, rect.y + rect.height * 0.42f);
                var radius = Mathf.Min(rect.width, rect.height) * 0.25f;
                const int segments = 36;
                painter.BeginPath();
                for (var i = 0; i <= segments; i++)
                {
                    var angle = i / (float)segments * Mathf.PI * 2f;
                    var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (i == 0)
                    {
                        painter.MoveTo(point);
                    }
                    else
                    {
                        painter.LineTo(point);
                    }
                }

                painter.Stroke();

                var handleStart = center + new Vector2(radius * 0.68f, radius * 0.68f);
                var handleEnd = new Vector2(rect.x + rect.width * 0.78f, rect.y + rect.height * 0.78f);
                painter.BeginPath();
                painter.MoveTo(handleStart);
                painter.LineTo(handleEnd);
                painter.Stroke();
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        private void PopulateIssuePanel(DependencyGraphData graphData)
        {
            if (issueList == null)
            {
                return;
            }

            var entries = BuildIssueEntries(graphData);
            var errorEntries = entries
                .Where(entry => entry.Severity == DependencyScanIssueSeverity.Error)
                .ToList();
            var warningEntries = entries
                .Where(entry => entry.Severity == DependencyScanIssueSeverity.Warning)
                .ToList();

            if (issueTitleLabel != null)
            {
                issueTitleLabel.text = "Issues";
            }

            UpdateIssueFilterButtons(errorEntries.Count, warningEntries.Count);
            issueList.contentContainer.Clear();
            if (entries.Count == 0)
            {
                var empty = new Label("No issues");
                empty.AddToClassList("dependency-issue-empty");
                issueList.Add(empty);
                return;
            }

            if (issueErrorsVisible)
            {
                AddIssueRows(errorEntries);
            }

            if (issueWarningsVisible)
            {
                AddIssueRows(warningEntries);
            }
        }

        private void AddIssueRows(IReadOnlyList<IssuePanelEntry> entries)
        {
            if (issueList == null || entries == null || entries.Count == 0)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                issueList.Add(CreateIssueRow(entries[i]));
            }
        }

        private VisualElement CreateIssueRow(IssuePanelEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("dependency-issue-row");
            row.AddToClassList(GetIssueSeverityClass(entry.Severity));
            if (string.IsNullOrEmpty(entry.TargetNodeId))
            {
                row.AddToClassList("dependency-issue-row--disabled");
            }
            else
            {
                row.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    graphView.FocusNode(entry.TargetNodeId, true);
                    evt.PreventDefault();
                    evt.StopPropagation();
                });
            }

            row.tooltip = entry.Detail;
            row.style.borderLeftColor = new StyleColor(entry.NodeColor);
            var severityIcon = new Image { image = IconUtility.GetIssueIcon(entry.Severity) };
            severityIcon.AddToClassList("dependency-issue-severity-icon");

            var nodeIcon = new Image { image = entry.NodeIcon };
            nodeIcon.AddToClassList("dependency-issue-node-icon");

            var text = new VisualElement();
            text.AddToClassList("dependency-issue-text");

            var main = new Label(entry.Title);
            main.AddToClassList("dependency-issue-main");
            var detail = new Label(entry.Detail);
            detail.AddToClassList("dependency-issue-detail");
            text.Add(main);
            text.Add(detail);

            row.Add(severityIcon);
            row.Add(nodeIcon);
            row.Add(text);
            return row;
        }

        private static Label ConfigureIssueFilterButton(
            Button button,
            DependencyScanIssueSeverity severity,
            string tooltip,
            Action clicked)
        {
            if (button == null)
            {
                return null;
            }

            button.text = string.Empty;
            button.tooltip = tooltip;
            button.clicked += clicked;
            button.Clear();

            var icon = new Image { image = IconUtility.GetIssueIcon(severity) };
            icon.AddToClassList("dependency-issue-filter-icon");
            var count = new Label("0");
            count.AddToClassList("dependency-issue-filter-count");
            button.Add(icon);
            button.Add(count);
            return count;
        }

        private void ToggleIssueWarnings()
        {
            issueWarningsVisible = !issueWarningsVisible;
            PopulateIssuePanel(currentGraph);
        }

        private void ToggleIssueErrors()
        {
            issueErrorsVisible = !issueErrorsVisible;
            PopulateIssuePanel(currentGraph);
        }

        private void UpdateIssueFilterButtons(int errorCount, int warningCount)
        {
            if (issueWarningCountLabel != null)
            {
                issueWarningCountLabel.text = warningCount.ToString();
            }

            if (issueErrorCountLabel != null)
            {
                issueErrorCountLabel.text = errorCount.ToString();
            }

            SetIssueFilterButtonState(issueWarningFilterButton, issueWarningsVisible);
            SetIssueFilterButtonState(issueErrorFilterButton, issueErrorsVisible);
        }

        private static void SetIssueFilterButtonState(Button button, bool visible)
        {
            if (button == null)
            {
                return;
            }

            if (visible)
            {
                button.RemoveFromClassList("dependency-issue-filter-button--off");
            }
            else
            {
                button.AddToClassList("dependency-issue-filter-button--off");
            }
        }

        private void ToggleIssueList()
        {
            SetIssueListVisible(!issueListVisible);
        }

        private void SetIssueListVisible(bool visible)
        {
            issueListVisible = visible;
            if (issueList != null)
            {
                issueList.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            ApplyIssuePanelHeight();

            if (issueToggleButton != null)
            {
                UpdateIssueToggleIcon();
            }
        }

        private void UpdateIssueToggleIcon()
        {
            if (issueToggleButton == null)
            {
                return;
            }

            SetChevronButtonIcon(issueToggleButton, !issueListVisible, 0.59f);
            issueToggleButton.tooltip = issueListVisible ? "Hide issues" : "Show issues";
        }

        private void HandleIssueResizeMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || issuePanel == null || !issueListVisible)
            {
                return;
            }

            isResizingIssuePanel = true;
            issueResizeStartMouseY = evt.mousePosition.y;
            issueResizeStartHeight = GetCurrentIssuePanelHeight();
            MouseCaptureController.CaptureMouse(issueResizeHandle);
            evt.StopPropagation();
        }

        private void HandleIssueResizeMouseMove(MouseMoveEvent evt)
        {
            if (!isResizingIssuePanel || issuePanel == null)
            {
                return;
            }

            var deltaY = evt.mousePosition.y - issueResizeStartMouseY;
            issuePanelHeight = Mathf.Clamp(issueResizeStartHeight - deltaY, MinimumIssuePanelHeight, GetMaximumIssuePanelHeight());
            ApplyIssuePanelHeight();
            evt.StopPropagation();
        }

        private void HandleIssueResizeMouseUp(MouseUpEvent evt)
        {
            if (!isResizingIssuePanel)
            {
                return;
            }

            isResizingIssuePanel = false;
            if (issueResizeHandle != null && MouseCaptureController.HasMouseCapture(issueResizeHandle))
            {
                MouseCaptureController.ReleaseMouse(issueResizeHandle);
            }

            evt.StopPropagation();
        }

        private void ApplyIssuePanelHeight()
        {
            if (issuePanel == null)
            {
                return;
            }

            issuePanel.style.height = issueListVisible
                ? Mathf.Clamp(issuePanelHeight, MinimumIssuePanelHeight, GetMaximumIssuePanelHeight())
                : CollapsedIssuePanelHeight;
        }

        private float GetMaximumIssuePanelHeight()
        {
            var rootHeight = root == null ? 0f : root.resolvedStyle.height;
            if (float.IsNaN(rootHeight) || rootHeight <= 0f)
            {
                return MaximumIssuePanelHeight;
            }

            return Mathf.Clamp(rootHeight * 0.68f, MinimumIssuePanelHeight, MaximumIssuePanelHeight);
        }

        private float GetCurrentIssuePanelHeight()
        {
            if (issuePanel == null)
            {
                return issuePanelHeight;
            }

            var currentHeight = issuePanel.resolvedStyle.height;
            return float.IsNaN(currentHeight) || currentHeight <= 0f
                ? issuePanelHeight
                : currentHeight;
        }

        private void HandleRootMouseDown(MouseDownEvent evt)
        {
            if (searchField == null || searchControl == null)
            {
                return;
            }

            var target = evt.target as VisualElement;
            if (IsDescendantOf(target, searchControl))
            {
                return;
            }

            HideSearchSuggestions();
            searchField.Blur();
        }

        private static bool IsDescendantOf(VisualElement element, VisualElement ancestor)
        {
            var current = element;
            while (current != null)
            {
                if (current == ancestor)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static int CountIssueEntries(DependencyGraphData graphData)
        {
            return BuildIssueEntries(graphData).Count;
        }

        private static List<IssuePanelEntry> BuildIssueEntries(DependencyGraphData graphData)
        {
            var entries = new List<IssuePanelEntry>();
            if (graphData == null)
            {
                return entries;
            }

            var missingNodes = graphData.Nodes
                .Where(node => node.Kind == DependencyNodeKind.MissingReference)
                .OrderBy(node => node.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase);
            foreach (var node in missingNodes)
            {
                entries.Add(new IssuePanelEntry(
                    "Missing Reference: " + FormatIssueTitle(node.DisplayName),
                    FormatIssueDetail(string.IsNullOrEmpty(node.Path) ? node.TypeName : node.Path),
                    DependencyScanIssueSeverity.Warning,
                    node.Id,
                    IconUtility.GetIcon(node),
                    IconUtility.GetNodeAccentColor(node)));
            }

            foreach (var issue in graphData.Issues)
            {
                if (!IsDisplayedIssueSeverity(issue.Severity))
                {
                    continue;
                }

                var targetNodeId = FindIssueEntryTargetNodeId(graphData, issue);
                if (string.IsNullOrEmpty(targetNodeId)
                    || !graphData.TryGetNode(targetNodeId, out var targetNode))
                {
                    continue;
                }

                entries.Add(new IssuePanelEntry(
                    FormatIssueTitle(targetNode.DisplayName),
                    FormatIssueDetail(issue.Message),
                    issue.Severity,
                    targetNodeId,
                    IconUtility.GetIcon(targetNode),
                    IconUtility.GetNodeAccentColor(targetNode)));
            }

            return entries
                .OrderByDescending(entry => entry.Severity == DependencyScanIssueSeverity.Error)
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Detail, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string FindIssueEntryTargetNodeId(DependencyGraphData graphData, DependencyScanIssueData issue)
        {
            if (graphData == null || issue == null)
            {
                return string.Empty;
            }

            var issueNode = FindIssueNode(graphData, issue);
            if (issueNode != null)
            {
                var sourceEdge = graphData.Edges.FirstOrDefault(edge =>
                    edge != null
                    && edge.ReferenceKind == DependencyReferenceKind.Issue
                    && string.Equals(edge.TargetNodeId, issueNode.Id, StringComparison.Ordinal));
                if (sourceEdge != null && !string.IsNullOrEmpty(sourceEdge.SourceNodeId))
                {
                    return sourceEdge.SourceNodeId;
                }
            }

            return FindIssueTargetNodeId(graphData, issue.SubjectPath);
        }

        private static DependencyNodeData FindIssueNode(DependencyGraphData graphData, DependencyScanIssueData issue)
        {
            if (graphData == null || issue == null)
            {
                return null;
            }

            return graphData.Nodes.FirstOrDefault(node =>
                node != null
                && node.HasIssue
                && node.IssueSeverity == issue.Severity
                && string.Equals(node.IssueMessage, issue.Message, StringComparison.Ordinal)
                && MatchesIssueSubject(node, issue.SubjectPath)
                && graphData.Edges.Any(edge =>
                    edge != null
                    && edge.ReferenceKind == DependencyReferenceKind.Issue
                    && string.Equals(edge.TargetNodeId, node.Id, StringComparison.Ordinal)));
        }

        private static bool MatchesIssueSubject(DependencyNodeData node, string subjectPath)
        {
            if (node == null || string.IsNullOrEmpty(subjectPath))
            {
                return false;
            }

            return string.Equals(node.Id, subjectPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.Path, subjectPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string FindIssueTargetNodeId(DependencyGraphData graphData, string subjectPath)
        {
            if (graphData == null || string.IsNullOrEmpty(subjectPath))
            {
                return string.Empty;
            }

            var exact = graphData.Nodes.FirstOrDefault(node =>
                string.Equals(node.Id, subjectPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.Path, subjectPath, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact.Id;
            }

            var containing = graphData.Nodes
                .Where(node => !string.IsNullOrEmpty(node.Path)
                    && (subjectPath.IndexOf(node.Path, StringComparison.OrdinalIgnoreCase) >= 0
                        || node.Path.IndexOf(subjectPath, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderByDescending(node => node.Path.Length)
                .FirstOrDefault();
            return containing == null ? string.Empty : containing.Id;
        }

        private static string FormatIssueTitle(string value)
        {
            value = TrimIssuePrefix(value);
            return string.IsNullOrEmpty(value)
                ? "Issue"
                : value.Replace('_', ' ');
        }

        private static string FormatIssueDetail(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var result = value.Trim();
            result = ReplaceIgnoreCase(result, "Assets/DependencyAnalyzerDemo/IssueAssets/", string.Empty);
            result = ReplaceIgnoreCase(result, "Issue_", string.Empty);
            result = ReplaceIgnoreCase(result, "IssueConsoleWarningBehaviour", "ConsoleWarningBehaviour");
            result = ReplaceIgnoreCase(result, "IssueConsoleErrorShader", "ConsoleErrorShader");
            result = ReplaceIgnoreCase(result, "Analyzer demo warning: ", string.Empty);
            result = ReplaceIgnoreCase(result, "Console demo warning: ", string.Empty);
            return result;
        }

        private static string TrimIssuePrefix(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var result = value.Trim();
            while (result.StartsWith("Issue_", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Substring("Issue_".Length);
            }

            if (result.Length > "Issue".Length
                && result.StartsWith("Issue", StringComparison.Ordinal)
                && char.IsUpper(result["Issue".Length]))
            {
                result = result.Substring("Issue".Length);
            }

            return result;
        }

        private static string ReplaceIgnoreCase(string value, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(oldValue))
            {
                return value ?? string.Empty;
            }

            var builder = new StringBuilder();
            var searchStart = 0;
            while (true)
            {
                var index = value.IndexOf(oldValue, searchStart, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    builder.Append(value, searchStart, value.Length - searchStart);
                    return builder.ToString();
                }

                builder.Append(value, searchStart, index - searchStart);
                builder.Append(newValue);
                searchStart = index + oldValue.Length;
            }
        }

        private static string GetIssueSeverityClass(DependencyScanIssueSeverity severity)
        {
            switch (severity)
            {
                case DependencyScanIssueSeverity.Error:
                    return "dependency-issue-row--error";
                case DependencyScanIssueSeverity.Warning:
                    return "dependency-issue-row--warning";
                default:
                    return "dependency-issue-row--info";
            }
        }

        private static bool IsDisplayedIssueSeverity(DependencyScanIssueSeverity severity)
        {
            return severity == DependencyScanIssueSeverity.Error
                || severity == DependencyScanIssueSeverity.Warning;
        }

        private sealed class ChevronIcon : VisualElement
        {
            private readonly bool pointsUp;
            private readonly float verticalScale;

            public ChevronIcon(bool pointsUp, float verticalScale)
            {
                this.pointsUp = pointsUp;
                this.verticalScale = Mathf.Clamp(verticalScale, 0.25f, 1f);
                pickingMode = PickingMode.Ignore;
                generateVisualContent += DrawChevron;
            }

            private void DrawChevron(MeshGenerationContext context)
            {
                var rect = contentRect;
                if (rect.width <= 0f || rect.height <= 0f)
                {
                    return;
                }

                var centerX = rect.center.x;
                var centerY = rect.center.y;
                var halfWidth = Mathf.Min(rect.width * 0.23f, 4.7f);
                var halfHeight = Mathf.Min(rect.height * 0.16f, 4f) * verticalScale;
                var left = new Vector2(centerX - halfWidth, pointsUp ? centerY + halfHeight : centerY - halfHeight);
                var peak = new Vector2(centerX, pointsUp ? centerY - halfHeight : centerY + halfHeight);
                var right = new Vector2(centerX + halfWidth, pointsUp ? centerY + halfHeight : centerY - halfHeight);

                var painter = context.painter2D;
                painter.strokeColor = new Color(0.72f, 0.72f, 0.72f, 1f);
                painter.lineWidth = 3f;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath();
                painter.MoveTo(left);
                painter.LineTo(peak);
                painter.LineTo(right);
                painter.Stroke();
            }
        }

        private sealed class IssuePanelEntry
        {
            public IssuePanelEntry(
                string title,
                string detail,
                DependencyScanIssueSeverity severity,
                string targetNodeId,
                Texture nodeIcon,
                Color nodeColor)
            {
                Title = title ?? string.Empty;
                Detail = detail ?? string.Empty;
                Severity = severity;
                TargetNodeId = targetNodeId ?? string.Empty;
                NodeIcon = nodeIcon;
                NodeColor = nodeColor;
            }

            public string Title { get; }
            public string Detail { get; }
            public DependencyScanIssueSeverity Severity { get; }
            public string TargetNodeId { get; }
            public Texture NodeIcon { get; }
            public Color NodeColor { get; }
        }
    }
}
