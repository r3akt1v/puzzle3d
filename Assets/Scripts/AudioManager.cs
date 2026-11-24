using UnityEngine;

namespace Puzzle3D
{
    /// <summary>Tiny procedural sound effects: click, place, hint, win.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I;

        AudioSource source;
        AudioClip clickClip, placeClip, hintClip, winClip;

        void Awake()
        {
            if (I != null) { Destroy(gameObject); return; }
            I = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.6f;
            clickClip = MakeClick();
            placeClip = MakePlace();
            hintClip = MakeHint();
            winClip = MakeWin();
        }

        void OnDestroy() { if (I == this) I = null; }

        public void PlayClick() => Play(clickClip, 0.8f);
        public void PlayPlace() => Play(placeClip, 0.9f);
        public void PlayHint() => Play(hintClip, 0.8f);
        public void PlayWin() => Play(winClip, 0.9f);

        void Play(AudioClip c, float vol)
        {
            if (c == null || source == null) return;
            source.pitch = 1f;
            source.PlayOneShot(c, vol);
        }

        static AudioClip MakeClick()
        {
            return MakeTone("click", 1400f, 0.05f, 0.5f, 22000f);
        }

        static AudioClip MakePlace()
        {
            // two quick low thocks
            int rate = 22050, len = rate / 8;
            var data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Exp(-t * 60f);
                float f = i < len / 3 ? 220f : 160f;
                data[i] = Mathf.Sin(2 * Mathf.PI * f * t) * env * 0.7f
                        + Mathf.Sin(2 * Mathf.PI * f * 2.7f * t) * env * 0.2f;
            }
            var c = AudioClip.Create("place", len, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip MakeHint()
        {
            return MakeTone("hint", 880f, 0.18f, 0.35f, 1400f);
        }

        static AudioClip MakeWin()
        {
            int rate = 22050, len = rate * 2 / 5;
            var data = new float[len];
            float[] notes = { 523f, 659f, 784f, 1047f };
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                int ni = Mathf.Min((int)(t / 0.4f), notes.Length - 1);
                float lt = t - ni * 0.4f;
                float env = Mathf.Exp(-lt * 6f);
                data[i] = Mathf.Sin(2 * Mathf.PI * notes[ni] * t) * env * 0.4f
                        + Mathf.Sin(2 * Mathf.PI * notes[ni] * 2f * t) * env * 0.12f;
            }
            var c = AudioClip.Create("win", len, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip MakeTone(string name, float freq, float dur, float vol, float freqEnd)
        {
            int rate = 22050, len = Mathf.CeilToInt(dur * rate);
            var data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float k = t / dur;
                float f = Mathf.Lerp(freq, freqEnd, k);
                float env = Mathf.Sin(Mathf.PI * k); // in-out
                data[i] = Mathf.Sin(2 * Mathf.PI * f * t) * env * vol;
            }
            var c = AudioClip.Create(name, len, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
