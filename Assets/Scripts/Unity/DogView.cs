using Doggiehood.Core.Cameras;
using Doggiehood.Core.Decorations;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side dog (#8, #9, #10): body rendered as the shared Kenney
    /// Cube Pets placeholder model when importable (#119), falling back to
    /// graybox capsule+sphere primitives otherwise — both tinted to the
    /// breed coat color. The imported model additionally plays the pack's
    /// walk/idle takes via the Playables API (walk while wandering, idle
    /// otherwise); the primitive fallback stays animation-free. Also owns
    /// the speech bubble bound to
    /// HasActiveQuest, wander movement driven by Core's
    /// WanderBehavior/MovementProfile, and pose application per DogState
    /// (#66). Tapping the body opens the dog profile (#165); tapping the
    /// speech bubble opens the conversation (#11) — the two affordances are
    /// split so the bubble stays the sole quest-discovery surface.
    /// </summary>
    public sealed class DogView : MonoBehaviour, IInteractable
    {
        public const string BubbleName = "SpeechBubble";

        /// <summary>#148 follow-up: world-unit gap between the top of the
        /// dog's tallest renderer and the bottom of the speech bubble, so
        /// the bubble reads clearly above the head for adults and puppies
        /// alike. Guarded by an EditMode test.</summary>
        public const float BubbleClearanceAboveHead = 1f;

        /// <summary>#148: bubble size fixed for all dogs (readability at
        /// DefaultZoom in the x7 kit-scale world — see the readable-tap-
        /// target EditMode test); only the hover height adapts, via the
        /// measured body bounds.</summary>
        private static readonly Vector3 BubbleScale = new Vector3(2.4f, 2f, 0.6f);

        /// <summary>An axis-aligned bounding box has 8 corners; used when
        /// projecting the bubble's world bounds to screen space for
        /// TryHandleBubbleTap (#169).</summary>
        private const int BoundsCornerCount = 8;

        /// <summary>Minimum squared travel (world units) before the dog turns
        /// to face its direction of movement — below this the step is
        /// numerically zero and re-facing would jitter. Shared by the wander
        /// step and the #470 quest walk-home turn.</summary>
        private const float FacingThresholdSqr = 0.001f;

        /// <summary>Resources-relative path to the Kenney Cube Pets model
        /// (#119) — the single standard shared model used for every roster dog
        /// (decision 2026-07-16, #166/#35: Cube Pets is the standard mesh;
        /// breeds vary by coat/tint, not by a per-breed mesh). Lives under
        /// Assets/Art/Dogs/CubePets/Resources/, and Resources.Load paths are
        /// relative to the Resources folder itself, so the asset's own file
        /// name is the whole path.</summary>
        private const string CubePetsModelResourcePath = "animal-dog";

        public Dog Dog { get; private set; }

        private WanderBehavior wander;
        private MovementProfile profile;
        // #398: resolves the LIVE walk network each wander step. DogSpawner
        // binds this to () => state.WalkNetwork so an already-spawned dog
        // wanders onto newly unlocked tiles the moment the map grows; a null
        // binding (e.g. a bare EditMode Init) falls back to the starting
        // intersection's static network.
        private System.Func<WalkNetwork> networkProvider;
        private GameObject bubble;
        private CameraRig cameraRig;
        private OnboardingOverlay onboardingOverlay;
        private Transform body;
        private Vector3 currentTarget;
        private DogState appliedState;
        private bool hasTarget;
        private bool usingImportedModel;

        // #546: the walk-network edge of the wander hop currently in flight, and
        // whether it crosses a road. A dog stepping onto a Crosswalk edge must
        // first claim it on the shared RoadCrossingGate; if a vehicle already
        // holds it the dog waits at its curb node until it releases, and once the
        // dog reaches the far curb it releases its own claim.
        private WalkEdge hopEdge;
        private bool hopIsCrosswalk;
        private bool holdingCrossingClaim;

        // #470: the last wander-reset token observed from Core. When the dog's
        // Dog.WanderResetToken advances (it bumps on every hand-back to the
        // street, e.g. after a quest delivery), the cached wander target is
        // dropped so the next step recomputes a fresh one from the dog's real
        // position instead of beelining to the stale pre-quest target.
        private int lastWanderToken;

        // #112: the in-progress walk-over to a comfort decoration, when the
        // dog has decided to rest. Core owns the route and the per-step
        // position; this view just mirrors Position onto the transform each
        // frame and commits the Rest flip on arrival. Null while wandering.
        private RestApproach restApproach;

        // Cube Pets animation state: the pack's takes (idle/walk/...) are
        // clip sub-assets of the FBX, played through a PlayableGraph because
        // the FBX's .meta (and thus its GUID) is generated locally — no
        // AnimatorController asset can reference the clips from the repo.
        private PlayableGraph playableGraph;
        private AnimationPlayableOutput animationOutput;
        private AnimationClipPlayable clipPlayable;
        private AnimationClip idleClip;
        private AnimationClip walkClip;
        private AnimationClip currentClip;
        private bool hasAnimation;

        /// <summary>Name of the animation clip currently playing, or null
        /// when no animation is wired (primitive fallback, or the walk/idle
        /// takes weren't found). Exposed for EditMode tests, which can't run
        /// the Play-mode Update loop.</summary>
        public string CurrentAnimationClipName => currentClip != null ? currentClip.name : null;

        /// <summary>Local time of the playing clip, for asserting manual
        /// looping in EditMode tests. 0 when no animation is wired.</summary>
        public double CurrentAnimationTime => clipPlayable.IsValid() ? clipPlayable.GetTime() : 0.0;

        public void Init(Dog dog, Transform windowAnchor, System.Func<WalkNetwork> networkProvider = null)
        {
            Dog = dog;
            lastWanderToken = dog.WanderResetToken;
            this.networkProvider = networkProvider ?? (() => NeighborhoodLayout.WalkNetwork);
            profile = MovementProfile.ForPersonality(dog.Personality);
            // #430: thread the dog's own house through so its wander may step
            // onto its OWN front walkway (and no other house's).
            wander = new WanderBehavior(StableSeed(dog.Name), profile, this.networkProvider, dog.HouseId);

            var scale = dog.IsPuppy ? 0.55f : 1f;
            var coat = BreedCoats.ForDog(dog);

            var cubePetsModel = Resources.Load<GameObject>(CubePetsModelResourcePath);
            usingImportedModel = cubePetsModel != null;
            if (usingImportedModel)
            {
                // #119: shared Cube Pets placeholder — a single imported
                // model stands in for body+head together, so there is no
                // separate Head sibling in this path.
                body = Object.Instantiate(cubePetsModel, transform).transform;
                body.name = "Body";
                body.localScale = Vector3.one * scale;
                body.localPosition = Vector3.zero;
                PaintModel(body.gameObject, coat);
                SetupAnimation();

                // #148: the imported FBX has no collider (the primitive
                // fallback rig gets them for free from CreatePrimitive), so
                // without this fitted box TapRouter's raycast passes straight
                // through the dog and taps never register. Added while the
                // root still has identity rotation — ApplyPose runs later.
                TapColliders.AddFitted(gameObject, body.gameObject);
            }
            else
            {
                body = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
                body.name = "Body";
                body.SetParent(transform);
                body.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.localScale = new Vector3(0.5f * scale, 0.7f * scale, 0.6f * scale);
                body.localPosition = new Vector3(0f, 0.5f * scale, 0f);
                Paint(body.gameObject, coat);

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                head.name = "Head";
                head.SetParent(transform);
                head.localScale = Vector3.one * 0.45f * scale;
                head.localPosition = new Vector3(0f, 0.75f * scale, 0.6f * scale);
                Paint(head.gameObject, coat);
            }

            // #148 follow-up: hover height derived from the measured body
            // (bubble not created yet, so only body/head renderers count).
            // The root still has identity rotation and unit scale here, so
            // world bounds convert to local by subtracting the position.
            var dogTopLocalY = 0f;
            foreach (var bodyRenderer in GetComponentsInChildren<Renderer>())
            {
                dogTopLocalY = Mathf.Max(dogTopLocalY, bodyRenderer.bounds.max.y - transform.position.y);
            }

            bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = BubbleName;
            bubble.transform.SetParent(transform);
            // #148: sized for the x7 kit-scale world — at DefaultZoom the
            // camera shows 36 world units of height, so the bubble needs ~2
            // world units to project to a readable >=40 px tap target on a
            // 1080p-reference view (guarded by an EditMode test).
            bubble.transform.localScale = BubbleScale;
            bubble.transform.localPosition = new Vector3(
                0f, dogTopLocalY + BubbleClearanceAboveHead + BubbleScale.y / 2f, 0f);
            Paint(bubble, Color.white);
            // #148: the bubble keeps its primitive collider — it is the sole
            // quest-discovery tap surface (conversation-system.md), and a hit
            // on it routes to this DogView via GetComponentInParent. Inactive
            // bubble (no quest) means inactive collider, so it never
            // intercepts taps meant for the dog underneath.

            ApplyPose(windowAnchor);
            RefreshBubble();
            FaceBubbleToCamera();
        }

        /// <summary>#148 follow-up / #266: billboards the bubble at the
        /// Core-defined camera-facing orientation (<see cref="CameraFacing"/>),
        /// sourced from the live camera yaw so the bubble stays head-on at
        /// every rotation (#203) rather than pinned to the old fixed 45° yaw.
        /// The bubble inherits the dog's rotation as a child, so this
        /// world-space re-assert runs at Init and every frame. Purely
        /// rotational: the collider stays a sphere and the tap routing is
        /// unaffected.</summary>
        public void FaceBubbleToCamera()
        {
            WorldMarkerBillboard.Face(bubble.transform, ResolveCameraRig());
        }

        /// <summary>Lazily finds and caches the scene's <see cref="CameraRig"/>
        /// so the bubble can read live yaw (#266) without a per-frame scene
        /// scan. Re-searches while null, so a rig created after Init is still
        /// picked up; null (no rig) makes <see cref="WorldMarkerBillboard"/>
        /// fall back to the fixed default yaw.</summary>
        private CameraRig ResolveCameraRig()
        {
            if (cameraRig == null)
            {
                cameraRig = FindFirstObjectByType<CameraRig>();
            }

            return cameraRig;
        }

        /// <summary>Tapping a dog's body opens its profile (#165,
        /// docs/specs/ui/dog-profile.md). The conversation is reached instead
        /// by tapping the speech bubble — the sole quest-discovery surface
        /// (conversation-system.md, #11) — routed through
        /// <see cref="OpenConversation"/>.</summary>
        public void OnTapped()
        {
            Doggiehood.Core.Audio.AudioEventBus.Publish(Doggiehood.Core.Audio.SfxEvent.Bark);

            var overlay = FindFirstObjectByType<DogProfileOverlay>();
            if (overlay != null)
            {
                overlay.Open(Dog);
            }
        }

        /// <summary>Opens this dog's conversation (#11) — the speech bubble's
        /// action. Core decides whether anything opens; a dog with no active
        /// quest is a silent no-op. Separate from <see cref="OnTapped"/> so the
        /// bubble surfaces the quest while the body opens the profile (#165).</summary>
        public void OpenConversation()
        {
            Doggiehood.Core.Audio.AudioEventBus.Publish(Doggiehood.Core.Audio.SfxEvent.Bark);

            var presenter = FindFirstObjectByType<ConversationPresenter>();
            if (presenter != null)
            {
                presenter.TryOpen(Dog);
            }
        }

        /// <summary>#169: true when the bubble is currently shown (a quest
        /// is active) and the given screen-space tap falls within its
        /// projected bounds, padded per Core's BubbleTapZone. A mouse
        /// cursor is pixel-precise; a finger touch is not — the #148/#158
        /// SphereCollider-only raycast has zero forgiveness for a tap that
        /// visually reads as "on the bubble" but lands a little outside its
        /// exact rendered mesh, which this catches. Calls OnTapped and
        /// returns true on a hit; otherwise a no-op false (bubble inactive,
        /// no renderers yet, or the tap missed even the padded zone).
        /// TapRouter checks this ahead of its physics raycast.</summary>
        public bool TryHandleBubbleTap(Camera camera, Vector2 screenPosition)
        {
            if (!bubble.activeSelf)
            {
                return false;
            }

            var renderers = bubble.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return false;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
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

            if (!BubbleTapZone.Contains(minX, minY, maxX, maxY, screenPosition.x, screenPosition.y))
            {
                return false;
            }

            OpenConversation();
            return true;
        }

        /// <summary>Applies the pose for the dog's current state (#66); each
        /// state produces a visually distinct rotation on the body. Rotations
        /// are rig-specific: the graybox capsule is authored standing on end
        /// (its idle is a 90° pitch to lay it flat), while the imported Cube
        /// Pets model already stands on its feet with a ground-level pivot
        /// (its idle is identity — pitching it tips it face-down and below
        /// ground).</summary>
        public void ApplyPose(Transform windowAnchor)
        {
            appliedState = Dog.State;

            var idle = usingImportedModel ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);

            switch (Dog.State)
            {
                case DogState.WindowWatch:
                    if (windowAnchor != null)
                    {
                        transform.position = windowAnchor.position;
                        transform.rotation = windowAnchor.rotation;
                    }

                    body.localRotation = idle;
                    break;
                case DogState.Rest:
                    body.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                case DogState.Sit:
                    body.localRotation = usingImportedModel
                        ? Quaternion.Euler(-30f, 0f, 0f)
                        : Quaternion.Euler(45f, 0f, 0f);
                    break;
                default:
                    body.localRotation = idle;
                    break;
            }
        }

        public void RefreshBubble()
        {
            // #329: the bubble follows the dog's active quest, except that
            // during onboarding the target dog's bubble is gated to appear
            // only at the TapBubble step — so the player can't open the
            // conversation during Pan/Zoom and strand the tutorial.
            bubble.SetActive(Dog.HasActiveQuest && !SuppressedByOnboarding());
        }

        /// <summary>#329: consults the live onboarding overlay (if one exists)
        /// for whether this dog's bubble is currently gated shut. Only ever
        /// true for the onboarding target dog before the TapBubble step; the
        /// short-circuit in <see cref="RefreshBubble"/> keeps the lookup off
        /// the path for dogs without a quest, and the resolved overlay is
        /// cached like the camera rig. No overlay (post-onboarding, or a save
        /// with onboarding already complete) means no suppression.</summary>
        private bool SuppressedByOnboarding()
        {
            if (onboardingOverlay == null)
            {
                onboardingOverlay = FindFirstObjectByType<OnboardingOverlay>();
            }

            return onboardingOverlay != null && onboardingOverlay.SuppressesBubbleFor(Dog);
        }

        private void Update()
        {
            RefreshBubble();

            if (appliedState != Dog.State)
            {
                ApplyPose(null);
            }

            var moving = false;
            if (restApproach != null)
            {
                moving = TickRestApproach(Time.deltaTime);
            }
            else
            {
                moving = TickWander(Time.deltaTime);
            }

            TickAnimation(Time.deltaTime, moving);
            FaceBubbleToCamera();
        }

        /// <summary>#470: true when the dog is holding a cached wander target.
        /// Exposed for EditMode tests, which can't run the Play-mode Update
        /// loop.</summary>
        public bool HasWanderTarget => hasTarget;

        /// <summary>#470: the dog's current cached wander destination (only
        /// meaningful while <see cref="HasWanderTarget"/>). Exposed for
        /// EditMode tests.</summary>
        public Vector3 WanderTarget => currentTarget;

        /// <summary>Advances the free-roam wander one frame: picks a fresh
        /// target when needed (including right after a #470 delivery hand-back,
        /// signalled by Core's <see cref="Dog.WanderResetToken"/>), steps
        /// toward it, and turns to face the direction of travel. A no-op
        /// returning false when the dog isn't currently a wanderer (window
        /// dog, or mid-delivery — the QuestDirector owns the transform then).
        /// Returns whether the dog visibly moved this frame (drives the walk
        /// take). Public so EditMode tests can drive a wander step without the
        /// Play-mode Update loop, like <see cref="TickRestApproach"/>.</summary>
        public bool TickWander(float deltaTime)
        {
            if (!Dog.WantsToWander)
            {
                return false;
            }

            ConsumeWanderReset();

            if (!hasTarget)
            {
                var next = wander.NextTarget(new GridPoint(transform.position.x, transform.position.z));
                BeginWanderHop(next);
            }

            // #546: yield at the curb. A hop across a crosswalk may only proceed
            // once this dog claims that crosswalk on the shared gate; while a
            // vehicle holds it the dog stays put at its curb node (no advance) and
            // retries on later frames.
            if (hopIsCrosswalk && !holdingCrossingClaim)
            {
                if (!RoadCrossingGate.Shared.TryEnter(hopEdge, this))
                {
                    return false;
                }

                holdingCrossingClaim = true;
            }

            var step = profile.Speed * deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, currentTarget, step);
            var toward = currentTarget - transform.position;
            if (toward.sqrMagnitude > FacingThresholdSqr)
            {
                transform.rotation = Quaternion.LookRotation(toward.normalized, Vector3.up);
                return true;
            }

            ArriveAtWanderTarget();
            return false;
        }

        /// <summary>
        /// #546: begins a wander hop toward network node <paramref name="toNode"/>
        /// from the dog's current node, resolving the connecting walk edge so a
        /// crosswalk hop can be gated at the curb. Production picks
        /// <paramref name="toNode"/> from <see cref="WanderBehavior"/>; exposed
        /// publicly so an EditMode test can drive a deterministic hop across a
        /// specific crosswalk (the random wander can't be steered onto one).
        /// </summary>
        public void BeginWanderHop(GridPoint toNode)
        {
            var network = networkProvider();
            var fromNode = network.NearestWalkableNode(
                new GridPoint(transform.position.x, transform.position.z));
            hopEdge = ResolveHopEdge(network, fromNode, toNode, out hopIsCrosswalk);
            var groundY = network.GroundHeight(fromNode, toNode);
            currentTarget = new Vector3(toNode.X, groundY, toNode.Z);
            hasTarget = true;
            holdingCrossingClaim = false;
        }

        /// <summary>#546: the dog reached its target node — drop the target and,
        /// if this hop crossed a road, release the crosswalk claim so the next
        /// waiting occupant may enter.</summary>
        private void ArriveAtWanderTarget()
        {
            hasTarget = false;
            AbandonCrossing();
        }

        /// <summary>#546: releases any crosswalk claim this dog holds and clears
        /// the in-flight crosswalk state, so a claim never leaks when the hop
        /// completes, when the scripted quest walk / rest approach takes over, or
        /// on a wander reset.</summary>
        private void AbandonCrossing()
        {
            if (holdingCrossingClaim)
            {
                RoadCrossingGate.Shared.Exit(hopEdge, this);
                holdingCrossingClaim = false;
            }

            hopIsCrosswalk = false;
        }

        /// <summary>#546: the walk edge joining <paramref name="fromNode"/> to
        /// <paramref name="toNode"/>, and whether it is a crosswalk. Returns a
        /// default edge (and false) when the two aren't directly joined — e.g. a
        /// stuck dog whose next node is itself — in which case no gating applies.</summary>
        private static WalkEdge ResolveHopEdge(
            WalkNetwork network, GridPoint fromNode, GridPoint toNode, out bool isCrosswalk)
        {
            foreach (var edge in network.EdgesFrom(fromNode))
            {
                if (edge.Other(fromNode).Equals(toNode))
                {
                    isCrosswalk = edge.Kind == WalkEdgeKind.Crosswalk;
                    return edge;
                }
            }

            isCrosswalk = false;
            return default;
        }

        /// <summary>#470: drops the cached wander target if Core has advanced
        /// the dog's reset token since we last looked — so a delivery
        /// hand-back forces a fresh target computed from the dog's real (home)
        /// position rather than resuming the stale pre-quest one.</summary>
        private void ConsumeWanderReset()
        {
            if (Dog.WanderResetToken != lastWanderToken)
            {
                hasTarget = false;
                AbandonCrossing();
                lastWanderToken = Dog.WanderResetToken;
            }
        }

        /// <summary>#470: the QuestDirector's scripted walk home is taking over
        /// this dog's transform — drop any cached wander target so the wander
        /// branch can't beeline to it, and so the resume after delivery starts
        /// fresh. Public so the director and EditMode tests can invoke the
        /// takeover directly.</summary>
        public void BeginQuestWalk()
        {
            hasTarget = false;
            currentTarget = Vector3.zero;
            AbandonCrossing();
        }

        /// <summary>#470: one scripted-walk step toward a route waypoint —
        /// turns to face the direction of travel BEFORE moving (fixing the
        /// moonwalk where WalkDogHome never wrote rotation), then advances by
        /// <paramref name="maxDistance"/>. Facing is flattened to the ground
        /// plane so the dog turns (yaw) rather than tipping toward the y=0
        /// route point. Public so the QuestDirector drives it and EditMode
        /// tests can assert facing.</summary>
        public void WalkTowardWaypoint(Vector3 target, float maxDistance)
        {
            var before = transform.position;
            var toward = new Vector3(target.x - before.x, 0f, target.z - before.z);
            if (toward.sqrMagnitude > FacingThresholdSqr)
            {
                transform.rotation = Quaternion.LookRotation(toward.normalized, Vector3.up);
            }

            transform.position = Vector3.MoveTowards(before, target, maxDistance);
        }

        /// <summary>
        /// Picks the next wander destination from the LIVE walk network
        /// (#398): snaps <paramref name="fromPosition"/> to the nearest
        /// walkable node, asks Core's <see cref="WanderBehavior"/> for the
        /// next node, and resolves the ground height for the hop (#151 — the
        /// Kenney kit models the sidewalk band raised above the road, so a
        /// fixed Y clips the legs into it). Because the network is resolved
        /// through the live provider on every call, an already-spawned dog
        /// automatically wanders onto tiles unlocked after it spawned. Public
        /// so EditMode tests can drive the wander step without the Play-mode
        /// Update loop, exactly like <see cref="TickAnimation"/>.
        /// </summary>
        public Vector3 SelectWanderTarget(Vector3 fromPosition)
        {
            var network = networkProvider();
            var current = new GridPoint(fromPosition.x, fromPosition.z);
            var currentNode = network.NearestWalkableNode(current);
            var next = wander.NextTarget(current);
            var groundY = network.GroundHeight(currentNode, next);
            return new Vector3(next.X, groundY, next.Z);
        }

        /// <summary>True while the dog is walking over to a comfort decoration
        /// (#112) — QuestDirector checks this so it doesn't start a second
        /// approach for a dog already en route.</summary>
        public bool IsApproachingRest => restApproach != null;

        /// <summary>#112: hand this dog a Core-computed walk-over to its comfort
        /// decoration. The dog abandons its current wander target and follows
        /// the route until it arrives, then settles into the Rest pose.</summary>
        public void BeginRestApproach(RestApproach approach)
        {
            restApproach = approach;
            hasTarget = false;
            AbandonCrossing();
        }

        /// <summary>Advances the active rest approach one frame: Core moves the
        /// route position, this view mirrors it onto the transform (preserving
        /// the dog's ground height), faces the direction of travel, and on
        /// arrival commits the Rest flip and clears the approach. Returns
        /// whether the dog visibly moved this frame (drives the walk take).
        /// A no-op returning false when no approach is active. Public so
        /// EditMode tests can drive the walk-over without the Play-mode Update
        /// loop, exactly like <see cref="TickAnimation"/>.</summary>
        public bool TickRestApproach(float deltaTime)
        {
            if (restApproach == null)
            {
                return false;
            }

            var before = transform.position;
            restApproach.Advance(RestApproach.ApproachSpeed * deltaTime);

            transform.position = new Vector3(
                restApproach.Position.X, transform.position.y, restApproach.Position.Z);

            var toward = transform.position - before;
            var moving = toward.sqrMagnitude > 0.001f;
            if (moving)
            {
                transform.rotation = Quaternion.LookRotation(toward.normalized, Vector3.up);
            }

            if (restApproach.HasArrived)
            {
                Dog.TryRest(comfortDecorationSelected: true);
                restApproach = null;
            }

            return moving;
        }

        /// <summary>Advances the Cube Pets animation by one frame: walk take
        /// while actively moving toward a wander target, idle take otherwise,
        /// with manual looping (imported FBX takes default to non-looping
        /// because loop-time lives in importer settings the repo doesn't
        /// control). Public so EditMode tests can drive frames
        /// deterministically — they can't run the Play-mode Update loop. A
        /// silent no-op when no animation is wired (primitive fallback, or
        /// the walk/idle takes weren't found in the FBX).</summary>
        public void TickAnimation(float deltaTime, bool isMoving)
        {
            if (!hasAnimation)
            {
                return;
            }

            var desired = isMoving ? walkClip : idleClip;
            if (desired != currentClip)
            {
                PlayClip(desired);
            }

            playableGraph.Evaluate(deltaTime);

            var length = currentClip.length;
            if (length > 0f && clipPlayable.GetTime() >= length)
            {
                clipPlayable.SetTime(clipPlayable.GetTime() % length);
            }
        }

        private void OnDestroy()
        {
            // #546: never leave a crosswalk claimed when the dog is torn down.
            AbandonCrossing();

            // Leaked PlayableGraphs spam errors on domain reload/exit.
            if (playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }
        }

        /// <summary>Wires the Cube Pets walk/idle takes (clip sub-assets of
        /// the FBX, loaded via Resources.LoadAll on the same
        /// Resources-relative path as the model) into a manually-evaluated
        /// PlayableGraph on the Body's Animator. Degrades silently to the
        /// un-animated behavior if either take can't be found — never throws,
        /// never logs per-frame.</summary>
        private void SetupAnimation()
        {
            var clips = Resources.LoadAll<AnimationClip>(CubePetsModelResourcePath);
            idleClip = FindClip(clips, "idle");
            walkClip = FindClip(clips, "walk");
            if (idleClip == null || walkClip == null)
            {
                return;
            }

            var animator = body.GetComponent<Animator>();
            if (animator == null)
            {
                animator = body.gameObject.AddComponent<Animator>();
            }

            playableGraph = PlayableGraph.Create($"DogView.{Dog.Name}");
            // Manual mode: Update (Play mode) and tests (EditMode) both
            // advance time through TickAnimation, so behavior is identical
            // and deterministic in both.
            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            animationOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
            hasAnimation = true;
            PlayClip(idleClip);
            playableGraph.Play();
            playableGraph.Evaluate(0f);
        }

        private void PlayClip(AnimationClip clip)
        {
            if (clipPlayable.IsValid())
            {
                clipPlayable.Destroy();
            }

            clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
            animationOutput.SetSourcePlayable(clipPlayable);
            currentClip = clip;
        }

        /// <summary>Finds an FBX take by name: exact case-insensitive match,
        /// or importer-decorated form ("animal-dog|walk") matched by
        /// "|"-prefixed suffix. Editor-only "__preview__" clips are skipped —
        /// they'd otherwise satisfy the suffix match.</summary>
        private static AnimationClip FindClip(AnimationClip[] clips, string takeName)
        {
            foreach (var clip in clips)
            {
                if (clip == null)
                {
                    continue;
                }

                var name = clip.name.ToLowerInvariant();
                if (name.StartsWith("__preview__"))
                {
                    continue;
                }

                if (name == takeName || name.EndsWith("|" + takeName))
                {
                    return clip;
                }
            }

            return null;
        }

        private static int StableSeed(string name)
        {
            var seed = 17;
            foreach (var c in name)
            {
                seed = seed * 31 + c;
            }

            return seed;
        }

        private static void Paint(GameObject target, Color color)
        {
            var material = new Material(Shader.Find("Standard"));
            material.color = color;
            target.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>Tints every renderer on an imported model (#119) by
        /// cloning its existing material and overwriting .color, so the
        /// model's colormap texture (base color white) is preserved and
        /// multiplies cleanly with the breed coat tint.</summary>
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
    }
}
