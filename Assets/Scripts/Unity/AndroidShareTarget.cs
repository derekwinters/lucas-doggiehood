using Doggiehood.Core.Diagnostics;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #695: hands a written bug report to Android's standard share sheet.
    ///
    /// <para>This is the only part of the feature no test can reach — JNI does
    /// not run in EditMode and the intent only resolves on a device — so it is
    /// kept as close to <b>zero logic</b> as the API allows: no branches, no
    /// formatting, no decisions. It builds one <c>ACTION_SEND</c> intent from
    /// values it was handed and constants pinned below, and starts the chooser.
    /// Everything that could be decided differently was decided above the
    /// <see cref="IBugReportShareTarget"/> seam, where it is tested.</para>
    ///
    /// <para>The file is exposed through a <c>FileProvider</c> rather than a
    /// <c>file://</c> URI, because handing out a raw file URI throws
    /// <c>FileUriExposedException</c> on modern Android. The provider is declared
    /// by the <c>doggiehood-share.androidlib</c> plug-in, and its authority is
    /// derived from the live application id by the engine-free
    /// <see cref="FileProviderAuthority"/> so the side-by-side <c>.debug</c>
    /// build (#80/#734) gets its own for free.</para>
    ///
    /// <para><b>Invariant — a shared bug report is never silently truncated.</b>
    /// The report goes in <c>EXTRA_STREAM</c> as an attachment; only the short
    /// summary line goes in <c>EXTRA_SUBJECT</c>/<c>EXTRA_TEXT</c>. A receiving
    /// app may trim message text, and a report arriving with its <c>LOG</c>
    /// section quietly cut off would be worse than one that never sent.</para>
    /// </summary>
    public sealed class AndroidShareTarget : IBugReportShareTarget
    {
        // --- the Java surface this touches, pinned rather than typed inline ---

        /// <summary>The AndroidX <c>FileProvider</c>. Pinned as a constant so the
        /// manifest guard test can assert the declaration names this exact class
        /// (docs/engineering/unity-serialization.md).</summary>
        public const string FileProviderClassName = "androidx.core.content.FileProvider";

        /// <summary>The meta-data key a <c>FileProvider</c> reads its path
        /// configuration from. AndroidX kept the legacy support-library name.</summary>
        public const string FileProviderPathsMetaDataName = "android.support.FILE_PROVIDER_PATHS";

        private const string UnityPlayerClassName = "com.unity3d.player.UnityPlayer";
        private const string CurrentActivityField = "currentActivity";
        private const string JavaFileClassName = "java.io.File";
        private const string IntentClassName = "android.content.Intent";

        private const string GetUriForFileMethod = "getUriForFile";
        private const string SetActionMethod = "setAction";
        private const string SetTypeMethod = "setType";
        private const string PutExtraMethod = "putExtra";
        private const string AddFlagsMethod = "addFlags";
        private const string CreateChooserMethod = "createChooser";
        private const string StartActivityMethod = "startActivity";

        private const string ActionSendField = "ACTION_SEND";
        private const string ExtraSubjectField = "EXTRA_SUBJECT";
        private const string ExtraTextField = "EXTRA_TEXT";
        private const string ExtraStreamField = "EXTRA_STREAM";
        private const string GrantReadUriPermissionField = "FLAG_GRANT_READ_URI_PERMISSION";

        /// <summary>Plain text — the report is meant to be read and pasted into an
        /// issue, and matches <see cref="BugReportFile.FileExtension"/>.</summary>
        public const string MimeType = "text/plain";

        /// <summary>The chooser's title. ASCII only (#291).</summary>
        public const string ChooserTitle = "Send bug report";

        /// <summary>The authority this build's <c>content://</c> URIs are issued
        /// under — derived from the live application id, never hard-coded, so the
        /// release build and the <c>.debug</c> build each get their own.</summary>
        public static string Authority
        {
            get { return FileProviderAuthority.For(Application.identifier); }
        }

        /// <inheritdoc />
        public void Share(string filePath, string summary)
        {
            using (var player = new AndroidJavaClass(UnityPlayerClassName))
            using (var activity = player.GetStatic<AndroidJavaObject>(CurrentActivityField))
            using (var file = new AndroidJavaObject(JavaFileClassName, filePath))
            using (var provider = new AndroidJavaClass(FileProviderClassName))
            using (var intentClass = new AndroidJavaClass(IntentClassName))
            using (var intent = new AndroidJavaObject(IntentClassName))
            using (var uri = provider.CallStatic<AndroidJavaObject>(
                       GetUriForFileMethod, activity, Authority, file))
            {
                intent.Call<AndroidJavaObject>(
                    SetActionMethod, intentClass.GetStatic<string>(ActionSendField));
                intent.Call<AndroidJavaObject>(SetTypeMethod, MimeType);
                intent.Call<AndroidJavaObject>(
                    PutExtraMethod, intentClass.GetStatic<string>(ExtraSubjectField), summary);
                intent.Call<AndroidJavaObject>(
                    PutExtraMethod, intentClass.GetStatic<string>(ExtraTextField), summary);
                intent.Call<AndroidJavaObject>(
                    PutExtraMethod, intentClass.GetStatic<string>(ExtraStreamField), uri);
                intent.Call<AndroidJavaObject>(
                    AddFlagsMethod, intentClass.GetStatic<int>(GrantReadUriPermissionField));

                using (var chooser = intentClass.CallStatic<AndroidJavaObject>(
                           CreateChooserMethod, intent, ChooserTitle))
                {
                    activity.Call(StartActivityMethod, chooser);
                }
            }
        }
    }
}
