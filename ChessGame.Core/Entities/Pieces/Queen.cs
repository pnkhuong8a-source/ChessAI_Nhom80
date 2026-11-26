using ChessGame.Core.Enums;

namespace ChessGame.Core.Entities.Pieces
{
    public class Queen : ChessPiece
    {
        public Queen(PieceColor color, Position pos) : base(color, PieceType.Queen, pos) { }

        public override List<Position> GetPseudoLegalMoves(BoardState board)
        {
            var dirs = new[] { (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1) };
            return Rook.GetSlidingMoves(board, dirs, CurrentPosition, Color);
        }

        public override ChessPiece Clone() => new Queen(Color, CurrentPosition) { HasMoved = HasMoved };
    }
}