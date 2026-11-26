using ChessGame.Core.Entities;

namespace ChessGame.Core.Commands
{
    public class CommandHistory
    {
        private readonly Stack<MoveCommand> _undoStack = new();
        private readonly Stack<MoveCommand> _redoStack = new();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public IReadOnlyCollection<MoveCommand> History => _undoStack;

        public void ExecuteCommand(BoardState board, MoveCommand cmd)
        {
            board.ApplyMove(cmd);
            _undoStack.Push(cmd);
            _redoStack.Clear(); // Đi nước mới thì xóa lịch sử Redo cũ
        }

        public MoveCommand? Undo(BoardState board)
        {
            if (!CanUndo) return null;
            var cmd = _undoStack.Pop();
            board.UndoMove(cmd);
            _redoStack.Push(cmd);
            return cmd;
        }

        public MoveCommand? Redo(BoardState board)
        {
            if (!CanRedo) return null;
            var cmd = _redoStack.Pop();
            board.ApplyMove(cmd);
            _undoStack.Push(cmd);
            return cmd;
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}