using System.Collections.Generic;
using UnityEngine;

namespace VoidStunts
{
    /// <summary>
    /// All sound is synthesised on the audio thread, like the web build: an engine (saw + square through a resonant low-pass),
    /// jet, wind and tyre-squeal noise bands, and short sine/triangle/square tones for UI and scoring cues. No audio assets.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SynthAudio : MonoBehaviour
    {
        struct Tone { public float f, f2, dur, gain, t, delay, phase; public int wave; }
        readonly List<Tone> tones = new(); readonly List<Tone> pending = new(); readonly object gate = new();
        float sr = 48000;
        // targets written on the main thread
        volatile float tEngF = 60, tEngCut = 600, tEngG, tJet, tJetF = 900, tWind, tSkid, tMaster = .55f;
        float engF = 60, engCut = 600, engG, jet, jetF = 900, wind, skid, master = .55f;
        float p1, p2, lp1, lp2, bpJ1, bpJ2, bpS1, bpS2, wLp; uint rng = 22222;
        public bool muted;

        void Awake()
        {
            sr = AudioSettings.outputSampleRate;
            var src = GetComponent<AudioSource>(); src.playOnAwake = true; src.loop = true; src.spatialBlend = 0;
            src.clip = AudioClip.Create("silence", 1024, 1, (int)sr, false); src.Play();
        }

        public void Tone1(float f, float d = .12f, int wave = 0, float g = .06f, float delay = 0, float f2 = 0)
        { lock (gate) pending.Add(new Tone { f = f, f2 = f2 > 0 ? f2 : f, dur = d, gain = g, delay = delay, wave = wave }); }

        public void Sfx(string id)
        {
            switch (id)
            {
                case "tick": Tone1(2400, .03f, 2, .015f); break;
                case "go": Tone1(880, .25f, 0, .08f); Tone1(1320, .35f, 0, .05f, .05f); break;
                case "count": Tone1(660, .12f, 0, .07f); break;
                case "pad": Tone1(220, .45f, 3, .03f, 0, 880); Tone1(1760, .2f, 0, .03f, .05f); break;
                case "goal": { float[] f = { 784, 988, 1175, 1568 }; for (int i = 0; i < 4; i++) Tone1(f[i], .6f, 0, .04f, i * .07f); } break;
                case "hoop": { float[] f = { 1046, 1318, 1568, 2093 }; for (int i = 0; i < 4; i++) Tone1(f[i], .35f, 1, .035f, i * .04f); } break;
                case "bail": Tone1(220, .2f, 2, .03f); Tone1(160, .3f, 2, .03f, .1f); break;
                case "trick": Tone1(1760, .06f, 0, .03f); break;
                case "end": { float[] f = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }; for (int i = 0; i < 5; i++) Tone1(f[i], 1.4f, 0, .05f, i * .09f); } break;
                case "mode": { int[] st = { 0, 4, 7, 11, 14 }; for (int i = 0; i < 5; i++) Tone1(392 * Mathf.Pow(2, st[i] / 12f), .5f, 0, .035f, i * .045f); Tone1(98, .35f, 0, .06f, 0, 60); } break;
                default:
                    if (id.StartsWith("bank")) { int m = int.Parse(id.Substring(4)); float b = 523.25f * Mathf.Pow(2, Mathf.Min(m - 1, 7) / 12f); float[] r = { 1, 1.25f, 1.5f, 2 }; for (int i = 0; i < 4; i++) Tone1(b * r[i], .5f, 0, .045f, i * .05f); }
                    break;
            }
        }
        public void Thud(float s) { Tone1(80, .25f, 0, Mathf.Min(.35f, .08f + s * .025f), 0, 38); Tone1(55, .18f, 1, Mathf.Min(.2f, s * .015f), 0, 30); }

        public void Drive(CarController car, bool playing)
        {
            if (car == null) return;
            float speed = car.rb.linearVelocity.magnitude, thr = Mathf.Max(car.inp.gas, car.boosting ? 1 : 0);
            const float gs = 13; int gear = Mathf.Min(5, Mathf.FloorToInt(speed / gs)); float frac = (speed - gear * gs) / gs;
            float f = 52 + frac * 64 + gear * 7 + thr * 14; if (car.grounded == 0) f = Mathf.Lerp(f, 80 + thr * 70, .6f);
            tEngF = f; tEngCut = 380 + thr * 900 + speed * 12; tEngG = playing ? .07f + thr * .05f : .02f;
            tJet = car.boosting ? .28f : 0; tJetF = car.boosting ? 700 + Random.value * 500 : 900;
            tWind = car.grounded == 0 ? Mathf.Min(.18f, speed * .004f) : Mathf.Min(.05f, speed * .001f);
            float slip = 0; foreach (var w in car.wheels) if (w.hit) slip = Mathf.Max(slip, Mathf.Abs(w.vlat));
            tSkid = slip > 4 ? Mathf.Min(.06f, (slip - 4) * .008f) : 0; tMaster = muted ? 0 : .55f;
        }

        float Noise() { rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5; return (rng / 4294967296f) * 2 - 1; }
        static void Svf(ref float lo, ref float bp, float x, float f, float q, float sr, out float band, out float low)
        {
            float F = 2 * Mathf.Sin(Mathf.PI * Mathf.Min(f, sr * .2f) / sr), hp = x - lo - bp / q; bp += F * hp; lo += F * bp; band = bp; low = lo;
        }

        void OnAudioFilterRead(float[] data, int ch)
        {
            lock (gate) { tones.AddRange(pending); pending.Clear(); }
            int n = data.Length / ch; float dt = 1f / sr, k = 1 - Mathf.Exp(-dt / .04f), k2 = 1 - Mathf.Exp(-dt / .1f);
            for (int i = 0; i < n; i++)
            {
                engF += (tEngF - engF) * k; engCut += (tEngCut - engCut) * k; engG += (tEngG - engG) * k2; jet += (tJet - jet) * k; jetF += (tJetF - jetF) * k;
                wind += (tWind - wind) * k2 * .5f; skid += (tSkid - skid) * k; master += (tMaster - master) * k2;
                p1 += engF * dt; if (p1 > 1) p1 -= 1; p2 += engF * .5f * dt; if (p2 > 1) p2 -= 1;
                float e = (p1 * 2 - 1) + (p2 < .5f ? .35f : -.35f);
                Svf(ref lp1, ref lp2, e, engCut, 3, sr, out _, out float eLow);
                float nz = Noise();
                Svf(ref bpJ1, ref bpJ2, nz, jetF, .7f, sr, out float jb, out _);
                Svf(ref bpS1, ref bpS2, nz, 2300, 6, sr, out float sb, out _);
                wLp += (nz - wLp) * .065f;
                float s = eLow * engG + jb * jet + wLp * wind * 2.4f + sb * skid;
                for (int t = 0; t < tones.Count; t++)
                {
                    var T = tones[t];
                    if (T.delay > 0) { T.delay -= dt; tones[t] = T; continue; }
                    float u = T.t / T.dur, f = T.f * Mathf.Pow(T.f2 / T.f, Mathf.Min(1, u));
                    T.phase += f * dt; if (T.phase > 1) T.phase -= 1;
                    float w = T.wave == 0 ? Mathf.Sin(T.phase * 6.2831853f) : T.wave == 1 ? 1 - 4 * Mathf.Abs(T.phase - .5f) : T.wave == 2 ? (T.phase < .5f ? 1 : -1) : T.phase * 2 - 1;
                    float env = T.t < .006f ? T.t / .006f : Mathf.Exp(-9.2f * (T.t - .006f) / T.dur);
                    s += w * env * T.gain; T.t += dt; tones[t] = T;
                }
                if ((i & 63) == 0) tones.RemoveAll(x => x.delay <= 0 && x.t > x.dur + .05f);
                s = s * master; s = s / (1 + Mathf.Abs(s)); // soft clip
                for (int c = 0; c < ch; c++) data[i * ch + c] = s;
            }
        }
    }
}
