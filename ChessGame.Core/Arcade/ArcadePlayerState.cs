using ChessGame.Core.Entities;
using ChessGame.Core.Enums;

namespace ChessGame.Core.Arcade
{
    public class ArcadePlayerState
    {
        public PieceColor Color { get; }
        public int MilestoneDrawsCount { get; set; } = 0; // 0..4 tương ứng các mốc 20, 40, 60, 80 nước
        public List<CardDefinition> PendingChoices { get; set; } = new(); // 3 thẻ chờ chọn 1
        public List<CardDefinition> HandCards { get; set; } = new();      // Thẻ đang giữ trên tay
        public bool HasPlayedCardThisTurn { get; set; } = false;

        // Các hiệu ứng đặc biệt cấp người chơi
        public Position? ForcedPiecePosition { get; set; } = null; // C3_09: Khiêu Khích
        public int BonusTurnSeconds { get; set; } = 0;             // C1_14: Bù Giờ (+20s)
        public int ThreatVisionTurns { get; set; } = 0;            // C1_15: Cảnh Giác (2 lượt)
        public bool HasPendingSubMove { get; set; } = false;       // C4_03: Nước Đi Kép
        public bool FreeHintAvailable { get; set; } = false;       // C1_08: Quân Sư

        public ArcadePlayerState(PieceColor color)
        {
            Color = color;
        }
    }
}
