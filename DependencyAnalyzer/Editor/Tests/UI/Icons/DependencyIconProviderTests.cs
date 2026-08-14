using System;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.Icons;
using NUnit.Framework;
using UnityEditor;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class DependencyIconProviderTests
    {
        [Test]
        public void GetIcon_UsesBuiltInScriptIconForEveryScriptState()
        {
            var missingComponentScript = CreateScriptNode("missing-component-script");
            missingComponentScript.MarkAsMissingTarget(MissingTargetKind.MissingScript);
            var brokenSerializedScript = CreateScriptNode("broken-serialized-script");
            brokenSerializedScript.MarkAsMissingTarget(MissingTargetKind.BrokenReference);
            var validScript = CreateScriptNode("valid-script");
            var builtInScriptIcon = EditorGUIUtility.IconContent("cs Script Icon").image;

            Assert.AreSame(builtInScriptIcon, DependencyIconProvider.GetIcon(missingComponentScript));
            Assert.AreSame(builtInScriptIcon, DependencyIconProvider.GetIcon(brokenSerializedScript));
            Assert.AreSame(builtInScriptIcon, DependencyIconProvider.GetIcon(validScript));
        }

        private static DependencyNode CreateScriptNode(string id)
        {
            return new DependencyNode(
                id,
                default,
                "Scene::" + id,
                id,
                "Script",
                "UnityEngine.MonoBehaviour",
                Array.Empty<string>(),
                "cs Script Icon",
                DependencyNodeKind.Component);
        }
    }
}
