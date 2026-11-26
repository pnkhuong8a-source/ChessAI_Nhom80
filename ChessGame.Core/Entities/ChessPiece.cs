using ChessGame.Core.Enums;

namespace ChessGame.Core.Entities
{
    public abstract class ChessPiece
    {
        public PieceColor Color { get; }
        public PieceType Type { get; }
        public Position CurrentPosition { get; set; }
        public bool HasMoved { get; set; }

        protected ChessPiece(PieceColor color, PieceType type, Position pos)
        {
            Color = color;
            Type = type;
            CurrentPosition = pos;
            HasMoved = false;
        }

        // Đa hình: Mỗi loại quân cờ tự cài đặt cách sinh nước đi thô (chưa xét chiếu tướng)
        public abstract List<Position> GetPseudoLegalMoves(BoardState board);

        public abstract ChessPiece Clone();
    }
}