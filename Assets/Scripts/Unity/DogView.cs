using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side dog (#8, #9, #10): body rendered as the shared Kenney
    /// Cube Pets placeholder model when importable (#119), falling back to
    /// graybox capsule+sphere primitives otherwise — both tinted to the
    /// breed coat color. Also owns the speech bubble bound to
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
            Object.DestroyImmediate(bubble.GetComponent<Collider>());

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

            if (!Dog.WantsToWander)
            {
                return;
            }

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
            }
            else
            {
                hasTarget = false;
            }
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
