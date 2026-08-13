using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side tap target for a delivered gift package (#471). Same
    /// pass-through stub shape as <see cref="HouseView"/>/<see cref="EmptyLotView"/>:
    /// tap handling is a stub until a package interaction is designed. Before
    /// this existed the delivered package carried no <see cref="IInteractable"/>,
    /// so <see cref="TapRouter"/>'s <c>GetComponentInParent&lt;IInteractable&gt;()</c>
    /// lookup returned null and the tap was silently swallowed; TapCount/Tapped
    /// let tests observe that the package now routes.
    ///
    /// #703: the package is also TRANSIENT — it shows for a short beat and then
    /// removes itself. Nothing else destroyed it before, so a white cube piled up
    /// in every doorway that ever received a delivery and its stub collider
    /// parked a permanent tap-swallower over that door. The beat is owned HERE,
    /// on the package, so neither the truck's teardown (the package is parented
    /// to the world root so it can outlive the truck) nor quest completion (drop
    /// and receipt are the same frame) can strand it. The duration and the "has
    /// it elapsed" decision live in Core (<see cref="DeliveredPackageLifetime"/>);
    /// this layer only feeds it frame deltas and performs the destruction.
    /// </summary>
    public sealed class PackageView : MonoBehaviour, IInteractable
    {
        private readonly DeliveredPackageLifetime lifetime = new DeliveredPackageLifetime();

        public int TapCount { get; private set; }

        /// <summary>Raised on tap so future package wiring can react.</summary>
        public event System.Action Tapped;

        /// <summary>#703: seconds this package has been sitting at the door.</summary>
        public float ElapsedSeconds => lifetime.ElapsedSeconds;

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>#703: advances this package's own beat by one frame and, once
        /// Core says the beat has elapsed, removes the package outright — the
        /// whole GameObject, collider included, never just its renderer (a hidden
        /// box with a live collider would still swallow every tap on the door).
        /// Public and deterministic so EditMode tests can drive it without the
        /// Play-mode Update loop, mirroring <see cref="DogView"/>.TickAnimation.</summary>
        public void Tick(float deltaTime)
        {
            lifetime.Advance(deltaTime);
            if (!lifetime.HasElapsed)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        public void OnTapped()
        {
            TapCount++;
            Tapped?.Invoke();
        }
    }
}
