using System;
using Doggiehood.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Covers the debug-build `.debug` applicationId suffix wiring (#80).
    /// PlayerSettings and the environment variable are both process-global,
    /// so every test saves and restores them to avoid leaking state into
    /// other EditMode tests (see SceneContractTests for the sibling pattern
    /// of asserting directly against PlayerSettings).
    /// </summary>
    public class DebugApplicationIdBuildProcessorTests
    {
        private const string PermanentApplicationId = "com.derekwinters.doggiehood";

        private string _originalApplicationId;
        private string _originalEnvValue;

        [SetUp]
        public void SaveGlobalState()
        {
            _originalApplicationId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            _originalEnvValue = Environment.GetEnvironmentVariable(DebugApplicationIdBuildProcessor.DebugBuildEnvironmentVariable);
        }

        [TearDown]
        public void RestoreGlobalState()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, _originalApplicationId);
            Environment.SetEnvironmentVariable(DebugApplicationIdBuildProcessor.DebugBuildEnvironmentVariable, _originalEnvValue);
        }

        [Test]
        public void ApplyIfRequested_AppendsDebugSuffix_WhenEnvironmentVariableTruthy()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PermanentApplicationId);
            Environment.SetEnvironmentVariable(DebugApplicationIdBuildProcessor.DebugBuildEnvironmentVariable, "true");

            DebugApplicationIdBuildProcessor.ApplyIfRequested();

            Assert.That(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Is.EqualTo("com.derekwinters.doggiehood.debug"));
        }

        [Test]
        public void ApplyIfRequested_LeavesIdentifierUnchanged_WhenEnvironmentVariableUnset()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PermanentApplicationId);
            Environment.SetEnvironmentVariable(DebugApplicationIdBuildProcessor.DebugBuildEnvironmentVariable, null);

            DebugApplicationIdBuildProcessor.ApplyIfRequested();

            Assert.That(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Is.EqualTo(PermanentApplicationId));
        }

        [Test]
        public void RestoreIfApplied_RestoresThePermanentIdentifier_AfterASuffixWasApplied()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PermanentApplicationId);
            Environment.SetEnvironmentVariable(DebugApplicationIdBuildProcessor.DebugBuildEnvironmentVariable, "true");

            DebugApplicationIdBuildProcessor.ApplyIfRequested();
            DebugApplicationIdBuildProcessor.RestoreIfApplied();

            Assert.That(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Is.EqualTo(PermanentApplicationId));
        }

        [Test]
        public void RestoreIfApplied_IsANoOp_WhenNoSuffixWasApplied()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PermanentApplicationId);
            Environment.SetEnvironmentVariable(DebugApplicationIdBuildProcessor.DebugBuildEnvironmentVariable, null);

            DebugApplicationIdBuildProcessor.ApplyIfRequested();
            DebugApplicationIdBuildProcessor.RestoreIfApplied();

            Assert.That(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Is.EqualTo(PermanentApplicationId));
        }
    }
}
