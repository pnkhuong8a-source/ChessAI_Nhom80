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
            var whitePawn = board.GetPieceAt(new Position(6, 4))!;
            var moves = whitePawn.GetPseudoLegalMoves(board);

            Assert.Equal(2, moves.Count);
            Assert.Contains(new Position(5, 4), moves);
            Assert.Contains(new Position(4, 4), moves);
        }

        [Fact]
        public void Test03_Knight_CanJumpOverPieces()
        {
            var board = PieceFactory.CreateBoardFromFen(StandardStartFen);
            var whiteKnight = board.GetPieceAt(new Position(7, 1))!;
            var moves = whiteKnight.GetPseudoLegalMoves(board);

            Assert.Equal(2, moves.Count);
            Assert.Contains(new Position(5, 0), moves);
            Assert.Contains(new Position(5, 2), moves);
        }

        [Fact]
        public void Test04_Rook_BlockedByFriendlyPieces_HasZeroMovesAtStart()
        {
            var board = PieceFactory.CreateBoardFromFen(StandardStartFen);
            var whiteRook = board.GetPieceAt(new Position(7, 0))!;
            Assert.Empty(whiteRook.GetPseudoLegalMoves(board));
        }

        [Fact]
        public void Test05_RuleEngine_DetectCheck_WhenKingAttackedByRook()
        {
            var board = PieceFactory.CreateBoardFromFen("4r3/8/8/8/8/8/8/4K3");
            Assert.True(RuleEngine.IsKingInCheck(board, PieceColor.White));
        }

        [Fact]
        public void Test06_RuleEngine_FilterMovesThatLeaveKingInCheck()
        {
            var board = PieceFactory.CreateBoardFromFen("4r3/8/8/8/8/8/4R3/4K3");
            var validMoves = board.GetAllValidMoves(PieceColor.White);

            var rookMoves = validMoves.Where(m => m.MovedPiece.Type == PieceType.Rook).ToList();
            Assert.All(rookMoves, m => Assert.Equal(4, m.To.Col));
        }

        [Fact]
        public void Test07_RuleEngine_DetectCheckmate_BackRankMate()
        {
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

            File.WriteAllText(tempFile, "{ invalid_json ");
            Assert.Throws<SaveCorruptedException>(() => storage.LoadGame(tempFile));

            File.Delete(tempFile);
        }

        [Fact]
        public void Test11_Castling_Kingside_MovesBothKingAndRook()
        {
            var board = PieceFactory.CreateBoardFromFen("4k3/8/8/8/8/8/8/4K2R");
            var validMoves = board.GetAllValidMoves(PieceColor.White);
            var castlingMove = validMoves.FirstOrDefault(m => m.IsCastling && m.To.Col == 6);

            Assert.NotNull(castlingMove);
            board.ApplyMove(castlingMove!);
            Assert.Equal(PieceType.King, board.GetPieceAt(new Position(7, 6))?.Type);
            Assert.Equal(PieceType.Rook, board.GetPieceAt(new Position(7, 5))?.Type);
        }

        [Fact]
        public void Test12_EnPassant_CapturesPawnBehindTargetSquare()
        {
            var board = PieceFactory.CreateBoardFromFen("4k3/3p4/8/4P3/8/8/8/4K3");
            var blackPawn = board.GetPieceAt(new Position(1, 3))!;
            board.ApplyMove(new MoveCommand(new Position(1, 3), new Position(3, 3), blackPawn));

            var whiteMoves = board.GetAllValidMoves(PieceColor.White);
            var epMove = whiteMoves.FirstOrDefault(m => m.IsEnPassant);

            Assert.NotNull(epMove);
            board.ApplyMove(epMove!);
            Assert.Null(board.GetPieceAt(new Position(3, 3)));
            Assert.Equal(PieceType.Pawn, board.GetPieceAt(new Position(2, 3))?.Type);
        }

        [Fact]
        public void Test13_ReputationAndRating_AbandonPenaltyAndRankLock()
        {
            var player = new PlayerProfile { Username = "Khuong", Elo = 1000 };
            Assert.True(player.CanPlayRanked());

            for (int i = 0; i < 4; i++) player.ApplyAbandonPenalty();
            Assert.Equal(88, player.Reputation);
            Assert.False(player.CanPlayRanked());
            Assert.True(player.CanPlayCasual());
        }

        [Fact]
        public void Test14_Matchmaking_PityBotTriggersAfterFiveConsecutiveLosses()
        {
            var player = new PlayerProfile { Elo = 1400, ConsecutiveLosses = 6 };
            int botLevel = MatchmakingAndRatingService.SelectBotLevelForRanked(player);
            Assert.Equal(1, botLevel);
        }
    }
}