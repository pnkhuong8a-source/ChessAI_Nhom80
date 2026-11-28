using ChessGame.Core.Enums;

namespace ChessGame.Core.Rules
{
    /// <summary>
    /// Đồng hồ đếm ngược theo từng lượt đi (mặc định 60s/lượt) dùng cho Đấu Giải Trí và Đấu Thường.
    /// </summary>
    public class CasualTurnTimer
    {
        public int SecondsPerTurn { get; }
        public int RemainingSeconds { get; private set; }
        public int WhiteConsecutiveTimeouts { get; private set; }
        public int BlackConsecutiveTimeouts { get; private set; }

        public CasualTurnTimer(int secondsPerTurn = 60)
        {
            SecondsPerTurn = Math.Max(0, secondsPerTurn);
            RemainingSeconds = SecondsPerTurn;
        }

        public void AddBonusSeconds(int seconds)
        {
            if (SecondsPerTurn > 0 && seconds > 0)
                RemainingSeconds += seconds;
        }

        public bool Tick()
        {
            if (SecondsPerTurn == 0) return false;
            if (RemainingSeconds > 0) RemainingSeconds--;
            return RemainingSeconds == 0;
        }

        public void OnValidMoveMade(PieceColor playerColor)
        {
            if (playerColor == PieceColor.White) WhiteConsecutiveTimeouts = 0;
            else BlackConsecutiveTimeouts = 0;
            RemainingSeconds = SecondsPerTurn;
        }

        public bool OnTurnTimeout(PieceColor playerColor)
        {
            RemainingSeconds = SecondsPerTurn;
            if (playerColor == PieceColor.White)
            {
                WhiteConsecutiveTimeouts++;
                return WhiteConsecutiveTimeouts >= 2;
            }
            BlackConsecutiveTimeouts++;
            return BlackConsecutiveTimeouts >= 2;
        }
    }
}
