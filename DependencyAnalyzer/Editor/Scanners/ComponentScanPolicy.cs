using System;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Scanners
{
    internal static class ComponentScanPolicy
    {
        internal static bool ShouldScanInspectorObjectReference(Component component, SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                return false;
            }

            if (property.propertyPath.StartsWith("m_Children.", StringComparison.Ordinal))
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

        internal static bool ShouldVisualizeComponent(Component component)
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
