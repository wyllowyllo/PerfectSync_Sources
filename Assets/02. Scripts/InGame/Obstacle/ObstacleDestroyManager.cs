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

        [Tooltip("파괴된 장애물이 리스폰되기까지 대기 시간 (초)")]
        [SerializeField] private float _respawnDelay = 5f;

        private readonly List<IDestroyable> _registry = new();
        private readonly Dictionary<IDestroyable, int> _idLookup = new();

        // 현재 destroyed 상태인 장애물 ID 집합 (위치 스트림용).
        private readonly HashSet<int> _destroyedIds = new();

        // MasterClient 리스폰 타이머.
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

        public void RequestDestroy(int id, Vector3 force)
        {
            bool isMaster = PhotonNetwork.IsMasterClient;
            ApplyDestroy(id, force, isMaster);

            var content = new object[] { id, force };
            var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(PhotonEventCodes.ObstacleDestroy, content, opts,
                SendOptions.SendReliable);

            if (isMaster)
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
                    bool isMaster = PhotonNetwork.IsMasterClient;
                    ApplyDestroy(id, force, isMaster);

                    if (isMaster && !_respawnTimers.ContainsKey(id))
                        StartRespawnTimer(id);
                    break;
                }
                case PhotonEventCodes.ObstacleRespawn:
                {
                    var data = (object[])photonEvent.CustomData;
                    int id = (int)data[0];
                    ApplyRespawn(id);
                    break;
                }
                case PhotonEventCodes.ObstaclePositionSync:
                {
                    ApplyPositionSync(photonEvent.CustomData);
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

        // ── 위치 스트림 (Master → Others) ───────────────────────

        private void FixedUpdate()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (_destroyedIds.Count == 0) return;

            // 데이터 직렬화: [count, id0, posX, posY, posZ, rotX, rotY, rotZ, rotW, id1, ...]
            int count = _destroyedIds.Count;
            var data = new object[1 + count * 8];
            data[0] = count;

            int idx = 1;
            foreach (int id in _destroyedIds)
            {
                if (id < 0 || id >= _registry.Count) continue;

                var destroyable = _registry[id] as MonoBehaviour;
                if (destroyable == null) continue;

                var t = destroyable.transform;
                var pos = t.position;
                var rot = t.rotation;

                data[idx++] = id;
                data[idx++] = pos.x;
                data[idx++] = pos.y;
                data[idx++] = pos.z;
                data[idx++] = rot.x;
                data[idx++] = rot.y;
                data[idx++] = rot.z;
                data[idx++] = rot.w;
            }

            var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(PhotonEventCodes.ObstaclePositionSync, data, opts,
                SendOptions.SendUnreliable);
        }

        private void ApplyPositionSync(object customData)
        {
            if (PhotonNetwork.IsMasterClient) return;

            var data = (object[])customData;
            int count = (int)data[0];

            int idx = 1;
            for (int i = 0; i < count; i++)
            {
                int id = (int)data[idx++];
                var pos = new Vector3((float)data[idx++], (float)data[idx++], (float)data[idx++]);
                var rot = new Quaternion((float)data[idx++], (float)data[idx++], (float)data[idx++], (float)data[idx++]);

                if (id >= 0 && id < _registry.Count)
                    _registry[id].ApplyNetworkState(pos, rot);
            }
        }

        // ── 리스폰 타이머 ───────────────────────────────────────

        private void StartRespawnTimer(int id)
        {
            if (_respawnTimers.ContainsKey(id)) return;
            _respawnTimers[id] = StartCoroutine(RespawnCoroutine(id));
        }

        private IEnumerator RespawnCoroutine(int id)
        {
            yield return new WaitForSeconds(_respawnDelay);
            _respawnTimers.Remove(id);
            RequestRespawn(id);
        }

        // ── 내부 ────────────────────────────────────────────────

        private void ApplyDestroy(int id, Vector3 force, bool isMaster)
        {
            if (id < 0 || id >= _registry.Count) return;

            _registry[id].Destroy(force, isMaster);
            _destroyedIds.Add(id);
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
