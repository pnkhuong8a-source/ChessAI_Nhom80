namespace ChessGame.Core.Rules
{
    public static class ScoreCalculator
    {
        public static int CalculateFinalScore(int baseLevelScore, int remainingSeconds, int hintUsedCount, bool isVictory)
        {
            if (!isVictory) return 0;

            int timeBonus = Math.Max(0, remainingSeconds) * 10;
            int hintPenalty = Math.Max(0, hintUsedCount) * 50;
            int total = baseLevelScore + timeBonus - hintPenalty;

            // Bảo vệ bất biến (Invariant): Điểm luôn >= 0
            return Math.Max(0, total);
        }
    }
}