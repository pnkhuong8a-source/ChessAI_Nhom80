using ChessGame.Core.Entities;
using ChessGame.Core.Enums;

namespace ChessGame.Core.Commands
{
    public class MoveCommand
    {
        public Position From { get; }
        public Position To { get; }
        public ChessPiece MovedPiece { get; }
        public ChessPiece? CapturedPiece { get; set; }
        public PieceType? PromotionType { get; set; }
        public bool WasFirstMove { get; set; }
        public bool IsCastling { get; set; }

        public MoveCommand(Position from, Position to, ChessPiece movedPiece, PieceType? promotion = null)
        {
            From = from;
            To = to;
            MovedPiece = movedPiece;
            PromotionType = promotion;
        }

        public override string ToString() => $"{MovedPiece.Type}: {From.ToAlgebraic()} -> {To.ToAlgebraic()}";
    }
}