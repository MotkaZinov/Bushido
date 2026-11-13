using System;

namespace Bushido
{
    public enum Rank { None, C, B, A, S, Perfect }

    [Serializable]
    public sealed class CampaignProgress
    {
        public int version = 1;
        public int unlockedThrough = 1;
        public int currentNode;
        public int[] bestRanks = new int[5];
        public bool[] completed = new bool[5];

        public static bool IsBoss(int node) => node == 2 || node == 4;

        public bool IsUnlocked(int node) => node >= 0 && node < 5 && node <= unlockedThrough;

        public void Normalize()
        {
            unlockedThrough = Math.Max(1, Math.Min(4, unlockedThrough));
            currentNode = Math.Max(0, Math.Min(unlockedThrough, currentNode));
            if (bestRanks == null || bestRanks.Length != 5) Array.Resize(ref bestRanks, 5);
            if (completed == null || completed.Length != 5) Array.Resize(ref completed, 5);
            for (int i = 0; i < 5; i++)
                bestRanks[i] = Math.Max(0, Math.Min((int)Rank.Perfect, bestRanks[i]));
        }

        public bool Record(int node, Rank rank, bool won)
        {
            if (node < 1 || !IsUnlocked(node)) return false;
            currentNode = node;
            if (!won) return false;
            bestRanks[node] = Math.Max(bestRanks[node], (int)rank);
            bool passed = IsBoss(node) || rank >= Rank.B;
            if (passed)
            {
                completed[node] = true;
                unlockedThrough = Math.Max(unlockedThrough, Math.Min(4, node + 1));
            }
            return passed;
        }

        // Provisional rank boundaries, independent of the campaign unlock rules.
        public static Rank Grade(int hits, int missed, int wounds)
        {
            if (hits <= 0) return Rank.C;
            if (missed == 0 && wounds == 0) return Rank.Perfect;
            float accuracy = (float)hits / Math.Max(1, hits + missed);
            if (accuracy >= 0.9f && wounds <= 1) return Rank.S;
            if (accuracy >= 0.75f && wounds <= 2) return Rank.A;
            if (accuracy >= 0.55f) return Rank.B;
            return Rank.C;
        }
    }
}
