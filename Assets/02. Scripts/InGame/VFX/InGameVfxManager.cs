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

        private readonly Dictionary<GameObject, Queue<GameObject>> _pools = new();
        private readonly Dictionary<GameObject, int> _totalSpawned = new();
        private readonly Dictionary<(SpatialVfxProfile profile, int callerId), float> _cooldowns = new();

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
            InitializePoolRoot();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void EmitAt(SpatialVfxProfile profile, Vector3 position, Quaternion rotation, Object caller = null)
        {
            if (!TryBeginEmit(profile, caller, out GameObject instance))
                return;

            instance.transform.SetPositionAndRotation(position + profile.Offset, rotation);
            StartCoroutine(CoPlayStatic(profile, instance));
        }

        public void EmitOn(SpatialVfxProfile profile, Transform follow, Object caller = null)
        {
            if (follow == null)
                return;

            if (!TryBeginEmit(profile, caller, out GameObject instance))
                return;

            instance.transform.SetPositionAndRotation(follow.position + profile.Offset, follow.rotation);

            if (profile.FollowTarget)
                StartCoroutine(CoPlayFollowing(profile, instance, follow));
            else
                StartCoroutine(CoPlayStatic(profile, instance));
        }

        private bool TryBeginEmit(SpatialVfxProfile profile, Object caller, out GameObject instance)
        {
            instance = null;

            if (profile == null || profile.Prefab == null)
                return false;

            int callerId = caller != null ? caller.GetInstanceID() : 0;
            var key = (profile, callerId);
            if (_cooldowns.TryGetValue(key, out float lastPlayTime) && Time.time - lastPlayTime < profile.Cooldown)
                return false;

            _cooldowns[key] = Time.time;

            instance = AcquireInstance(profile.Prefab);
            float scale = profile.GetRandomScale();
            instance.transform.localScale = profile.Prefab.transform.localScale * scale;
            return true;
        }

        private IEnumerator CoPlayStatic(SpatialVfxProfile profile, GameObject instance)
        {
            instance.SetActive(true);
            PlayAll(instance);

            yield return new WaitForSeconds(profile.GetEffectiveLifetime());

            ReleaseInstance(profile.Prefab, instance);
        }

        private IEnumerator CoPlayFollowing(SpatialVfxProfile profile, GameObject instance, Transform follow)
        {
            instance.SetActive(true);
            PlayAll(instance);

            float endTime = Time.time + profile.GetEffectiveLifetime();
            while (Time.time < endTime)
            {
                if (follow == null)
                    break;
                instance.transform.position = follow.position + profile.Offset;
                yield return null;
            }

            ReleaseInstance(profile.Prefab, instance);
        }

        private static void PlayAll(GameObject instance)
        {
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
                systems[i].Play(false);
        }

        private static void StopAll(GameObject instance)
        {
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void InitializePoolRoot()
        {
            var root = new GameObject(VfxPoolChildName);
            root.transform.SetParent(transform, false);
            _poolRoot = root.transform;
        }

        private GameObject AcquireInstance(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out Queue<GameObject> queue))
            {
                queue = new Queue<GameObject>();
                _pools[prefab] = queue;
                _totalSpawned[prefab] = 0;
            }

            while (queue.Count > 0)
            {
                GameObject candidate = queue.Dequeue();
                if (candidate != null)
                    return candidate;
            }

            int spawned = _totalSpawned[prefab];
            if (spawned == PoolGrowthWarnThreshold)
                Debug.LogWarning($"[InGameVfxManager] Pool for '{prefab.name}' grew to {PoolGrowthWarnThreshold} instances. Review cooldown or pre-allocation.");

            var instance = Instantiate(prefab, _poolRoot);
            instance.SetActive(false);
            _totalSpawned[prefab] = spawned + 1;
            return instance;
        }

        private void ReleaseInstance(GameObject prefab, GameObject instance)
        {
            if (instance == null)
                return;

            StopAll(instance);
            instance.transform.SetParent(_poolRoot, worldPositionStays: false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.SetActive(false);

            if (_pools.TryGetValue(prefab, out Queue<GameObject> queue))
                queue.Enqueue(instance);
        }
    }
}
