using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DependencyAnalyzer.Editor.Scanners
{
    public sealed partial class SerializedPropertyScanner
    {
        private static void AddHiddenComponentMaterialDependencies(
            Component component,
            DependencyNodeData gameObjectNode,
            DependencyGraphData graph,
            DependencyCache cache,
            AnalyzerSettings settings)
        {
            var renderer = component as Renderer;
            if (renderer == null)
            {
                return;
            }

            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null)
                {
                    continue;
                }

                var materialNode = CreateObjectReferenceNode(material, cache, settings);
                if (materialNode == null)
                {
                    continue;
                }

                graph.AddOrUpdateNode(materialNode);
                graph.AddEdge(new DependencyEdgeData(
                    gameObjectNode.Id,
                    materialNode.Id,
                    "Renderer.sharedMaterials[" + i + "]",
                    DependencyReferenceKind.SerializedProperty));
            }
        }

        private static bool ShouldScanInspectorObjectReference(Component component, SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                return false;
            }

            switch (property.propertyPath)
            {
                case "m_GameObject":
                case "m_CorrespondingSourceObject":
                case "m_PrefabInstance":
                case "m_PrefabAsset":
                case "m_PrefabParentObject":
                case "m_PrefabInternal":
                case "m_Father":
                case "m_Script":
                    return false;
                default:
                    return true;
            }
        }

        private static bool ShouldVisualizeComponent(Component component)
        {
            if (component == null)
            {
                return false;
            }

            if (IsDefaultTemplateComponent(component))
            {
                return false;
            }

            var monoBehaviour = component as MonoBehaviour;
            if (monoBehaviour != null)
            {
                var monoScript = MonoScript.FromMonoBehaviour(monoBehaviour);
                if (monoScript == null)
                {
                    return false;
                }

                var scriptPath = AssetDatabase.GetAssetPath(monoScript);
                return !string.IsNullOrEmpty(scriptPath)
                    && scriptPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private static bool IsDefaultTemplateComponent(Component component)
        {
            if (component is Transform || component is RectTransform)
            {
                return true;
            }

            if (component is MeshFilter || component is Renderer || component is Collider)
            {
                return true;
            }

            var gameObject = component.gameObject;
            var type = component.GetType();
            if (gameObject.GetComponent<Camera>() != null)
            {
                return type == typeof(Camera)
                    || type == typeof(AudioListener);
            }

            if (gameObject.GetComponent<Light>() != null)
            {
                return type == typeof(Light);
            }

            if (IsPrimitiveTemplateObject(gameObject))
            {
                return type == typeof(MeshFilter)
                    || type == typeof(MeshRenderer)
                    || type == typeof(BoxCollider)
                    || type == typeof(SphereCollider)
                    || type == typeof(CapsuleCollider)
                    || type == typeof(MeshCollider);
            }

            if (IsAudioSourceTemplateObject(gameObject))
            {
                return type == typeof(AudioSource);
            }

            return false;
        }

        private static bool IsPrimitiveTemplateObject(GameObject gameObject)
        {
            var meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null || gameObject.GetComponent<MeshRenderer>() == null)
            {
                return false;
            }

            var mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                return false;
            }

            var meshName = mesh.name;
            return string.Equals(meshName, "Cube", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Sphere", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Capsule", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Cylinder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Plane", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Quad", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAudioSourceTemplateObject(GameObject gameObject)
        {
            return gameObject.GetComponent<AudioSource>() != null
                && gameObject.GetComponents<Component>().Length == 2
                && gameObject.name.StartsWith("Audio Source", StringComparison.OrdinalIgnoreCase);
        }

    }
}
