using System.Linq;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Contract checks on the shipped scene and project settings
    /// (#19, #22, #38).
    /// </summary>
    public class SceneContractTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [Test]
        public void MainScene_HasTheCameraRigAndWorldBootstrap_AndNoPlayerObject()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var everything = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .ToList();

            Assert.That(everything.Count(t => t.GetComponent<CameraRig>() != null), Is.EqualTo(1),
                "exactly one CameraRig expected");
            Assert.That(everything.Count(t => t.GetComponent<WorldBootstrap>() != null), Is.EqualTo(1),
                "exactly one WorldBootstrap expected");

            // #19: only the camera rig and world objects — no player avatar.
            var offenders = everything
                .Where(t => t.name.ToLowerInvariant().Contains("player")
                    || t.name.ToLowerInvariant().Contains("avatar"))
                .Select(t => t.name)
                .ToList();
            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void Orientation_IsLockedToLandscape()
        {
            // #22: auto-rotate between the two landscape orientations only.
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.AutoRotation));
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft, Is.True);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeRight, Is.True);
            Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToPortraitUpsideDown, Is.False);
        }

        [Test]
        public void AndroidApplicationId_IsThePermanentOne()
        {
            Assert.That(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Is.EqualTo("com.derekwinters.doggiehood"));
        }
    }
}
