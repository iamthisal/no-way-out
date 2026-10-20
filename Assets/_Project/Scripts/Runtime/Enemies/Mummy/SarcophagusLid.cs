using Lockdown.Interfaces;
using UnityEngine;

namespace Lockdown.Enemies
{
    // The lid's interaction handle; removed once the sarcophagus is open so no prompt remains.
    [DisallowMultipleComponent]
    public sealed class SarcophagusLid : MonoBehaviour, IInteractable
    {
        [SerializeField] private Sarcophagus sarcophagus;

        public string InteractionLabel => sarcophagus != null ? "Open the sarcophagus of " + sarcophagus.GodName : "Open the sarcophagus";

        public bool TryInteract(GameObject interactor)
        {
            if (sarcophagus == null || sarcophagus.IsOpen || interactor == null) return false;
            sarcophagus.Open();
            return true;
        }
    }
}
