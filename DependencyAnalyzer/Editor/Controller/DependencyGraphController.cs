using System;
using System.Threading;
using System.Threading.Tasks;
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
    public sealed class DependencyGraphController : IDisposable
    {
        private readonly DependencyGraphView graphView;
        private readonly GraphToolbarView toolbarView;
        private readonly GraphSearchView searchView;
        private readonly IssuePanelView issuePanelView;
        private readonly Func<AnalyzerSettings, DependencyNodeCache, IProgress<ScanProgress>, CancellationToken, Task<DependencyGraph>> scanOperation;
        private readonly DependencyNodeCache cache;
        private readonly EditorSelectionSync selectionSync;
        private CancellationTokenSource scanCancellation;
        private DependencyGraph currentGraph;
        private bool disposed;
        private bool hierarchyRefreshQueued;
        private bool pendingRescan;
        private bool suppressNextSelectionFocus;
        private bool hasCompletedLoad;
        private bool isLoading;
        private int currentExpansionDepth;
        private int suppressedSelectionInstanceId;

        public DependencyGraphController(VisualElement root, DependencyGraphView graphView)
            : this(root, graphView, new ScannerOrchestrator().ScanAsync, true)
        {
        }

        internal DependencyGraphController(
            VisualElement root,
            DependencyGraphView graphView,
            Func<AnalyzerSettings, DependencyNodeCache, IProgress<ScanProgress>, CancellationToken, Task<DependencyGraph>> scanOperation,
            bool scheduleInitialScan)
        {
            this.graphView = graphView;
            this.scanOperation = scanOperation ?? throw new ArgumentNullException(nameof(scanOperation));
            cache = new DependencyNodeCache();
            selectionSync = new EditorSelectionSync();
            toolbarView = new GraphToolbarView(root);
            searchView = new GraphSearchView(root);
            issuePanelView = new IssuePanelView(root);
            toolbarView.LoadRequested += HandleLoadClicked;
            toolbarView.DepthChanged += SetExpansionDepth;
            searchView.QueryChanged += HandleSearchQueryChanged;
            searchView.NavigationRequested += HandleSearchNavigationRequested;
            searchView.SuggestionSelected += HandleSearchSuggestionSelected;
            issuePanelView.NodeFocusRequested += HandleIssueNodeFocusRequested;

            var settings = AnalyzerSettings.LoadOrCreateRuntimeSettings();
            currentExpansionDepth = DependencyGraphView.ClampExpansionDepth(settings.InitialExpansionDepth);
            toolbarView.SetDepth(currentExpansionDepth);
            ApplyGraphSettings(settings);
            graphView.NodeSelected += HandleNodeSelected;
            Selection.selectionChanged += HandleEditorSelectionChanged;
            if (scheduleInitialScan)
            {
                EditorApplication.delayCall += RequestInitialScan;
            }

            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            pendingRescan = false;
            hierarchyRefreshQueued = false;
            EditorApplication.delayCall -= RequestInitialScan;
            EditorApplication.delayCall -= RunQueuedHierarchyScan;
            EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
            Selection.selectionChanged -= HandleEditorSelectionChanged;
            graphView.NodeSelected -= HandleNodeSelected;
            toolbarView.LoadRequested -= HandleLoadClicked;
            toolbarView.DepthChanged -= SetExpansionDepth;
            searchView.QueryChanged -= HandleSearchQueryChanged;
            searchView.NavigationRequested -= HandleSearchNavigationRequested;
            searchView.SuggestionSelected -= HandleSearchSuggestionSelected;
            issuePanelView.NodeFocusRequested -= HandleIssueNodeFocusRequested;
            toolbarView.Dispose();
            searchView.Dispose();
            issuePanelView.Dispose();
            CancelActiveScan();
        }

        private void HandleSearchQueryChanged(string query)
        {
            searchView.SetSearchState(graphView.SetSearch(query, true));
        }

        private void HandleSearchNavigationRequested(bool reverse)
        {
            searchView.SetSearchState(graphView.FocusNextSearchResult(reverse));
        }

        private void HandleSearchSuggestionSelected(DependencyGraphView.SearchSuggestion suggestion)
        {
            searchView.SetSearchState(graphView.SetSearch(suggestion.DisplayName, false));
            graphView.FocusNode(suggestion.NodeId, true);
        }

        private void HandleIssueNodeFocusRequested(string nodeId)
        {
            graphView.FocusNode(nodeId, true);
        }

        private void ApplyGraphSettings(AnalyzerSettings settings)
        {
            graphView.ConfigureZoom(AnalyzerSettings.DefaultZoomMin, AnalyzerSettings.DefaultZoomMax, settings.ZoomStep);
        }

        private void HandleScanProgress(ScanProgress progress, CancellationTokenSource activeCancellation)
        {
            if (!disposed && isLoading && ReferenceEquals(scanCancellation, activeCancellation))
            {
                toolbarView.SetProgress(progress.Total <= 0 ? 0f : progress.Ratio);
            }
        }

        internal void SetExpansionDepth(int depth)
        {
            var nextDepth = DependencyGraphView.ClampExpansionDepth(depth);
            toolbarView.SetDepth(nextDepth);
            if (currentExpansionDepth == nextDepth)
            {
                return;
            }

            currentExpansionDepth = nextDepth;
            graphView.SetExpansionDepth(currentExpansionDepth);
        }

        internal void CancelActiveScan()
        {
            pendingRescan = false;
            if (scanCancellation == null || scanCancellation.IsCancellationRequested)
            {
                return;
            }

            scanCancellation.Cancel();
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
            if (disposed
                || (currentGraph == null && scanCancellation == null)
                || hierarchyRefreshQueued)
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
            if (disposed)
            {
                return;
            }

            if (scanCancellation != null)
            {
                pendingRescan = true;
                return;
            }

            do
            {
                pendingRescan = false;
                var wasCanceled = await RunSingleScanAsync();
                if (wasCanceled)
                {
                    pendingRescan = false;
                    return;
                }
            }
            while (!disposed && pendingRescan);

            pendingRescan = false;
        }

        private async Task<bool> RunSingleScanAsync()
        {
            var activeCancellation = new CancellationTokenSource();
            scanCancellation = activeCancellation;

            var token = activeCancellation.Token;
            cache.Clear();
            isLoading = true;
            toolbarView.SetLoading(true, hasCompletedLoad);
            toolbarView.SetProgress(0f);

            try
            {
                var settings = AnalyzerSettings.LoadOrCreateRuntimeSettings();
                ApplyGraphSettings(settings);
                var progress = new Progress<ScanProgress>(scanProgress => HandleScanProgress(scanProgress, activeCancellation));
                var scannedGraph = await scanOperation(settings, cache, progress, token);
                token.ThrowIfCancellationRequested();
                currentGraph = scannedGraph;
                graphView.Populate(currentGraph, currentExpansionDepth);
                searchView.SetSearchState(graphView.SetSearch(searchView.Query, false));
                issuePanelView.SetModel(ProjectIssuePanelBuilder.Build(currentGraph));
                hasCompletedLoad = true;
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return token.IsCancellationRequested;
            }
            finally
            {
                if (scanCancellation == activeCancellation)
                {
                    activeCancellation.Dispose();
                    scanCancellation = null;
                }

                isLoading = false;
                if (!disposed)
                {
                    toolbarView.SetLoading(false, hasCompletedLoad);
                }
            }
        }

        internal bool HasPendingRescan => pendingRescan;
        internal DependencyGraph CurrentGraph => currentGraph;
        internal int CurrentExpansionDepth => currentExpansionDepth;
        internal Task RequestScanAsync() => ScanAsync();

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
                suppressNextSelectionFocus = false;
                suppressedSelectionInstanceId = 0;
                graphView.ClearEditorSelectionHighlight();
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
