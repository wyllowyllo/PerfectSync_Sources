using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace InGame.Obstacle
{
    /// <summary>
    /// 장애물의 Visual GO에 부착.
    /// 로컬 플레이어에게 숨겨진 장애물이 충돌하면 반투명으로 잠깐 보여준 뒤 사라진다.
    /// Host에서 충돌 감지 → 로컬 리빌 + Photon 이벤트로 Guest에 통보.
    /// 팀 배정 시점에 초기화되므로 StandaloneTestManager 환경에서도 정상 동작.
    /// </summary>
    public class ObstacleRevealOnHit : MonoBehaviour
    {
        [SerializeField] private float _fadeInDuration = 0.1f;
        [SerializeField] private float _holdDuration = 0.3f;
        [SerializeField] private float _fadeOutDuration = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _peakAlpha = 0.7f;

        private const int VisibleLayer = 9;
        private const int CharacterBodyLayer = 7;

        private static readonly int ID_GeneralAlpha = Shader.PropertyToID("_GeneralAlpha");

        // ── ViewID → 인스턴스 매핑 (Photon 이벤트 수신 시 O(1) 탐색) ──
        private static readonly Dictionary<int, ObstacleRevealOnHit> s_registry = new();
        private static bool s_eventRegistered;

        private bool _initialized;
        private bool _isHiddenFromLocal;
        private bool _isRevealing;
        private int _originalLayer;
        private int _viewId = -1;
        private Coroutine _revealCoroutine;

        private Renderer[] _renderers;
        private MaterialPropertyBlock[] _propBlocks;
        private GameObject[] _layerTargets;
        private Material[][] _originalMaterials;
        private List<Material> _tempMaterials;
        private ObstacleHit _obstacleHit;
        private DestroyableObstacle _destroyable;

        private void Start()
        {
            _originalLayer = gameObject.layer;

            CacheTargets();
            SubscribeObstacleEvents();
            RegisterPhotonEventListener();

            // 팀이 이미 배정되어 있으면 즉시 초기화 (정상 게임 플로우).
            if (PhotonTeamManager.GetLocalTeamSlot() != PhotonTeamManager.SlotNone)
            {
                Initialize();
                return;
            }

            // 아직 팀 미배정 → 콜백 대기 (StandaloneTestManager 등).
            if (PhotonTeamManager.Instance != null)
                PhotonTeamManager.Instance.OnAllTeamsAssigned += HandleTeamsAssigned;
        }

        private void HandleTeamsAssigned()
        {
            PhotonTeamManager.Instance.OnAllTeamsAssigned -= HandleTeamsAssigned;
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _isHiddenFromLocal = IsLayerHiddenFromLocal(_originalLayer);

            var photonView = GetComponentInParent<PhotonView>();
            _viewId = photonView != null ? photonView.ViewID : -1;

            if (_viewId >= 0)
                s_registry[_viewId] = this;
        }

        private bool IsLayerHiddenFromLocal(int layer)
        {
            int slot = PhotonTeamManager.GetLocalTeamSlot();
            if (slot == PhotonTeamManager.SlotNone) return false;

            bool isHost = slot == PhotonTeamManager.SlotHost;
            // P1(Host)에게 숨겨진 레이어 = 12, P2(Guest)에게 숨겨진 레이어 = 13.
            return (isHost && layer == 12) || (!isHost && layer == 13);
        }

        // ── 캐싱 ──────────────────────────────────────────

        private void CacheTargets()
        {
            var targets = new List<GameObject>();
            var renderers = new List<Renderer>();

            CollectTargets(transform, targets, renderers);

            _layerTargets = targets.ToArray();
            _renderers = renderers.ToArray();
            _propBlocks = new MaterialPropertyBlock[_renderers.Length];
            for (int i = 0; i < _propBlocks.Length; i++)
                _propBlocks[i] = new MaterialPropertyBlock();

            _originalMaterials = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
                _originalMaterials[i] = _renderers[i].sharedMaterials;

            _tempMaterials = new List<Material>();
        }

        private void CollectTargets(Transform root, List<GameObject> targets, List<Renderer> renderers)
        {
            if (root.gameObject.layer == _originalLayer)
            {
                targets.Add(root.gameObject);

                var rend = root.GetComponent<Renderer>();
                if (rend != null)
                    renderers.Add(rend);
            }

            for (int i = 0; i < root.childCount; i++)
                CollectTargets(root.GetChild(i), targets, renderers);
        }

        // ── 이벤트 구독 ──────────────────────────────────────────

        private void SubscribeObstacleEvents()
        {
            _obstacleHit = GetComponentInParent<ObstacleHit>();
            if (_obstacleHit != null)
                _obstacleHit.OnCollisionDetected += HandleCollisionDetected;

            _destroyable = GetComponentInParent<DestroyableObstacle>();
            if (_destroyable != null)
            {
                _destroyable.OnDestroyed += HandleLifecycleInterrupt;
                _destroyable.OnHidden += HandleLifecycleInterrupt;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_obstacleHit != null)
                _obstacleHit.OnCollisionDetected -= HandleCollisionDetected;

            if (_destroyable != null)
            {
                _destroyable.OnDestroyed -= HandleLifecycleInterrupt;
                _destroyable.OnHidden -= HandleLifecycleInterrupt;
            }

            if (PhotonTeamManager.Instance != null)
                PhotonTeamManager.Instance.OnAllTeamsAssigned -= HandleTeamsAssigned;
        }

        // ── Photon 이벤트 ──────────────────────────────────────────

        private void RegisterPhotonEventListener()
        {
            if (!s_eventRegistered)
            {
                PhotonNetwork.NetworkingClient.EventReceived += OnPhotonEvent;
                s_eventRegistered = true;
            }
        }

        private void UnregisterPhoton()
        {
            if (_viewId >= 0)
                s_registry.Remove(_viewId);

            if (s_registry.Count == 0 && s_eventRegistered)
            {
                PhotonNetwork.NetworkingClient.EventReceived -= OnPhotonEvent;
                s_eventRegistered = false;
            }
        }

        private static void OnPhotonEvent(EventData eventData)
        {
            if (eventData.Code != PhotonEventCodes.ObstacleReveal) return;

            int viewId = (int)eventData.CustomData;
            if (s_registry.TryGetValue(viewId, out var reveal))
                reveal.TryReveal();
        }

        private void SendRevealEvent()
        {
            if (_viewId < 0) return;
            if (!PhotonNetwork.InRoom) return;

            PhotonNetwork.RaiseEvent(
                PhotonEventCodes.ObstacleReveal,
                _viewId,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                SendOptions.SendUnreliable);
        }

        // ── 충돌 처리 ──────────────────────────────────────────

        /// <summary>
        /// Host에서 OnCollisionEnter가 발생했을 때 호출.
        /// Host는 로컬 리빌 + Guest에 이벤트 전송.
        /// </summary>
        private void HandleCollisionDetected(Collision collision)
        {
            if (!_initialized) return;
            if (collision.gameObject.layer != CharacterBodyLayer) return;

            // Host 로컬 리빌.
            TryReveal();

            // Guest에 통보.
            SendRevealEvent();
        }

        /// <summary>
        /// 이 장애물이 로컬 플레이어에게 숨겨져 있으면 리빌을 실행한다.
        /// Host에서 직접 호출되거나, Guest에서 Photon 이벤트 수신 시 호출.
        /// </summary>
        private void TryReveal()
        {
            if (!_isHiddenFromLocal) return;
            if (_isRevealing) return;

            SetupRevealMaterials();
            _revealCoroutine = StartCoroutine(RevealCoroutine());
        }

        private void HandleLifecycleInterrupt()
        {
            if (!_isRevealing) return;

            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
                _revealCoroutine = null;
            }
            RestoreMaterialsAndLayers();
        }

        // ── 머티리얼/레이어 관리 ──────────────────────────────────────────

        private void SetupRevealMaterials()
        {
            _isRevealing = true;

            foreach (var go in _layerTargets)
                go.layer = VisibleLayer;

            _tempMaterials.Clear();

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;

                // .materials 접근으로 인스턴스 생성.
                var mats = _renderers[i].materials;
                foreach (var mat in mats)
                {
                    SetAlphaBlend(mat, true);
                    _tempMaterials.Add(mat);
                }
            }
        }

        private void RestoreMaterialsAndLayers()
        {
            foreach (var go in _layerTargets)
            {
                if (go != null)
                    go.layer = _originalLayer;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;

                _renderers[i].sharedMaterials = _originalMaterials[i];
                _propBlocks[i].Clear();
                _renderers[i].SetPropertyBlock(_propBlocks[i]);
            }

            foreach (var mat in _tempMaterials)
            {
                if (mat != null)
                    Destroy(mat);
            }
            _tempMaterials.Clear();

            _isRevealing = false;
        }

        // ── 페이드 연출 ──────────────────────────────────────────

        private IEnumerator RevealCoroutine()
        {
            // Fade In.
            float t = 0f;
            while (t < _fadeInDuration)
            {
                SetAlpha(Mathf.Lerp(0f, _peakAlpha, t / _fadeInDuration));
                t += Time.deltaTime;
                yield return null;
            }
            SetAlpha(_peakAlpha);

            // Hold.
            yield return new WaitForSeconds(_holdDuration);

            // Fade Out.
            t = 0f;
            while (t < _fadeOutDuration)
            {
                SetAlpha(Mathf.Lerp(_peakAlpha, 0f, t / _fadeOutDuration));
                t += Time.deltaTime;
                yield return null;
            }
            SetAlpha(0f);

            _revealCoroutine = null;
            RestoreMaterialsAndLayers();
        }

        private void SetAlpha(float alpha)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;

                _renderers[i].GetPropertyBlock(_propBlocks[i]);
                _propBlocks[i].SetFloat(ID_GeneralAlpha, alpha);
                _renderers[i].SetPropertyBlock(_propBlocks[i]);
            }
        }

        private static void SetAlphaBlend(Material mat, bool enable)
        {
            if (enable)
            {
                mat.SetFloat("_BlendSrc", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_BlendDst", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                mat.SetFloat("_BlendSrc", (float)BlendMode.One);
                mat.SetFloat("_BlendDst", (float)BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                mat.renderQueue = (int)RenderQueue.Geometry;
            }
        }

        // ── 정리 ──────────────────────────────────────────

        private void OnDisable()
        {
            if (_isRevealing)
            {
                if (_revealCoroutine != null)
                {
                    StopCoroutine(_revealCoroutine);
                    _revealCoroutine = null;
                }
                RestoreMaterialsAndLayers();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            UnregisterPhoton();

            if (_tempMaterials == null) return;
            foreach (var mat in _tempMaterials)
            {
                if (mat != null)
                    Destroy(mat);
            }
        }
    }
}
