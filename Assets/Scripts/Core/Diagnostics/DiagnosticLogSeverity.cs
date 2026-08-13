namespace Doggiehood.Core.Diagnostics
{
    /// <summary>
    /// #692: the severity of one captured log line, as engine-free Core data.
    /// Mirrors the engine's log-type vocabulary one-for-one so the Unity-layer
    /// ring buffer (<c>DiagnosticLogBuffer</c>) can translate without losing
    /// information, while Core — and therefore the whole report payload — stays
    /// free of any <c>UnityEngine</c> reference.
    /// </summary>
    public enum DiagnosticLogSeverity
    {
        Log,
        Warning,
        Assert,
        Error,
        Exception,
    }
}
