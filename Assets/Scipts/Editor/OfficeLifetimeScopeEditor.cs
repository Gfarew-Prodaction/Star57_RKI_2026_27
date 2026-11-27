using UnityEditor;
using UnityEngine;
using Zvezda57.Core;

namespace Zvezda57.Editor
{
    [CustomEditor(typeof(OfficeLifetimeScope))]
    internal sealed class OfficeLifetimeScopeEditor : UnityEditor.Editor
    {
        private ConfigurationValidationResult _result;

        public override void OnInspectorGUI()
        {
            if (DrawDefaultInspector())
            {
                _result = null;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Validate Configuration"))
            {
                _result = ((OfficeLifetimeScope)target).ValidateConfiguration();
            }

            if (_result == null)
            {
                return;
            }

            if (_result.IsValid)
            {
                EditorGUILayout.HelpBox($"Configuration valid. Incidents in catalog: {_result.Incidents.Count}.", MessageType.Info);
                return;
            }

            foreach (string error in _result.Errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }
    }
}
