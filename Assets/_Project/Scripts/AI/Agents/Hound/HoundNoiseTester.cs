using UnityEngine;

namespace Lockdown.AI.Agents
{
    // Attach to a separate object representing the location of a test noise.
    public sealed class HoundNoiseTester : MonoBehaviour
    {
        [SerializeField] private HoundHearing dog;
        [SerializeField, Range(0f, 1f)] private float loudness = 1f;

        [ContextMenu("Emit Test Noise")]
        public void EmitTestNoise()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("Enter Play mode before emitting a test noise.", this);
                return;
            }

            if (dog == null)
            {
                Debug.LogWarning("Assign the dog's HoundHearing component first.", this);
                return;
            }

            dog.HearNoise(transform.position, loudness);
        }
    }
}
