using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.GraphView;
using NUnit.Framework;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class DependencyGraphViewZoomTests
    {
        [Test]
        public void Constructor_UsesDefaultZoom()
        {
            var graphView = new DependencyGraphView();

            Assert.AreEqual(1.15f, DependencyGraphView.DefaultZoom);
            Assert.AreEqual(DependencyGraphView.DefaultZoom, graphView.Zoom);
        }

        [Test]
        public void PopulateNewGraph_ResetsZoomToDefault()
        {
            var graphView = new DependencyGraphView();
            graphView.ConfigureZoom(0.1f, 1f, 0.004f);
            Assert.AreEqual(1f, graphView.Zoom);
            graphView.ConfigureZoom(0.1f, 2f, 0.004f);

            graphView.Populate(new DependencyGraph(), 2);

            Assert.AreEqual(DependencyGraphView.DefaultZoom, graphView.Zoom);
        }

        [Test]
        public void ConfigureZoom_ClampsZoomToConfiguredBounds()
        {
            var graphView = new DependencyGraphView();

            graphView.ConfigureZoom(0.1f, 1f, 0.004f);

            Assert.AreEqual(1f, graphView.Zoom);
        }
    }
}
