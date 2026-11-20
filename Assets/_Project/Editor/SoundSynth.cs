using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpaceBetweenUs.EditorTools
{
    /// <summary>
    /// Placeholder sound effects, synthesised in code (no licences, consistent soft "space" style) and written as
    /// .wav files to Assets/_Project/Audio. A file that already exists is never overwritten (unless you pick
    /// Regenerate), so you can drop a real recording in with the same name and it stays.
    /// </summary>
    public static class SoundSynth
    {
        public const string Dir = "Assets/_Project/Audio";
        const int SR = 32000;
        const float TwoPi = Mathf.PI * 2f;

        [MenuItem("Space Between Us/Audio/Generate Placeholder Sounds (keep existing)", priority = 40)]
        static void Menu() => Report(EnsureClips(false));

        [MenuItem("Space Between Us/Audio/Regenerate All Placeholder Sounds (overwrite)", priority = 41)]
        static void MenuOverwrite()
        {
            if (EditorUtility.DisplayDialog("Regenerate sounds", "Overwrite every .wav in " + Dir + " with the generated placeholder?", "Overwrite", "Cancel"))
                Report(EnsureClips(true));
        }

        static void Report(Dictionary<string, AudioClip> clips) => Debug.Log($"[Audio] {clips.Count} clips ready in {Dir}");

        static readonly (string name, bool loop, Func<float[]> make)[] Recipes =
        {
            ("amb_builder", true, AmbBuilder),
            ("amb_destroyer", true, AmbDestroyer),
            ("punch", false, Punch),
            ("tap", false, Tap),
            ("violent", false, Violent),
            ("island_ruin", false, Ruin),
            ("attract_loop", true, AttractLoop),
            ("grab", false, Grab),
            ("place_rock", false, PlaceRock),
            ("pour_loop", true, PourLoop),
            ("water_fill", false, WaterFill),
            ("crystal", false, Crystal),
            ("grow", false, Grow),
            ("island_alive", false, Alive),
            ("switch_whoosh", false, Whoosh),
            ("reveal", false, Reveal),
        };

        // bump a clip's version when its recipe changes: an older generated file is then rebuilt automatically
        // (also by the "keep existing" menu and the art swap). Files you replaced yourself keep version 1 unless listed.
        static readonly Dictionary<string, int> Versions = new() { ["amb_builder"] = 2, ["amb_destroyer"] = 2 };
        static int Version(string name) => Versions.TryGetValue(name, out var v) ? v : 1;
        static int StoredVersion(string path) =>
            AssetImporter.GetAtPath(path) is { } imp && imp.userData.StartsWith("synth:v") && int.TryParse(imp.userData.Substring(7), out var v) ? v : 1;

        public static Dictionary<string, AudioClip> EnsureClips(bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project", "Audio");
            var written = new List<string>();
            foreach (var (name, _, make) in Recipes)
            {
                string path = $"{Dir}/{name}.wav";
                if (!overwrite && File.Exists(path) && StoredVersion(path) >= Version(name)) continue;
                WriteWav(path, make());
                written.Add(path);
            }
            AssetDatabase.Refresh();
            var clips = new Dictionary<string, AudioClip>();
            foreach (var (name, loop, _) in Recipes)
            {
                string path = $"{Dir}/{name}.wav";
                if (written.Contains(path) && AssetImporter.GetAtPath(path) is AudioImporter imp)
                {
                    imp.forceToMono = true;
                    imp.userData = "synth:v" + Version(name);
                    imp.loadInBackground = loop;
                    var s = imp.defaultSampleSettings;
                    s.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                    s.compressionFormat = AudioCompressionFormat.Vorbis;
                    s.quality = loop ? 0.7f : 0.6f;
                    imp.defaultSampleSettings = s;
                    imp.SaveAndReimport();
                }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip) clips[name] = clip;
            }
            return clips;
        }

        // ------------------------------------------------------------------ recipes

        // Ambience: calm, deep space. A 48 s seamless loop (every frequency and LFO fits a whole number of cycles):
        //  - an open drone (root, fifth, octave, ninth; no third, so it is neither happy nor sad), each voice two slightly
        //    detuned sines that slowly beat against each other, breathing on its own 20-50 s swell
        //  - a very low, slow "solar wind" of filtered noise
        //  - a few far-away glimmers: soft tones that fade in and out over seconds, with long echoes
        const float AmbT = 48f;

        static float[] AmbBuilder() => Space(
            new[] { 73.42f, 110f, 146.83f, 164.81f, 220f, 329.63f },
            new[] { 0.55f, 0.40f, 0.30f, 0.16f, 0.12f, 0.05f },
            glimmers: new[] { 587.33f, 659.26f, 880f, 739.99f, 987.77f },
            glimmerGain: 0.08f, windLp: 0.004f, windGain: 0.35f, seed: 3);

        static float[] AmbDestroyer() => Space(
            new[] { 41.2f, 55f, 82.41f, 110f, 123.47f, 164.81f },
            new[] { 0.55f, 0.50f, 0.32f, 0.20f, 0.10f, 0.06f },
            glimmers: new[] { 329.63f, 493.88f, 440f },
            glimmerGain: 0.05f, windLp: 0.0025f, windGain: 0.5f, seed: 5);

        static float[] Space(float[] f, float[] g, float[] glimmers, float glimmerGain, float windLp, float windGain, int seed)
        {
            const float T = AmbT;
            float Q(float hz) => Mathf.Max(1f, Mathf.Round(hz * T)) / T;          // whole cycles per loop
            var r = new System.Random(seed);
            var b = Buf(T);
            int n = b.Length;

            // drone voices
            for (int k = 0; k < f.Length; k++)
            {
                float fa = Q(f[k]), fb = Q(f[k] + 0.06f + 0.03f * k);          // slow beating (chorus)
                float lfo = (1 + k % 3) / T, lph = (float)r.NextDouble() * TwoPi;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)SR;
                    float swell = 0.55f + 0.45f * Mathf.Sin(TwoPi * lfo * t + lph);
                    float v = Mathf.Sin(TwoPi * fa * t) + 0.8f * Mathf.Sin(TwoPi * fb * t + k)
                            + 0.12f * Mathf.Sin(TwoPi * 2f * fa * t);              // a touch of warmth
                    b[i] += g[k] * swell * v;
                }
            }

            // solar wind: deep filtered noise with slow swells
            var wind = LoopNoise(T, windLp, seed + 100);
            var air = LoopNoise(T, windLp * 6f, seed + 200);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float sw = 0.5f + 0.5f * Mathf.Sin(TwoPi * 2f / T * t + 1.3f);
                b[i] += windGain * (wind[i] * (0.6f + 0.4f * sw) + 0.15f * air[i] * sw * sw);
            }

            // far-away glimmers, each with a few long echoes (wrapped, so the loop stays seamless)
            float slot = T / glimmers.Length;
            for (int k = 0; k < glimmers.Length; k++)
            {
                float t0 = k * slot + (float)r.NextDouble() * slot * 0.6f, fq = glimmers[k];
                float dur = 6f + 3f * (float)r.NextDouble();
                for (int e = 0; e < 4; e++)
                {
                    float te = t0 + e * 0.9f, ge = glimmerGain * Mathf.Pow(0.45f, e);
                    int s0 = (int)(te * SR), len = (int)(dur * SR);
                    float ph = 0f;
                    for (int j = 0; j < len; j++)
                    {
                        float t = j / (float)SR;
                        ph += TwoPi * fq / SR;
                        float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur));         // soft in, soft out
                        env *= env;
                        b[(s0 + j) % n] += ge * env * (Mathf.Sin(ph) + 0.3f * Mathf.Sin(2.003f * ph));
                    }
                }
            }

            // gentle low-pass so nothing is bright or sharp
            float lp = 0f;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++) { lp += 0.25f * (b[i] - lp); b[i] = lp; }
            return Norm(b, 0.5f);
        }

        static float[] Punch()
        {
            var r = new System.Random(1);
            var b = Buf(0.8f);
            float lp1 = 0f, lp2 = 0f, ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(120f, 45f, Mathf.Clamp01(t / 0.25f));
                ph += TwoPi * f / SR;
                float n = N(r);
                lp1 += 0.35f * (n - lp1);
                lp2 += 0.08f * (n - lp2);
                b[i] = Mathf.Sin(ph) * Exp(t, 0.18f) + lp1 * 0.9f * Exp(t, 0.04f) + lp2 * 1.2f * Exp(t, 0.25f);
            }
            return Norm(b, 0.95f);
        }

        static float[] Tap()
        {
            var r = new System.Random(2);
            var b = Buf(0.4f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                b[i] = Mathf.Sin(TwoPi * 820f * t) * Exp(t, 0.07f) + 0.4f * Mathf.Sin(TwoPi * 1230f * t) * Exp(t, 0.05f)
                       + 0.5f * N(r) * Exp(t, 0.005f);
            }
            return Norm(b, 0.8f);
        }

        static float[] Violent()
        {
            var r = new System.Random(3);
            var b = Buf(2.4f);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                lp += 0.03f * (N(r) - lp);
                b[i] = lp * 3f * Mathf.Min(1f, t / 0.02f) * Exp(t, 0.8f) + 0.7f * Mathf.Sin(TwoPi * 40f * t) * Exp(t, 0.6f);
            }
            for (int k = 0; k < 6; k++) Crack(b, r, (float)r.NextDouble() * 0.6f, 0.04f, 0.6f + (float)r.NextDouble() * 0.4f);
            return Norm(b, 0.95f);
        }

        static float[] Ruin()
        {
            var r = new System.Random(4);
            var b = Buf(2.6f);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                lp += 0.015f * (N(r) - lp);
                b[i] = lp * 4f * Mathf.Min(1f, t / 0.05f) * Exp(t, 1f);
            }
            for (int k = 0; k < 70; k++)
            {
                float t0 = Mathf.Pow((float)r.NextDouble(), 2f) * 2.2f;     // dense at first, then sparse
                Crack(b, r, t0, 0.006f, 0.15f + 0.3f * (float)r.NextDouble());
            }
            return Norm(b, 0.9f);
        }

        static float[] AttractLoop()
        {
            const float T = 2f;
            var r = new System.Random(5);
            var b = Buf(T);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float trem = 0.75f + 0.25f * Mathf.Sin(TwoPi * 6f * t);
                b[i] = trem * (Mathf.Sin(TwoPi * 220f * t) * 0.5f + Mathf.Sin(TwoPi * 330f * t) * 0.3f + Mathf.Sin(TwoPi * 660f * t) * 0.08f);
            }
            Mix(b, LoopNoise(T, 0.3f, 5), 0.04f);
            return Norm(b, 0.5f);
        }

        static float[] Grab()
        {
            var b = Buf(0.3f);
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                ph += TwoPi * Mathf.Lerp(520f, 660f, Mathf.Clamp01(t / 0.08f)) / SR;
                b[i] = (Mathf.Sin(ph) + 0.3f * Mathf.Sin(ph * 2f)) * Mathf.Min(1f, t / 0.004f) * Exp(t, 0.06f);
            }
            return Norm(b, 0.7f);
        }

        static float[] PlaceRock()
        {
            var r = new System.Random(6);
            var b = Buf(0.7f);
            float ph = 0f, lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                ph += TwoPi * Mathf.Lerp(90f, 55f, Mathf.Clamp01(t / 0.2f)) / SR;
                lp += 0.12f * (N(r) - lp);
                b[i] = Mathf.Sin(ph) * Exp(t, 0.12f) + lp * 1.5f * Exp(t, 0.08f);
            }
            return Norm(b, 0.9f);
        }

        static float[] PourLoop()
        {
            const float T = 3f;
            var r = new System.Random(7);
            var b = LoopNoise(T, 0.25f, 7);
            Scale(b, 0.5f);
            var bub = Buf(T + 0.2f);
            for (int k = 0; k < 45; k++) Bubble(bub, r, (float)r.NextDouble() * T);
            for (int i = 0; i < b.Length; i++) b[i] += bub[i] + (i < bub.Length - b.Length ? bub[b.Length + i] : 0f);   // wrap the tail round
            return Norm(b, 0.5f);
        }

        static float[] WaterFill()
        {
            var r = new System.Random(8);
            var b = Buf(1.4f);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                lp += 0.2f * (N(r) - lp);
                b[i] = lp * 0.5f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.2f));
            }
            for (int k = 0; k < 28; k++) Bubble(b, r, Mathf.Pow((float)r.NextDouble(), 1.5f) * 1.1f);
            return Norm(b, 0.7f);
        }

        static float[] Crystal()
        {
            var b = Buf(3f);
            float[] p = { 1f, 2.76f, 5.40f, 8.93f };
            float[] a = { 1f, 0.5f, 0.25f, 0.12f };
            float[] tau = { 1.4f, 0.8f, 0.4f, 0.2f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR, v = 0f;
                for (int k = 0; k < p.Length; k++)
                    v += a[k] * (Mathf.Sin(TwoPi * 880f * p[k] * t) + Mathf.Sin(TwoPi * 880f * p[k] * 1.003f * t)) * 0.5f * Exp(t, tau[k]);
                b[i] = v * Mathf.Min(1f, t / 0.003f);
            }
            return Norm(b, 0.7f);
        }

        static float[] Grow()
        {
            var r = new System.Random(9);
            var b = Buf(1.8f);
            float[] notes = { 1318.5f, 1568f, 1760f, 2093f, 2349.3f, 2637f };
            for (int k = 0; k < 18; k++)
            {
                float t0 = (float)r.NextDouble() * 1.3f, f = notes[r.Next(notes.Length)], g = 0.3f + 0.3f * (float)r.NextDouble();
                int s0 = (int)(t0 * SR);
                for (int i = s0; i < b.Length; i++)
                {
                    float t = (i - s0) / (float)SR;
                    if (t > 0.8f) break;
                    b[i] += g * Mathf.Sin(TwoPi * f * t) * Mathf.Min(1f, t / 0.003f) * Exp(t, 0.15f);
                }
            }
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                lp += 0.05f * (N(r) - lp);
                b[i] += lp * 0.3f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.6f));
            }
            return Norm(b, 0.6f);
        }

        static float[] Alive()
        {
            var b = Buf(2.8f);
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int k = 0; k < notes.Length; k++)
            {
                int s0 = (int)(k * 0.18f * SR);
                for (int i = s0; i < b.Length; i++)
                {
                    float t = (i - s0) / (float)SR;
                    b[i] += (Mathf.Sin(TwoPi * notes[k] * t) + 0.3f * Mathf.Sin(TwoPi * notes[k] * 2f * t)) * Mathf.Min(1f, t / 0.004f) * Exp(t, 0.9f);
                }
            }
            return Norm(b, 0.7f);
        }

        static float[] Whoosh()
        {
            var r = new System.Random(10);
            const float T = 1.4f;
            var b = Buf(T);
            float x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR, u = t / T;
                float fc = u < 0.5f ? Mathf.Lerp(200f, 2500f, u * 2f) : Mathf.Lerp(2500f, 300f, (u - 0.5f) * 2f);
                float w0 = TwoPi * fc / SR, alpha = Mathf.Sin(w0) / (2f * 1.2f), cos = Mathf.Cos(w0);
                float a0 = 1f + alpha;
                float x = N(r);
                float y = (alpha * x - alpha * x2 - (-2f * cos) * y1 - (1f - alpha) * y2) / a0;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                b[i] = y * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.5f);
            }
            return Norm(b, 0.7f);
        }

        static float[] Reveal()
        {
            const float T = 9f;
            var b = Buf(T);
            float[] f = { 130.81f, 196f, 261.63f, 329.63f, 392f, 587.33f };
            float[] g = { 0.5f, 0.4f, 0.35f, 0.3f, 0.25f, 0.15f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR, v = 0f;
                for (int k = 0; k < f.Length; k++)
                {
                    float vib = 1f + 0.002f * Mathf.Sin(TwoPi * 5f * t + k);
                    float start = k * 0.35f;
                    float env = Mathf.Clamp01((t - start) / 2.5f) * Mathf.Clamp01((T - t) / 3f);
                    v += g[k] * Mathf.Sin(TwoPi * f[k] * vib * t) * env;
                }
                b[i] = v;
            }
            return Norm(b, 0.6f);
        }

        // ------------------------------------------------------------------ helpers

        static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * SR)];
        static float N(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);
        static float Exp(float t, float tau) => Mathf.Exp(-t / tau);

        /// <summary>Low-passed noise that loops seamlessly (the tail is cross-faded into the head).</summary>
        static float[] LoopNoise(float seconds, float lpCoef, int seed)
        {
            var r = new System.Random(seed);
            int n = Mathf.CeilToInt(seconds * SR), fade = SR / 2;
            var raw = new float[n + fade];
            float lp = 0f;
            for (int i = 0; i < raw.Length; i++) { lp += lpCoef * (N(r) - lp); raw[i] = lp; }
            var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                b[i] = raw[i];
                if (i < fade) { float k = i / (float)fade; b[i] = raw[i] * k + raw[n + i] * (1f - k); }
            }
            return Norm(b, 1f);
        }

        static void Crack(float[] b, System.Random r, float t0, float tau, float gain)
        {
            int s0 = (int)(t0 * SR);
            float lp = 0f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)SR;
                if (t > tau * 8f) break;
                lp += 0.4f * (N(r) - lp);
                b[i] += lp * gain * 2f * Exp(t, tau);
            }
        }

        static void Bubble(float[] b, System.Random r, float t0)
        {
            int s0 = (int)(t0 * SR);
            float f0 = 300f + 600f * (float)r.NextDouble(), g = 0.15f + 0.25f * (float)r.NextDouble(), ph = 0f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)SR;
                if (t > 0.12f) break;
                ph += TwoPi * f0 * (1f + 0.6f * Mathf.Clamp01(t / 0.04f)) / SR;
                b[i] += g * Mathf.Sin(ph) * Mathf.Min(1f, t / 0.002f) * Exp(t, 0.03f);
            }
        }

        static void Mix(float[] b, float[] add, float gain)
        {
            for (int i = 0; i < b.Length && i < add.Length; i++) b[i] += add[i] * gain;
        }

        static void Scale(float[] b, float k) { for (int i = 0; i < b.Length; i++) b[i] *= k; }

        static float[] Norm(float[] b, float peak)
        {
            float m = 1e-6f;
            foreach (var v in b) m = Mathf.Max(m, Mathf.Abs(v));
            Scale(b, peak / m);
            return b;
        }

        static void WriteWav(string path, float[] data)
        {
            using var fs = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(fs);
            int bytes = data.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E' }); w.Write(new[] { 'f', 'm', 't', ' ' });
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(SR); w.Write(SR * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
            foreach (var v in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
        }
    }
}
