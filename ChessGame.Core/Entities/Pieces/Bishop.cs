using ChessGame.Core.Enums;

namespace ChessGame.Core.Entities.Pieces
{
    public class Bishop : ChessPiece
    {
        public Bishop(PieceColor color, Position pos) : base(color, PieceType.Bishop, pos) { }

        public override List<Position> GetPseudoLegalMoves(BoardState board)
        {
            return Rook.GetSlidingMoves(board, new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) }, CurrentPosition, Color);
        }

        public override ChessPiece Clone() => new Bishop(Color, CurrentPosition) { HasMoved = HasMoved };
    }
}