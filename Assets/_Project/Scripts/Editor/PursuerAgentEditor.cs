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
                + "\nPlayer visible: " + agent.IsPlayerVisible + "\nLast confirmed position: " + agent.LastKnownPlayerPosition
                + "\nReturning fire: " + agent.IsRetaliating + "\nArrest progress: " + agent.ArrestProgress.ToString("P0")
                + "\nIntercept prediction: " + agent.InterceptPoint, MessageType.Info);
            Repaint();
        }
    }
}
