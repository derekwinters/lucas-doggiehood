using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #695: picks the share target for the platform the game is actually running
    /// on — an <see cref="AndroidShareTarget"/> on the tablet, and nothing
    /// anywhere else.
    ///
    /// <para>The choice is made on <see cref="Application.platform"/> at runtime
    /// rather than on a <c>UNITY_ANDROID</c> compile symbol on purpose: with the
    /// Android build target selected, that symbol is defined <i>in the Editor
    /// too</i>, so a compile-time choice would hand the Editor a share sheet that
    /// does not exist. Returning no target is what makes the Debug row fall back
    /// to #692's <b>Save bug report</b> behaviour instead of throwing.</para>
    /// </summary>
    public static class BugReportShareTargets
    {
        /// <summary>The share target for this platform, or <c>null</c> where the
        /// OS has no share sheet to offer.</summary>
        public static IBugReportShareTarget ForThisPlatform()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                return new AndroidShareTarget();
            }

            return null;
        }
    }
}
