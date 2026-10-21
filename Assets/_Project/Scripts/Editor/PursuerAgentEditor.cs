using Lockdown.AI.Agents;
using UnityEditor;
using UnityEngine;
namespace Lockdown.Editor
{
    [CustomEditor(typeof(PursuerAgent))]
    public sealed class PursuerAgentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!Application.isPlaying) return;
            var agent = (PursuerAgent)target;
            EditorGUILayout.HelpBox("State: " + agent.State + "\nDecision: " + agent.DecisionReason
                + "\nPlayer visible: " + agent.IsPlayerVisible + "\nLast confirmed position: " + agent.LastKnownPlayerPosition, MessageType.Info);
            Repaint();
        }
    }
}
