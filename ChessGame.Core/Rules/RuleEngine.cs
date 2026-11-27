using ChessGame.Core.Commands;
using ChessGame.Core.Entities;
using ChessGame.Core.Enums;

namespace ChessGame.Core.Rules
{
    public static class RuleEngine
    {
        public static bool IsSquareAttacked(BoardState board, Position target, PieceColor byAttackerColor)
        {
            foreach (var enemy in board.GetAllPieces(byAttackerColor))
            {
                if (enemy.Type == PieceType.Pawn)
                {
                    int dir = (enemy.Color == PieceColor.White) ? -1 : 1;
                    if (enemy.CurrentPosition.Row + dir == target.Row &&
                        Math.Abs(enemy.CurrentPosition.Col - target.Col) == 1)
                        return true;
                }
                else
                {
                    if (enemy.GetPseudoLegalMoves(board).Contains(target))
                        return true;
                }
            }
            return false;
        }

        public static bool IsKingInCheck(BoardState board, PieceColor kingColor)
        {
            var king = board.GetAllPieces(kingColor).FirstOrDefault(p => p.Type == PieceType.King);
            if (king == null) return true;

            PieceColor enemyColor = (kingColor == PieceColor.White) ? PieceColor.Black : PieceColor.White;
            return IsSquareAttacked(board, king.CurrentPosition, enemyColor);
        }

        public static List<MoveCommand> GetValidMovesForColor(BoardState board, PieceColor color)
        {
            var validMoves = new List<MoveCommand>();
            PieceColor enemyColor = (color == PieceColor.White) ? PieceColor.Black : PieceColor.White;

            foreach (var piece in board.GetAllPieces(color))
            {
                // 1. Các nước đi cơ bản & Phong cấp 4 quân
                foreach (var targetPos in piece.GetPseudoLegalMoves(board))
                {
                    if (piece.Type == PieceType.Pawn && (targetPos.Row == 0 || targetPos.Row == 7))
                    {
                        PieceType[] promoOptions = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };
                        foreach (var promo in promoOptions)
                            TryAddSafeMove(board, color, new MoveCommand(piece.CurrentPosition, targetPos, piece, promo), validMoves);
                    }
                    else
                    {
                        TryAddSafeMove(board, color, new MoveCommand(piece.CurrentPosition, targetPos, piece), validMoves);
                    }
                }

                // 2. Luật Bắt tốt qua đường (En Passant)
                if (piece.Type == PieceType.Pawn && board.LastMove != null)
                {
                    var last = board.LastMove;
                    if (last.MovedPiece.Type == PieceType.Pawn &&
                        Math.Abs(last.From.Row - last.To.Row) == 2 &&
                        last.To.Row == piece.CurrentPosition.Row &&
                        Math.Abs(last.To.Col - piece.CurrentPosition.Col) == 1)
                    {
                        int dir = (color == PieceColor.White) ? -1 : 1;
                        var epTarget = new Position(piece.CurrentPosition.Row + dir, last.To.Col);
                        var epCmd = new MoveCommand(piece.CurrentPosition, epTarget, piece) { IsEnPassant = true };
                        TryAddSafeMove(board, color, epCmd, validMoves);
                    }
                }

                // 3. Luật Nhập thành (Castling)
                if (piece.Type == PieceType.King && !piece.HasMoved && !IsKingInCheck(board, color))
                {
                    int row = piece.CurrentPosition.Row;
                    // Nhập thành gần (cánh Vua: cột 4 -> 6, Xe ở cột 7)
                    var kingRook = board.GetPieceAt(new Position(row, 7));
                    if (kingRook != null && kingRook.Type == PieceType.Rook && !kingRook.HasMoved &&
                        board.GetPieceAt(new Position(row, 5)) == null &&
                        board.GetPieceAt(new Position(row, 6)) == null &&
                        !IsSquareAttacked(board, new Position(row, 5), enemyColor) &&
                        !IsSquareAttacked(board, new Position(row, 6), enemyColor))
                    {
                        validMoves.Add(new MoveCommand(piece.CurrentPosition, new Position(row, 6), piece) { IsCastling = true });
                    }

                    // Nhập thành xa (cánh Hậu: cột 4 -> 2, Xe ở cột 0)
                    var queenRook = board.GetPieceAt(new Position(row, 0));
                    if (queenRook != null && queenRook.Type == PieceType.Rook && !queenRook.HasMoved &&
                        board.GetPieceAt(new Position(row, 1)) == null &&
                        board.GetPieceAt(new Position(row, 2)) == null &&
                        board.GetPieceAt(new Position(row, 3)) == null &&
                        !IsSquareAttacked(board, new Position(row, 3), enemyColor) &&
                        !IsSquareAttacked(board, new Position(row, 2), enemyColor))
                    {
                        validMoves.Add(new MoveCommand(piece.CurrentPosition, new Position(row, 2), piece) { IsCastling = true });
                    }
                }
            }

            return validMoves;
        }

        private static void TryAddSafeMove(BoardState board, PieceColor color, MoveCommand cmd, List<MoveCommand> validMoves)
        {
            board.ApplyMove(cmd);
            bool stillInCheck = IsKingInCheck(board, color);
            board.UndoMove(cmd);

            if (!stillInCheck)
                validMoves.Add(cmd);
        }

        public static bool HasInsufficientMaterial(BoardState board)
        {
            var whitePieces = board.GetAllPieces(PieceColor.White);
            var blackPieces = board.GetAllPieces(PieceColor.Black);

            if (whitePieces.Count == 1 && blackPieces.Count == 1) return true; // Vua đấu Vua
            if (whitePieces.Count == 1 && blackPieces.Count == 2 &&
                blackPieces.Any(p => p.Type == PieceType.Bishop || p.Type == PieceType.Knight)) return true;
            if (blackPieces.Count == 1 && whitePieces.Count == 2 &&
                whitePieces.Any(p => p.Type == PieceType.Bishop || p.Type == PieceType.Knight)) return true;

            return false;
        }

        public static GameStatus EvaluateGameStatus(BoardState board, PieceColor turnColor)
        {
            bool inCheck = IsKingInCheck(board, turnColor);
            var validMoves = GetValidMovesForColor(board, turnColor);

            if (validMoves.Count == 0)
                return inCheck ? GameStatus.Checkmate : GameStatus.Stalemate;

            // Các điều kiện hòa cờ chuẩn quốc tế
            if (board.HalfMoveClock >= 100 || board.IsThreefoldRepetition() || HasInsufficientMaterial(board))
                return GameStatus.Draw;

            return inCheck ? GameStatus.Check : GameStatus.Playing;
        }
    }
}