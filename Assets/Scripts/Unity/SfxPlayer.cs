using System.Collections.Generic;
using Doggiehood.Core.Audio;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Plays SFX for AudioEventBus events and loops the background track
    /// (#40). Clip slots are empty until real audio assets land (they go
    /// through Git LFS); a missing clip is silently skipped — Core's bus
    /// additionally guarantees a throwing handler can't block gameplay.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip musicLoop;
        [SerializeField] private AudioClip bark;
        [SerializeField] private AudioClip truckArrival;
        [SerializeField] private AudioClip itemDelivered;
        [SerializeField] private AudioClip uiTap;
        [SerializeField] private AudioClip uiConfirm;

        private AudioSource sfxSource;
        private Dictionary<SfxEvent, AudioClip> clips;

        private void Awake()
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;

            if (musicLoop != null)
            {
                var musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.clip = musicLoop;
                musicSource.loop = true;
                musicSource.Play();
            }

            clips = new Dictionary<SfxEvent, AudioClip>
            {
                { SfxEvent.Bark, bark },
                { SfxEvent.TruckArrival, truckArrival },
                { SfxEvent.ItemDelivered, itemDelivered },
                { SfxEvent.UiTap, uiTap },
                { SfxEvent.UiConfirm, uiConfirm },
            };

            AudioEventBus.Subscribe(Play);
        }

        private void Play(SfxEvent sfxEvent)
        {
            if (clips.TryGetValue(sfxEvent, out var clip) && clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip);
            }
        }
    }
}
