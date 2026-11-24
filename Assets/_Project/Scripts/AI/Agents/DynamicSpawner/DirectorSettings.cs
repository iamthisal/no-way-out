using Lockdown.AI.Core.Director;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    [CreateAssetMenu(menuName = "No Way Out/Director Settings")]
    public sealed class DirectorSettings : ScriptableObject
    {
        public DirectorTuning tuning = new DirectorTuning();
    }
}
