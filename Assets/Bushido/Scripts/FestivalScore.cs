using System;

namespace Bushido
{
    // A qualifying stroke earns at most one petal, even across multiple input frames.
    public sealed class FestivalScore
    {
        public const int Target = 280;
        public const float ApexSpeed = 1.15f;
        public int Points { get; private set; }
        public int Petals { get; private set; }
        public float RushLeft { get; private set; }
        public int StrokeCuts { get; private set; }
        int currentStroke = -1;
        float firstCutTime;
        bool paidPetal;
        int spoiledStroke = -1;

        public void Tick(float seconds) => RushLeft = Math.Max(0f, RushLeft - seconds);

        public void HitBomb(int stroke)
        {
            spoiledStroke = stroke;
            Petals = 0;
            RushLeft = 0f;
            Points = Math.Max(0, Points - 25);
        }

        public void Cut(int stroke, float time, bool apex)
        {
            if (stroke != currentStroke)
            {
                currentStroke = stroke;
                StrokeCuts = 0;
                firstCutTime = time;
                paidPetal = false;
            }
            bool sweeping = time - firstCutTime <= .45f;
            StrokeCuts++;
            int sweepBonus = sweeping && StrokeCuts >= 3 ? 15 : 0;
            Points += (10 + (apex ? 10 : 0) + sweepBonus) * (RushLeft > 0 ? 2 : 1);
            if (RushLeft > 0 || paidPetal || stroke == spoiledStroke || (!apex && sweepBonus == 0)) return;
            paidPetal = true;
            if (++Petals == 3)
            {
                Petals = 0;
                RushLeft = 5f;
            }
        }
    }
}
