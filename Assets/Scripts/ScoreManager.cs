using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Lleva el puntaje, la tabla de multiplicadores y actualiza la UI.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Serializable]
    public class ChainMultiplier
    {
        [Tooltip("Mínimo de enemigos en la cadena para aplicar este multiplicador")]
        public int enemies;
        public float multiplier = 1f;

        public ChainMultiplier() { }
        public ChainMultiplier(int enemies, float multiplier)
        {
            this.enemies = enemies;
            this.multiplier = multiplier;
        }
    }

    [Header("UI")]
    public TMP_Text scoreText;
    [Tooltip("Opcional: muestra 'x3 +450' unos instantes al ejecutar un combo")]
    public TMP_Text comboText;
    public string scoreFormat = "Puntos: {0}";
    public float comboDisplayTime = 1.2f;

    [Header("Multiplicadores por tamaño de cadena")]
    [Tooltip("Se aplica el de mayor 'enemies' que sea <= al tamaño de la cadena")]
    public List<ChainMultiplier> multipliers = new List<ChainMultiplier>
    {
        new ChainMultiplier(2, 2f),
        new ChainMultiplier(3, 3f),
        new ChainMultiplier(4, 4f),
        new ChainMultiplier(5, 5f),
        new ChainMultiplier(6, 6f),
    };

    [Header("Crecimiento después de la última fila de la tabla")]
    [Tooltip("Cada enemigo extra suma esto al multiplicador")]
    public float extraPerEnemy = 1.5f;
    [Tooltip("Hace que el crecimiento se acelere: entre más enemigos, cada uno suma más")]
    public float extraAcceleration = 0.5f;

    public int Score { get; private set; }

    float comboHideTime;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        RefreshScore();
        if (comboText != null) comboText.text = "";
    }

    void Update()
    {
        if (comboText != null && comboText.text.Length > 0 && Time.time >= comboHideTime)
            comboText.text = "";
    }

    public float GetMultiplier(int chainSize)
    {
        // Fila de la tabla que le toca a esta cadena + la fila más alta de la tabla
        ChainMultiplier best = null;
        int highestRow = int.MinValue;

        foreach (ChainMultiplier m in multipliers)
        {
            if (m.enemies > highestRow) highestRow = m.enemies;
            if (m.enemies <= chainSize && (best == null || m.enemies > best.enemies))
                best = m;
        }

        if (best == null) return 1f;

        float result = best.multiplier;

        // Si la cadena pasa de la última fila, el multiplicador sigue creciendo
        if (best.enemies == highestRow)
        {
            int k = chainSize - best.enemies; // enemigos por encima de la última fila
            result += extraPerEnemy * k + extraAcceleration * k * (k - 1) / 2f;
        }

        return result;
    }

    public void AddChain(int baseScore, int chainSize, float multiplier)
    {
        int total = Mathf.RoundToInt(baseScore * multiplier);
        Score += total;
        RefreshScore();

        if (comboText != null)
        {
            comboText.text = $"{chainSize} yokais  x{multiplier:0.#}\n+{total}";
            comboHideTime = Time.time + comboDisplayTime;
        }
    }

    void RefreshScore()
    {
        if (scoreText != null)
            scoreText.text = string.Format(scoreFormat, Score);
    }
}