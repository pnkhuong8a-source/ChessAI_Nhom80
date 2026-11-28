using System.Text.Json;
using System.Text.Json.Serialization;
using ChessGame.Core.Entities;
using ChessGame.Core.Enums;

namespace ChessGame.Core.Arcade
{
    /// <summary>
    /// Bộ điều phối trung tâm cho Chế độ Giải trí (Card Chess).
    /// - Mỗi 20 nước tổng trên bàn cờ (20, 40, 60, 80) -> bốc 3 thẻ cùng cấp tương ứng (Cấp 1 -> 2 -> 3 -> 4) để chọn 1.
    /// - Cho phép tích trữ thẻ trên tay, mỗi lượt chỉ dùng tối đa 1 thẻ ở đầu lượt.
    /// </summary>
    public class ArcadeMatchManager
    {
        private readonly List<CardDefinition> _allCards;
        private readonly Dictionary<string, IBuffCard> _strategies;
        private readonly Func<PieceType, PieceColor, Position, ChessPiece> _pieceFactory;

        public ArcadePlayerState WhiteState { get; } = new(PieceColor.White);
        public ArcadePlayerState BlackState { get; } = new(PieceColor.Black);
        public List<BoardZoneEffect> ZoneEffects { get; } = new();

        public int TotalHalfMoves { get; private set; } = 0;
        public CardDefinition? LastPlayedCard { get; private set; }

        // Lưu danh sách quân bị ăn để phục vụ thẻ Hồi Sinh (C4_01)
        private readonly List<(PieceColor Color, PieceType Type)> _capturedPieces = new();

        // Lưu snapshot nước đi gần nhất để phục vụ thẻ Nghịch Đảo Thời Gian (C4_06)
        private ChessPiece?[,]? _previousBoardSnapshot;
        public bool CanRewindLastEnemyMove => _previousBoardSnapshot != null && TotalHalfMoves > 0;

        public ArcadeMatchManager(
            Func<PieceType, PieceColor, Position, ChessPiece> pieceFactory,
            string? cardsJsonPath = null)
        {
            _pieceFactory = pieceFactory;
            _allCards = LoadCardsFromJsonOrFallback(cardsJsonPath);
            _strategies = ArcadeCardRegistry.BuildAllStrategies();
        }

        public ArcadePlayerState GetPlayerState(PieceColor color)
            => color == PieceColor.White ? WhiteState : BlackState;

        public ChessPiece CreatePiece(PieceType type, PieceColor color, Position pos)
            => _pieceFactory(type, color, pos);

        public void RecordCapturedPiece(ChessPiece captured)
            => _capturedPieces.Add((captured.Color, captured.Type));

        public bool HasCapturedPiece(PieceColor color, PieceType type)
            => _capturedPieces.Any(x => x.Color == color && x.Type == type);

        public ChessPiece? PopCapturedPiece(PieceColor color, PieceType type, Position spawnPos)
        {
            int idx = _capturedPieces.FindIndex(x => x.Color == color && x.Type == type);
            if (idx < 0) return null;
            _capturedPieces.RemoveAt(idx);
            return _pieceFactory(type, color, spawnPos);
        }

        public void SaveSnapshotBeforeMove(BoardState currentBoard)
        {
            var snapshot = new ChessPiece?[8, 8];
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    snapshot[r, c] = currentBoard.GetPieceAt(new Position(r, c))?.Clone();
                }
            }
            _previousBoardSnapshot = snapshot;
        }

        public bool RewindLastEnemyMove(BoardState currentBoard, PieceColor actorColor)
        {
            if (_previousBoardSnapshot == null) return false;
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var pos = new Position(r, c);
                    var restored = _previousBoardSnapshot[r, c]?.Clone();
                    currentBoard.SetPieceAt(pos, restored);
                }
            }
            _previousBoardSnapshot = null;
            return true;
        }

        /// <summary>
        /// Gọi ngay sau khi một nước đi hoàn tất.
        /// Nếu chạm mốc 20, 40, 60, 80 nửa nước -> tự động quay 3 thẻ Cấp 1, 2, 3, 4 cho cả hai người chơi.
        /// </summary>
        public bool OnHalfMoveCompleted(BoardState board, PieceColor playerWhoJustMoved)
        {
            TotalHalfMoves++;

            var moverState = GetPlayerState(playerWhoJustMoved);
            moverState.HasPlayedCardThisTurn = false;
            moverState.ForcedPiecePosition = null;
            if (moverState.ThreatVisionTurns > 0) moverState.ThreatVisionTurns--;

            // Cập nhật thời hạn buff/debuff trên các quân cờ
            foreach (var p in board.GetAllPieces(playerWhoJustMoved))
                p.Buffs.OnTurnEndedForOwner();

            var nextPlayer = playerWhoJustMoved == PieceColor.White ? PieceColor.Black : PieceColor.White;
            foreach (var p in board.GetAllPieces(nextPlayer))
                p.Buffs.OnEnemyTurnEnded();

            // Giảm thời hạn hiệu ứng ô (Sương Mù, Cọc Chắn, Phong Ấn)
            for (int i = ZoneEffects.Count - 1; i >= 0; i--)
            {
                ZoneEffects[i].RemainingHalfTurns--;
                if (ZoneEffects[i].RemainingHalfTurns <= 0)
                    ZoneEffects.RemoveAt(i);
            }

            // Kiểm tra mốc bốc thẻ: mỗi 20 nước tổng trên bàn cờ, tối đa 4 lần
            if (TotalHalfMoves % 20 == 0 && WhiteState.MilestoneDrawsCount < 4)
            {
                RollThreeChoices(PieceColor.White);
                RollThreeChoices(PieceColor.Black);
                return true; // Báo hiệu UI mở bảng chọn 1 trong 3 thẻ
            }

            return false;
        }

        /// <summary>
        /// Quay ngẫu nhiên 3 thẻ thuộc đúng cấp bậc của lần rút hiện tại (Lần 1->Cấp 1 ... Lần 4->Cấp 4).
        /// </summary>
        public List<CardDefinition> RollThreeChoices(PieceColor playerColor)
        {
            var state = GetPlayerState(playerColor);
            if (state.MilestoneDrawsCount >= 4)
                return new List<CardDefinition>();

            int targetTier = state.MilestoneDrawsCount + 1;
            var choices = _allCards
                .Where(c => c.Tier == targetTier)
                .OrderBy(_ => Random.Shared.Next())
                .Take(3)
                .ToList();

            state.PendingChoices = choices;
            return choices;
        }

        /// <summary>
        /// Người chơi xác nhận chọn 1 trong 3 thẻ (nếu hết 15s chưa chọn, UI truyền PendingChoices[0].Id).
        /// </summary>
        public bool ConfirmCardChoice(PieceColor playerColor, string selectedCardId)
        {
            var state = GetPlayerState(playerColor);
            var picked = state.PendingChoices.FirstOrDefault(c => c.Id == selectedCardId);
            if (picked == null) return false;

            state.HandCards.Add(picked);
            state.MilestoneDrawsCount++;
            state.PendingChoices.Clear();
            return true;
        }

        /// <summary>
        /// Kích hoạt 1 thẻ trên tay ở đầu lượt. Tự động kiểm tra điều kiện hợp lệ và xóa thẻ sau khi dùng.
        /// </summary>
        public bool TryUseCard(BoardState board, PieceColor playerColor, string cardId, CardTargetContext context)
        {
            var state = GetPlayerState(playerColor);
            if (state.HasPlayedCardThisTurn) return false;

            var card = state.HandCards.FirstOrDefault(c => c.Id == cardId);
            if (card == null) return false;

            if (!_strategies.TryGetValue(card.EffectCode, out var strategy))
                return false;

            if (!strategy.CanUse(board, playerColor, context, this))
                return false;

            bool success = strategy.Execute(board, playerColor, context, this);
            if (!success) return false;

            state.HandCards.Remove(card);
            state.HasPlayedCardThisTurn = true;
            if (card.EffectCode != "COPY_LAST_ENEMY_CARD")
                LastPlayedCard = card;

            return true;
        }

        // --- Các hàm hỗ trợ nhóm thẻ thao tác bài (C1_16, C2_14, C3_12, C4_10) ---
        public bool MulliganCard(PieceColor playerColor, string discardCardId)
        {
            var state = GetPlayerState(playerColor);
            var toDiscard = state.HandCards.FirstOrDefault(c => c.Id == discardCardId && c.EffectCode != "MULLIGAN_CARD");
            if (toDiscard == null) return false;

            state.HandCards.Remove(toDiscard);
            var replacement = _allCards
                .Where(c => c.Tier is (1 or 2))
                .OrderBy(_ => Random.Shared.Next())
                .First();
            state.HandCards.Add(replacement);
            return true;
        }

        public bool CopyLastEnemyCardOrDrawTier2(PieceColor playerColor)
        {
            var state = GetPlayerState(playerColor);
            var cardToAdd = LastPlayedCard ?? _allCards.Where(c => c.Tier == 2).OrderBy(_ => Random.Shared.Next()).First();
            state.HandCards.Add(cardToAdd);
            return true;
        }

        public bool StealEnemyCardOrDrawTier3(PieceColor playerColor)
        {
            var state = GetPlayerState(playerColor);
            var enemyState = GetPlayerState(playerColor == PieceColor.White ? PieceColor.Black : PieceColor.White);

            if (enemyState.HandCards.Count > 0)
            {
                int idx = Random.Shared.Next(enemyState.HandCards.Count);
                var stolen = enemyState.HandCards[idx];
                enemyState.HandCards.RemoveAt(idx);
                state.HandCards.Add(stolen);
            }
            else
            {
                var fallback = _allCards.Where(c => c.Tier == 3).OrderBy(_ => Random.Shared.Next()).First();
                state.HandCards.Add(fallback);
            }
            return true;
        }

        public bool DrawDestinyCards(PieceColor playerColor)
        {
            var state = GetPlayerState(playerColor);
            var tier2 = _allCards.Where(c => c.Tier == 2).OrderBy(_ => Random.Shared.Next()).First();
            var tier3 = _allCards.Where(c => c.Tier == 3).OrderBy(_ => Random.Shared.Next()).First();
            state.HandCards.Add(tier2);
            state.HandCards.Add(tier3);
            return true;
        }

        private static List<CardDefinition> LoadCardsFromJsonOrFallback(string? jsonPath)
        {
            if (!string.IsNullOrWhiteSpace(jsonPath) && File.Exists(jsonPath))
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };
                var json = File.ReadAllText(jsonPath);
                var loaded = JsonSerializer.Deserialize<List<CardDefinition>>(json, options);
                if (loaded is { Count: > 0 }) return loaded;
            }
            return GetBuiltIn52Cards();
        }

        public static List<CardDefinition> GetBuiltIn52Cards() => new()
        {
            // Cấp 1 (16 thẻ)
            new() { Id = "C1_01", Name = "Bước Nhỏ", Tier = 1, EffectCode = "PAWN_SIDE_STEP", TargetType = CardTargetType.AllyPieceAndSquare, Description = "Dịch chuyển 1 Tốt đồng minh sang ngang 1 ô (trái hoặc phải)." },
            new() { Id = "C1_02", Name = "Lùi Bước", Tier = 1, EffectCode = "ALLY_STEP_BACK", TargetType = CardTargetType.AllyPieceAndSquare, Description = "Cho phép 1 quân đồng minh lùi thẳng về phía sau 1 ô." },
            new() { Id = "C1_03", Name = "Tiên Phong", Tier = 1, EffectCode = "PAWN_DOUBLE_ADVANCE", TargetType = CardTargetType.AllyPiece, Description = "1 Tốt ở hàng 3 (Trắng) hoặc hàng 6 (Đen) được tiến thẳng 2 ô." },
            new() { Id = "C1_04", Name = "Giáp Gỗ", Tier = 1, EffectCode = "PAWN_SHIELD", TargetType = CardTargetType.AllyPiece, Description = "Chọn 1 Tốt đồng minh; Tốt đó miễn nhiễm bị ăn trong 1 lượt địch." },
            new() { Id = "C1_05", Name = "Trói Chân Tốt", Tier = 1, EffectCode = "FREEZE_ENEMY_PAWN", TargetType = CardTargetType.EnemyPiece, Description = "Khóa 1 Tốt địch, không cho di chuyển trong lượt kế tiếp." },
            new() { Id = "C1_06", Name = "Sương Mù", Tier = 1, EffectCode = "FOG_SQUARE", TargetType = CardTargetType.EmptySquare, Description = "Chọn 1 ô trống; đối thủ không được dừng quân tại ô đó trong 1 lượt." },
            new() { Id = "C1_07", Name = "Lách Chéo", Tier = 1, EffectCode = "PAWN_DIAGONAL_EMPTY", TargetType = CardTargetType.AllyPiece, Description = "Chọn 1 Tốt đồng minh; lượt này Tốt được đi chéo 1 ô về phía trước vào ô trống." },
            new() { Id = "C1_08", Name = "Quân Sư", Tier = 1, EffectCode = "FREE_AI_HINT", TargetType = CardTargetType.None, Description = "Kích hoạt 1 lần AI Gợi ý nước đi tốt nhất miễn phí." },
            new() { Id = "C1_09", Name = "Đâm Thẳng", Tier = 1, EffectCode = "PAWN_FORWARD_CAPTURE", TargetType = CardTargetType.AllyPiece, Description = "1 Tốt đồng minh được phép ăn thẳng 1 ô phía trước trong lượt này." },
            new() { Id = "C1_10", Name = "Vũ Điệu Mã", Tier = 1, EffectCode = "KNIGHT_ORTHO_STEP", TargetType = CardTargetType.AllyPiece, Description = "Chọn 1 Mã; trong lượt này Mã được phép đi thẳng 1 ô (ngang/dọc)." },
            new() { Id = "C1_11", Name = "Đổi Màu Tượng", Tier = 1, EffectCode = "BISHOP_COLOR_SHIFT", TargetType = CardTargetType.AllyPieceAndSquare, Description = "Chọn 1 Tượng; Tượng được nhích ngang hoặc dọc 1 ô trống." },
            new() { Id = "C1_12", Name = "Xe Nhích Bước", Tier = 1, EffectCode = "ROOK_DIAG_STEP", TargetType = CardTargetType.AllyPiece, Description = "Chọn 1 Xe; Xe được phép đi chéo 1 ô trống trong lượt này." },
            new() { Id = "C1_13", Name = "Cọc Chắn Đường", Tier = 1, EffectCode = "PLACE_BARRICADE", TargetType = CardTargetType.EmptySquare, Description = "Đặt chướng ngại vật trên 1 ô trống ở nửa sân nhà trong 1 vòng lượt." },
            new() { Id = "C1_14", Name = "Bù Giờ", Tier = 1, EffectCode = "ADD_TIME_OR_SCORE", TargetType = CardTargetType.None, Description = "Cộng thêm +20 giây vào đồng hồ lượt hiện tại (hoặc +150 điểm)." },
            new() { Id = "C1_15", Name = "Cảnh Giác", Tier = 1, EffectCode = "REVEAL_THREATS", TargetType = CardTargetType.None, Description = "Tô sáng toàn bộ các ô đang bị quân địch khống chế trong 2 lượt." },
            new() { Id = "C1_16", Name = "Đổi Bài", Tier = 1, EffectCode = "MULLIGAN_CARD", TargetType = CardTargetType.HandCard, Description = "Hủy 1 thẻ khác trên tay và rút ngẫu nhiên 1 thẻ Cấp 1 hoặc Cấp 2." },

            // Cấp 2 (14 thẻ)
            new() { Id = "C2_01", Name = "Đóng Băng", Tier = 2, EffectCode = "FREEZE_UNIT_1", TargetType = CardTargetType.EnemyPiece, Description = "Khóa 1 quân địch, không cho đi hoặc ăn quân trong 1 lượt địch." },
            new() { Id = "C2_02", Name = "Hộ Giá", Tier = 2, EffectCode = "SHIELD_UNIT_1", TargetType = CardTargetType.AllyPiece, Description = "Chọn 1 quân đồng minh; miễn nhiễm bị ăn trong lượt tới của địch." },
            new() { Id = "C2_03", Name = "Hoán Đổi", Tier = 2, EffectCode = "SWAP_ALLIES", TargetType = CardTargetType.TwoAllyPieces, Description = "Đổi vị trí 2 quân đồng minh bất kỳ đang đứng trên bàn cờ." },
            new() { Id = "C2_04", Name = "Mã Nhảy Xa", Tier = 2, EffectCode = "KNIGHT_EXTENDED_L", TargetType = CardTargetType.AllyPiece, Description = "Chọn 1 Mã; lượt này Mã được nhảy chữ L mở rộng (3x1 ô)." },
            new() { Id = "C2_05", Name = "Tượng Xuyên Thấu", Tier = 2, EffectCode = "BISHOP_PHASE", TargetType = CardTargetType.AllyPiece, Description = "Chọn 1 Tượng; lượt này Tượng được nhảy qua tối đa 1 quân cản." },
            new() { Id = "C2_06", Name = "Xe Xung Kích", Tier = 2, EffectCode = "ROOK_JUMP_PAWN", TargetType = CardTargetType.AllyPiece, Description = "Chọn 1 Xe; lượt này Xe được nhảy qua 1 Tốt đồng minh để đi hoặc ăn." },
            new() { Id = "C2_07", Name = "Tốt Cảm Tử", Tier = 2, EffectCode = "PAWN_KAMIKAZE", TargetType = CardTargetType.AllyAndEnemyPiece, Description = "Chọn 1 Tốt mình đứng sát cạnh 1 quân địch; tiêu diệt cả hai." },
            new() { Id = "C2_08", Name = "Vua Hành Quân", Tier = 2, EffectCode = "KING_MARCH_2", TargetType = CardTargetType.None, Description = "Vua được phép đi thẳng (ngang/dọc) tối đa 2 ô trong lượt này." },
            new() { Id = "C2_09", Name = "Cạm Bẫy", Tier = 2, EffectCode = "PLACE_FREEZE_TRAP", TargetType = CardTargetType.EmptySquare, Description = "Gài bẫy bí mật lên 1 ô trống; quân địch bước vào sẽ bị Đóng Băng 1 lượt." },
            new() { Id = "C2_10", Name = "Tước Vũ Khí", Tier = 2, EffectCode = "DISARM_ENEMY", TargetType = CardTargetType.EnemyPiece, Description = "Chọn 1 quân địch; lượt tới quân đó chỉ được đi vào ô trống, cấm ăn quân." },
            new() { Id = "C2_11", Name = "Chiêu Mộ", Tier = 2, EffectCode = "RECRUIT_PAWN", TargetType = CardTargetType.EmptySquare, Description = "Triệu hồi 1 Tốt mới vào một ô trống trên hàng xuất phát của Tốt." },
            new() { Id = "C2_12", Name = "Triệu Hồi Về", Tier = 2, EffectCode = "RECALL_HOME", TargetType = CardTargetType.AllyPieceAndSquare, Description = "Đưa 1 quân đồng minh quay về đúng hàng xuất phát ban đầu của nó." },
            new() { Id = "C2_13", Name = "Khóa Đường Chéo", Tier = 2, EffectCode = "LOCK_DIAGONAL", TargetType = CardTargetType.EnemyPiece, Description = "Chọn 1 quân địch; lượt tới quân đó mất khả năng đi chéo." },
            new() { Id = "C2_14", Name = "Sao Chép", Tier = 2, EffectCode = "COPY_LAST_ENEMY_CARD", TargetType = CardTargetType.None, Description = "Tạo bản sao của thẻ bài gần nhất mà đối thủ vừa kích hoạt." },

            // Cấp 3 (12 thẻ)
            new() { Id = "C3_01", Name = "Băng Giá Vĩnh Cửu", Tier = 3, EffectCode = "FREEZE_UNIT_2", TargetType = CardTargetType.EnemyPiece, Description = "Đóng băng 1 quân địch bất kỳ (kể cả Hậu) trong 2 lượt địch." },
            new() { Id = "C3_02", Name = "Thăng Cấp Nhanh", Tier = 3, EffectCode = "QUICK_PROMOTE_MINOR", TargetType = CardTargetType.AllyPiece, Description = "Chọn 1 Tốt đã qua nửa bàn cờ; phong cấp ngay thành Mã hoặc Tượng." },
            new() { Id = "C3_03", Name = "Khiên Thánh", Tier = 3, EffectCode = "HOLY_SHIELD", TargetType = CardTargetType.AllyPiece, Description = "Chọn 1 quân mình (kể cả Hậu); miễn nhiễm bị ăn & hiệu ứng thẻ 1 vòng." },
            new() { Id = "C3_04", Name = "Dịch Chuyển", Tier = 3, EffectCode = "BLINK_3", TargetType = CardTargetType.AllyPieceAndSquare, Description = "Dịch chuyển 1 quân mình tới ô trống bất kỳ trong bán kính 3 ô." },
            new() { Id = "C3_05", Name = "Sóng Xung Kích", Tier = 3, EffectCode = "SHOCKWAVE_PUSH", TargetType = CardTargetType.AllyPiece, Description = "Chọn 1 quân mình; đẩy lùi tất cả quân địch đứng sát cạnh ra xa 1 ô." },
            new() { Id = "C3_06", Name = "Phong Ấn Vùng", Tier = 3, EffectCode = "SEAL_2X2", TargetType = CardTargetType.EmptySquare, Description = "Khóa chặt một vùng 2x2 ô trống; cấm mọi quân đi vào trong 2 lượt." },
            new() { Id = "C3_07", Name = "Đổi Hồn Tốt", Tier = 3, EffectCode = "SWAP_ALLY_ENEMY_PAWN", TargetType = CardTargetType.AllyAndEnemyPiece, Description = "Hoán đổi vị trí của 1 Tốt đồng minh với 1 Tốt của đối thủ." },
            new() { Id = "C3_08", Name = "Hậu Tàng Hình", Tier = 3, EffectCode = "QUEEN_KNIGHT_MOVE", TargetType = CardTargetType.AllyPiece, Description = "Chọn Hậu mình; lượt này Hậu có thêm khả năng nhảy chữ L như Mã." },
            new() { Id = "C3_09", Name = "Khiêu Khích", Tier = 3, EffectCode = "TAUNT_ENEMY", TargetType = CardTargetType.EnemyPiece, Description = "Chọn 1 quân địch; lượt kế tiếp đối thủ bắt buộc phải đi quân đó." },
            new() { Id = "C3_10", Name = "Thanh Tẩy", Tier = 3, EffectCode = "PURIFY_BOARD", TargetType = CardTargetType.None, Description = "Xóa mọi hiệu ứng xấu trên quân mình và gỡ bỏ Khiên của quân địch." },
            new() { Id = "C3_11", Name = "Đội Hình Phalanx", Tier = 3, EffectCode = "PHALANX_SHIELD", TargetType = CardTargetType.None, Description = "Toàn bộ Tốt đồng minh đang đứng liền kề nhau (ngang/chéo) nhận Khiên 1 lượt." },
            new() { Id = "C3_12", Name = "Đạo Chích", Tier = 3, EffectCode = "STEAL_ENEMY_CARD", TargetType = CardTargetType.None, Description = "Cướp ngẫu nhiên 1 thẻ chưa dùng trên tay đối thủ về tay mình." },

            // Cấp 4 (10 thẻ)
            new() { Id = "C4_01", Name = "Hồi Sinh Tinh Anh", Tier = 4, EffectCode = "REVIVE_MINOR", TargetType = CardTargetType.EmptySquare, Description = "Hồi sinh 1 Mã hoặc Tượng đã bị ăn đặt vào ô trống ở hàng cuối sân nhà." },
            new() { Id = "C4_02", Name = "Thăng Hoa", Tier = 4, EffectCode = "ASCEND_PAWN", TargetType = CardTargetType.AllyPiece, Description = "Chọn 1 Tốt bất kỳ trên bàn cờ; phong cấp ngay thành Xe hoặc Hậu." },
            new() { Id = "C4_03", Name = "Nước Đi Kép", Tier = 4, EffectCode = "DOUBLE_MOVE", TargetType = CardTargetType.None, Description = "Sau khi đi xong lượt này, được đi thêm 1 nước phụ bằng quân khác." },
            new() { Id = "C4_04", Name = "Bão Tuyết", Tier = 4, EffectCode = "BLIZZARD_3X3", TargetType = CardTargetType.EmptySquare, Description = "Đóng băng toàn bộ quân địch nằm trong vùng 3x3 ô trong 1 lượt địch." },
            new() { Id = "C4_05", Name = "Đổi Chỗ Ngang Tầm", Tier = 4, EffectCode = "EQUAL_VALUE_SWAP", TargetType = CardTargetType.AllyAndEnemyPiece, Description = "Hoán đổi vị trí 1 quân mình với 1 quân địch cùng giá trị vật chất." },
            new() { Id = "C4_06", Name = "Nghịch Đảo Thời Gian", Tier = 4, EffectCode = "REWIND_ENEMY_MOVE", TargetType = CardTargetType.None, Description = "Hoàn tác lại nước đi vừa xong của đối thủ và giành quyền đi tiếp." },
            new() { Id = "C4_07", Name = "Vua Xuất Chinh", Tier = 4, EffectCode = "KING_QUEEN_3", TargetType = CardTargetType.None, Description = "Lượt này Vua được phép di chuyển như Hậu (tối đa 3 ô thẳng/chéo)." },
            new() { Id = "C4_08", Name = "Phản Đòn", Tier = 4, EffectCode = "COUNTER_STRIKE", TargetType = CardTargetType.AllyPiece, Description = "Gắn ấn ký lên 1 quân mình; nếu địch ăn quân này, quân địch cũng bị hủy." },
            new() { Id = "C4_09", Name = "Nhân Bản", Tier = 4, EffectCode = "MIRROR_CLONE", TargetType = CardTargetType.AllyPieceAndSquare, Description = "Chọn 1 quân đồng minh (trừ Vua, Hậu) và tạo bản sao đặt vào ô trống hàng cuối sân nhà." },
            new() { Id = "C4_10", Name = "Thiên Mệnh", Tier = 4, EffectCode = "DESTINY_DRAW_2", TargetType = CardTargetType.None, Description = "Rút ngay lập tức 2 thẻ ngẫu nhiên (1 thẻ Cấp 2 và 1 thẻ Cấp 3) vào tay." }
        };
    }
}
