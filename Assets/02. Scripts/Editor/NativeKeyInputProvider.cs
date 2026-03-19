using System.Collections.Generic;
using System.Runtime.InteropServices;
using Core.Utilities;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [InitializeOnLoad]
    public static class NativeKeyInputProvider
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        // 추적 대상 키 목록 및 엣지 감지용 상태
        private static readonly HashSet<int> _trackedKeys = new();
        private static readonly Dictionary<int, bool> _current = new();
        private static readonly Dictionary<int, bool> _previous = new();
        private static int _lastUpdateFrame = -1;

        static NativeKeyInputProvider()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _trackedKeys.Clear();
                _current.Clear();
                _previous.Clear();
                _lastUpdateFrame = -1;

                NativeKeyInput.UpdateAction = UpdateImpl;
                NativeKeyInput.GetAxisFunc = GetAxisImpl;
                NativeKeyInput.IsKeyDownFunc = IsKeyDownImpl;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                NativeKeyInput.UpdateAction = null;
                NativeKeyInput.GetAxisFunc = null;
                NativeKeyInput.IsKeyDownFunc = null;
            }
        }

        private static void EnsureTracked(int vKey)
        {
            if (_trackedKeys.Add(vKey))
            {
                _current[vKey] = false;
                _previous[vKey] = false;
            }
        }

        private static bool IsHeld(int vKey)
        {
            return (GetAsyncKeyState(vKey) & 0x8000) != 0;
        }

        private static void UpdateImpl()
        {
            int frame = Time.frameCount;
            if (frame == _lastUpdateFrame) return;
            _lastUpdateFrame = frame;

            foreach (int key in _trackedKeys)
            {
                _previous[key] = _current[key];
                _current[key] = IsHeld(key);
            }
        }

        private static float GetAxisImpl(int positiveKey, int negativeKey)
        {
            EnsureTracked(positiveKey);
            EnsureTracked(negativeKey);

            float value = 0f;
            if (IsHeld(positiveKey)) value += 1f;
            if (IsHeld(negativeKey)) value -= 1f;
            return value;
        }

        private static bool IsKeyDownImpl(int vKey)
        {
            EnsureTracked(vKey);
            return _current.TryGetValue(vKey, out bool cur) && cur
                && _previous.TryGetValue(vKey, out bool prev) && !prev;
        }
    }
}
