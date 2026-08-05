namespace Doggiehood.Unity
{
    /// <summary>
    /// #541: the Unity-layer payload the shared
    /// <see cref="Doggiehood.Core.Ui.ToastQueue{T}"/> carries — a single
    /// already-assembled message line for one toast. Copy lives entirely in the
    /// Unity layer (rule #2): Core's completion signals name only the quest/step +
    /// amount, and the directors turn those into the approved line
    /// (<see cref="ToastCopy"/>) before enqueuing. The queue itself never inspects
    /// this — it just sequences requests.
    /// </summary>
    public readonly struct ToastRequest
    {
        public ToastRequest(string message)
        {
            Message = message;
        }

        /// <summary>The full one-line toast copy — accomplishment + "+N coins".</summary>
        public string Message { get; }
    }
}
