using ChessGame.Core.Enums;

namespace ChessGame.Core.Entities.Pieces
{
    public class Knight : ChessPiece
    {
        private static readonly (int dr, int dc)[] Offsets =
            { (-2, -1), (-2, 1), (-1, -2), (-1, 2), (1, -2), (1, 2), (2, -1), (2, 1) };

        public Knight(PieceColor color, Position pos) : base(color, PieceType.Knight, pos) { }

        public override List<Position> GetPseudoLegalMoves(BoardState board)
        {
            var moves = new List<Position>();
            foreach (var (dr, dc) in Offsets)
            {
                var next = new Position(CurrentPosition.Row + dr, CurrentPosition.Col + dc);
                if (next.IsValid())
                {
                    var target = board.GetPieceAt(next);
                    if (target == null || target.Color != Color)
                        moves.Add(next);
                }
            }
            return moves;
        }

        public override ChessPiece Clone() => new Knight(Color, CurrentPosition) { HasMoved = HasMoved };
    }
}