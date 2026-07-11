namespace Doggiehood.Unity
{
    /// <summary>Anything in the world the player can tap (#20) — houses now,
    /// dogs when milestone 03 lands.</summary>
    public interface IInteractable
    {
        void OnTapped();
    }
}
