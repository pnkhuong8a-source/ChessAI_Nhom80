using ChessGame.Core.Entities;
using ChessGame.Core.Enums;

namespace ChessGame.Core.Arcade
{
    public enum CardTargetType
    {
        None,
        AllyPiece,
        EnemyPiece,
        EmptySquare,
        AllyPieceAndSquare,
        TwoAllyPieces,
        AllyAndEnemyPiece,
        HandCard
    }

    public enum OneTurnMoveBuff
    {
        None,
        PawnStepBack,
        PawnDiagonalEmpty,
        PawnForwardCapture,
        KnightOrthoStep,
        KnightExtendedL,
        BishopPhase,
        RookDiagStep,
        RookJumpPawn,
        KingMarch2,
        KingQueen3,
        QueenKnightMove
    }

    /// <summary>
    /// Trạng thái buff/debuff gắn kèm trên mỗi quân cờ (thêm 1 property Buffs vào ChessPiece).
    /// </summary>
    public class PieceBuffFlags
    {
        public int FrozenTurns { get; set; } = 0;
        public bool IsFrozen => FrozenTurns > 0;

        public bool HasShield { get; set; } = false;
        public bool HasHolyShield { get; set; } = false; // Miễn nhiễm bị ăn + miễn nhiễm thẻ địch
        public bool HasCounterStrike { get; set; } = false; // Phản đòn khi bị ăn

        public int DisarmedTurns { get; set; } = 0; // Cấm ăn quân
        public bool IsDisarmed => DisarmedTurns > 0;

        public int DiagonalLockedTurns { get; set; } = 0; // Cấm đi chéo
        public bool IsDiagonalLocked => DiagonalLockedTurns > 0;

        public OneTurnMoveBuff ActiveMoveBuff { get; set; } = OneTurnMoveBuff.None;

        public void ClearDebuffs()
        {
            FrozenTurns = 0;
            DisarmedTurns = 0;
            DiagonalLockedTurns = 0;
        }

        public void ClearEnemyShields()
        {
            HasShield = false;
            HasHolyShield = false;
            HasCounterStrike = false;
        }

        public void OnTurnEndedForOwner()
        {
            if (FrozenTurns > 0) FrozenTurns--;
            if (DisarmedTurns > 0) DisarmedTurns--;
            if (DiagonalLockedTurns > 0) DiagonalLockedTurns--;
            ActiveMoveBuff = OneTurnMoveBuff.None;
        }

        public void OnEnemyTurnEnded()
        {
            HasShield = false;
            HasHolyShield = false;
        }
    }

    public class CardDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Tier { get; set; } = 1;
        public string EffectCode { get; set; } = "";
        public CardTargetType TargetType { get; set; } = CardTargetType.None;
        public string Description { get; set; } = "";
        public string Constraint { get; set; } = "";
    }

    public class CardTargetContext
    {
        public Position? PrimaryPos { get; set; }
        public Position? SecondaryPos { get; set; }
        public string? TargetCardId { get; set; }
        public PieceType? PromoteToType { get; set; }
    }

    public enum ZoneEffectType
    {
        Fog,        // C1_06: Cấm địch dừng quân
        Barricade,  // C1_13: Chặn cả 2 bên
        FreezeTrap, // C2_09: Bẫy đóng băng địch khi bước vào
        SealedZone  // C3_06: Phong ấn vùng 2x2
    }

    public class BoardZoneEffect
    {
        public ZoneEffectType Type { get; set; }
        public PieceColor OwnerColor { get; set; }
        public List<Position> Squares { get; set; } = new();
        public int RemainingHalfTurns { get; set; } = 2;
    }
}
