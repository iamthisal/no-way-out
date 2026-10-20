using System;
using UnityEngine;

namespace Lockdown.Enemies
{
    // Tracks the Egypt gallery's curse for the player: the canopic jar counter and short messages
    // that explain each rule the first time it matters.
    [DisallowMultipleComponent]
    public sealed class MummyCurse : MonoBehaviour
    {
        [SerializeField] private MummyBody[] mummies = new MummyBody[0];
        [SerializeField] private bool showHud = true;

        private Action[] released;
        private Action[] risen;
        private Action[] jarBroken;
        private Action[] destroyed;
        private Action[] rejected;
        private string message;
        private float messageUntil;
        private float nextSealedHint;
        private bool explainedRising;
        private GUIStyle counterStyle;
        private GUIStyle messageStyle;

        public bool Active { get; private set; }
        public bool Lifted { get; private set; }
        public string Message => Time.time < messageUntil ? message : null;
        public int JarsBroken
        {
            get
            {
                int count = 0;
                foreach (var mummy in mummies) if (mummy != null && mummy.BoundJar != null && mummy.BoundJar.IsBroken) count++;
                return count;
            }
        }

        private void OnEnable()
        {
            int n = mummies.Length;
            released = new Action[n];
            risen = new Action[n];
            jarBroken = new Action[n];
            destroyed = new Action[n];
            rejected = new Action[n];
            for (int i = 0; i < n; i++)
            {
                var body = mummies[i];
                if (body == null) continue;
                released[i] = () => OnReleased(body);
                risen[i] = () => OnRisen(body);
                jarBroken[i] = () => OnJarBroken(body);
                destroyed[i] = () => OnDestroyed(body);
                rejected[i] = OnSealedJarHit;
                body.Released += released[i];
                body.Risen += risen[i];
                body.JarBroken += jarBroken[i];
                body.Destroyed += destroyed[i];
                if (body.BoundJar != null) body.BoundJar.Rejected += rejected[i];
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < mummies.Length; i++)
            {
                var body = mummies[i];
                if (body == null) continue;
                body.Released -= released[i];
                body.Risen -= risen[i];
                body.JarBroken -= jarBroken[i];
                body.Destroyed -= destroyed[i];
                if (body.BoundJar != null) body.BoundJar.Rejected -= rejected[i];
            }
        }

        private void OnReleased(MummyBody mummy)
        {
            Active = true;
            Show(GodOf(mummy) + "'s mummy is free. Its canopic jar can now be broken.", 4f);
        }

        private void OnSealedJarHit()
        {
            if (Time.time < nextSealedHint) return;
            nextSealedHint = Time.time + 3f;
            Show("The jar is sealed by its curse. Open its mummy's sarcophagus first.", 4f);
        }

        private void OnRisen(MummyBody mummy)
        {
            if (explainedRising || mummy.IsMortal) return;
            explainedRising = true;
            Show("It rises again. Smash its canopic jar to lay it to rest.", 5f);
        }

        private void OnJarBroken(MummyBody mummy)
        {
            string swatch = "<color=#" + ColorUtility.ToHtmlStringRGB(mummy.BoundJar.Glaze) + ">●</color>";
            Show(GodOf(mummy) + "'s jar shattered " + swatch + "  its mummy can now be destroyed.", 5f);
        }

        private void OnDestroyed(MummyBody mummy)
        {
            foreach (var other in mummies)
                if (other != null && other.Life != MummyLife.Destroyed)
                {
                    Show(GodOf(mummy) + "'s mummy crumbles to dust, leaving its amulet behind.", 4f);
                    return;
                }
            Lifted = true;
            Show("The curse is lifted. Collect the amulets.", 6f);
        }

        private static string GodOf(MummyBody mummy) => mummy.BoundJar != null ? mummy.BoundJar.GodName : "The";

        private void Show(string text, float seconds)
        {
            message = text;
            messageUntil = Time.time + seconds;
        }

        private void OnGUI()
        {
            if (!showHud || (!Active && Message == null)) return;
            if (counterStyle == null)
            {
                counterStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = Color.white } };
                messageStyle = new GUIStyle(counterStyle) { fontSize = 24 };
            }
            if (Active && !Lifted) GUI.Label(new Rect(0f, 16f, Screen.width, 30f), "Canopic jars broken: " + JarsBroken + " / " + mummies.Length, counterStyle);
            if (Message != null) GUI.Label(new Rect(0f, 52f, Screen.width, 36f), Message, messageStyle);
        }
    }
}
