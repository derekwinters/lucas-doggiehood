using Doggiehood.Core.Art;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// The hidden lost item for a LostItem quest (#31): a small object at
    /// the quest's hidden world position. Tapping it forwards to Core,
    /// which decides whether the quest completes. No hints, no radar.
    ///
    /// #335: a lost "puppy" reuses the shared Cube Pets dog model (the same
    /// asset every roster dog renders, see <see cref="DogView"/>), tinted and
    /// scaled slightly smaller than a puppy dog, staying in place with a slow
    /// look-around yaw. Every other subject (toy #332, ball #333 — no reusable
    /// model) still renders the graybox sphere fallback.
    ///
    /// #311: also offers a screen-space padded tap fallback
    /// (<see cref="TryHandleLostItemTap"/>), checked by TapRouter ahead of
    /// its physics raycast — mirroring the dog speech bubble precedent
    /// (#169, DogView.TryHandleBubbleTap). The ball's SphereCollider (radius
    /// 0.3) projects to a tiny on-screen target under the fixed 45-degree
    /// rig, and the full-map ground Plane collider underlies the whole spawn
    /// area, so a bare Physics.Raycast has effectively zero forgiveness for
    /// touch imprecision — without the padded fallback, the intended
    /// QuestManager.LostItemTapRadius tolerance never actually gets
    /// exercised at runtime, since a hit always arrives with the item's own
    /// exact position (distance 0).
    /// </summary>
    public sealed class LostItemView : MonoBehaviour, IInteractable
    {
        /// <summary>An axis-aligned bounding box has 8 corners; used when
        /// projecting the item's world bounds to screen space for
        /// <see cref="TryHandleLostItemTap"/> (#311).</summary>
        private const int BoundsCornerCount = 8;

        /// <summary>The one Lost-eligible subject with a reusable model (#335):
        /// a lost "puppy" renders the shared Cube Pets dog model rather than
        /// the graybox sphere. toy (#332) / ball (#333) have no reusable model
        /// and keep the sphere fallback.</summary>
        private const string PuppyItemName = "puppy";

        /// <summary>Resources-relative path to the shared Kenney Cube Pets dog
        /// model — the same asset <see cref="DogView"/> loads for every roster
        /// dog (see DogView.CubePetsModelResourcePath). Resources.Load paths
        /// are relative to the Resources folder, so the file name is the whole
        /// path.</summary>
        private const string CubePetsModelResourcePath = "animal-dog";

        /// <summary>#335: the lost puppy reuses the shared dog model but reads
        /// as "slightly smaller" than a puppy DOG (DogView scales puppies to
        /// 0.55) per Derek's direction. Named per rule #161.</summary>
        public const float PuppyModelScale = 0.4f;

        /// <summary>Warm light-brown tint for the lost puppy — there is no
        /// owning Dog to source a breed coat from, so the model gets its own
        /// puppy-ish coat. Multiplies over the model's white base colormap
        /// the same way <see cref="DogView"/>'s PaintModel does.</summary>
        private static readonly Color PuppyCoat = CoreColors.FromHex("#D9A066");

        /// <summary>#335 (optional "consider"): a slow in-place yaw so the
        /// puppy appears to look around while staying put. Rotation only —
        /// never any translation.</summary>
        private const float LookAroundDegreesPerSecond = 30f;

        /// <summary>Graybox sphere fallback geometry for every non-puppy
        /// subject. Named per rule #161.</summary>
        private const float SphereScale = 0.6f;
        private const float SphereGroundHeight = 0.3f;

        /// <summary>#521: child object names for the finder-glow parts, so
        /// EditMode tests and any wiring can find them by name.</summary>
        private const string GlowRootName = "FinderGlow";
        private const string HaloName = "Halo";
        private const string GroundRingName = "GroundRing";
        private const string SparkleName = "Sparkle";

        /// <summary>Translucency of the graybox glow materials — a soft halo
        /// reads through rather than as a solid red blob. Named per #161.</summary>
        private const float GlowAlpha = 0.55f;

        /// <summary>Unity Standard-shader keyword/constants for switching a
        /// cloned material to additive-ish transparent rendering so the glow
        /// blends over the surface instead of occluding it.</summary>
        private const string ShaderModeProperty = "_Mode";
        private const float ShaderTransparentMode = 3f;

        private GameState state;
        private Quest quest;
        private bool looksAround;

        private Transform glowRoot;
        private Transform haloTransform;
        private Transform sparkleTransform;
        private float glowElapsedSeconds;

        public static LostItemView Spawn(GameState state, Quest quest, Transform parent)
        {
            return quest.ItemName == PuppyItemName
                ? SpawnPuppy(state, quest, parent)
                : SpawnSphere(state, quest, parent);
        }

        private static LostItemView SpawnSphere(GameState state, Quest quest, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "LostItem - " + quest.ItemName;
            go.transform.SetParent(parent);
            go.transform.localScale = Vector3.one * SphereScale;
            go.transform.position = new Vector3(
                quest.HiddenItemPosition.Value.X, SphereGroundHeight, quest.HiddenItemPosition.Value.Z);

            return Attach(go, state, quest, looksAround: false);
        }

        /// <summary>#335: a lost "puppy" reuses the shared Cube Pets dog model
        /// (mirroring <see cref="DogView"/>'s importable path): an empty
        /// interactable root with the tinted, slightly-smaller model as a
        /// "Body" child, a fitted tap collider on the root so TapRouter's
        /// raycast can hit it, and a slow in-place look-around. If the model
        /// can't be loaded (a test seam / missing asset) it degrades to the
        /// sphere fallback so the item is still findable.</summary>
        private static LostItemView SpawnPuppy(GameState state, Quest quest, Transform parent)
        {
            var model = Resources.Load<GameObject>(CubePetsModelResourcePath);
            if (model == null)
            {
                return SpawnSphere(state, quest, parent);
            }

            var go = new GameObject("LostItem - " + quest.ItemName);
            go.transform.SetParent(parent);
            // Ground-level pivot, like DogView's imported-model path — the
            // puppy stands on the ground rather than floating like the sphere.
            go.transform.position = new Vector3(
                quest.HiddenItemPosition.Value.X, 0f, quest.HiddenItemPosition.Value.Z);

            var body = Object.Instantiate(model, go.transform).transform;
            body.name = "Body";
            body.localPosition = Vector3.zero;
            body.localScale = Vector3.one * PuppyModelScale;
            PaintModel(body.gameObject, PuppyCoat);

            // The imported FBX has no collider, so add a fitted box while the
            // root still has identity rotation and unit scale (#148 pattern).
            TapColliders.AddFitted(go, body.gameObject);

            return Attach(go, state, quest, looksAround: true);
        }

        private static LostItemView Attach(GameObject go, GameState state, Quest quest, bool looksAround)
        {
            var view = go.AddComponent<LostItemView>();
            view.state = state;
            view.quest = quest;
            view.looksAround = looksAround;
            if (LostItemGlow.ShouldShow(quest))
            {
                view.AttachFinderGlow();
            }

            return view;
        }

        /// <summary>#521: builds the red finder glow as a child of the item — a
        /// soft pulsing halo, a flat ground contact ring and a subtle orbiting
        /// sparkle — so the item pops on any surface regardless of its own
        /// colour. Pure decoration: every part is collider-free and
        /// non-interactable, so TapRouter's raycast passes through to the item
        /// beneath and tap-to-collect (<see cref="LostItemTapZone"/>) is
        /// untouched. A graybox first pass built from Unity primitives; all
        /// sizes/timings are the named <see cref="LostItemGlow"/> constants
        /// (#161) and the colour is the named <c>Palette.LostItemGlowHex</c>.
        /// </summary>
        private void AttachFinderGlow()
        {
            var glowColor = CoreColors.FromHex(Palette.LostItemGlowHex);

            var glow = new GameObject(GlowRootName);
            glowRoot = glow.transform;
            glowRoot.SetParent(transform, worldPositionStays: false);
            // Neutralise the item root's own scale (e.g. the sphere fallback
            // sits at SphereScale) so the glow dimensions are absolute world
            // units, consistent across every item model. Also drop the glow
            // container to ground level so the contact ring sits on the surface
            // no matter the item root's Y (sphere floats at SphereGroundHeight,
            // the puppy is grounded).
            var rootScale = transform.localScale;
            glowRoot.localScale = new Vector3(
                Invert(rootScale.x), Invert(rootScale.y), Invert(rootScale.z));
            glowRoot.localPosition = new Vector3(
                0f, -transform.position.y * Invert(rootScale.y), 0f);

            var halo = BuildGlowPart(
                glowRoot, HaloName, PrimitiveType.Sphere, glowColor,
                new Vector3(0f, LostItemGlow.HaloHeight, 0f),
                Vector3.one * LostItemGlow.HaloScale);
            haloTransform = halo.transform;

            BuildGlowPart(
                glowRoot, GroundRingName, PrimitiveType.Cylinder, glowColor,
                new Vector3(0f, LostItemGlow.GroundRingHeight, 0f),
                new Vector3(LostItemGlow.GroundRingScale, LostItemGlow.GroundRingThickness, LostItemGlow.GroundRingScale));

            var sparkle = BuildGlowPart(
                glowRoot, SparkleName, PrimitiveType.Sphere, glowColor,
                new Vector3(LostItemGlow.SparkleOrbitRadius, LostItemGlow.SparkleHeight, 0f),
                Vector3.one * LostItemGlow.SparkleScale);
            sparkleTransform = sparkle.transform;
        }

        /// <summary>Builds one glow child primitive, strips its collider (the
        /// glow is non-interactive) and paints it with a translucent clone of
        /// the glow colour.</summary>
        private static GameObject BuildGlowPart(
            Transform parent, string name, PrimitiveType shape, Color color,
            Vector3 localPosition, Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;

            // Non-interactive: primitives ship a collider; remove it so the
            // glow never intercepts a tap meant for the item. Mode-aware like
            // Collect (#157) — DestroyImmediate so EditMode tests see it gone
            // at once, Destroy under Play.
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            part.transform.SetParent(parent, worldPositionStays: false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            PaintGlow(part, color);
            return part;
        }

        /// <summary>Paints a glow primitive with a translucent clone of the
        /// standard material so the halo blends over the surface rather than
        /// occluding it — mirrors <see cref="PaintModel"/> but for the glow's
        /// see-through look.</summary>
        private static void PaintGlow(GameObject part, Color color)
        {
            var renderer = part.GetComponent<Renderer>();
            var material = renderer.sharedMaterial != null
                ? new Material(renderer.sharedMaterial)
                : new Material(Shader.Find("Standard"));
            var translucent = new Color(color.r, color.g, color.b, GlowAlpha);
            material.color = translucent;
            if (material.HasProperty(ShaderModeProperty))
            {
                material.SetFloat(ShaderModeProperty, ShaderTransparentMode);
            }

            renderer.sharedMaterial = material;
        }

        private static float Invert(float value)
        {
            return Mathf.Approximately(value, 0f) ? 1f : 1f / value;
        }

        /// <summary>True when a renderer belongs to the finder-glow subtree, so
        /// the tap-bounds computation can skip it (the glow is non-interactive
        /// decoration, not part of the item's tap footprint).</summary>
        private bool IsGlowRenderer(Renderer renderer)
        {
            return glowRoot != null && renderer.transform.IsChildOf(glowRoot);
        }

        /// <summary>Tints every renderer on the imported model by cloning its
        /// material and overwriting .color, preserving the model's colormap
        /// texture so it multiplies cleanly with the coat tint — the same
        /// approach as <see cref="DogView"/>.PaintModel.</summary>
        private static void PaintModel(GameObject root, Color color)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var material = renderer.sharedMaterial != null
                    ? new Material(renderer.sharedMaterial)
                    : new Material(Shader.Find("Standard"));
                material.color = color;
                renderer.sharedMaterial = material;
            }
        }

        private void Update()
        {
            TickLookAround(Time.deltaTime);
            TickGlow(Time.deltaTime);
        }

        /// <summary>#521: advances the finder glow by one frame — pulses the
        /// halo along the deterministic <see cref="LostItemGlow.PulseScaleAt"/>
        /// breathe and drifts the sparkle around the item on
        /// <see cref="LostItemGlow.SparkleAngleAt"/>. Public and deterministic so
        /// EditMode tests can drive it without the Play-mode Update loop
        /// (mirroring <see cref="TickLookAround"/>). A silent no-op when the
        /// glow isn't present (e.g. it was gated off).</summary>
        public void TickGlow(float deltaTime)
        {
            if (haloTransform == null)
            {
                return;
            }

            glowElapsedSeconds += deltaTime;

            haloTransform.localScale =
                Vector3.one * (LostItemGlow.HaloScale * LostItemGlow.PulseScaleAt(glowElapsedSeconds));

            var angleRadians = LostItemGlow.SparkleAngleAt(glowElapsedSeconds) * Mathf.Deg2Rad;
            sparkleTransform.localPosition = new Vector3(
                Mathf.Cos(angleRadians) * LostItemGlow.SparkleOrbitRadius,
                LostItemGlow.SparkleHeight,
                Mathf.Sin(angleRadians) * LostItemGlow.SparkleOrbitRadius);
        }

        /// <summary>#335 (optional): advances the puppy's slow in-place yaw by
        /// one frame so it appears to look around while staying rooted to the
        /// hidden spot. Rotation only — never translation. Public and
        /// deterministic so EditMode tests can drive it without the Play-mode
        /// Update loop (mirroring <see cref="DogView.TickAnimation"/>). A
        /// silent no-op for the sphere fallback.</summary>
        public void TickLookAround(float deltaTime)
        {
            if (!looksAround)
            {
                return;
            }

            transform.Rotate(Vector3.up, LookAroundDegreesPerSecond * deltaTime, Space.World);
        }

        public void OnTapped()
        {
            Collect();
        }

        /// <summary>#311: true when the given screen-space tap falls within
        /// the item's projected bounds, padded per Core's LostItemTapZone —
        /// mirrors DogView.TryHandleBubbleTap (#169). A mouse cursor is
        /// pixel-precise; a finger touch is not, and the tiny SphereCollider
        /// sitting atop the full-map ground Plane has zero forgiveness for
        /// that imprecision — a tap that visually reads as "on the ball" but
        /// lands a little outside its rendered mesh would otherwise land on
        /// the ground and do nothing. Once this padded zone registers a hit,
        /// proximity is already established, so it completes the quest the
        /// same way OnTapped does (forwarding the item's own position keeps
        /// QuestManager.LostItemTapRadius as the single source of truth for
        /// the game-logic tolerance). Returns true on a hit; otherwise a
        /// no-op false (no renderers yet, or the tap missed even the padded
        /// zone). TapRouter checks this ahead of its physics raycast.</summary>
        public bool TryHandleLostItemTap(Camera camera, Vector2 screenPosition)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            // #521: the finder glow adds child renderers, but it's decoration —
            // exclude its subtree so the padded tap zone stays sized to the item
            // itself, not inflated by the halo/ring/sparkle.
            var hasBounds = false;
            var bounds = default(Bounds);
            foreach (var renderer in renderers)
            {
                if (IsGlowRenderer(renderer))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            for (var i = 0; i < BoundsCornerCount; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f));
                var screen = camera.WorldToScreenPoint(corner);
                minX = Mathf.Min(minX, screen.x);
                maxX = Mathf.Max(maxX, screen.x);
                minY = Mathf.Min(minY, screen.y);
                maxY = Mathf.Max(maxY, screen.y);
            }

            if (!LostItemTapZone.Contains(minX, minY, maxX, maxY, screenPosition.x, screenPosition.y))
            {
                return false;
            }

            Collect();
            return true;
        }

        private void Collect()
        {
            if (state.Quests.TapWorldPosition(quest.HiddenItemPosition.Value))
            {
                // Match the mode-aware teardown RefreshBugSwarms uses (#157):
                // Destroy is deferred in edit mode, so EditMode tests (and any
                // edit-time caller) need DestroyImmediate to see it removed.
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
            }
        }
    }
}
