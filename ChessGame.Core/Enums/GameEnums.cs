namespace ChessGame.Core.Enums
{
    public enum PieceColor
    {
        White,
        Black
    }

    public enum PieceType
    {
        King,
        Queen,
        Rook,
        Bishop,
        Knight,
        Pawn
    }

    public enum GameStatus
    {
        Playing,
        Check,
        Checkmate,
        Stalemate,
        Draw
    }
}