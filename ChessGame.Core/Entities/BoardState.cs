using System.Text;
using ChessGame.Core.Commands;
using ChessGame.Core.Enums;
using ChessGame.Core.Rules;

namespace ChessGame.Core.Entities
{
    public class BoardState
    {
        private readonly ChessPiece?[,] _grid = new ChessPiece?[8, 8];
        public PieceColor CurrentTurn { get; private set; } = PieceColor.White;

        public ChessPiece? GetPieceAt(Position pos) => pos.IsValid() ? _grid[pos.Row, pos.Col] : null;

        public void SetPieceAt(Position pos, ChessPiece? piece)
        {
            if (!pos.IsValid()) return;
            _grid[pos.Row, pos.Col] = piece;
            if (piece != null)
            {
                piece.CurrentPosition = pos;
            }
        }

        public List<ChessPiece> GetAllPieces(PieceColor color)
        {
            var pieces = new List<ChessPiece>();
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if (_grid[r, c] != null && _grid[r, c]!.Color == color)
                        pieces.Add(_grid[r, c]!);
                }
            }
            return pieces;
        }

        // Hợp đồng 1: Người 2 gọi hàm này cho AI và Highlight ô đi được
        public List<MoveCommand> GetAllValidMoves(PieceColor color)
        {
            return RuleEngine.GetValidMovesForColor(this, color);
        }

        // Hợp đồng 2: Thực thi nước đi
        public void ApplyMove(MoveCommand move)
        {
            move.CapturedPiece = GetPieceAt(move.To);
            move.WasFirstMove = move.MovedPiece.HasMoved;

            SetPieceAt(move.To, move.MovedPiece);
            SetPieceAt(move.From, null);
            move.MovedPiece.HasMoved = true;

            CurrentTurn = (CurrentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
        }

        // Hợp đồng 3: Hoàn tác nước đi
        public void UndoMove(MoveCommand move)
        {
            SetPieceAt(move.From, move.MovedPiece);
            SetPieceAt(move.To, move.CapturedPiece);
            move.MovedPiece.HasMoved = move.WasFirstMove;

            CurrentTurn = (CurrentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
        }

        // Xuất trạng thái bàn cờ hiện tại ra chuỗi FEN để lưu JSON
        public string ToFenString()
        {
            var sb = new StringBuilder();
            for (int r = 0; r < 8; r++)
            {
                int emptyCount = 0;
                for (int c = 0; c < 8; c++)
                {
                    var p = _grid[r, c];
                    if (p == null)
                    {
                        emptyCount++;
                    }
                    else
                    {
                        if (emptyCount > 0) { sb.Append(emptyCount); emptyCount = 0; }
                        char ch = p.Type switch
                        {
                            PieceType.Pawn => 'p',
                            PieceType.Rook => 'r',
                            PieceType.Knight => 'n',
                            PieceType.Bishop => 'b',
                            PieceType.Queen => 'q',
                            PieceType.King => 'k',
                            _ => '?'
                        };
                        sb.Append(p.Color == PieceColor.White ? char.ToUpper(ch) : ch);
                    }
                }
                if (emptyCount > 0) sb.Append(emptyCount);
                if (r < 7) sb.Append('/');
            }
            return sb.ToString();
        }
    }
}