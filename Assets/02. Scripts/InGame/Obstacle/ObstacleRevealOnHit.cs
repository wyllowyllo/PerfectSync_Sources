using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace InGame.Obstacle
{
    public enum HiddenFrom { P1, P2 }

    /// <summary>
    /// 장애물의 Visual GO에 부착.
    /// 로컬 플레이어에게 숨겨진 장애물을 셰이더 투명으로 처리하고,
    /// 충돌 시 잠깐 반투명으로 보여준 뒤 다시 사라진다.
    /// </summary>
    public class ObstacleRevealOnHit : MonoBehaviour
    {
        [SerializeField] private HiddenFrom _hiddenFrom;
        [SerializeField] private float _fadeInDuration = 0.1f;
        [SerializeField] private float _holdDuration = 0.3f;
        [SerializeField] private float _fadeOutDuration = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _peakAlpha = 0.7f;

        private const int CharacterBodyLayer = 7;

        private static readonly int ID_GeneralAlpha = Shader.PropertyToID("_GeneralAlpha");

        // ── ViewID → 인스턴스 매핑 (Photon 이벤트 수신 시 O(1) 탐색) ──
        private static readonly Dictionary<int, ObstacleRevealOnHit> s_registry = new();
        private static bool s_eventRegistered;

        private bool _initialized;
        private bool _isHiddenFromLocal;
        private bool _isRevealing;
        private int _viewId = -1;
        private Coroutine _revealCoroutine;

        private Renderer[] _renderers;
        private MaterialPropertyBlock[] _propBlocks;
        private ObstacleHit _obstacleHit;
        private DestroyableObstacle _destroyable;

        private void Start()
        {
            CacheRenderers();
            SubscribeObstacleEvents();
            RegisterPhotonEventListener();

            if (PhotonTeamManager.GetLocalTeamSlot() != PhotonTeamManager.SlotNone)
            {
                Initialize();
                return;
            }

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

            bool isHost = PhotonTeamManager.IsLocalSlotHost();
            _isHiddenFromLocal = (isHost && _hiddenFrom == HiddenFrom.P1)
                              || (!isHost && _hiddenFrom == HiddenFrom.P2);

            var photonView = GetComponentInParent<PhotonView>();
            _viewId = photonView != null ? photonView.ViewID : -1;

            if (_viewId >= 0)
                s_registry[_viewId] = this;

            if (_isHiddenFromLocal)
                MakeTransparent();
        }

        // ── 캐싱 ──────────────────────────────────────────

        private void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _propBlocks = new MaterialPropertyBlock[_renderers.Length];
            for (int i = 0; i < _propBlocks.Length; i++)
                _propBlocks[i] = new MaterialPropertyBlock();
        }

        // ── 투명 설정 (1회, 영구) ──────────────────────────────────────────

        private void MakeTransparent()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;

                var mats = _renderers[i].materials;
                foreach (var mat in mats)
                    SetAlphaBlend(mat, true);
            }

            SetAlpha(0f);
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
                _destroyable.OnRespawned += HandleRespawned;
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
                _destroyable.OnRespawned -= HandleRespawned;
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

        private void HandleCollisionDetected(Collision collision)
        {
            if (!_initialized) return;
            if (collision.gameObject.layer != CharacterBodyLayer) return;

            TryReveal();
            SendRevealEvent();
        }

        private void TryReveal()
        {
            if (!_isHiddenFromLocal) return;
            if (_isRevealing) return;

            _revealCoroutine = StartCoroutine(RevealCoroutine());
        }

        private void HandleLifecycleInterrupt()
        {
            if (!_isRevealing) return;

            StopCoroutine(_revealCoroutine);
            _revealCoroutine = null;
            _isRevealing = false;
            SetAlpha(0f);
        }

        private void HandleRespawned()
        {
            if (!_isHiddenFromLocal) return;

            // 리스폰 시 DestroyableObstacle이 머티리얼을 리셋할 수 있으므로 재설정.
            MakeTransparent();
        }

        // ── 페이드 연출 ──────────────────────────────────────────

        private IEnumerator RevealCoroutine()
        {
            _isRevealing = true;

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
            _isRevealing = false;
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
            if (!_isRevealing) return;

            StopCoroutine(_revealCoroutine);
            _revealCoroutine = null;
            _isRevealing = false;
            SetAlpha(0f);
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            UnregisterPhoton();
        }
    }
}
