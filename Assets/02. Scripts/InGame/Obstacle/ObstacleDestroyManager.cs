using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace InGame.Obstacle
{
    public class ObstacleDestroyManager : SingletonPunCallbacks<ObstacleDestroyManager>, IOnEventCallback
    {
        protected override bool PersistAcrossScenes => false;

        [Tooltip("파괴 후 장애물이 사라지기까지 시간 (초)")]
        [SerializeField] private float _hideDelay = 2f;

        [Tooltip("파괴 후 장애물이 리스폰되기까지 총 시간 (초)")]
        [SerializeField] private float _respawnDelay = 5f;

        [Tooltip("리스폰 전 깜빡임 예고 시간 (초)")]
        [SerializeField] private float _warningDuration = 0.5f;

        private readonly List<IDestroyable> _registry = new();
        private readonly Dictionary<IDestroyable, int> _idLookup = new();

        // 현재 destroyed 상태인 장애물 ID 집합.
        private readonly HashSet<int> _destroyedIds = new();

        // MasterClient 타이머 (hide + respawn).
        private readonly Dictionary<int, Coroutine> _respawnTimers = new();

        private void Start()
        {
            CollectDestroyables();
        }

        // ── 등록 ───────────────────────────────────────────────

        private void CollectDestroyables()
        {
            var all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            var destroyables = new List<(MonoBehaviour mb, IDestroyable d)>();

            foreach (var mb in all)
            {
                if (mb is IDestroyable d)
                    destroyables.Add((mb, d));
            }

            destroyables.Sort((a, b) =>
            {
                var pa = a.mb.transform.position;
                var pb = b.mb.transform.position;
                int cmp = pa.x.CompareTo(pb.x);
                if (cmp != 0) return cmp;
                cmp = pa.y.CompareTo(pb.y);
                if (cmp != 0) return cmp;
                cmp = pa.z.CompareTo(pb.z);
                if (cmp != 0) return cmp;
                return string.Compare(a.mb.gameObject.name, b.mb.gameObject.name,
                    System.StringComparison.Ordinal);
            });

            foreach (var (_, d) in destroyables)
            {
                int id = _registry.Count;
                _registry.Add(d);
                _idLookup[d] = id;
            }
        }

        public int GetId(IDestroyable destroyable)
        {
            return _idLookup.TryGetValue(destroyable, out int id) ? id : -1;
        }

        // ── 요청 (authority 클라이언트에서 호출) ─────────────────

        /// <summary>
        /// Non-authority 클라이언트의 로컬 예측 파괴.
        /// 네트워크 이벤트 없이 즉시 파괴만 적용한다.
        /// 이후 실제 ObstacleDestroy 이벤트 도착 시 IsDestroyed로 중복 방지.
        /// </summary>
        public void PredictDestroy(int id, Vector3 force)
        {
            if (id < 0 || id >= _registry.Count) return;
            if (_registry[id].IsDestroyed) return;

            Vector3 randomTorque = Random.insideUnitSphere;
            _registry[id].Destroy(force, randomTorque);
            _destroyedIds.Add(id);
        }

        public void RequestDestroy(int id, Vector3 force)
        {
            Vector3 randomTorque = Random.insideUnitSphere;
            ApplyDestroy(id, force, randomTorque);

            var content = new object[] { id, force, randomTorque };
            var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(PhotonEventCodes.ObstacleDestroy, content, opts,
                SendOptions.SendReliable);

            if (PhotonNetwork.IsMasterClient)
                StartRespawnTimer(id);
        }

        private void RequestRespawn(int id)
        {
            ApplyRespawn(id);

            var content = new object[] { id };
            var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(PhotonEventCodes.ObstacleRespawn, content, opts,
                SendOptions.SendReliable);
        }

        // ── Photon 이벤트 수신 ──────────────────────────────────

        public void OnEvent(EventData photonEvent)
        {
            switch (photonEvent.Code)
            {
                case PhotonEventCodes.ObstacleDestroy:
                {
                    var data = (object[])photonEvent.CustomData;
                    int id = (int)data[0];
                    var force = (Vector3)data[1];
                    var randomTorque = (Vector3)data[2];
                    ApplyDestroy(id, force, randomTorque);

                    if (PhotonNetwork.IsMasterClient && !_respawnTimers.ContainsKey(id))
                        StartRespawnTimer(id);
                    break;
                }
                case PhotonEventCodes.ObstacleHide:
                {
                    var data = (object[])photonEvent.CustomData;
                    int id = (int)data[0];
                    ApplyHide(id);
                    break;
                }
                case PhotonEventCodes.ObstacleRespawnWarning:
                {
                    var data = (object[])photonEvent.CustomData;
                    int id = (int)data[0];
                    ApplyRespawnWarning(id);
                    break;
                }
                case PhotonEventCodes.ObstacleRespawn:
                {
                    var data = (object[])photonEvent.CustomData;
                    int id = (int)data[0];
                    ApplyRespawn(id);
                    break;
                }
            }
        }

        public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 새 Master: destroyed 상태인 장애물의 리스폰 타이머 재생성.
            foreach (int id in _destroyedIds)
            {
                if (!_respawnTimers.ContainsKey(id))
                    StartRespawnTimer(id);
            }
        }

        // ── 리스폰 타이머 ───────────────────────────────────────

        private void StartRespawnTimer(int id)
        {
            if (_respawnTimers.ContainsKey(id)) return;
            _respawnTimers[id] = StartCoroutine(DestroySequenceCoroutine(id));
        }

        private IEnumerator DestroySequenceCoroutine(int id)
        {
            // Phase 1: 물리 시뮬 후 숨김.
            yield return new WaitForSeconds(_hideDelay);
            RequestHide(id);

            // Phase 2: 숨김 후 리스폰 예고 대기.
            float warningStart = _respawnDelay - _hideDelay - _warningDuration;
            if (warningStart > 0f)
                yield return new WaitForSeconds(warningStart);

            // Phase 3: 깜빡임 예고.
            RequestRespawnWarning(id);
            yield return new WaitForSeconds(_warningDuration);

            _respawnTimers.Remove(id);
            RequestRespawn(id);
        }

        // ── 내부 ────────────────────────────────────────────────

        private void ApplyDestroy(int id, Vector3 force, Vector3 randomTorque)
        {
            if (id < 0 || id >= _registry.Count) return;

            _registry[id].Destroy(force, randomTorque);
            _destroyedIds.Add(id);
        }

        private void RequestHide(int id)
        {
            ApplyHide(id);

            var content = new object[] { id };
            var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(PhotonEventCodes.ObstacleHide, content, opts,
                SendOptions.SendReliable);
        }

        private void ApplyHide(int id)
        {
            if (id < 0 || id >= _registry.Count) return;

            _registry[id].Hide();
        }

        private void RequestRespawnWarning(int id)
        {
            ApplyRespawnWarning(id);

            var content = new object[] { id };
            var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(PhotonEventCodes.ObstacleRespawnWarning, content, opts,
                SendOptions.SendReliable);
        }

        private void ApplyRespawnWarning(int id)
        {
            if (id < 0 || id >= _registry.Count) return;

            _registry[id].PrepareRespawn();
        }

        private void ApplyRespawn(int id)
        {
            if (id < 0 || id >= _registry.Count) return;

            _registry[id].Respawn();
            _destroyedIds.Remove(id);

            if (_respawnTimers.TryGetValue(id, out var coroutine))
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
                _respawnTimers.Remove(id);
            }
        }
    }
}
