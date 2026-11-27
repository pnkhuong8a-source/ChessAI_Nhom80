namespace ChessGame.Core.Entities
{
    public class PlayerProfile
    {
        public string Username { get; set; } = "Guest";
        public string PasswordHash { get; set; } = "";
        public bool IsGuest { get; set; } = true;
        public int Elo { get; set; } = 1000;
        public int Reputation { get; set; } = 100; // Bất biến: 0 <= Reputation <= 100
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public int ConsecutiveLosses { get; set; }

        public double WinRate => (Wins + Losses + Draws) == 0 ? 0 : Math.Round((double)Wins / (Wins + Losses + Draws) * 100, 1);

        public string RankTier => Elo switch
        {
            < 900 => "Đồng",
            < 1100 => "Bạc",
            < 1300 => "Vàng",
            < 1600 => "Bạch Kim",
            _ => "Kim Cương"
        };

        public bool CanPlayCasual() => Reputation >= 80;
        public bool CanPlayRanked() => Reputation >= 90;

        public void ApplyAbandonPenalty() => Reputation = Math.Max(0, Reputation - 3);
        public void RecoverReputation(int amount = 2) => Reputation = Math.Min(100, Reputation + amount);
    }
}