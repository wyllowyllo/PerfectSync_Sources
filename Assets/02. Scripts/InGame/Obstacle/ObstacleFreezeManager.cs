using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace InGame.Obstacle
{
    public class ObstacleFreezeManager : SingletonPunCallbacks<ObstacleFreezeManager>, IOnEventCallback
    {
        protected override bool PersistAcrossScenes => false;

        private readonly List<IFreezable> _registry = new();
        private readonly Dictionary<IFreezable, int> _idLookup = new();

        // obstacleId → 현재 freeze 중인 actorNumber 집합.
        private readonly Dictionary<int, HashSet<int>> _freezeSources = new();

        private void Start()
        {
            CollectFreezables();
        }

        // ── 등록 ───────────────────────────────────────────────

        /// <summary>
        /// 씬의 모든 IFreezable을 위치 기준 정렬하여 등록.
        /// 정렬 기준이 결정적이므로 모든 클라이언트에서 동일한 ID를 부여한다.
        /// </summary>
        private void CollectFreezables()
        {
            var all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            var freezables = new List<(MonoBehaviour mb, IFreezable f)>();

            foreach (var mb in all)
            {
                if (mb is IFreezable f)
                    freezables.Add((mb, f));
            }

            freezables.Sort((a, b) =>
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

            foreach (var (_, f) in freezables)
            {
                int id = _registry.Count;
                _registry.Add(f);
                _idLookup[f] = id;
            }
        }

        public int GetId(IFreezable freezable)
        {
            return _idLookup.TryGetValue(freezable, out int id) ? id : -1;
        }

        // ── 요청 (authority 클라이언트에서 호출) ─────────────────

        public void RequestFreeze(int[] ids)
        {
            int actorNr = PhotonNetwork.LocalPlayer.ActorNumber;
            ApplyFreeze(actorNr, ids);

            var content = new object[] { actorNr, ids };
            var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(PhotonEventCodes.ObstacleFreeze, content, opts,
                SendOptions.SendReliable);
        }

        public void RequestUnfreeze(int[] ids)
        {
            int actorNr = PhotonNetwork.LocalPlayer.ActorNumber;
            ApplyUnfreeze(actorNr, ids);

            var content = new object[] { actorNr, ids };
            var opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(PhotonEventCodes.ObstacleUnfreeze, content, opts,
                SendOptions.SendReliable);
        }

        /// <summary>
        /// 특정 플레이어의 모든 freeze를 로컬에서 해제한다.
        /// 브로드캐스트하지 않는다 (OnPlayerLeftRoom 등 모든 클라이언트에서 독립 호출).
        /// </summary>
        public void UnfreezeAllByActor(int actorNr)
        {
            var toUnfreeze = new List<int>();

            foreach (var kvp in _freezeSources)
            {
                if (kvp.Value.Remove(actorNr) && kvp.Value.Count == 0)
                    toUnfreeze.Add(kvp.Key);
            }

            foreach (int id in toUnfreeze)
            {
                if (id >= 0 && id < _registry.Count)
                    _registry[id].Unfreeze();
            }
        }

        // ── Photon 이벤트 수신 ──────────────────────────────────

        public void OnEvent(EventData photonEvent)
        {
            switch (photonEvent.Code)
            {
                case PhotonEventCodes.ObstacleFreeze:
                {
                    var data = (object[])photonEvent.CustomData;
                    int actorNr = (int)data[0];
                    int[] ids = (int[])data[1];
                    ApplyFreeze(actorNr, ids);
                    break;
                }
                case PhotonEventCodes.ObstacleUnfreeze:
                {
                    var data = (object[])photonEvent.CustomData;
                    int actorNr = (int)data[0];
                    int[] ids = (int[])data[1];
                    ApplyUnfreeze(actorNr, ids);
                    break;
                }
            }
        }

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            UnfreezeAllByActor(otherPlayer.ActorNumber);
        }

        // ── 내부 ────────────────────────────────────────────────

        private void ApplyFreeze(int actorNr, int[] ids)
        {
            foreach (int id in ids)
            {
                if (id < 0 || id >= _registry.Count) continue;

                if (!_freezeSources.TryGetValue(id, out var sources))
                {
                    sources = new HashSet<int>();
                    _freezeSources[id] = sources;
                }

                bool wasEmpty = sources.Count == 0;
                sources.Add(actorNr);

                if (wasEmpty)
                    _registry[id].Freeze();
            }
        }

        private void ApplyUnfreeze(int actorNr, int[] ids)
        {
            foreach (int id in ids)
            {
                if (id < 0 || id >= _registry.Count) continue;
                if (!_freezeSources.TryGetValue(id, out var sources)) continue;

                sources.Remove(actorNr);

                if (sources.Count == 0)
                    _registry[id].Unfreeze();
            }
        }
    }
}
