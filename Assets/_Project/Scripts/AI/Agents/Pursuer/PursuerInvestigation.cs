using UnityEngine;
namespace Lockdown.AI.Agents
{
    public sealed class PursuerInvestigation
    {
        public Vector3 Position { get; private set; }
        private float deadline, inspectUntil = float.PositiveInfinity;
        public void Begin(Vector3 position, float now, float timeout)
        { Position = position; deadline = now + timeout; inspectUntil = float.PositiveInfinity; }
        public bool Finished(bool arrived, float now, float inspectDuration)
        {
            if (arrived && float.IsPositiveInfinity(inspectUntil)) inspectUntil = now + inspectDuration;
            return now >= deadline || now >= inspectUntil;
        }
    }
}
