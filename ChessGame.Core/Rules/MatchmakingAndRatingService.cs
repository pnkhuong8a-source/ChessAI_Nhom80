using ChessGame.Core.Entities;

namespace ChessGame.Core.Rules
{
    public static class MatchmakingAndRatingService
    {
        // 1. Cập nhật Elo, tỷ lệ thắng và điểm uy tín sau mỗi ván (result: 1.0 = Thắng, 0.5 = Hòa, 0.0 = Thua)
        public static void UpdateMatchResult(PlayerProfile player, int opponentElo, double result, bool isRanked)
        {
            player.RecoverReputation(2);

            if (result == 1.0)
            {
                player.Wins++;
                player.ConsecutiveLosses = 0;
            }
            else if (result == 0.0)
            {
                player.Losses++;
                player.ConsecutiveLosses++;
            }
            else
            {
                player.Draws++;
                player.ConsecutiveLosses = 0;
            }

            if (isRanked)
            {
                int kFactor = 32;
                double expected = 1.0 / (1.0 + Math.Pow(10, (opponentElo - player.Elo) / 400.0));
                int delta = (int)Math.Round(kFactor * (result - expected));
                player.Elo = Math.Max(100, player.Elo + delta);
            }
        }

        // 2. Sinh mã phòng 6 ký tự cho Đấu Thường
        public static string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[Random.Shared.Next(s.Length)])
                .ToArray());
        }

        // 3. Tìm đối thủ thực ngang trình trong Đấu Rank (chênh lệch <= maxEloDiff, mặc định 100)
        public static PlayerProfile? FindRankedOpponent(PlayerProfile currentPlayer, List<PlayerProfile> waitingQueue, int maxEloDiff = 100)
        {
            if (!currentPlayer.CanPlayRanked())
                throw new InvalidOperationException("Điểm uy tín dưới 90, không đủ điều kiện tham gia Đấu Rank.");

            return waitingQueue
                .Where(p => p.Username != currentPlayer.Username
                         && p.CanPlayRanked()
                         && Math.Abs(p.Elo - currentPlayer.Elo) <= maxEloDiff)
                .OrderBy(p => Math.Abs(p.Elo - currentPlayer.Elo))
                .FirstOrDefault();
        }

        // 4. Chọn cấp độ Bot (1..10) cho Fallback Bot & Pity Bot trong Đấu Rank
        public static int SelectBotLevelForRanked(PlayerProfile player)
        {
            // Pity Bot: Thua > 5 trận liên tiếp -> ghép Bot cấp 1 (Dễ)
            if (player.ConsecutiveLosses > 5) return 1;

            // Fallback Bot: Ghép Bot tương đương Elo hiện tại
            return Math.Clamp((player.Elo - 750) / 140, 1, 10);
        }
    }
}