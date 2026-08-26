namespace Doggiehood.Core.Diagnostics
{
    /// <summary>#692: how loud one captured log line was, as engine-free Core
    /// data. The Unity layer maps <c>UnityEngine.LogType</c> onto this when it
    /// records a line, so Core never names an engine type.</summary>
    public enum DiagnosticLogSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Exception = 3,
    }
}
