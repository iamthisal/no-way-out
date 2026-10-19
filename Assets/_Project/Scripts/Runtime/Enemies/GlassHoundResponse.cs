using System.Collections;
using System.Collections.Generic;
using Lockdown.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Lockdown.Enemies
{
    [DisallowMultipleComponent]
    public sealed class GlassHoundResponse : MonoBehaviour
    {
        [SerializeField] private GameObject houndPrefab;
        [SerializeField] private BreakableGlass[] glassSources = new BreakableGlass[0];
        [SerializeField] private Transform[] entryPoints = new Transform[0];
        [SerializeField, Min(0f)] private float arrivalInterval = 0.65f;
        private readonly Dictionary<BreakableGlass, UnityAction> subscriptions = new Dictionary<BreakableGlass, UnityAction>();
        private readonly List<HoundInvestigator> pack = new List<HoundInvestigator>();
        private readonly HashSet<int> dispatchedEntries = new HashSet<int>();
        private Coroutine dispatch;
        public bool IsAlerted { get; private set; }
        public Vector3 LatestNoisePosition { get; private set; }
        public IReadOnlyList<HoundInvestigator> Pack => pack;

        private void OnEnable()
        {
            foreach (var glass in glassSources)
            {
                if (glass == null || subscriptions.ContainsKey(glass)) continue;
                var source = glass;
                UnityAction callback = () => ReportBreak(source);
                source.OnBroken.AddListener(callback);
                subscriptions.Add(source, callback);
            }
        }

        private void ReportBreak(BreakableGlass glass)
        {
            if (!glass.IsBroken) return;
            IsAlerted = true;
            LatestNoisePosition = glass.transform.position;
            foreach (var hound in pack) if (hound != null) hound.Investigate(LatestNoisePosition);
            // One pack per scene. Further broken cases redirect it, without endless spawning.
            if (dispatch == null && pack.Count < entryPoints.Length) dispatch = StartCoroutine(Dispatch());
        }

        private IEnumerator Dispatch()
        {
            // Yield once so the coroutine handle is assigned even with zero valid entries.
            yield return null;
            for (int i = 0; i < entryPoints.Length; i++)
            {
                if (dispatchedEntries.Contains(i)) continue;
                var entry = entryPoints[i];
                if (houndPrefab != null && entry != null &&
                    NavMesh.SamplePosition(entry.position, out var spawn, 0.8f, NavMesh.AllAreas))
                {
                    var instance = Instantiate(houndPrefab, spawn.position, entry.rotation, transform);
                    var hound = instance.GetComponent<HoundInvestigator>();
                    var agent = instance.GetComponent<NavMeshAgent>();
                    if (hound != null && agent != null && agent.isOnNavMesh)
                    {
                        pack.Add(hound);
                        dispatchedEntries.Add(i);
                        hound.Investigate(LatestNoisePosition);
                    }
                    else Destroy(instance);
                }
                if (arrivalInterval > 0f) yield return new WaitForSeconds(arrivalInterval);
            }
            dispatch = null;
        }

        private void OnDisable()
        {
            foreach (var pair in subscriptions) if (pair.Key != null) pair.Key.OnBroken.RemoveListener(pair.Value);
            subscriptions.Clear();
            if (dispatch != null) StopCoroutine(dispatch);
            dispatch = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            foreach (var entry in entryPoints) if (entry != null)
            { Gizmos.DrawWireSphere(entry.position + Vector3.up * 0.4f, 0.4f); Gizmos.DrawRay(entry.position, entry.forward); }
        }
    }
}
