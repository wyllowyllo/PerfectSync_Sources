using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InGame.Audio
{
    public class InGameSfxManager : MonoBehaviour
    {
        private const string SpatialPoolChildName = "SpatialSfxPool";
        private const int InitialSpatialPoolSize = 16;
        private const int SpatialPoolGrowthWarnThreshold = 64;

        public static InGameSfxManager Instance { get; private set; }

        private Transform _spatialPoolRoot;
        private readonly Queue<AudioSource> _spatialSfxPool = new Queue<AudioSource>();
        private readonly Dictionary<(SpatialSfxProfile profile, int callerId), float> _spatialCooldowns = new Dictionary<(SpatialSfxProfile, int), float>();
        private int _totalSpatialSources;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[InGameSfxDirector] Duplicate instance on '{name}'; destroying.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeSpatialSfxPool();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void EmitSpatialAt(SpatialSfxProfile profile, Vector3 position, Object caller = null)
        {
            if (!TryBeginSpatialEmit(profile, caller, out AudioSource source, out AudioClip clip, out float volume))
                return;

            StartCoroutine(CoEmitStatic(source, profile, clip, volume, position));
        }

        public void EmitSpatialOn(SpatialSfxProfile profile, Transform follow, Object caller = null)
        {
            if (follow == null)
                return;

            if (!TryBeginSpatialEmit(profile, caller, out AudioSource source, out AudioClip clip, out float volume))
                return;

            StartCoroutine(CoEmitFollowing(source, profile, clip, volume, follow));
        }

        public void PlaySfx2D(SfxProfile profile)
        {
            if (profile == null)
                return;

            AudioClip clip = profile.GetRandomClip();
            if (clip == null)
                return;

            AudioManager.Instance?.Play(AudioType.Sfx, clip);
        }

        private bool TryBeginSpatialEmit(SpatialSfxProfile profile, Object caller, out AudioSource source, out AudioClip clip, out float volume)
        {
            source = null;
            clip = null;
            volume = 0f;

            if (profile == null)
                return false;

            clip = profile.GetRandomClip();
            if (clip == null)
                return false;

            int callerId = caller != null ? caller.GetInstanceID() : 0;
            var key = (profile, callerId);
            if (_spatialCooldowns.TryGetValue(key, out float lastPlayTime) && Time.time - lastPlayTime < profile.Cooldown)
                return false;

            _spatialCooldowns[key] = Time.time;

            source = AcquireSpatialSource();
            volume = ComputeEffectiveSfxVolume() * profile.VolumeScale;
            return true;
        }

        private static float ComputeEffectiveSfxVolume()
        {
            if (AudioManager.Instance == null)
                return 1f;
            return AudioManager.Instance.MasterVolume * AudioManager.Instance.SfxVolume;
        }

        private IEnumerator CoEmitStatic(AudioSource source, SpatialSfxProfile profile, AudioClip clip, float volume, Vector3 position)
        {
            profile.ConfigureSource(source);
            source.pitch = profile.GetRandomPitch();
            source.transform.position = position;
            source.gameObject.SetActive(true);
            source.PlayOneShot(clip, volume);

            float waitDuration = clip.length / Mathf.Max(source.pitch, 0.01f);
            yield return new WaitForSeconds(waitDuration);

            ReleaseSpatialSource(source);
        }

        private IEnumerator CoEmitFollowing(AudioSource source, SpatialSfxProfile profile, AudioClip clip, float volume, Transform follow)
        {
            profile.ConfigureSource(source);
            source.pitch = profile.GetRandomPitch();
            source.transform.position = follow.position;
            source.gameObject.SetActive(true);
            source.PlayOneShot(clip, volume);

            float endTime = Time.time + clip.length / Mathf.Max(source.pitch, 0.01f);
            while (Time.time < endTime)
            {
                if (follow == null)
                    break;
                source.transform.position = follow.position;
                yield return null;
            }

            ReleaseSpatialSource(source);
        }

        private void ReleaseSpatialSource(AudioSource source)
        {
            if (source == null)
                return;

            source.Stop();
            source.clip = null;
            source.transform.SetParent(_spatialPoolRoot, worldPositionStays: false);
            source.transform.localPosition = Vector3.zero;
            source.gameObject.SetActive(false);
            _spatialSfxPool.Enqueue(source);
        }

        private void InitializeSpatialSfxPool()
        {
            var root = new GameObject(SpatialPoolChildName);
            root.transform.SetParent(transform, false);
            _spatialPoolRoot = root.transform;

            for (int i = 0; i < InitialSpatialPoolSize; i++)
            {
                AudioSource source = CreatePooledSpatialSource();
                _spatialSfxPool.Enqueue(source);
            }
        }

        private AudioSource AcquireSpatialSource()
        {
            while (_spatialSfxPool.Count > 0)
            {
                AudioSource candidate = _spatialSfxPool.Dequeue();
                if (candidate != null)
                    return candidate;
            }

            if (_totalSpatialSources == SpatialPoolGrowthWarnThreshold)
                Debug.LogWarning($"[InGameSfxDirector] SpatialSfxPool grew to {SpatialPoolGrowthWarnThreshold} sources. Consider pre-allocating more or reviewing SFX cooldowns.");

            return CreatePooledSpatialSource();
        }

        private AudioSource CreatePooledSpatialSource()
        {
            var child = new GameObject($"SpatialSfx_{_totalSpatialSources}");
            child.transform.SetParent(_spatialPoolRoot, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            child.SetActive(false);
            _totalSpatialSources++;
            return source;
        }
    }
}
