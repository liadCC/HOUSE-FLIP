using System.Collections.Generic;
using HouseFlip.Core;
using UnityEngine;

namespace HouseFlip.Audio
{
    /// <summary>
    /// One-shot SFX and looping music (GDD 24).
    ///
    /// Listens to <see cref="GameEvents.SfxRequested"/> rather than being called directly,
    /// so gameplay code never holds a reference to the audio system. Clips are optional:
    /// with none assigned the game runs silently instead of throwing.
    /// </summary>
    public class AudioManager : LocalSingleton<AudioManager>
    {
        [System.Serializable]
        public struct SfxEntry
        {
            public SfxId id;
            public AudioClip clip;

            [Range(0f, 1f)] public float volume;

            [Tooltip("Random pitch spread, for variety on repeated sounds like hammer hits.")]
            [Range(0f, 0.5f)] public float pitchJitter;
        }

        [Header("Music")]
        [SerializeField] private AudioClip backgroundMusic;
        [Range(0f, 1f)][SerializeField] private float musicVolume = 0.35f;

        [Header("SFX")]
        [SerializeField] private List<SfxEntry> sfx = new List<SfxEntry>();
        [SerializeField] private int voiceCount = 8;

        private AudioSource _musicSource;
        private AudioSource[] _voices;
        private int _nextVoice;
        private readonly Dictionary<SfxId, SfxEntry> _lookup = new Dictionary<SfxId, SfxEntry>();

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

            BuildSources();
            BuildLookup();
        }

        private void OnEnable()
        {
            GameEvents.SfxRequested += Play;
        }

        private void OnDisable()
        {
            GameEvents.SfxRequested -= Play;
        }

        private void Start()
        {
            PlayMusic(backgroundMusic);
        }

        private void BuildSources()
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.volume = musicVolume;
            _musicSource.spatialBlend = 0f;

            _voices = new AudioSource[Mathf.Max(1, voiceCount)];
            for (int i = 0; i < _voices.Length; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                _voices[i] = source;
            }
        }

        private void BuildLookup()
        {
            _lookup.Clear();
            foreach (SfxEntry entry in sfx)
            {
                if (entry.clip != null)
                {
                    _lookup[entry.id] = entry;
                }
            }
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null || _musicSource == null)
            {
                return;
            }

            _musicSource.clip = clip;
            _musicSource.volume = musicVolume;
            _musicSource.Play();
        }

        public void StopMusic()
        {
            if (_musicSource != null)
            {
                _musicSource.Stop();
            }
        }

        /// <summary>2D one-shot. Silently does nothing when no clip is assigned for the id.</summary>
        public void Play(SfxId id)
        {
            if (!_lookup.TryGetValue(id, out SfxEntry entry) || entry.clip == null || _voices == null)
            {
                return;
            }

            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;

            voice.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);
            voice.PlayOneShot(entry.clip, entry.volume <= 0f ? 1f : entry.volume);
        }

        /// <summary>Positional one-shot for world events like a wall coming down.</summary>
        public void PlayAt(SfxId id, Vector3 position)
        {
            if (!_lookup.TryGetValue(id, out SfxEntry entry) || entry.clip == null)
            {
                return;
            }

            AudioSource.PlayClipAtPoint(entry.clip, position, entry.volume <= 0f ? 1f : entry.volume);
        }
    }
}
