using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Scanners;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.UI.Issues;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class SerializedPropertyScannerEditModeTests
    {
        private const string TempFolder = "Assets/__DependencyAnalyzerTests";
        private const string FixtureFolder = "Assets/DependencyAnalyzer/Editor/Tests/Fixtures";
        private readonly List<UnityEngine.Object> transientObjects = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "__DependencyAnalyzerTests");
            }
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = transientObjects.Count - 1; i >= 0; i--)
            {
                if (transientObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(transientObjects[i]);
                }
            }

            transientObjects.Clear();
            AssetDatabase.DeleteAsset(TempFolder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public async Task ScanAsync_CollectsHierarchyComponentsAndInspectorReferences()
        {
            var root = new GameObject("ReferenceRoot");
            var child = new GameObject("ReferenceChild");
            child.transform.SetParent(root.transform);
            var rigidbody = child.AddComponent<Rigidbody>();
            var fixture = root.AddComponent<ReferenceFixtureComponent>();
            var material = CreateMaterialAsset("ValidMaterial.mat");
            fixture.gameObjectReference = child;
            fixture.componentReference = rigidbody;
            fixture.assetReference = material;
            fixture.nested.reference = material;
            fixture.references.Add(child);

            var graph = await ScanAsync();
            var rootNode = FindNode(graph, root);
            var childNode = FindNode(graph, child);
            var componentNode = FindNode(graph, fixture);
            var rigidbodyNode = FindNode(graph, rigidbody);
            var materialNode = FindNode(graph, material);

            AssertEdge(graph, rootNode, childNode, DependencyReferenceKind.Hierarchy, "Child");
            AssertEdge(graph, rootNode, componentNode, DependencyReferenceKind.Component, string.Empty);
            AssertEdge(graph, componentNode, childNode, DependencyReferenceKind.SerializedProperty, "gameObjectReference");
            AssertEdge(graph, componentNode, rigidbodyNode, DependencyReferenceKind.SerializedProperty, "componentReference");
            AssertEdge(graph, componentNode, materialNode, DependencyReferenceKind.SerializedProperty, "assetReference");
            AssertEdge(graph, componentNode, materialNode, DependencyReferenceKind.SerializedProperty, "nested.reference");
            AssertEdge(graph, componentNode, childNode, DependencyReferenceKind.SerializedProperty, "references.Array.data[0]");
            Assert.IsFalse(graph.Edges.Any(edge => edge.MemberName == "m_Script"));
        }

        [Test]
        public async Task ScanAsync_DoesNotTreatNoneAsMissing()
        {
            var gameObject = new GameObject("NoneReference");
            var fixture = gameObject.AddComponent<ReferenceFixtureComponent>();

            var graph = await ScanAsync();
            var componentNode = FindNode(graph, fixture);

            Assert.IsFalse(componentNode.HasMissingReferences);
            Assert.IsFalse(graph.Edges.Any(edge => edge.PointsToMissingReference));
            Assert.IsFalse(graph.Nodes.Any(node => node.Kind == DependencyNodeKind.MissingReference));
            Assert.AreEqual(0, ProjectIssuePanelBuilder.Build(graph).WarningCount);
        }

        [Test]
        public async Task ScanAsync_CollectsHiddenRendererMaterialReferenceFromOwner()
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = "RendererReference";
            var renderer = gameObject.GetComponent<MeshRenderer>();
            var material = CreateMaterialAsset("RendererMaterial.mat");
            renderer.sharedMaterial = material;

            var graph = await ScanAsync();
            var ownerNode = FindNode(graph, gameObject);
            var materialNode = FindNode(graph, material);

            Assert.IsFalse(graph.Nodes.Any(node => node.InstanceId == renderer.GetInstanceID()));
            Assert.IsTrue(graph.Edges.Any(edge =>
                edge.SourceNodeId == ownerNode.Id
                && edge.TargetNodeId == materialNode.Id
                && edge.ReferenceKind == DependencyReferenceKind.SerializedProperty
                && edge.MemberName.EndsWith("m_Materials.Array.data[0]", StringComparison.Ordinal)));
        }

        [Test]
        public async Task ScanAsync_DoesNotTreatEmptyRendererMaterialSlotAsMissing()
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = "EmptyRendererSlot";
            gameObject.GetComponent<MeshRenderer>().sharedMaterials = new Material[] { null };

            var graph = await ScanAsync();

            Assert.IsFalse(graph.Edges.Any(edge => edge.PointsToMissingReference
                && edge.MemberName.EndsWith("m_Materials.Array.data[0]", StringComparison.Ordinal)));
        }

        [Test]
        public void ReferenceReader_DistinguishesNoneFromMissing()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FixtureFolder + "/MissingObjectReference.prefab");
            Assert.NotNull(prefab);
            var missingMeshFilter = prefab.GetComponent<MeshFilter>();
            var noneOwner = new GameObject("NoneMesh");
            var noneMeshFilter = noneOwner.AddComponent<MeshFilter>();
            var reader = new UnitySerializedObjectReferenceReader();

            var missingReference = reader.Read(missingMeshFilter)
                .Single(reference => reference.PropertyPath == "m_Mesh");
            var noneReference = reader.Read(noneMeshFilter)
                .Single(reference => reference.PropertyPath == "m_Mesh");

            Assert.AreEqual(
                SerializedObjectReferenceState.Missing,
                missingReference.State,
                "Serialized display value: " + missingReference.SerializedDisplayValue);
            Assert.AreEqual(SerializedObjectReferenceState.None, noneReference.State);
        }

        [Test]
        public async Task ScanAsync_CollectsInactiveDisabledAndAdditiveSceneObjects()
        {
            var primaryObject = new GameObject("PrimarySceneObject");
            primaryObject.SetActive(false);
            var primaryComponent = primaryObject.AddComponent<ReferenceFixtureComponent>();
            primaryComponent.enabled = false;
            Assert.IsTrue(EditorSceneManager.SaveScene(
                SceneManager.GetActiveScene(),
                TempFolder + "/Primary.unity"));

            var additiveScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var additiveObject = new GameObject("AdditiveSceneObject");
            SceneManager.MoveGameObjectToScene(additiveObject, additiveScene);
            var additivePath = TempFolder + "/Additive.unity";
            Assert.IsTrue(EditorSceneManager.SaveScene(additiveScene, additivePath));

            var graph = await ScanAsync();

            Assert.NotNull(FindNode(graph, primaryObject));
            Assert.NotNull(FindNode(graph, primaryComponent));
            Assert.NotNull(FindNode(graph, additiveObject));
            Assert.IsTrue(EditorSceneManager.CloseScene(additiveScene, true));

            var graphAfterClose = await ScanAsync();
            Assert.IsFalse(graphAfterClose.Nodes.Any(node =>
                node.Path.StartsWith(additivePath + "::", StringComparison.Ordinal)));
        }

        [Test]
        public async Task ScanAsync_PreservesDistinctMemberPathsForRepeatedAndCyclicReferences()
        {
            var first = new GameObject("CycleFirst");
            var second = new GameObject("CycleSecond");
            var firstFixture = first.AddComponent<ReferenceFixtureComponent>();
            var secondFixture = second.AddComponent<ReferenceFixtureComponent>();
            firstFixture.gameObjectReference = second;
            firstFixture.references.Add(second);
            secondFixture.gameObjectReference = first;

            var graph = await ScanAsync();
            var firstNode = FindNode(graph, firstFixture);
            var secondNode = FindNode(graph, second);

            Assert.AreEqual(2, graph.Edges.Count(edge =>
                edge.SourceNodeId == firstNode.Id
                && edge.TargetNodeId == secondNode.Id
                && edge.ReferenceKind == DependencyReferenceKind.SerializedProperty));
            Assert.IsTrue(graph.Edges.Any(edge => edge.MemberName == "gameObjectReference"));
            Assert.IsTrue(graph.Edges.Any(edge => edge.MemberName == "references.Array.data[0]"));
        }

        [Test]
        public async Task ScanAsync_DistinguishesSubAssetsAtTheSamePath()
        {
            var mainAsset = ScriptableObject.CreateInstance<ReferenceFixtureAsset>();
            AssetDatabase.CreateAsset(mainAsset, TempFolder + "/SubAssets.asset");
            var firstSubAsset = ScriptableObject.CreateInstance<ReferenceFixtureAsset>();
            firstSubAsset.name = "FirstSubAsset";
            var secondSubAsset = ScriptableObject.CreateInstance<ReferenceFixtureAsset>();
            secondSubAsset.name = "SecondSubAsset";
            AssetDatabase.AddObjectToAsset(firstSubAsset, mainAsset);
            AssetDatabase.AddObjectToAsset(secondSubAsset, mainAsset);
            AssetDatabase.SaveAssets();

            var gameObject = new GameObject("SubAssetReferences");
            var fixture = gameObject.AddComponent<ReferenceFixtureComponent>();
            fixture.assetReference = firstSubAsset;
            fixture.secondAssetReference = secondSubAsset;

            var graph = await ScanAsync();
            var firstNode = FindNode(graph, firstSubAsset);
            var secondNode = FindNode(graph, secondSubAsset);

            Assert.AreEqual(firstNode.Path, secondNode.Path);
            Assert.AreNotEqual(firstNode.Id, secondNode.Id);
            Assert.AreEqual("FirstSubAsset", firstNode.DisplayName);
            Assert.AreEqual("SecondSubAsset", secondNode.DisplayName);
        }

        [Test]
        public async Task ScanAsync_ExcludesOnlyConfiguredAssetPath()
        {
            AssetDatabase.CreateFolder(TempFolder, "Excluded");
            var included = CreateMaterialAsset("Included.mat");
            var excluded = CreateMaterialAsset("Excluded/Excluded.mat");
            var gameObject = new GameObject("ExcludedReferenceOwner");
            var fixture = gameObject.AddComponent<ReferenceFixtureComponent>();
            fixture.assetReference = included;
            fixture.secondAssetReference = excluded;
            var settings = CreateSettingsWithExcludedFolder(TempFolder + "/Excluded");

            var graph = await ScanAsync(settings: settings);

            Assert.NotNull(FindNode(graph, included));
            Assert.IsFalse(graph.Nodes.Any(node => node.InstanceId == excluded.GetInstanceID()));
        }

        [Test]
        public async Task ScanAsync_AddsPrefabSourceAndUsesInstanceOverrideReference()
        {
            var firstMaterial = CreateMaterialAsset("PrefabSource.mat");
            var overrideMaterial = CreateMaterialAsset("PrefabOverride.mat");
            var source = new GameObject("PrefabReferenceSource");
            source.AddComponent<ReferenceFixtureComponent>().assetReference = firstMaterial;
            var prefabPath = TempFolder + "/Reference.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            UnityEngine.Object.DestroyImmediate(source);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var instanceFixture = instance.GetComponent<ReferenceFixtureComponent>();
            instanceFixture.assetReference = overrideMaterial;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instanceFixture);

            var graph = await ScanAsync();
            var instanceNode = FindNode(graph, instance);
            var prefabNode = graph.Nodes.Single(node => node.Path == prefabPath && node.TypeName == "Prefab");
            var componentNode = FindNode(graph, instanceFixture);
            var overrideNode = FindNode(graph, overrideMaterial);

            AssertEdge(graph, instanceNode, prefabNode, DependencyReferenceKind.PrefabInstance, "Prefab Source");
            AssertEdge(graph, componentNode, overrideNode, DependencyReferenceKind.SerializedProperty, "assetReference");
            Assert.IsFalse(graph.Edges.Any(edge =>
                edge.SourceNodeId == componentNode.Id
                && graph.TryGetNode(edge.TargetNodeId, out var target)
                && target.InstanceId == firstMaterial.GetInstanceID()));
        }

        [Test]
        public async Task ScanAsync_AddsSourceEdgesForNestedPrefabs()
        {
            var innerSource = new GameObject("InnerPrefabRoot");
            var innerPath = TempFolder + "/Inner.prefab";
            var innerPrefab = PrefabUtility.SaveAsPrefabAsset(innerSource, innerPath);
            UnityEngine.Object.DestroyImmediate(innerSource);

            var outerSource = new GameObject("OuterPrefabRoot");
            var nestedInstance = (GameObject)PrefabUtility.InstantiatePrefab(innerPrefab);
            nestedInstance.transform.SetParent(outerSource.transform);
            var outerPath = TempFolder + "/Outer.prefab";
            var outerPrefab = PrefabUtility.SaveAsPrefabAsset(outerSource, outerPath);
            UnityEngine.Object.DestroyImmediate(outerSource);
            var outerInstance = (GameObject)PrefabUtility.InstantiatePrefab(outerPrefab);

            var graph = await ScanAsync();
            var prefabTargetPaths = graph.Edges
                .Where(edge => edge.ReferenceKind == DependencyReferenceKind.PrefabInstance)
                .Select(edge => graph.TryGetNode(edge.TargetNodeId, out var node) ? node.Path : string.Empty)
                .ToList();

            Assert.Contains(outerPath, prefabTargetPaths);
            Assert.Contains(innerPath, prefabTargetPaths);
            Assert.NotNull(outerInstance);
        }

        [Test]
        public async Task ScanAsync_RetainsPartialResultsWhenOneComponentReaderFails()
        {
            var material = CreateMaterialAsset("PartialResult.mat");
            var brokenObject = new GameObject("ReaderFailure");
            brokenObject.AddComponent<ReferenceFixtureComponent>();
            var validObject = new GameObject("ValidAfterFailure");
            var validFixture = validObject.AddComponent<ReferenceFixtureComponent>();
            validFixture.assetReference = material;
            var reader = new FaultInjectingReferenceReader(brokenObject, material);

            var graph = await ScanAsync(new SerializedPropertyScanner(reader));
            var brokenComponentNode = FindNode(graph, brokenObject.GetComponent<ReferenceFixtureComponent>());
            var validComponentNode = FindNode(graph, validFixture);
            var materialNode = FindNode(graph, material);

            Assert.IsTrue(graph.Edges.Any(edge =>
                edge.SourceNodeId == brokenComponentNode.Id
                && edge.TargetNodeId == materialNode.Id
                && edge.MemberName == "partialReference"));
            AssertEdge(graph, validComponentNode, materialNode, DependencyReferenceKind.SerializedProperty, "assetReference");
            Assert.IsTrue(graph.Issues.Any(issue => issue.SubjectPath == brokenComponentNode.Path
                && issue.Message.Contains("Injected property enumeration failure")));
        }

        [Test]
        public async Task ScannerOrchestrator_RetainsSceneGraphWhenExtensionScannerFails()
        {
            var gameObject = new GameObject("SceneResultBeforeFailure");
            var orchestrator = new ScannerOrchestrator();
            orchestrator.RegisterScanner(new ThrowingDependencyScanner());
            var settings = ScriptableObject.CreateInstance<AnalyzerSettings>();
            transientObjects.Add(settings);
            LogAssert.Expect(
                LogType.Error,
                "[Dependency Analyzer Diagnostic] " + ThrowingDependencyScanner.ScannerName
                + " [" + ThrowingDependencyScanner.ScannerName + "]: Scanner failed: Injected scanner failure");

            var graph = await orchestrator.ScanAsync(
                settings,
                new DependencyNodeCache(),
                null,
                CancellationToken.None);

            Assert.NotNull(FindNode(graph, gameObject));
            Assert.IsTrue(graph.Issues.Any(issue => issue.ScannerName == ThrowingDependencyScanner.ScannerName
                && issue.Message.Contains("Scanner failed")));
        }

        [Test]
        public async Task ScannerOrchestrator_DoesNotReadUnityConsoleMessagesAsProjectIssues()
        {
            const string consoleMessage = "Dependency Analyzer test runtime warning";
            LogAssert.Expect(LogType.Warning, consoleMessage);
            Debug.LogWarning(consoleMessage);
            var orchestrator = new ScannerOrchestrator();
            var settings = ScriptableObject.CreateInstance<AnalyzerSettings>();
            transientObjects.Add(settings);

            var graph = await orchestrator.ScanAsync(
                settings,
                new DependencyNodeCache(),
                null,
                CancellationToken.None);

            Assert.AreEqual(0, ProjectIssuePanelBuilder.Build(graph).WarningCount);
            Assert.IsFalse(graph.Issues.Any(issue => issue.ScannerName == "Unity Console"));
        }

        [TestCase("MissingScript.prefab", "Missing Component")]
        [TestCase("MissingObjectReference.prefab", "m_Mesh")]
        [TestCase("MissingRendererMaterial.prefab", "m_Materials.Array.data[0]")]
        public async Task ScanAsync_DetectsFixedBrokenReferenceFixtures(string prefabName, string expectedMemberName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FixtureFolder + "/" + prefabName);
            Assert.NotNull(prefab, "Fixture failed to import: " + prefabName);
            PrefabUtility.InstantiatePrefab(prefab);

            var graph = await ScanAsync();
            var missingEdge = graph.Edges.FirstOrDefault(edge =>
                edge.PointsToMissingReference
                && edge.MemberName.IndexOf(expectedMemberName, StringComparison.Ordinal) >= 0);

            Assert.NotNull(missingEdge, "Missing edge was not detected for " + prefabName);
            Assert.IsTrue(graph.TryGetNode(missingEdge.TargetNodeId, out var missingNode));
            Assert.IsTrue(missingNode.HasMissingReferences);
        }

        [Test]
        public async Task ScanAsync_BuildsMissingScriptIssueWithOwningGameObjectLocation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FixtureFolder + "/MissingScript.prefab");
            Assert.NotNull(prefab);
            PrefabUtility.InstantiatePrefab(prefab);

            var graph = await ScanAsync();
            var missingEdge = graph.Edges.Single(edge =>
                edge.PointsToMissingReference
                && edge.ReferenceKind == DependencyReferenceKind.Component);
            var model = ProjectIssuePanelBuilder.Build(graph);
            var group = model.Groups.Single(candidate => candidate.Type == ProjectIssueType.MissingScript);
            var location = group.Locations.Single();

            Assert.AreEqual(1, group.Count);
            Assert.AreEqual(missingEdge.SourceNodeId, location.TargetNodeId);
            StringAssert.Contains("Missing Component", location.Label);
        }

        private Material CreateMaterialAsset(string relativePath)
        {
            var shader = Shader.Find("Hidden/InternalErrorShader") ?? Shader.Find("Standard");
            Assert.NotNull(shader, "No built-in shader was available for the fixture material.");
            var material = new Material(shader);
            var assetPath = TempFolder + "/" + relativePath;
            EnsureFolder(System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(folderPath).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folderPath));
        }

        private static AnalyzerSettings CreateSettingsWithExcludedFolder(string folderPath)
        {
            var settings = ScriptableObject.CreateInstance<AnalyzerSettings>();
            var serializedSettings = new SerializedObject(settings);
            var excludedFolders = serializedSettings.FindProperty("excludedFolderPaths");
            excludedFolders.arraySize = 1;
            excludedFolders.GetArrayElementAtIndex(0).stringValue = folderPath;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            return settings;
        }

        private async Task<DependencyGraph> ScanAsync(
            SerializedPropertyScanner scanner = null,
            AnalyzerSettings settings = null)
        {
            scanner = scanner ?? new SerializedPropertyScanner();
            settings = settings ?? ScriptableObject.CreateInstance<AnalyzerSettings>();
            transientObjects.Add(settings);

            return await scanner.ScanAsync(
                settings,
                new DependencyNodeCache(),
                null,
                CancellationToken.None);
        }

        private static DependencyNode FindNode(DependencyGraph graph, UnityEngine.Object unityObject)
        {
            var node = graph.Nodes.FirstOrDefault(candidate => candidate.InstanceId == unityObject.GetInstanceID());
            Assert.NotNull(node, "Node was not found for " + unityObject.name + " (" + unityObject.GetType().Name + ")");
            return node;
        }

        private static void AssertEdge(
            DependencyGraph graph,
            DependencyNode source,
            DependencyNode target,
            DependencyReferenceKind kind,
            string memberName)
        {
            Assert.IsTrue(graph.Edges.Any(edge =>
                edge.SourceNodeId == source.Id
                && edge.TargetNodeId == target.Id
                && edge.ReferenceKind == kind
                && edge.MemberName == memberName),
                "Expected edge was not found: " + source.DisplayName + " -> " + target.DisplayName + " (" + kind + ", " + memberName + ")");
        }

        private sealed class FaultInjectingReferenceReader : ISerializedObjectReferenceReader
        {
            private readonly GameObject failingOwner;
            private readonly UnityEngine.Object partialReference;
            private readonly UnitySerializedObjectReferenceReader innerReader = new UnitySerializedObjectReferenceReader();

            public FaultInjectingReferenceReader(GameObject failingOwner, UnityEngine.Object partialReference)
            {
                this.failingOwner = failingOwner;
                this.partialReference = partialReference;
            }

            public IEnumerable<SerializedObjectReferenceInfo> Read(Component component)
            {
                if (component.gameObject == failingOwner && component is ReferenceFixtureComponent)
                {
                    yield return new SerializedObjectReferenceInfo(
                        "partialReference",
                        "PPtr<Object>",
                        SerializedObjectReferenceState.Valid,
                        partialReference,
                        0,
                        string.Empty);
                    throw new InvalidOperationException("Injected property enumeration failure");
                }

                foreach (var reference in innerReader.Read(component))
                {
                    yield return reference;
                }
            }
        }

        private sealed class ThrowingDependencyScanner : IDependencyScanner
        {
            public const string ScannerName = "Throwing Test Scanner";
            public string Name => ScannerName;

            public Task<DependencyGraph> ScanAsync(
                AnalyzerSettings settings,
                DependencyNodeCache cache,
                IProgress<ScanProgress> progress,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Injected scanner failure");
            }
        }
    }
}
