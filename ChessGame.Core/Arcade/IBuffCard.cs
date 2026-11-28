using ChessGame.Core.Entities;
using ChessGame.Core.Enums;

namespace ChessGame.Core.Arcade
{
    public interface IBuffCard
    {
        string EffectCode { get; }
        bool CanUse(BoardState board, PieceColor playerColor, CardTargetContext context, ArcadeMatchManager match);
        bool Execute(BoardState board, PieceColor playerColor, CardTargetContext context, ArcadeMatchManager match);
    }
}
