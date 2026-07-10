using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side dog (#8, #9, #10): graybox body/head primitives in the
    /// breed coat color, a speech bubble bound to HasActiveQuest, wander
    /// movement driven by Core's WanderBehavior/MovementProfile, and pose
    /// application per DogState (#66). Tapping forwards to the conversation
    /// presenter (#11) — Core decides whether anything opens.
    /// </summary>
    public sealed class DogView : MonoBehaviour, IInteractable
    {
        public const string BubbleName = "SpeechBubble";

        public Dog Dog { get; private set; }

        private WanderBehavior wander;
        private MovementProfile profile;
        private GameObject bubble;
        private Transform body;
        private Vector3 currentTarget;
        private DogState appliedState;
        private bool hasTarget;

        public void Init(Dog dog, Transform windowAnchor)
        {
            Dog = dog;
            profile = MovementProfile.ForPersonality(dog.Personality);
            wander = new WanderBehavior(StableSeed(dog.Name), profile);

            var scale = dog.IsPuppy ? 0.55f : 1f;
            var coat = BreedCoats.ForDog(dog);

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
        /// state produces a visually distinct transform on the graybox rig.</summary>
        public void ApplyPose(Transform windowAnchor)
        {
            appliedState = Dog.State;

            switch (Dog.State)
            {
                case DogState.WindowWatch:
                    if (windowAnchor != null)
                    {
                        transform.position = windowAnchor.position;
                        transform.rotation = windowAnchor.rotation;
                    }

                    body.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case DogState.Rest:
                    body.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                case DogState.Sit:
                    body.localRotation = Quaternion.Euler(45f, 0f, 0f);
                    break;
                default:
                    body.localRotation = Quaternion.Euler(90f, 0f, 0f);
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
    }
}
