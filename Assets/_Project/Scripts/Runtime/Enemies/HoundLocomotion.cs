using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.Enemies
{
    // Adds a speed-driven diagonal gait over the existing breathing/idle pose.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class HoundLocomotion : MonoBehaviour
    {
        private NavMeshAgent agent;
        private readonly Transform[] legs = new Transform[8];
        private readonly Quaternion[] rest = new Quaternion[8];
        private float phase;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            string[] names = { "FrontUpper_L", "FrontUpper_R", "HindUpper_L", "HindUpper_R",
                "FrontLower_L", "FrontLower_R", "HindLower_L", "HindLower_R" };
            var bones = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < names.Length; i++)
                foreach (var bone in bones)
                    if (bone.name == names[i]) { legs[i] = bone; rest[i] = bone.localRotation; break; }
        }

        private void LateUpdate()
        {
            float speed = agent.isActiveAndEnabled ? agent.velocity.magnitude : 0f;
            float amount = Mathf.Clamp01(speed / 2.5f);
            phase += Time.deltaTime * speed * 7f;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                int leg = i % 4;
                float wave = Mathf.Sin(phase + (leg == 0 || leg == 3 ? 0f : Mathf.PI));
                float angle = i < 4 ? wave * 24f : Mathf.Max(0f, -wave) * 27f;
                legs[i].localRotation = rest[i] * Quaternion.Euler(angle * amount, 0f, 0f);
            }
        }
    }
}
