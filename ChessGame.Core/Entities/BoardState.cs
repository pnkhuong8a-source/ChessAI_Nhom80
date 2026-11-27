using System.Text;
using ChessGame.Core.Commands;
using ChessGame.Core.Entities.Pieces;
using ChessGame.Core.Enums;
using ChessGame.Core.Rules;

namespace ChessGame.Core.Entities
{
    public class BoardState
    {
        private readonly ChessPiece?[,] _grid = new ChessPiece?[8, 8];
        private readonly Stack<MoveCommand> _moveHistory = new();
        private readonly Dictionary<string, int> _fenRepetitionCount = new();

        public PieceColor CurrentTurn { get; private set; } = PieceColor.White;
        public MoveCommand? LastMove => _moveHistory.Count > 0 ? _moveHistory.Peek() : null;
        public int HalfMoveClock { get; private set; } = 0; // Đếm luật 50 nước (100 nửa nước)

        public ChessPiece? GetPieceAt(Position pos) => pos.IsValid() ? _grid[pos.Row, pos.Col] : null;

        public void SetPieceAt(Position pos, ChessPiece? piece)
        {
            if (!pos.IsValid()) return;
            _grid[pos.Row, pos.Col] = piece;
            if (piece != null)
                piece.CurrentPosition = pos;
        }

        public List<ChessPiece> GetAllPieces(PieceColor color)
        {
            var pieces = new List<ChessPiece>();
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    if (_grid[r, c] != null && _grid[r, c]!.Color == color)
                        pieces.Add(_grid[r, c]!);
            return pieces;
        }

        public List<MoveCommand> GetAllValidMoves(PieceColor color)
        {
            return RuleEngine.GetValidMovesForColor(this, color);
        }

        public void ApplyMove(MoveCommand move)
        {
            move.WasFirstMove = move.MovedPiece.HasMoved;
            move.PreviousHalfMoveClock = HalfMoveClock;

            // 1. Xử lý Bắt tốt qua đường (En Passant)
            if (move.IsEnPassant)
            {
                var capturedPawnPos = new Position(move.From.Row, move.To.Col);
                move.CapturedPiece = GetPieceAt(capturedPawnPos);
                SetPieceAt(capturedPawnPos, null);
            }
            else
            {
                move.CapturedPiece = GetPieceAt(move.To);
            }

            // 2. Di chuyển quân chính
            SetPieceAt(move.To, move.MovedPiece);
            SetPieceAt(move.From, null);
            move.MovedPiece.HasMoved = true;

            // 3. Xử lý Nhập thành (Castling) -> dịch chuyển thêm Xe
            if (move.IsCastling)
            {
                int row = move.From.Row;
                if (move.To.Col == 6) // Nhập thành gần (cánh Vua)
                {
                    var rook = GetPieceAt(new Position(row, 7))!;
                    SetPieceAt(new Position(row, 5), rook);
                    SetPieceAt(new Position(row, 7), null);
                    rook.HasMoved = true;
                }
                else if (move.To.Col == 2) // Nhập thành xa (cánh Hậu)
                {
                    var rook = GetPieceAt(new Position(row, 0))!;
                    SetPieceAt(new Position(row, 3), rook);
                    SetPieceAt(new Position(row, 0), null);
                    rook.HasMoved = true;
                }
            }

            // 4. Xử lý Phong cấp Tốt (Promotion)
            if (move.PromotionType != null)
            {
                ChessPiece promoted = move.PromotionType switch
                {
                    PieceType.Rook => new Rook(move.MovedPiece.Color, move.To) { HasMoved = true },
                    PieceType.Bishop => new Bishop(move.MovedPiece.Color, move.To) { HasMoved = true },
                    PieceType.Knight => new Knight(move.MovedPiece.Color, move.To) { HasMoved = true },
                    _ => new Queen(move.MovedPiece.Color, move.To) { HasMoved = true }
                };
                move.PromotedPiece = promoted;
                SetPieceAt(move.To, promoted);
            }

            // 5. Cập nhật luật 50 nước & Lịch sử lặp thế cờ
            if (move.MovedPiece.Type == PieceType.Pawn || move.CapturedPiece != null)
                HalfMoveClock = 0;
            else
                HalfMoveClock++;

            _moveHistory.Push(move);
            string fen = ToFenString();
            _fenRepetitionCount[fen] = _fenRepetitionCount.GetValueOrDefault(fen, 0) + 1;

            CurrentTurn = (CurrentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
        }

        public void UndoMove(MoveCommand move)
        {
            if (_moveHistory.Count > 0 && _moveHistory.Peek() == move)
                _moveHistory.Pop();

            string currentFen = ToFenString();
            if (_fenRepetitionCount.ContainsKey(currentFen))
                _fenRepetitionCount[currentFen] = Math.Max(0, _fenRepetitionCount[currentFen] - 1);

            // Hoàn tác quân chính (trả lại quân Tốt gốc nếu đã phong cấp)
            SetPieceAt(move.From, move.MovedPiece);
            move.MovedPiece.HasMoved = move.WasFirstMove;
            HalfMoveClock = move.PreviousHalfMoveClock;

            if (move.IsEnPassant)
            {
                SetPieceAt(move.To, null);
                SetPieceAt(new Position(move.From.Row, move.To.Col), move.CapturedPiece);
            }
            else
            {
                SetPieceAt(move.To, move.CapturedPiece);
            }

            // Hoàn tác Nhập thành
            if (move.IsCastling)
            {
                int row = move.From.Row;
                if (move.To.Col == 6)
                {
                    var rook = GetPieceAt(new Position(row, 5))!;
                    SetPieceAt(new Position(row, 7), rook);
                    SetPieceAt(new Position(row, 5), null);
                    rook.HasMoved = false;
                }
                else if (move.To.Col == 2)
                {
                    var rook = GetPieceAt(new Position(row, 3))!;
                    SetPieceAt(new Position(row, 0), rook);
                    SetPieceAt(new Position(row, 3), null);
                    rook.HasMoved = false;
                }
            }

            CurrentTurn = (CurrentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
        }

        public bool IsThreefoldRepetition() => _fenRepetitionCount.GetValueOrDefault(ToFenString(), 0) >= 3;

        public string ToFenString()
        {
            var sb = new StringBuilder();
            for (int r = 0; r < 8; r++)
            {
                int emptyCount = 0;
                for (int c = 0; c < 8; c++)
                {
                    var p = _grid[r, c];
                    if (p == null) emptyCount++;
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