namespace Doggiehood.Unity
{
    /// <summary>
    /// #695: the seam between "a bug report has been written to disk" and "the
    /// operating system is now offering it to whatever app the player picks".
    ///
    /// <para>It exists because JNI cannot run in EditMode. Everything above this
    /// interface — rendering the snapshot, writing the file, composing the
    /// summary line, wiring the Debug row, raising the toast — is ordinary
    /// testable code asserted against a fake target that records what it was
    /// handed. Below it sits <see cref="AndroidShareTarget"/>, deliberately as
    /// close to zero logic as possible, because it is the one part no test can
    /// reach.</para>
    ///
    /// <para>Off Android there is no share sheet at all, so
    /// <see cref="BugReportShareTargets.ForThisPlatform"/> returns no target and
    /// the row falls back to #692's <b>Save bug report</b> behaviour rather than
    /// throwing.</para>
    /// </summary>
    public interface IBugReportShareTarget
    {
        /// <summary>Offers the report at <paramref name="filePath"/> to the OS as
        /// a file attachment, with <paramref name="summary"/> as the short human
        /// readable message beside it.
        ///
        /// <para><b>Invariant — a shared bug report is never silently
        /// truncated.</b> The report travels as the <i>file</i>; the summary is a
        /// one-line label, never the report body. An implementation that inlined
        /// the report into the message would be free to have it cut off by the
        /// receiving app, and nobody could tell it happened.</para></summary>
        void Share(string filePath, string summary);
    }
}
