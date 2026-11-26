using ChessGame.Core.Commands;
using ChessGame.Core.Entities;
using ChessGame.Core.Entities.Pieces;
using ChessGame.Core.Enums;
using ChessGame.Core.Exceptions;
using ChessGame.Core.Interfaces;
using ChessGame.Core.Patterns;
using ChessGame.Core.Rules;
using ChessGame.Data;

namespace ChessGame.Tests
{
    public class BoardAndRuleTests
    {
        private const string StandardStartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";

        [Fact]
        public void Test01_PieceFactory_CreateBoardFromStandardFen_Has32Pieces()
        {
            var board = PieceFactory.CreateBoardFromFen(StandardStartFen);
            Assert.Equal(16, board.GetAllPieces(PieceColor.White).Count);
            Assert.Equal(16, board.GetAllPieces(PieceColor.Black).Count);
        }

        [Fact]
        public void Test02_Pawn_InitialMove_CanMoveOneOrTwoSteps()
        {
            var board = PieceFactory.CreateBoardFromFen(StandardStartFen);
            var whitePawn = board.GetPieceAt(new Position(6, 4))!; // Tốt e2
            var moves = whitePawn.GetPseudoLegalMoves(board);

            Assert.Equal(2, moves.Count);
            Assert.Contains(new Position(5, 4), moves); // e3
            Assert.Contains(new Position(4, 4), moves); // e4
        }

        [Fact]
        public void Test03_Knight_CanJumpOverPieces()
        {
            var board = PieceFactory.CreateBoardFromFen(StandardStartFen);
            var whiteKnight = board.GetPieceAt(new Position(7, 1))!; // Mã b1
            var moves = whiteKnight.GetPseudoLegalMoves(board);

            Assert.Equal(2, moves.Count);
            Assert.Contains(new Position(5, 0), moves); // a3
            Assert.Contains(new Position(5, 2), moves); // c3
        }

        [Fact]
        public void Test04_Rook_BlockedByFriendlyPieces_HasZeroMovesAtStart()
        {
            var board = PieceFactory.CreateBoardFromFen(StandardStartFen);
            var whiteRook = board.GetPieceAt(new Position(7, 0))!; // Xe a1
            Assert.Empty(whiteRook.GetPseudoLegalMoves(board));
        }

        [Fact]
        public void Test05_RuleEngine_DetectCheck_WhenKingAttackedByRook()
        {
            // Vua trắng ở e1 (7,4), Xe đen chiếu thẳng ở e8 (0,4)
            var board = PieceFactory.CreateBoardFromFen("4r3/8/8/8/8/8/8/4K3");
            Assert.True(RuleEngine.IsKingInCheck(board, PieceColor.White));
        }

        [Fact]
        public void Test06_RuleEngine_FilterMovesThatLeaveKingInCheck()
        {
            // Vua trắng e1 (7,4), Xe trắng e2 (6,4) đang che chắn cho Vua khỏi Xe đen e8 (0,4)
            var board = PieceFactory.CreateBoardFromFen("4r3/8/8/8/8/8/4R3/4K3");
            var validMoves = board.GetAllValidMoves(PieceColor.White);

            // Xe trắng e2 chỉ được đi dọc cột e (không được rời cột e vì sẽ làm Vua bị chiếu)
            var rookMoves = validMoves.Where(m => m.MovedPiece.Type == PieceType.Rook).ToList();
            Assert.All(rookMoves, m => Assert.Equal(4, m.To.Col));
        }

        [Fact]
        public void Test07_RuleEngine_DetectCheckmate_BackRankMate()
        {
            // Chiếu bí hàng ngang cuối: Vua đen g8 (0,6), Tốt đen f7,g7,h7 chặn đường, Xe trắng a8 (0,0) chiếu bí
            var board = PieceFactory.CreateBoardFromFen("R5k1/5ppp/8/8/8/8/8/4K3");
            var status = RuleEngine.EvaluateGameStatus(board, PieceColor.Black);
            Assert.Equal(GameStatus.Checkmate, status);
        }

        [Fact]
        public void Test08_CommandHistory_UndoAndRedo_RestoresExactBoardState()
        {
            var board = PieceFactory.CreateBoardFromFen(StandardStartFen);
            var history = new CommandHistory();
            var pawn = board.GetPieceAt(new Position(6, 4))!;
            var cmd = new MoveCommand(new Position(6, 4), new Position(4, 4), pawn);

            history.ExecuteCommand(board, cmd);
            Assert.Equal("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR", board.ToFenString());

            history.Undo(board);
            Assert.Equal(StandardStartFen, board.ToFenString());

            history.Redo(board);
            Assert.Equal("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR", board.ToFenString());
        }

        [Fact]
        public void Test09_ScoreCalculator_NeverReturnsNegativeScore()
        {
            int score = ScoreCalculator.CalculateFinalScore(baseLevelScore: 100, remainingSeconds: 0, hintUsedCount: 10, isVictory: true);
            Assert.Equal(0, score);
        }

        [Fact]
        public void Test10_JsonSaveStorage_SaveLoadAndCorruptedException()
        {
            var storage = new JsonSaveStorage();
            string tempFile = Path.Combine(Path.GetTempPath(), $"test_save_{Guid.NewGuid()}.json");

            var dto = new GameSaveDto("LV01", StandardStartFen, 45, 1, DateTime.UtcNow);
            storage.SaveGame(tempFile, dto);

            var loaded = storage.LoadGame(tempFile);
            Assert.Equal(StandardStartFen, loaded.FenString);
            Assert.Equal(45, loaded.ElapsedSeconds);

            // Ghi đè dữ liệu hỏng và kiểm tra Custom Exception
            File.WriteAllText(tempFile, "{ invalid_json ");
            Assert.Throws<SaveCorruptedException>(() => storage.LoadGame(tempFile));

            File.Delete(tempFile);
        }
    }
}