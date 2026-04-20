using System;
using System.Collections;
using System.Collections.Generic;
using Core.VFX;
using UnityEngine;

namespace InGame.VFX
{
    public class InGameVfxManager : MonoBehaviour
    {
        private const string VfxPoolChildName = "VfxPool";
        private const int PoolGrowthWarnThreshold = 32;

        public static InGameVfxManager Instance { get; private set; }

        [Serializable]
        private struct ProfileEntry
        {
            public EVfxId Id;
            public SpatialVfxProfile Profile;
        }

        [Header("VFX Catalog")]
        [Tooltip("EVfxId ↔ SpatialVfxProfile 매핑. 디자이너는 이곳에서 모든 게임 VFX를 중앙 관리합니다.")]
        [SerializeField] private List<ProfileEntry> _catalog = new();

        private readonly Dictionary<EVfxId, SpatialVfxProfile> _profileMap = new();
        private readonly Dictionary<ParticleSystem, Queue<ParticleSystem>> _pools = new();
        private readonly Dictionary<ParticleSystem, int> _totalSpawned = new();
        private readonly Dictionary<(EVfxId id, int callerId), float> _cooldowns = new();

        private Transform _poolRoot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[InGameVfxManager] Duplicate instance on '{name}'; destroying.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildProfileMap();
            InitializePoolRoot();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Emit(EVfxId id, Vector3 position, Quaternion rotation, UnityEngine.Object caller = null)
        {
            if (!TryBeginEmit(id, caller, out SpatialVfxProfile profile, out ParticleSystem instance))
                return;

            instance.transform.SetPositionAndRotation(position, rotation);
            StartCoroutine(CoPlayStatic(profile, instance));
        }

        public void EmitOn(EVfxId id, Transform follow, UnityEngine.Object caller = null)
        {
            if (follow == null)
                return;

            if (!TryBeginEmit(id, caller, out SpatialVfxProfile profile, out ParticleSystem instance))
                return;

            instance.transform.SetPositionAndRotation(follow.position, follow.rotation);

            if (profile.FollowTarget)
                StartCoroutine(CoPlayFollowing(profile, instance, follow));
            else
                StartCoroutine(CoPlayStatic(profile, instance));
        }

        private bool TryBeginEmit(EVfxId id, UnityEngine.Object caller, out SpatialVfxProfile profile, out ParticleSystem instance)
        {
            profile = null;
            instance = null;

            if (!_profileMap.TryGetValue(id, out profile) || profile == null)
            {
                Debug.LogWarning($"[InGameVfxManager] No profile registered for '{id}'.");
                return false;
            }

            if (profile.Prefab == null)
                return false;

            int callerId = caller != null ? caller.GetInstanceID() : 0;
            var key = (id, callerId);
            if (_cooldowns.TryGetValue(key, out float lastPlayTime) && Time.time - lastPlayTime < profile.Cooldown)
                return false;

            _cooldowns[key] = Time.time;

            instance = AcquireInstance(profile.Prefab);
            float scale = profile.GetRandomScale();
            instance.transform.localScale = profile.Prefab.transform.localScale * scale;
            return true;
        }

        private IEnumerator CoPlayStatic(SpatialVfxProfile profile, ParticleSystem instance)
        {
            instance.gameObject.SetActive(true);
            instance.Play(true);

            yield return new WaitForSeconds(profile.GetEffectiveLifetime());

            ReleaseInstance(profile.Prefab, instance);
        }

        private IEnumerator CoPlayFollowing(SpatialVfxProfile profile, ParticleSystem instance, Transform follow)
        {
            instance.gameObject.SetActive(true);
            instance.Play(true);

            float endTime = Time.time + profile.GetEffectiveLifetime();
            while (Time.time < endTime)
            {
                if (follow == null)
                    break;
                instance.transform.position = follow.position;
                yield return null;
            }

            ReleaseInstance(profile.Prefab, instance);
        }

        private void BuildProfileMap()
        {
            _profileMap.Clear();
            foreach (var entry in _catalog)
            {
                if (entry.Profile == null)
                    continue;

                if (_profileMap.ContainsKey(entry.Id))
                {
                    Debug.LogWarning($"[InGameVfxManager] Duplicate catalog entry for '{entry.Id}'; later entries ignored.");
                    continue;
                }

                _profileMap[entry.Id] = entry.Profile;
            }
        }

        private void InitializePoolRoot()
        {
            var root = new GameObject(VfxPoolChildName);
            root.transform.SetParent(transform, false);
            _poolRoot = root.transform;
        }

        private ParticleSystem AcquireInstance(ParticleSystem prefab)
        {
            if (!_pools.TryGetValue(prefab, out Queue<ParticleSystem> queue))
            {
                queue = new Queue<ParticleSystem>();
                _pools[prefab] = queue;
                _totalSpawned[prefab] = 0;
            }

            while (queue.Count > 0)
            {
                ParticleSystem candidate = queue.Dequeue();
                if (candidate != null)
                    return candidate;
            }

            int spawned = _totalSpawned[prefab];
            if (spawned == PoolGrowthWarnThreshold)
                Debug.LogWarning($"[InGameVfxManager] Pool for '{prefab.name}' grew to {PoolGrowthWarnThreshold} instances. Review cooldown or pre-allocation.");

            var instance = Instantiate(prefab, _poolRoot);
            instance.gameObject.SetActive(false);
            _totalSpawned[prefab] = spawned + 1;
            return instance;
        }

        private void ReleaseInstance(ParticleSystem prefab, ParticleSystem instance)
        {
            if (instance == null)
                return;

            instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            instance.transform.SetParent(_poolRoot, worldPositionStays: false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.gameObject.SetActive(false);

            if (_pools.TryGetValue(prefab, out Queue<ParticleSystem> queue))
                queue.Enqueue(instance);
        }
    }
}
