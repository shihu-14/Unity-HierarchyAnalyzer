using System;
using System.Threading;
using System.Threading.Tasks;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Scanners;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.UI.GraphView;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Controller
{
    public sealed class DependencyGraphController : IDisposable
    {
        private readonly DependencyGraphView graphView;
        private readonly ScannerOrchestrator scannerOrchestrator;
        private readonly DependencyCache cache;
        private readonly EditorSelectionSync selectionSync;
        private readonly Button scanButton;
        private readonly Button cancelButton;
        private readonly IntegerField depthField;
        private readonly Slider zoomStepSlider;
        private readonly Label zoomStepValueLabel;
        private readonly Label statusLabel;

        private CancellationTokenSource scanCancellation;
        private DependencyGraphData currentGraph;
        private bool disposed;
        private bool hierarchyRefreshQueued;
        private bool suppressNextSelectionFocus;
        private int suppressedSelectionInstanceId;

        public DependencyGraphController(VisualElement root, DependencyGraphView graphView)
        {
            this.graphView = graphView;
            scannerOrchestrator = new ScannerOrchestrator();
            cache = new DependencyCache();
            selectionSync = new EditorSelectionSync();

            scanButton = root.Q<Button>("scan-button");
            cancelButton = root.Q<Button>("cancel-button");
            depthField = root.Q<IntegerField>("depth-field");
            zoomStepSlider = root.Q<Slider>("zoom-step-slider");
            zoomStepValueLabel = root.Q<Label>("zoom-step-value-label");
            statusLabel = root.Q<Label>("status-label");

            if (scanButton != null)
            {
                scanButton.clicked += HandleScanClicked;
            }

            if (cancelButton != null)
            {
                cancelButton.clicked += CancelActiveScan;
                cancelButton.SetEnabled(false);
            }

            if (depthField != null)
            {
                depthField.value = Mathf.Clamp(depthField.value == 0 ? 3 : depthField.value, 3, 4);
                depthField.RegisterValueChangedCallback(HandleDepthChanged);
            }

            var settings = AnalyzerSettings.LoadOrCreateRuntimeSettings();
            InitializeZoomFields(settings);
            ApplyGraphSettings(settings);
            graphView.NodeSelected += HandleNodeSelected;
            Selection.selectionChanged += HandleEditorSelectionChanged;
            EditorApplication.delayCall += RequestInitialScan;
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
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

            if (scanButton != null)
            {
                scanButton.clicked -= HandleScanClicked;
            }

            if (cancelButton != null)
            {
                cancelButton.clicked -= CancelActiveScan;
            }

            if (zoomStepSlider != null)
            {
                zoomStepSlider.UnregisterValueChangedCallback(HandleZoomChanged);
            }

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

        private void HandleScanClicked()
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
            SetScanControlsEnabled(false);
            SetStatus("Scanning");

            try
            {
                var settings = AnalyzerSettings.LoadOrCreateRuntimeSettings();
                ApplyGraphSettings(settings);
                var progress = new Progress<ScanProgress>(HandleScanProgress);
                currentGraph = await scannerOrchestrator.ScanAsync(settings, cache, progress, token);
                graphView.Populate(currentGraph, GetInitialDepth(settings));
                ReportIssues(currentGraph);
                SetStatus("Completed: " + currentGraph.Nodes.Count + " nodes, "
                    + currentGraph.Edges.Count + " edges, "
                    + currentGraph.Issues.Count + " issues");
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

                SetScanControlsEnabled(true);
            }
        }

        private int GetInitialDepth(AnalyzerSettings settings)
        {
            if (depthField == null)
            {
                return settings.InitialExpansionDepth;
            }

            var depth = Mathf.Clamp(depthField.value, 3, 4);
            if (depthField.value != depth)
            {
                depthField.value = depth;
            }

            return depth;
        }

        private void ApplyGraphSettings(AnalyzerSettings settings)
        {
            var step = zoomStepSlider == null ? settings.ZoomStep : zoomStepSlider.value;

            step = Mathf.Clamp(step, 0.001f, 0.03f);
            SetZoomSliderValue(step);
            UpdateZoomStepValueLabel(step);
            graphView.ConfigureZoom(AnalyzerSettings.DefaultZoomMin, AnalyzerSettings.DefaultZoomMax, step);
        }

        private void HandleScanProgress(ScanProgress progress)
        {
            var percentage = progress.Total <= 0 ? 0f : progress.Ratio * 100f;
            SetStatus(progress.ScannerName + ": " + progress.Message + " (" + percentage.ToString("0") + "%)");
        }

        private void HandleDepthChanged(ChangeEvent<int> evt)
        {
            var clampedValue = Mathf.Clamp(evt.newValue, 3, 4);
            if (depthField != null && depthField.value != clampedValue)
            {
                depthField.value = clampedValue;
                return;
            }

            if (currentGraph != null)
            {
                graphView.Populate(currentGraph, clampedValue);
            }
        }

        private void InitializeZoomFields(AnalyzerSettings settings)
        {
            SetZoomSliderValue(settings.ZoomStep);
            UpdateZoomStepValueLabel(settings.ZoomStep);
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

        private void UpdateZoomStepValueLabel(float value)
        {
            if (zoomStepValueLabel != null)
            {
                zoomStepValueLabel.text = value.ToString("0.000");
            }
        }

        private void HandleNodeSelected(DependencyNodeData node)
        {
            graphView.ExpandNode(node.Id);
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

        private void SetScanControlsEnabled(bool enabled)
        {
            scanButton?.SetEnabled(enabled);
            cancelButton?.SetEnabled(!enabled);
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        private static void ReportIssues(DependencyGraphData graphData)
        {
            if (graphData == null || graphData.Issues.Count == 0)
            {
                return;
            }

            foreach (var issue in graphData.Issues)
            {
                var message = "[" + issue.ScannerName + "] " + issue.SubjectPath + ": " + issue.Message;
                if (issue.Severity == DependencyScanIssueSeverity.Error)
                {
                    Debug.LogError(message);
                }
                else if (issue.Severity == DependencyScanIssueSeverity.Warning)
                {
                    Debug.LogWarning(message);
                }
                else
                {
                    Debug.Log(message);
                }
            }
        }
    }
}
