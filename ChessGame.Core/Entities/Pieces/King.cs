using ChessGame.Core.Enums;

namespace ChessGame.Core.Entities.Pieces
{
    public class King : ChessPiece
    {
        public King(PieceColor color, Position pos) : base(color, PieceType.King, pos) { }

        public override List<Position> GetPseudoLegalMoves(BoardState board)
        {
            var moves = new List<Position>();
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    var next = new Position(CurrentPosition.Row + dr, CurrentPosition.Col + dc);
                    if (next.IsValid())
                    {
                        var target = board.GetPieceAt(next);
                        if (target == null || target.Color != Color)
                            moves.Add(next);
                    }
                }
            }
            return moves;
        }

        public override ChessPiece Clone() => new King(Color, CurrentPosition) { HasMoved = HasMoved };
    }
}