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
    /// (#66). Tapping forwards to the conversation presenter (#11) — Core
    /// decides whether anything opens.
    /// </summary>
    public sealed class DogView : MonoBehaviour, IInteractable
    {
        public const string BubbleName = "SpeechBubble";

        /// <summary>Resources-relative path to the shared Kenney Cube Pets
        /// placeholder model (#119) — a single low-poly model used for every
        /// roster dog until breed-distinct modeling (#35) lands. Lives under
        /// Assets/Art/Dogs/CubePets/Resources/, and Resources.Load paths are
        /// relative to the Resources folder itself, so the asset's own file
        /// name is the whole path.</summary>
        private const string CubePetsModelResourcePath = "animal-dog";

        public Dog Dog { get; private set; }

        private WanderBehavior wander;
        private MovementProfile profile;
        private GameObject bubble;
        private Transform body;
        private Vector3 currentTarget;
        private DogState appliedState;
        private bool hasTarget;
        private bool usingImportedModel;

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

        public void Init(Dog dog, Transform windowAnchor)
        {
            Dog = dog;
            profile = MovementProfile.ForPersonality(dog.Personality);
            wander = new WanderBehavior(StableSeed(dog.Name), profile);

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

            bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = BubbleName;
            bubble.transform.SetParent(transform);
            bubble.transform.localScale = new Vector3(0.5f, 0.4f, 0.15f);
            bubble.transform.localPosition = new Vector3(0f, 1.6f * scale, 0f);
            Paint(bubble, Color.white);
            // #148: the bubble keeps its primitive collider — it is the sole
            // quest-discovery tap surface (conversation-system.md), and a hit
            // on it routes to this DogView via GetComponentInParent. Inactive
            // bubble (no quest) means inactive collider, so it never
            // intercepts taps meant for the dog underneath.

            ApplyPose(windowAnchor);
            RefreshBubble();
        }

        public void OnTapped()
        {
            Doggiehood.Core.Audio.AudioEventBus.Publish(Doggiehood.Core.Audio.SfxEvent.Bark);

            var presenter = FindFirstObjectByType<ConversationPresenter>();
            if (presenter != null)
            {
                presenter.TryOpen(Dog);
            }
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
            bubble.SetActive(Dog.HasActiveQuest);
        }

        private void Update()
        {
            RefreshBubble();

            if (appliedState != Dog.State)
            {
                ApplyPose(null);
            }

            var moving = false;
            if (Dog.WantsToWander)
            {
                if (!hasTarget)
                {
                    var next = wander.NextTarget(new GridPoint(transform.position.x, transform.position.z));
                    currentTarget = new Vector3(next.X, 0f, next.Z);
                    hasTarget = true;
                }

                var step = profile.Speed * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, currentTarget, step);
                var toward = currentTarget - transform.position;
                if (toward.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(toward.normalized, Vector3.up);
                    moving = true;
                }
                else
                {
                    hasTarget = false;
                }
            }

            TickAnimation(Time.deltaTime, moving);
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
