using ChessGame.Core.Enums;

namespace ChessGame.Core.Entities.Pieces
{
    public class Rook : ChessPiece
    {
        public Rook(PieceColor color, Position pos) : base(color, PieceType.Rook, pos) { }

        public override List<Position> GetPseudoLegalMoves(BoardState board)
        {
            return GetSlidingMoves(board, new[] { (-1, 0), (1, 0), (0, -1), (0, 1) });
        }

        internal static List<Position> GetSlidingMoves(BoardState board, (int dr, int dc)[] directions, Position start, PieceColor color)
        {
            var moves = new List<Position>();
            foreach (var (dr, dc) in directions)
            {
                int r = start.Row + dr, c = start.Col + dc;
                while (r >= 0 && r < 8 && c >= 0 && c < 8)
                {
                    var pos = new Position(r, c);
                    var target = board.GetPieceAt(pos);
                    if (target == null)
                    {
                        moves.Add(pos);
                    }
                    else
                    {
                        if (target.Color != color) moves.Add(pos);
                        break; // Bị chặn đường
                    }
                    r += dr; c += dc;
                }
            }
            return moves;
        }

        private List<Position> GetSlidingMoves(BoardState board, (int dr, int dc)[] dirs)
            => GetSlidingMoves(board, dirs, CurrentPosition, Color);

        public override ChessPiece Clone() => new Rook(Color, CurrentPosition) { HasMoved = HasMoved };
    }
}