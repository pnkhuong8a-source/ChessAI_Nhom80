namespace ChessGame.Core.Entities
{
    public record Position(int Row, int Col)
    {
        public bool IsValid() => Row >= 0 && Row < 8 && Col >= 0 && Col < 8;

        // Chuyển đổi tọa độ sang ký hiệu cờ vua chuẩn (VD: (7, 0) -> "a1")
        public string ToAlgebraic()
        {
            char file = (char)('a' + Col);
            int rank = 8 - Row;
            return $"{file}{rank}";
        }
    }
}