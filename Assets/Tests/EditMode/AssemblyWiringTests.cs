using Doggiehood.Core.Versioning;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    public class AssemblyWiringTests
    {
        [Test]
        public void CoreAssembly_IsReachableFromTheUnityLayer()
        {
            Assert.That(VersionName.ForDebugBuild("0.1.0", "a1b2c3d"), Is.EqualTo("0.1.0-a1b2c3d"));
        }

        [Test]
        public void GameBootstrap_CanBeAddedToAGameObject()
        {
            var host = new GameObject("bootstrap-under-test");
            try
            {
                Assert.That(host.AddComponent<Doggiehood.Unity.GameBootstrap>(), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
