using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lockdown.AI.Core.Director
{
    /// <summary>Ring buffer of recent decisions, for the debug overlay and as CSV evidence.</summary>
    public sealed class DirectorLog
    {
        public struct Entry
        {
            public float Time, Stress, Intensity, Target;
            public DirectorPhase Phase;
            public DirectorObservation Observation;
            public DirectorDecision Decision;
        }

        private readonly Entry[] entries;
        private int next;
        public int Count { get; private set; }

        public DirectorLog(int capacity) { entries = new Entry[Math.Max(1, capacity)]; }

        public void Add(in Entry entry)
        {
            entries[next] = entry;
            next = (next + 1) % entries.Length;
            if (Count < entries.Length) Count++;
        }

        /// <summary>Oldest first.</summary>
        public Entry this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                return entries[(next - Count + index + entries.Length) % entries.Length];
            }
        }

        public string ToCsv()
        {
            var text = new StringBuilder();
            text.Append("time,phase,stress,intensity,target,damage,health,engaged,seen,noise,loot,alarm,guards,archers,hounds,"
                + "action,reason,option1,utility1,option2,utility2,option3,utility3\n");
            for (int i = 0; i < Count; i++) Append(text, this[i]);
            return text.ToString();
        }

        private static void Append(StringBuilder text, Entry entry)
        {
            var seen = entry.Observation;
            var fields = new List<string>
            {
                Number(entry.Time), entry.Phase.ToString(), Number(entry.Stress), Number(entry.Intensity), Number(entry.Target),
                Number(seen.RecentDamage), Number(seen.HealthFraction), Number(seen.EngagedEnemies),
                Number(seen.Seen), Number(seen.Noise), Number(seen.LootFraction), seen.AlarmUnanswered ? "1" : "0",
                Number(seen.Census.Guards), Number(seen.Census.Archers), Number(seen.Census.Hounds),
                entry.Decision != null && entry.Decision.Spawn ? Name(entry.Decision.Choice) : "Wait",
                Quote(entry.Decision != null ? entry.Decision.Reason : "")
            };
            for (int i = 0; i < 3; i++)
            {
                bool has = entry.Decision != null && i < entry.Decision.Top.Count;
                fields.Add(has ? Name(entry.Decision.Top[i]) : "");
                fields.Add(has ? Number(entry.Decision.Top[i].Utility) : "");
            }
            text.Append(string.Join(",", fields)).Append('\n');
        }

        private static string Name(SpawnOption option) => Quote(option.Kind + "@" + option.RoomId + "#" + option.PointId);
        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static string Quote(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    }
}
