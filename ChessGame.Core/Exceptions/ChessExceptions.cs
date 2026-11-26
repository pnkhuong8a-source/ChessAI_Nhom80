namespace ChessGame.Core.Exceptions
{
    public class InvalidMoveException : Exception
    {
        public InvalidMoveException(string message) : base(message) { }
    }

    public class SaveCorruptedException : Exception
    {
        public SaveCorruptedException(string message, Exception? inner = null) : base(message, inner) { }
    }
}