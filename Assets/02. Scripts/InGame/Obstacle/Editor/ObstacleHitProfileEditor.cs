using UnityEditor;
using UnityEngine;

namespace InGame.Obstacle.Editor
{
    [CustomEditor(typeof(ObstacleHitProfile))]
    public class ObstacleHitProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty _response;
        private SerializedProperty _extraKnockback;
        private SerializedProperty _speedCurve;
        private SerializedProperty _maxSpeed;
        private SerializedProperty _direction;
        private SerializedProperty _customDirection;
        private SerializedProperty _upwardBias;
        private SerializedProperty _torqueScale;
        private SerializedProperty _cooldown;

        private void OnEnable()
        {
            _response = serializedObject.FindProperty("_response");
            _extraKnockback = serializedObject.FindProperty("_extraKnockback");
            _speedCurve = serializedObject.FindProperty("_speedCurve");
            _maxSpeed = serializedObject.FindProperty("_maxSpeed");
            _direction = serializedObject.FindProperty("_direction");
            _customDirection = serializedObject.FindProperty("_customDirection");
            _upwardBias = serializedObject.FindProperty("_upwardBias");
            _torqueScale = serializedObject.FindProperty("_torqueScale");
            _cooldown = serializedObject.FindProperty("_cooldown");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Response", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_response);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Extra Knockback", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_extraKnockback);
            EditorGUILayout.PropertyField(_speedCurve);
            EditorGUILayout.PropertyField(_maxSpeed);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Direction", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_direction);

            if ((EHitDirection)_direction.enumValueIndex == EHitDirection.Custom)
                EditorGUILayout.PropertyField(_customDirection);

            EditorGUILayout.PropertyField(_upwardBias);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Torque", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_torqueScale);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cooldown", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_cooldown);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
