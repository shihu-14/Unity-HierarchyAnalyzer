using System;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Scanners
{
    public static class UnityObjectIconNameResolver
    {
        public static string GetIconContentName(Type type)
        {
            if (type == null)
            {
                return "DefaultAsset Icon";
            }

            if (typeof(GameObject).IsAssignableFrom(type))
            {
                return "GameObject Icon";
            }

            if (typeof(Camera).IsAssignableFrom(type))
            {
                return "Camera Icon";
            }

            if (typeof(Canvas).IsAssignableFrom(type))
            {
                return "Canvas Icon";
            }

            if (typeof(Light).IsAssignableFrom(type))
            {
                return "Light Icon";
            }

            if (typeof(AudioSource).IsAssignableFrom(type))
            {
                return "AudioSource Icon";
            }

            if (typeof(Component).IsAssignableFrom(type))
            {
                return typeof(MonoBehaviour).IsAssignableFrom(type) ? "cs Script Icon" : type.Name + " Icon";
            }

            if (typeof(Material).IsAssignableFrom(type))
            {
                return "Material Icon";
            }

            if (typeof(Texture).IsAssignableFrom(type))
            {
                return "Texture Icon";
            }

            if (typeof(AudioClip).IsAssignableFrom(type))
            {
                return "AudioClip Icon";
            }

            if (typeof(Mesh).IsAssignableFrom(type))
            {
                return "Mesh Icon";
            }

            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                return "ScriptableObject Icon";
            }

            if (type.Name == "MonoScript")
            {
                return "cs Script Icon";
            }

            return "DefaultAsset Icon";
        }
    }
}
