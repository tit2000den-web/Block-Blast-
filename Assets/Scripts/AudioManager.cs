using UnityEngine;

public enum Sfx { Click, Place, Clear, Bomb, Hammer, Lose, Win }

// Âm thanh + nhạc nền tự tổng hợp bằng code (không cần file âm thanh),
// kèm các tùy chọn Sound / Music / Vibration lưu bằng PlayerPrefs.
public class AudioManager : MonoBehaviour
{
    const int Rate = 22050;
    const string SoundKey = "opt_sound", MusicKey = "opt_music", VibrateKey = "opt_vibrate";

    static AudioManager instance;

    AudioSource[] sfxSources;
    int nextSource;
    AudioSource musicSource;
    AudioClip[] clips;

    public static bool SoundOn
    {
        get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
        set => PlayerPrefs.SetInt(SoundKey, value ? 1 : 0);
    }

    public static bool MusicOn
    {
        get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(MusicKey, value ? 1 : 0);
            if (instance == null) return;
            if (value) instance.musicSource.Play();
            else instance.musicSource.Stop();
        }
    }

    public static bool VibrationOn
    {
        get => PlayerPrefs.GetInt(VibrateKey, 1) == 1;
        set => PlayerPrefs.SetInt(VibrateKey, value ? 1 : 0);
    }

    public static void Ensure()
    {
        if (instance == null) new GameObject("AudioManager").AddComponent<AudioManager>();
    }

    public static void Play(Sfx sfx, float pitch = 1f)
    {
        if (instance == null || !SoundOn) return;
        var src = instance.sfxSources[instance.nextSource];
        instance.nextSource = (instance.nextSource + 1) % instance.sfxSources.Length;
        src.pitch = pitch;
        src.PlayOneShot(instance.clips[(int)sfx]);
    }

    public static void Vibrate()
    {
        if (!VibrationOn) return;
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        sfxSources = new AudioSource[4];
        for (int i = 0; i < sfxSources.Length; i++)
        {
            sfxSources[i] = gameObject.AddComponent<AudioSource>();
            sfxSources[i].playOnAwake = false;
        }

        clips = new AudioClip[7];
        clips[(int)Sfx.Click] = MakeClick();
        clips[(int)Sfx.Place] = MakePlace();
        clips[(int)Sfx.Clear] = MakeNotes("Clear", new[] { 84, 88, 91, 96 }, 0.055f, 0.35f, 0.4f);
        clips[(int)Sfx.Bomb] = MakeBomb();
        clips[(int)Sfx.Hammer] = MakeHammer();
        clips[(int)Sfx.Lose] = MakeNotes("Lose", new[] { 67, 64, 60, 55 }, 0.2f, 0.45f, 0.35f);
        clips[(int)Sfx.Win] = MakeNotes("Win", new[] { 72, 76, 79, 84, 88 }, 0.11f, 0.6f, 0.35f);

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = MakeMusic();
        musicSource.loop = true;
        musicSource.volume = 0.35f;
        if (MusicOn) musicSource.Play();
    }

    // ---------- tổng hợp âm ----------

    static float Freq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    static float Tri(float phase)
    {
        float p = phase - Mathf.Floor(phase);
        return 4f * Mathf.Abs(p - 0.5f) - 1f;
    }

    static AudioClip Clip(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip MakeClick()
    {
        var d = new float[(int)(Rate * 0.07f)];
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            phase += Mathf.Lerp(900f, 1500f, t / 0.07f) / Rate;
            d[i] = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 60f) * 0.35f;
        }
        return Clip("Click", d);
    }

    static AudioClip MakePlace()
    {
        var d = new float[(int)(Rate * 0.14f)];
        var rnd = new System.Random(1);
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float tone = Mathf.Sin(t * 170f * Mathf.PI * 2f) + 0.5f * Mathf.Sin(t * 340f * Mathf.PI * 2f);
            float noise = (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-t * 120f);
            d[i] = (tone * Mathf.Exp(-t * 35f) * 0.4f + noise * 0.25f);
        }
        return Clip("Place", d);
    }

    // Chuỗi nốt nối tiếp (dạng tam giác, tắt dần)
    static AudioClip MakeNotes(string name, int[] notes, float step, float tail, float volume)
    {
        float len = step * notes.Length + tail;
        var d = new float[(int)(Rate * len)];
        for (int n = 0; n < notes.Length; n++)
        {
            float f = Freq(notes[n]);
            int start = (int)(n * step * Rate);
            for (int i = start; i < d.Length; i++)
            {
                float t = (float)(i - start) / Rate;
                float env = Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * 9f);
                d[i] += Tri(t * f) * env * volume;
            }
        }
        return Clip(name, d);
    }

    static AudioClip MakeBomb()
    {
        var d = new float[(int)(Rate * 0.6f)];
        var rnd = new System.Random(2);
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            lp += ((float)(rnd.NextDouble() * 2 - 1) - lp) * 0.18f;    // nhiễu lọc thấp
            float boom = Mathf.Sin(t * Mathf.Lerp(120f, 40f, t / 0.6f) * Mathf.PI * 2f);
            d[i] = (lp * 1.6f + boom * 0.6f) * Mathf.Exp(-t * 7f) * 0.6f;
        }
        return Clip("Bomb", d);
    }

    static AudioClip MakeHammer()
    {
        var d = new float[(int)(Rate * 0.2f)];
        var rnd = new System.Random(3);
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float noise = (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-t * 80f);
            float ring = Mathf.Sin(t * 620f * Mathf.PI * 2f) * Mathf.Exp(-t * 25f);
            d[i] = (noise * 0.5f + ring * 0.3f);
        }
        return Clip("Hammer", d);
    }

    // Nhạc nền nhẹ nhàng: vòng hợp âm C - Am - F - G, mỗi hợp âm 2 giây, lặp lại
    static AudioClip MakeMusic()
    {
        int[][] chords =
        {
            new[] { 60, 64, 67 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 },
        };
        int[] arp = { 0, 1, 2, 1, 2, 1, 0, 1 };
        const float chordLen = 2f, noteLen = chordLen / 8f;
        var d = new float[(int)(Rate * chordLen * chords.Length)];

        for (int c = 0; c < chords.Length; c++)
        {
            int start = (int)(c * chordLen * Rate);
            int len = (int)(chordLen * Rate);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t * 3f) * Mathf.Min(1f, (chordLen - t) * 3f);
                float pad = 0f;
                foreach (int n in chords[c])
                    pad += Mathf.Sin(t * Freq(n) * Mathf.PI * 2f);
                float bass = Mathf.Sin(t * Freq(chords[c][0] - 12) * Mathf.PI * 2f);

                int step = Mathf.Min(7, (int)(t / noteLen));
                float nt = t - step * noteLen;
                float lead = Tri(nt * Freq(chords[c][arp[step]] + 12)) * Mathf.Min(1f, nt * 300f) * Mathf.Exp(-nt * 6f);

                d[start + i] = pad * env * 0.05f + bass * env * 0.08f + lead * 0.09f;
            }
        }
        return Clip("Music", d);
    }
}
