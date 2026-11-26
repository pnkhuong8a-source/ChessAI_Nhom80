using ChessGame.Core.Enums;

namespace ChessGame.Core.Entities.Pieces
{
    public class Pawn : ChessPiece
    {
        public Pawn(PieceColor color, Position pos) : base(color, PieceType.Pawn, pos) { }

        public override List<Position> GetPseudoLegalMoves(BoardState board)
        {
            var moves = new List<Position>();
            int dir = (Color == PieceColor.White) ? -1 : 1; // Trắng đi từ hàng 6 lên 0, Đen từ 1 xuống 7
            int startRow = (Color == PieceColor.White) ? 6 : 1;

            // 1. Đi thẳng 1 ô
            var oneStep = new Position(CurrentPosition.Row + dir, CurrentPosition.Col);
            if (oneStep.IsValid() && board.GetPieceAt(oneStep) == null)
            {
                moves.Add(oneStep);
                // 2. Đi thẳng 2 ô ở lượt đầu
                var twoSteps = new Position(CurrentPosition.Row + 2 * dir, CurrentPosition.Col);
                if (CurrentPosition.Row == startRow && board.GetPieceAt(twoSteps) == null)
                {
                    moves.Add(twoSteps);
                }
            }

            // 3. Ăn chéo trái/phải
            int[] captureCols = { CurrentPosition.Col - 1, CurrentPosition.Col + 1 };
            foreach (int c in captureCols)
            {
                var target = new Position(CurrentPosition.Row + dir, c);
                if (target.IsValid())
                {
                    var piece = board.GetPieceAt(target);
                    if (piece != null && piece.Color != Color)
                        moves.Add(target);
                }
            }
            return moves;
        }

        public override ChessPiece Clone() => new Pawn(Color, CurrentPosition) { HasMoved = HasMoved };
    }
}