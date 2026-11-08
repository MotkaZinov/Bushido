using UnityEngine;

/// <summary>
/// Sonido del juego, GENERADO POR CÓDIGO (no necesitas archivos de audio).
/// - Cada yokai marcado toca una nota que SUBE en una escala japonesa (sonido tipo koto).
/// - Al ejecutar el tajo: "zan!" + arpegio rápido con las notas de la cadena + campanada final
///   que sube una nota por cada tajo seguido del combo.
/// - Cadena rota, swoosh de la navaja y gong del frenesí.
/// Cada sonido se puede reemplazar por un AudioClip propio.
/// Requiere un AudioListener en la escena (la Main Camera ya lo trae).
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    public enum ScaleType { Hirajoshi, Yo, PentatonicMajor, PentatonicMinor }

    [Header("Escala musical (cada yokai marcado sube una nota)")]
    public ScaleType scaleType = ScaleType.Hirajoshi;
    [Tooltip("Frecuencia de la nota base (261.63 = Do central)")]
    public float rootFrequency = 261.63f;

    [Header("Volúmenes")]
    [Range(0f, 1f)] public float masterVolume = 0.8f;
    [Range(0f, 1f)] public float markVolume = 0.55f;
    [Range(0f, 1f)] public float sliceVolume = 0.9f;
    [Range(0f, 1f)] public float swooshVolume = 0.35f;

    [Header("Swoosh de la navaja")]
    [Tooltip("Velocidad mínima del dedo para que suene (alturas de pantalla por segundo)")]
    public float swooshMinSpeed = 1.2f;
    public float swooshCooldown = 0.22f;

    [Header("Al ejecutar el tajo")]
    public int maxArpeggioNotes = 8;
    [Tooltip("Segundos entre nota y nota del arpegio")]
    public float arpeggioStep = 0.045f;
    [Tooltip("El arpegio completo nunca dura más que esto")]
    public float arpeggioMaxDuration = 0.5f;

    [Header("Opcional: tus propios sonidos (vacío = se genera solo)")]
    [Tooltip("Se transporta de tono; se asume que es la nota base")]
    public AudioClip customNote;
    public AudioClip customSlice;
    public AudioClip customSwoosh;
    public AudioClip customBroken;
    public AudioClip customGong;

    const int Octaves = 3;
    const int PoolSize = 24;

    static readonly int[][] Scales =
    {
        new[] { 0, 2, 3, 7, 8 },   // Hirajoshi (japonesa)
        new[] { 0, 2, 5, 7, 9 },   // Yo (japonesa, más brillante)
        new[] { 0, 2, 4, 7, 9 },   // Pentatónica mayor
        new[] { 0, 3, 5, 7, 10 },  // Pentatónica menor
    };

    AudioSource[] pool;
    int poolIndex;
    AudioClip[] notes;
    int[] noteSemitones;
    int scaleLength;
    AudioClip sliceClip, swooshClip, brokenClip, gongClip;
    int sampleRate;

    float nextSwooshTime;
    float lastSlashTime = -999f;
    int comboCuts;

    void Awake()
    {
        Instance = this;
        sampleRate = AudioSettings.outputSampleRate;

        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f; // sonido 2D
            pool[i] = s;
        }

        BuildNotes();
        if (customSlice == null)  sliceClip  = MakeClip("Slice",  MakeSlice());
        if (customSwoosh == null) swooshClip = MakeClip("Swoosh", MakeSwoosh());
        if (customBroken == null) brokenClip = MakeClip("Broken", MakeBroken());
        if (customGong == null)   gongClip   = MakeClip("Gong",   MakeGong());
    }

    // ───────────── API (la llaman BladeController y EnemySpawner) ─────────────

    /// <summary>Un yokai fue marcado. chainIndex = su lugar en la cadena (0, 1, 2...)</summary>
    public void OnMark(int chainIndex)
    {
        PlayNote(chainIndex, markVolume, 0f);
    }

    /// <summary>Se ejecutó el tajo.</summary>
    public void OnSlash(int chainSize, float multiplier)
    {
        // ¿Seguimos en el mismo combo? Cada tajo seguido sube la campanada final.
        float window = ScoreManager.Instance != null ? ScoreManager.Instance.comboWindow : 2.5f;
        comboCuts = (Time.time - lastSlashTime > window) ? 0 : comboCuts + 1;
        lastSlashTime = Time.time;

        // 1) El "zan!": más grave y fuerte mientras más yokais
        float heavy = Mathf.InverseLerp(2f, 10f, chainSize);
        PlayClip(customSlice != null ? customSlice : sliceClip, sliceVolume, Mathf.Lerp(1.1f, 0.8f, heavy), 0f);

        // 2) Arpegio rápido: repite las notas que fuiste marcando
        int count = Mathf.Min(chainSize, maxArpeggioNotes);
        float step = Mathf.Min(arpeggioStep, arpeggioMaxDuration / Mathf.Max(1, count));
        for (int i = 0; i < count; i++)
            PlayNote(i, markVolume * 0.8f, 0.03f + i * step);

        // 3) Campanada final: una octava arriba, y sube una nota por cada tajo seguido
        int chime = scaleLength + comboCuts;
        float comboT = Mathf.Clamp01(comboCuts / 8f);
        PlayNote(chime, Mathf.Lerp(0.6f, 1f, comboT), 0.03f + count * step);
    }

    /// <summary>La cadena se rompió (un solo marcado, o un yokai se escapó).</summary>
    public void OnChainBroken()
    {
        PlayClip(customBroken != null ? customBroken : brokenClip, 0.6f, 1f, 0f);
    }

    public void OnFrenzyStart()
    {
        PlayClip(customGong != null ? customGong : gongClip, 0.9f, 1f, 0f);
    }

    /// <summary>screenSpeed = alturas de pantalla por segundo que recorre el dedo.</summary>
    public void OnBladeMoved(float screenSpeed)
    {
        if (screenSpeed < swooshMinSpeed || Time.unscaledTime < nextSwooshTime) return;
        nextSwooshTime = Time.unscaledTime + swooshCooldown;

        float k = Mathf.InverseLerp(swooshMinSpeed, swooshMinSpeed * 3f, screenSpeed);
        PlayClip(customSwoosh != null ? customSwoosh : swooshClip,
                 swooshVolume * Mathf.Lerp(0.5f, 1f, k),
                 Mathf.Lerp(0.9f, 1.3f, k), 0f);
    }

    // ───────────── Reproducción ─────────────

    void PlayNote(int index, float volume, float delay)
    {
        index = Mathf.Clamp(index, 0, noteSemitones.Length - 1);

        if (customNote != null)
        {
            float pitch = Mathf.Clamp(Mathf.Pow(2f, noteSemitones[index] / 12f), 0.5f, 3f);
            PlayClip(customNote, volume, pitch, delay);
        }
        else
        {
            PlayClip(notes[index], volume, 1f, delay);
        }
    }

    void PlayClip(AudioClip clip, float volume, float pitch, float delay)
    {
        if (clip == null) return;

        AudioSource s = pool[poolIndex];
        poolIndex = (poolIndex + 1) % pool.Length;

        s.Stop();
        s.clip = clip;
        s.volume = volume * masterVolume;
        s.pitch = pitch;

        // PlayScheduled usa el reloj de audio: no le afecta el hit-stop (timeScale = 0)
        if (delay <= 0f) s.Play();
        else s.PlayScheduled(AudioSettings.dspTime + delay);
    }

    // ───────────── Generación de sonidos ─────────────

    void BuildNotes()
    {
        int[] offsets = Scales[(int)scaleType];
        scaleLength = offsets.Length;

        int total = scaleLength * Octaves;
        noteSemitones = new int[total];
        notes = new AudioClip[total];

        for (int i = 0; i < total; i++)
        {
            int semis = offsets[i % scaleLength] + 12 * (i / scaleLength);
            noteSemitones[i] = semis;

            if (customNote == null)
            {
                float freq = rootFrequency * Mathf.Pow(2f, semis / 12f);
                notes[i] = MakeClip("Note" + i, Pluck(freq, 0.7f));
            }
        }
    }

    AudioClip MakeClip(string clipName, float[] data)
    {
        AudioClip clip = AudioClip.Create(clipName, data.Length, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Cuerda pulsada (Karplus-Strong): suena parecido a un koto
    float[] Pluck(float freq, float duration)
    {
        int n = Mathf.Max(1, (int)(sampleRate * duration));
        int period = Mathf.Max(2, Mathf.RoundToInt(sampleRate / freq));

        float[] buf = new float[period];
        for (int i = 0; i < period; i++) buf[i] = Random.Range(-1f, 1f);
        for (int i = 0; i < period; i++) buf[i] = 0.5f * (buf[i] + buf[(i + 1) % period]); // más redondo

        float[] data = new float[n];
        int idx = 0;
        for (int i = 0; i < n; i++)
        {
            int next = (idx + 1) % period;
            data[i] = buf[idx];
            buf[idx] = 0.997f * 0.5f * (buf[idx] + buf[next]);
            idx = next;
        }
        return Finish(data, 0.15f);
    }

    // "Zan!": chasquido agudo + golpe grave que baja de tono
    float[] MakeSlice()
    {
        int n = (int)(sampleRate * 0.3f);
        float[] d = new float[n];
        float phase = 0f, lastNoise = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sampleRate;
            float noise = Random.Range(-1f, 1f);
            float crisp = (noise - lastNoise) * Mathf.Exp(-t * 28f);
            lastNoise = noise;

            float freq = Mathf.Lerp(170f, 55f, Mathf.Clamp01(t / 0.15f));
            phase += 2f * Mathf.PI * freq / sampleRate;
            float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 14f);

            d[i] = crisp * 0.55f + thump * 0.9f;
        }
        return Finish(d, 0.02f);
    }

    // Aire que se vuelve más agudo
    float[] MakeSwoosh()
    {
        int n = (int)(sampleRate * 0.22f);
        float[] d = new float[n];
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float noise = Random.Range(-1f, 1f);
            lp += Mathf.Lerp(0.02f, 0.5f, t) * (noise - lp);

            float env = Mathf.Sin(Mathf.PI * t);
            d[i] = (noise - lp) * env * env;
        }
        return Finish(d, 0.02f);
    }

    // Tono grave que cae: "bu-dum" apagado
    float[] MakeBroken()
    {
        int n = (int)(sampleRate * 0.35f);
        float[] d = new float[n];
        float phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sampleRate;
            float freq = Mathf.Lerp(240f, 110f, Mathf.Clamp01(t / 0.3f));
            phase += 2f * Mathf.PI * freq / sampleRate;
            d[i] = (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f)) * Mathf.Exp(-t * 9f);
        }
        return Finish(d, 0.02f);
    }

    // Gong: parciales no armónicos con caída lenta
    float[] MakeGong()
    {
        float[] ratios = { 1f, 2f, 2.76f, 4.07f, 5.4f, 6.9f };
        float[] amps   = { 1f, 0.6f, 0.5f, 0.3f, 0.25f, 0.15f };
        float[] decays = { 1.2f, 1.6f, 2f, 2.6f, 3.2f, 4f };
        const float baseFreq = 98f;

        int n = (int)(sampleRate * 2.5f);
        float[] d = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sampleRate;
            float sum = 0f;
            for (int p = 0; p < ratios.Length; p++)
                sum += amps[p] * Mathf.Sin(2f * Mathf.PI * baseFreq * ratios[p] * t) * Mathf.Exp(-decays[p] * t);
            d[i] = sum;
        }
        return Finish(d, 0.1f);
    }

    // Normaliza y suaviza inicio y final para que no haya "clics"
    float[] Finish(float[] d, float fadeOutSeconds)
    {
        float max = 0.0001f;
        for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
        float gain = 0.9f / max;

        int fadeIn = Mathf.Min(d.Length, sampleRate / 500);
        int fadeOut = Mathf.Min(d.Length, Mathf.Max(1, (int)(sampleRate * fadeOutSeconds)));

        for (int i = 0; i < d.Length; i++)
        {
            d[i] *= gain;
            if (i < fadeIn) d[i] *= i / (float)fadeIn;

            int fromEnd = d.Length - 1 - i;
            if (fromEnd < fadeOut) d[i] *= fromEnd / (float)fadeOut;
        }
        return d;
    }
}