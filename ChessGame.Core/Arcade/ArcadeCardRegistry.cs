using ChessGame.Core.Entities;
using ChessGame.Core.Enums;
using ChessGame.Core.Rules;

namespace ChessGame.Core.Arcade
{
    /// <summary>
    /// Bộ xử lý chiến thuật (Strategy Pattern) cho toàn bộ 52 thẻ bài Cấp 1 - 4.
    /// </summary>
    public class GenericBuffCardStrategy : IBuffCard
    {
        public string EffectCode { get; }
        private readonly Func<BoardState, PieceColor, CardTargetContext, ArcadeMatchManager, bool> _canUse;
        private readonly Func<BoardState, PieceColor, CardTargetContext, ArcadeMatchManager, bool> _execute;

        public GenericBuffCardStrategy(
            string effectCode,
            Func<BoardState, PieceColor, CardTargetContext, ArcadeMatchManager, bool> canUse,
            Func<BoardState, PieceColor, CardTargetContext, ArcadeMatchManager, bool> execute)
        {
            EffectCode = effectCode;
            _canUse = canUse;
            _execute = execute;
        }

        public bool CanUse(BoardState board, PieceColor playerColor, CardTargetContext context, ArcadeMatchManager match)
            => _canUse(board, playerColor, context, match);

        public bool Execute(BoardState board, PieceColor playerColor, CardTargetContext context, ArcadeMatchManager match)
            => _execute(board, playerColor, context, match);
    }

    public static class ArcadeCardRegistry
    {
        private static int GetPieceValue(PieceType type) => type switch
        {
            PieceType.Pawn => 1,
            PieceType.Knight => 3,
            PieceType.Bishop => 3,
            PieceType.Rook => 5,
            PieceType.Queen => 9,
            _ => 999
        };

        private static bool IsTargetProtectedByEnemyCard(ChessPiece piece, PieceColor actorColor)
            => piece.Color != actorColor && piece.Buffs.HasHolyShield;

        private static bool IsSafeAfterBoardAction(BoardState board, PieceColor playerColor, Action apply, Action rollback)
        {
            apply();
            bool inCheck = RuleEngine.IsKingInCheck(board, playerColor);
            if (inCheck)
            {
                rollback();
                return false;
            }
            return true;
        }

        public static Dictionary<string, IBuffCard> BuildAllStrategies()
        {
            var map = new Dictionary<string, IBuffCard>(StringComparer.OrdinalIgnoreCase);

            void Register(
                string code,
                Func<BoardState, PieceColor, CardTargetContext, ArcadeMatchManager, bool> canUse,
                Func<BoardState, PieceColor, CardTargetContext, ArcadeMatchManager, bool> exec)
            {
                map[code] = new GenericBuffCardStrategy(code, canUse, exec);
            }

            // ================= CẤP 1 =================
            Register("PAWN_SIDE_STEP",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null || ctx.SecondaryPos == null) return false;
                    var p = b.GetPieceAt(ctx.PrimaryPos);
                    return p != null && p.Color == c && p.Type == PieceType.Pawn
                        && ctx.PrimaryPos.Row == ctx.SecondaryPos.Row
                        && Math.Abs(ctx.PrimaryPos.Col - ctx.SecondaryPos.Col) == 1
                        && b.GetPieceAt(ctx.SecondaryPos) == null;
                },
                (b, c, ctx, _) =>
                {
                    var from = ctx.PrimaryPos!;
                    var to = ctx.SecondaryPos!;
                    var p = b.GetPieceAt(from)!;
                    return IsSafeAfterBoardAction(b, c,
                        () => { b.SetPieceAt(from, null); b.SetPieceAt(to, p); p.CurrentPosition = to; },
                        () => { b.SetPieceAt(to, null); b.SetPieceAt(from, p); p.CurrentPosition = from; });
                });

            Register("ALLY_STEP_BACK",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null || ctx.SecondaryPos == null) return false;
                    var p = b.GetPieceAt(ctx.PrimaryPos);
                    int backDir = (c == PieceColor.White) ? 1 : -1;
                    return p != null && p.Color == c && p.Type != PieceType.King && p.Type != PieceType.Pawn
                        && ctx.SecondaryPos.Col == ctx.PrimaryPos.Col
                        && ctx.SecondaryPos.Row == ctx.PrimaryPos.Row + backDir
                        && b.GetPieceAt(ctx.SecondaryPos) == null;
                },
                (b, c, ctx, _) =>
                {
                    var from = ctx.PrimaryPos!;
                    var to = ctx.SecondaryPos!;
                    var p = b.GetPieceAt(from)!;
                    return IsSafeAfterBoardAction(b, c,
                        () => { b.SetPieceAt(from, null); b.SetPieceAt(to, p); p.CurrentPosition = to; },
                        () => { b.SetPieceAt(to, null); b.SetPieceAt(from, p); p.CurrentPosition = from; });
                });

            Register("PAWN_DOUBLE_ADVANCE",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null) return false;
                    var p = b.GetPieceAt(ctx.PrimaryPos);
                    int expectedRow = (c == PieceColor.White) ? 5 : 2; // Hàng 3 Trắng / Hàng 6 Đen
                    int dir = (c == PieceColor.White) ? -1 : 1;
                    if (p == null || p.Color != c || p.Type != PieceType.Pawn || ctx.PrimaryPos.Row != expectedRow)
                        return false;
                    var mid = new Position(ctx.PrimaryPos.Row + dir, ctx.PrimaryPos.Col);
                    var dest = new Position(ctx.PrimaryPos.Row + 2 * dir, ctx.PrimaryPos.Col);
                    return dest.Row is >= 0 and <= 7 && b.GetPieceAt(mid) == null && b.GetPieceAt(dest) == null;
                },
                (b, c, ctx, _) =>
                {
                    var from = ctx.PrimaryPos!;
                    int dir = (c == PieceColor.White) ? -1 : 1;
                    var to = new Position(from.Row + 2 * dir, from.Col);
                    var p = b.GetPieceAt(from)!;
                    return IsSafeAfterBoardAction(b, c,
                        () => { b.SetPieceAt(from, null); b.SetPieceAt(to, p); p.CurrentPosition = to; },
                        () => { b.SetPieceAt(to, null); b.SetPieceAt(from, p); p.CurrentPosition = from; });
                });

            Register("PAWN_SHIELD",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { Type: PieceType.Pawn } p && p.Color == c,
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.HasShield = true; return true; });

            Register("FREEZE_ENEMY_PAWN",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { Type: PieceType.Pawn } p && p.Color != c && !IsTargetProtectedByEnemyCard(p, c),
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.FrozenTurns = 1; return true; });

            Register("FOG_SQUARE",
                (b, _, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) == null,
                (_, c, ctx, m) => { m.ZoneEffects.Add(new BoardZoneEffect { Type = ZoneEffectType.Fog, OwnerColor = c, Squares = new() { ctx.PrimaryPos! }, RemainingHalfTurns = 2 }); return true; });

            Register("PAWN_DIAGONAL_EMPTY",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { Type: PieceType.Pawn } p && p.Color == c,
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.ActiveMoveBuff = OneTurnMoveBuff.PawnDiagonalEmpty; return true; });

            Register("FREE_AI_HINT",
                (_, _, _, _) => true,
                (_, c, _, m) => { m.GetPlayerState(c).FreeHintAvailable = true; return true; });

            Register("PAWN_FORWARD_CAPTURE",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { Type: PieceType.Pawn } p && p.Color == c,
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.ActiveMoveBuff = OneTurnMoveBuff.PawnForwardCapture; return true; });

            Register("KNIGHT_ORTHO_STEP",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { Type: PieceType.Knight } p && p.Color == c,
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.ActiveMoveBuff = OneTurnMoveBuff.KnightOrthoStep; return true; });

            Register("BISHOP_COLOR_SHIFT",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null || ctx.SecondaryPos == null) return false;
                    var p = b.GetPieceAt(ctx.PrimaryPos);
                    int dist = Math.Abs(ctx.PrimaryPos.Row - ctx.SecondaryPos.Row) + Math.Abs(ctx.PrimaryPos.Col - ctx.SecondaryPos.Col);
                    return p != null && p.Color == c && p.Type == PieceType.Bishop && dist == 1 && b.GetPieceAt(ctx.SecondaryPos) == null;
                },
                (b, c, ctx, _) =>
                {
                    var from = ctx.PrimaryPos!;
                    var to = ctx.SecondaryPos!;
                    var p = b.GetPieceAt(from)!;
                    return IsSafeAfterBoardAction(b, c,
                        () => { b.SetPieceAt(from, null); b.SetPieceAt(to, p); p.CurrentPosition = to; },
                        () => { b.SetPieceAt(to, null); b.SetPieceAt(from, p); p.CurrentPosition = from; });
                });

            Register("ROOK_DIAG_STEP",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { Type: PieceType.Rook } p && p.Color == c,
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.ActiveMoveBuff = OneTurnMoveBuff.RookDiagStep; return true; });

            Register("PLACE_BARRICADE",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null || b.GetPieceAt(ctx.PrimaryPos) != null) return false;
                    return c == PieceColor.White ? ctx.PrimaryPos.Row >= 4 : ctx.PrimaryPos.Row <= 3;
                },
                (_, c, ctx, m) => { m.ZoneEffects.Add(new BoardZoneEffect { Type = ZoneEffectType.Barricade, OwnerColor = c, Squares = new() { ctx.PrimaryPos! }, RemainingHalfTurns = 2 }); return true; });

            Register("ADD_TIME_OR_SCORE",
                (_, _, _, _) => true,
                (_, c, _, m) => { m.GetPlayerState(c).BonusTurnSeconds += 20; return true; });

            Register("REVEAL_THREATS",
                (_, _, _, _) => true,
                (_, c, _, m) => { m.GetPlayerState(c).ThreatVisionTurns = 2; return true; });

            Register("MULLIGAN_CARD",
                (_, c, ctx, m) => m.GetPlayerState(c).HandCards.Count >= 2 && !string.IsNullOrEmpty(ctx.TargetCardId),
                (_, c, ctx, m) => m.MulliganCard(c, ctx.TargetCardId!));

            // ================= CẤP 2 =================
            Register("FREEZE_UNIT_1",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { } p && p.Color != c && p.Type is not (PieceType.King or PieceType.Queen) && !IsTargetProtectedByEnemyCard(p, c),
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.FrozenTurns = 1; return true; });

            Register("SHIELD_UNIT_1",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { } p && p.Color == c && p.Type is not (PieceType.King or PieceType.Queen),
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.HasShield = true; return true; });

            Register("SWAP_ALLIES",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null || ctx.SecondaryPos == null) return false;
                    var p1 = b.GetPieceAt(ctx.PrimaryPos);
                    var p2 = b.GetPieceAt(ctx.SecondaryPos);
                    if (p1 == null || p2 == null || p1.Color != c || p2.Color != c || p1 == p2) return false;
                    if (p1.Type == PieceType.King || p2.Type == PieceType.King) return false;
                    int promoRow = (c == PieceColor.White) ? 0 : 7;
                    if ((p1.Type == PieceType.Pawn && ctx.SecondaryPos.Row == promoRow) ||
                        (p2.Type == PieceType.Pawn && ctx.PrimaryPos.Row == promoRow)) return false;
                    return true;
                },
                (b, c, ctx, _) =>
                {
                    var pos1 = ctx.PrimaryPos!;
                    var pos2 = ctx.SecondaryPos!;
                    var p1 = b.GetPieceAt(pos1)!;
                    var p2 = b.GetPieceAt(pos2)!;
                    return IsSafeAfterBoardAction(b, c,
                        () => { b.SetPieceAt(pos1, p2); p2.CurrentPosition = pos1; b.SetPieceAt(pos2, p1); p1.CurrentPosition = pos2; },
                        () => { b.SetPieceAt(pos1, p1); p1.CurrentPosition = pos1; b.SetPieceAt(pos2, p2); p2.CurrentPosition = pos2; });
                });

            Register("KNIGHT_EXTENDED_L",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { Type: PieceType.Knight } p && p.Color == c,
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.ActiveMoveBuff = OneTurnMoveBuff.KnightExtendedL; return true; });

            Register("BISHOP_PHASE",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { Type: PieceType.Bishop } p && p.Color == c,
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.ActiveMoveBuff = OneTurnMoveBuff.BishopPhase; return true; });

            Register("ROOK_JUMP_PAWN",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { Type: PieceType.Rook } p && p.Color == c,
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.ActiveMoveBuff = OneTurnMoveBuff.RookJumpPawn; return true; });

            Register("PAWN_KAMIKAZE",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null || ctx.SecondaryPos == null) return false;
                    var ally = b.GetPieceAt(ctx.PrimaryPos);
                    var enemy = b.GetPieceAt(ctx.SecondaryPos);
                    int dist = Math.Abs(ctx.PrimaryPos.Row - ctx.SecondaryPos.Row) + Math.Abs(ctx.PrimaryPos.Col - ctx.SecondaryPos.Col);
                    return ally is { Type: PieceType.Pawn } && ally.Color == c
                        && enemy != null && enemy.Color != c && enemy.Type is not (PieceType.King or PieceType.Queen)
                        && !IsTargetProtectedByEnemyCard(enemy, c) && dist == 1;
                },
                (b, c, ctx, _) =>
                {
                    var p1Pos = ctx.PrimaryPos!;
                    var p2Pos = ctx.SecondaryPos!;
                    var ally = b.GetPieceAt(p1Pos)!;
                    var enemy = b.GetPieceAt(p2Pos)!;
                    return IsSafeAfterBoardAction(b, c,
                        () => { b.SetPieceAt(p1Pos, null); b.SetPieceAt(p2Pos, null); },
                        () => { b.SetPieceAt(p1Pos, ally); b.SetPieceAt(p2Pos, enemy); });
                });

            Register("KING_MARCH_2",
                (b, c, _, _) => b.GetAllPieces(c).Any(p => p.Type == PieceType.King),
                (b, c, _, _) => { b.GetAllPieces(c).First(p => p.Type == PieceType.King).Buffs.ActiveMoveBuff = OneTurnMoveBuff.KingMarch2; return true; });

            Register("PLACE_FREEZE_TRAP",
                (b, _, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) == null,
                (_, c, ctx, m) => { m.ZoneEffects.Add(new BoardZoneEffect { Type = ZoneEffectType.FreezeTrap, OwnerColor = c, Squares = new() { ctx.PrimaryPos! }, RemainingHalfTurns = 6 }); return true; });

            Register("DISARM_ENEMY",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { } p && p.Color != c && p.Type != PieceType.King && !IsTargetProtectedByEnemyCard(p, c),
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.DisarmedTurns = 1; return true; });

            Register("RECRUIT_PAWN",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null || b.GetPieceAt(ctx.PrimaryPos) != null) return false;
                    int pawnRow = (c == PieceColor.White) ? 6 : 1;
                    int pawnCount = b.GetAllPieces(c).Count(p => p.Type == PieceType.Pawn);
                    return ctx.PrimaryPos.Row == pawnRow && pawnCount < 8;
                },
                (b, c, ctx, m) =>
                {
                    var pos = ctx.PrimaryPos!;
                    var newPawn = m.CreatePiece(PieceType.Pawn, c, pos);
                    return IsSafeAfterBoardAction(b, c,
                        () => b.SetPieceAt(pos, newPawn),
                        () => b.SetPieceAt(pos, null));
                });

            Register("RECALL_HOME",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null || ctx.SecondaryPos == null) return false;
                    var p = b.GetPieceAt(ctx.PrimaryPos);
                    int homeRow = (c == PieceColor.White) ? (p?.Type == PieceType.Pawn ? 6 : 7) : (p?.Type == PieceType.Pawn ? 1 : 0);
                    return p != null && p.Color == c && p.Type != PieceType.King && ctx.SecondaryPos.Row == homeRow && b.GetPieceAt(ctx.SecondaryPos) == null;
                },
                (b, c, ctx, _) =>
                {
                    var from = ctx.PrimaryPos!;
                    var to = ctx.SecondaryPos!;
                    var p = b.GetPieceAt(from)!;
                    return IsSafeAfterBoardAction(b, c,
                        () => { b.SetPieceAt(from, null); b.SetPieceAt(to, p); p.CurrentPosition = to; },
                        () => { b.SetPieceAt(to, null); b.SetPieceAt(from, p); p.CurrentPosition = from; });
                });

            Register("LOCK_DIAGONAL",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { } p && p.Color != c && p.Type != PieceType.King && !IsTargetProtectedByEnemyCard(p, c),
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.DiagonalLockedTurns = 1; return true; });

            Register("COPY_LAST_ENEMY_CARD",
                (_, _, _, _) => true,
                (_, c, _, m) => m.CopyLastEnemyCardOrDrawTier2(c));

            // ================= CẤP 3 =================
            Register("FREEZE_UNIT_2",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { } p && p.Color != c && p.Type != PieceType.King && !IsTargetProtectedByEnemyCard(p, c),
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.FrozenTurns = 2; return true; });

            Register("QUICK_PROMOTE_MINOR",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null) return false;
                    var p = b.GetPieceAt(ctx.PrimaryPos);
                    bool pastHalf = (c == PieceColor.White) ? ctx.PrimaryPos.Row <= 3 : ctx.PrimaryPos.Row >= 4;
                    bool validType = ctx.PromoteToType is PieceType.Knight or PieceType.Bishop;
                    return p != null && p.Color == c && p.Type == PieceType.Pawn && pastHalf && validType;
                },
                (b, c, ctx, m) =>
                {
                    var pos = ctx.PrimaryPos!;
                    var oldPawn = b.GetPieceAt(pos)!;
                    var promoted = m.CreatePiece(ctx.PromoteToType!.Value, c, pos);
                    return IsSafeAfterBoardAction(b, c,
                        () => b.SetPieceAt(pos, promoted),
                        () => b.SetPieceAt(pos, oldPawn));
                });

            Register("HOLY_SHIELD",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { } p && p.Color == c && p.Type != PieceType.King,
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.HasHolyShield = true; return true; });

            Register("BLINK_3",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null || ctx.SecondaryPos == null) return false;
                    var p = b.GetPieceAt(ctx.PrimaryPos);
                    int dr = Math.Abs(ctx.PrimaryPos.Row - ctx.SecondaryPos.Row);
                    int dc = Math.Abs(ctx.PrimaryPos.Col - ctx.SecondaryPos.Col);
                    return p != null && p.Color == c && p.Type is not (PieceType.King or PieceType.Queen)
                        && Math.Max(dr, dc) is >= 1 and <= 3 && b.GetPieceAt(ctx.SecondaryPos) == null;
                },
                (b, c, ctx, _) =>
                {
                    var from = ctx.PrimaryPos!;
                    var to = ctx.SecondaryPos!;
                    var p = b.GetPieceAt(from)!;
                    var enemyColor = (c == PieceColor.White) ? PieceColor.Black : PieceColor.White;
                    b.SetPieceAt(from, null);
                    b.SetPieceAt(to, p);
                    p.CurrentPosition = to;
                    if (RuleEngine.IsKingInCheck(b, c) || RuleEngine.IsKingInCheck(b, enemyColor))
                    {
                        b.SetPieceAt(to, null);
                        b.SetPieceAt(from, p);
                        p.CurrentPosition = from;
                        return false;
                    }
                    return true;
                });

            Register("SHOCKWAVE_PUSH",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { } p && p.Color == c,
                (b, c, ctx, _) =>
                {
                    var center = ctx.PrimaryPos!;
                    bool pushedAny = false;
                    for (int dr = -1; dr <= 1; dr++)
                    {
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            if (dr == 0 && dc == 0) continue;
                            int r = center.Row + dr, col = center.Col + dc;
                            int nr = r + dr, ncol = col + dc;
                            if (r is < 0 or > 7 || col is < 0 or > 7 || nr is < 0 or > 7 || ncol is < 0 or > 7) continue;
                            var target = b.GetPieceAt(new Position(r, col));
                            if (target != null && target.Color != c && target.Type != PieceType.King && !IsTargetProtectedByEnemyCard(target, c)
                                && b.GetPieceAt(new Position(nr, ncol)) == null)
                            {
                                b.SetPieceAt(new Position(r, col), null);
                                var dest = new Position(nr, ncol);
                                b.SetPieceAt(dest, target);
                                target.CurrentPosition = dest;
                                pushedAny = true;
                            }
                        }
                    }
                    return pushedAny;
                });

            Register("SEAL_2X2",
                (b, _, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null) return false;
                    var tl = ctx.PrimaryPos;
                    if (tl.Row > 6 || tl.Col > 6) return false;
                    for (int r = tl.Row; r <= tl.Row + 1; r++)
                        for (int col = tl.Col; col <= tl.Col + 1; col++)
                            if (b.GetPieceAt(new Position(r, col)) != null) return false;
                    return true;
                },
                (_, c, ctx, m) =>
                {
                    var tl = ctx.PrimaryPos!;
                    var squares = new List<Position>
                    {
                        new(tl.Row, tl.Col), new(tl.Row, tl.Col + 1),
                        new(tl.Row + 1, tl.Col), new(tl.Row + 1, tl.Col + 1)
                    };
                    m.ZoneEffects.Add(new BoardZoneEffect { Type = ZoneEffectType.SealedZone, OwnerColor = c, Squares = squares, RemainingHalfTurns = 4 });
                    return true;
                });

            Register("SWAP_ALLY_ENEMY_PAWN",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null || ctx.SecondaryPos == null) return false;
                    var p1 = b.GetPieceAt(ctx.PrimaryPos);
                    var p2 = b.GetPieceAt(ctx.SecondaryPos);
                    return p1 is { Type: PieceType.Pawn } && p1.Color == c
                        && p2 is { Type: PieceType.Pawn } && p2.Color != c && !IsTargetProtectedByEnemyCard(p2, c)
                        && ctx.PrimaryPos.Row is >= 1 and <= 6 && ctx.SecondaryPos.Row is >= 1 and <= 6;
                },
                (b, c, ctx, _) =>
                {
                    var pos1 = ctx.PrimaryPos!;
                    var pos2 = ctx.SecondaryPos!;
                    var p1 = b.GetPieceAt(pos1)!;
                    var p2 = b.GetPieceAt(pos2)!;
                    return IsSafeAfterBoardAction(b, c,
                        () => { b.SetPieceAt(pos1, p2); p2.CurrentPosition = pos1; b.SetPieceAt(pos2, p1); p1.CurrentPosition = pos2; },
                        () => { b.SetPieceAt(pos1, p1); p1.CurrentPosition = pos1; b.SetPieceAt(pos2, p2); p2.CurrentPosition = pos2; });
                });

            Register("QUEEN_KNIGHT_MOVE",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { Type: PieceType.Queen } p && p.Color == c,
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.ActiveMoveBuff = OneTurnMoveBuff.QueenKnightMove; return true; });

            Register("TAUNT_ENEMY",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { } p && p.Color != c && p.Type != PieceType.King && !IsTargetProtectedByEnemyCard(p, c),
                (_, c, ctx, m) =>
                {
                    var enemyColor = (c == PieceColor.White) ? PieceColor.Black : PieceColor.White;
                    m.GetPlayerState(enemyColor).ForcedPiecePosition = ctx.PrimaryPos!;
                    return true;
                });

            Register("PURIFY_BOARD",
                (_, _, _, _) => true,
                (b, c, _, _) =>
                {
                    foreach (var ally in b.GetAllPieces(c)) ally.Buffs.ClearDebuffs();
                    var enemyColor = (c == PieceColor.White) ? PieceColor.Black : PieceColor.White;
                    foreach (var enemy in b.GetAllPieces(enemyColor)) enemy.Buffs.ClearEnemyShields();
                    return true;
                });

            Register("PHALANX_SHIELD",
                (b, c, _, _) =>
                {
                    var pawns = b.GetAllPieces(c).Where(p => p.Type == PieceType.Pawn).ToList();
                    return pawns.Any(p1 => pawns.Any(p2 => p1 != p2 && Math.Abs(p1.CurrentPosition.Col - p2.CurrentPosition.Col) == 1 && Math.Abs(p1.CurrentPosition.Row - p2.CurrentPosition.Row) <= 1));
                },
                (b, c, _, _) =>
                {
                    var pawns = b.GetAllPieces(c).Where(p => p.Type == PieceType.Pawn).ToList();
                    var linked = pawns.Where(p1 => pawns.Any(p2 => p1 != p2 && Math.Abs(p1.CurrentPosition.Col - p2.CurrentPosition.Col) == 1 && Math.Abs(p1.CurrentPosition.Row - p2.CurrentPosition.Row) <= 1)).ToList();
                    foreach (var p in linked) p.Buffs.HasShield = true;
                    return linked.Count >= 2;
                });

            Register("STEAL_ENEMY_CARD",
                (_, _, _, _) => true,
                (_, c, _, m) => m.StealEnemyCardOrDrawTier3(c));

            // ================= CẤP 4 =================
            Register("REVIVE_MINOR",
                (b, c, ctx, m) =>
                {
                    if (ctx.PrimaryPos == null || b.GetPieceAt(ctx.PrimaryPos) != null) return false;
                    int homeRow = (c == PieceColor.White) ? 7 : 0;
                    var desired = ctx.PromoteToType ?? PieceType.Knight;
                    return ctx.PrimaryPos.Row == homeRow && desired is (PieceType.Knight or PieceType.Bishop) && m.HasCapturedPiece(c, desired);
                },
                (b, c, ctx, m) =>
                {
                    var pos = ctx.PrimaryPos!;
                    var desired = ctx.PromoteToType ?? PieceType.Knight;
                    var revived = m.PopCapturedPiece(c, desired, pos);
                    if (revived == null) return false;
                    return IsSafeAfterBoardAction(b, c,
                        () => b.SetPieceAt(pos, revived),
                        () => b.SetPieceAt(pos, null));
                });

            Register("ASCEND_PAWN",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { Type: PieceType.Pawn } p && p.Color == c && ctx.PromoteToType is (PieceType.Rook or PieceType.Queen),
                (b, c, ctx, m) =>
                {
                    var pos = ctx.PrimaryPos!;
                    var oldPawn = b.GetPieceAt(pos)!;
                    var ascended = m.CreatePiece(ctx.PromoteToType!.Value, c, pos);
                    return IsSafeAfterBoardAction(b, c,
                        () => b.SetPieceAt(pos, ascended),
                        () => b.SetPieceAt(pos, oldPawn));
                });

            Register("DOUBLE_MOVE",
                (_, _, _, _) => true,
                (_, c, _, m) => { m.GetPlayerState(c).HasPendingSubMove = true; return true; });

            Register("BLIZZARD_3X3",
                (_, _, ctx, _) => ctx.PrimaryPos != null,
                (b, c, ctx, _) =>
                {
                    var center = ctx.PrimaryPos!;
                    int count = 0;
                    for (int r = Math.Max(0, center.Row - 1); r <= Math.Min(7, center.Row + 1); r++)
                    {
                        for (int col = Math.Max(0, center.Col - 1); col <= Math.Min(7, center.Col + 1); col++)
                        {
                            var p = b.GetPieceAt(new Position(r, col));
                            if (p != null && p.Color != c && p.Type != PieceType.King && !IsTargetProtectedByEnemyCard(p, c))
                            {
                                p.Buffs.FrozenTurns = 1;
                                count++;
                            }
                        }
                    }
                    return count > 0;
                });

            Register("EQUAL_VALUE_SWAP",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null || ctx.SecondaryPos == null) return false;
                    var p1 = b.GetPieceAt(ctx.PrimaryPos);
                    var p2 = b.GetPieceAt(ctx.SecondaryPos);
                    return p1 != null && p2 != null && p1.Color == c && p2.Color != c
                        && p1.Type != PieceType.King && p2.Type != PieceType.King && !IsTargetProtectedByEnemyCard(p2, c)
                        && GetPieceValue(p1.Type) == GetPieceValue(p2.Type);
                },
                (b, c, ctx, _) =>
                {
                    var pos1 = ctx.PrimaryPos!;
                    var pos2 = ctx.SecondaryPos!;
                    var p1 = b.GetPieceAt(pos1)!;
                    var p2 = b.GetPieceAt(pos2)!;
                    return IsSafeAfterBoardAction(b, c,
                        () => { b.SetPieceAt(pos1, p2); p2.CurrentPosition = pos1; b.SetPieceAt(pos2, p1); p1.CurrentPosition = pos2; },
                        () => { b.SetPieceAt(pos1, p1); p1.CurrentPosition = pos1; b.SetPieceAt(pos2, p2); p2.CurrentPosition = pos2; });
                });

            Register("REWIND_ENEMY_MOVE",
                (_, _, _, m) => m.CanRewindLastEnemyMove,
                (b, c, _, m) => m.RewindLastEnemyMove(b, c));

            Register("KING_QUEEN_3",
                (b, c, _, _) => b.GetAllPieces(c).Any(p => p.Type == PieceType.King),
                (b, c, _, _) => { b.GetAllPieces(c).First(p => p.Type == PieceType.King).Buffs.ActiveMoveBuff = OneTurnMoveBuff.KingQueen3; return true; });

            Register("COUNTER_STRIKE",
                (b, c, ctx, _) => ctx.PrimaryPos != null && b.GetPieceAt(ctx.PrimaryPos) is { } p && p.Color == c && p.Type != PieceType.King,
                (b, _, ctx, _) => { b.GetPieceAt(ctx.PrimaryPos!)!.Buffs.HasCounterStrike = true; return true; });

            Register("MIRROR_CLONE",
                (b, c, ctx, _) =>
                {
                    if (ctx.PrimaryPos == null || ctx.SecondaryPos == null) return false;
                    var src = b.GetPieceAt(ctx.PrimaryPos);
                    int homeRow = (c == PieceColor.White) ? 7 : 0;
                    return src != null && src.Color == c && src.Type is not (PieceType.King or PieceType.Queen)
                        && ctx.SecondaryPos.Row == homeRow && b.GetPieceAt(ctx.SecondaryPos) == null;
                },
                (b, c, ctx, _) =>
                {
                    var src = b.GetPieceAt(ctx.PrimaryPos!)!;
                    var dest = ctx.SecondaryPos!;
                    var clone = src.Clone();
                    clone.HasMoved = true;
                    clone.CurrentPosition = dest;
                    return IsSafeAfterBoardAction(b, c,
                        () => b.SetPieceAt(dest, clone),
                        () => b.SetPieceAt(dest, null));
                });

            Register("DESTINY_DRAW_2",
                (_, _, _, _) => true,
                (_, c, _, m) => m.DrawDestinyCards(c));

            return map;
        }
    }
}