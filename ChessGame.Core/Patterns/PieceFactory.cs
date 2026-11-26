using ChessGame.Core.Entities;
using ChessGame.Core.Entities.Pieces;
using ChessGame.Core.Enums;
using ChessGame.Core.Exceptions;

namespace ChessGame.Core.Patterns
{
    public static class PieceFactory
    {
        // Chuyển ký tự FEN ('P', 'n', 'K'...) thành đối tượng quân cờ
        public static ChessPiece CreateFromFenChar(char fenChar, Position pos)
        {
            PieceColor color = char.IsUpper(fenChar) ? PieceColor.White : PieceColor.Black;
            return char.ToLower(fenChar) switch
            {
                'p' => new Pawn(color, pos),
                'r' => new Rook(color, pos),
                'n' => new Knight(color, pos),
                'b' => new Bishop(color, pos),
                'q' => new Queen(color, pos),
                'k' => new King(color, pos),
                _ => throw new SaveCorruptedException($"Ký tự quân cờ FEN không hợp lệ: '{fenChar}'")
            };
        }

        // Dựng bàn cờ từ chuỗi FEN chuẩn (VD: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR")
        public static BoardState CreateBoardFromFen(string fen)
        {
            var board = new BoardState();
            string boardPart = fen.Split(' ')[0];
            string[] ranks = boardPart.Split('/');

            if (ranks.Length != 8)
                throw new SaveCorruptedException("Chuỗi FEN phải có đúng 8 hàng.");

            for (int r = 0; r < 8; r++)
            {
                int col = 0;
                foreach (char ch in ranks[r])
                {
                    if (char.IsDigit(ch))
                    {
                        col += (ch - '0');
                    }
                    else
                    {
                        if (col >= 8) throw new SaveCorruptedException("Số cột trong FEN vượt quá 8.");
                        board.SetPieceAt(new Position(r, col), CreateFromFenChar(ch, new Position(r, col)));
                        col++;
                    }
                }
            }
            return board;
        }
    }
}