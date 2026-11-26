using ChessGame.Core.Commands;
using ChessGame.Core.Entities;
using ChessGame.Core.Enums;

namespace ChessGame.Core.Rules
{
    public static class RuleEngine
    {
        // Kiểm tra Vua của màu `kingColor` có đang bị chiếu không
        public static bool IsKingInCheck(BoardState board, PieceColor kingColor)
        {
            var king = board.GetAllPieces(kingColor).FirstOrDefault(p => p.Type == PieceType.King);
            if (king == null) return true;

            PieceColor enemyColor = (kingColor == PieceColor.White) ? PieceColor.Black : PieceColor.White;
            foreach (var enemy in board.GetAllPieces(enemyColor))
            {
                var attacks = enemy.GetPseudoLegalMoves(board);
                if (attacks.Contains(king.CurrentPosition))
                    return true;
            }
            return false;
        }

        // Lọc toàn bộ nước đi hợp lệ (không để Vua nhà bị chiếu sau khi đi)
        public static List<MoveCommand> GetValidMovesForColor(BoardState board, PieceColor color)
        {
            var validMoves = new List<MoveCommand>();
            foreach (var piece in board.GetAllPieces(color))
            {
                foreach (var targetPos in piece.GetPseudoLegalMoves(board))
                {
                    // Xử lý phong cấp Tốt nếu đi tới hàng cuối (0 hoặc 7)
                    PieceType? promo = (piece.Type == PieceType.Pawn && (targetPos.Row == 0 || targetPos.Row == 7))
                        ? PieceType.Queen : null;

                    var cmd = new MoveCommand(piece.CurrentPosition, targetPos, piece, promo);

                    board.ApplyMove(cmd);
                    bool stillInCheck = IsKingInCheck(board, color);
                    board.UndoMove(cmd);

                    if (!stillInCheck)
                        validMoves.Add(cmd);
                }
            }
            return validMoves;
        }

        // Kiểm tra trạng thái ván đấu (Đang chơi / Chiếu / Chiếu bí / Hòa cờ)
        public static GameStatus EvaluateGameStatus(BoardState board, PieceColor turnColor)
        {
            bool inCheck = IsKingInCheck(board, turnColor);
            var validMoves = GetValidMovesForColor(board, turnColor);

            if (validMoves.Count == 0)
                return inCheck ? GameStatus.Checkmate : GameStatus.Stalemate;

            return inCheck ? GameStatus.Check : GameStatus.Playing;
        }
    }
}