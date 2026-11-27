using ChessGame.Core.Entities;

namespace ChessGame.Core.Rules
{
    public class RoomSession
    {
        public string RoomCode { get; set; } = "";
        public PlayerProfile HostPlayer { get; set; } = null!;
        public PlayerProfile? GuestPlayer { get; set; }
        public bool IsStarted => GuestPlayer != null;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class CasualRoomService
    {
        private static readonly Dictionary<string, RoomSession> _activeRooms = new();

        // 1. Tạo phòng mới -> trả về mã phòng 6 ký tự (VD: "A8K9P2")
        public static RoomSession CreateRoom(PlayerProfile host)
        {
            if (!host.CanPlayCasual())
                throw new InvalidOperationException("Điểm uy tín dưới 80, không thể tạo phòng Đấu Thường.");

            string code;
            do
            {
                code = MatchmakingAndRatingService.GenerateRoomCode();
            } while (_activeRooms.ContainsKey(code));

            var room = new RoomSession { RoomCode = code, HostPlayer = host };
            _activeRooms[code] = room;
            return room;
        }

        // 2. Nhập mã phòng để tham gia cùng bạn
        public static RoomSession JoinRoomByCode(string roomCode, PlayerProfile guest)
        {
            if (!guest.CanPlayCasual())
                throw new InvalidOperationException("Điểm uy tín dưới 80, không thể tham gia Đấu Thường.");

            string normalized = roomCode.Trim().ToUpper();
            if (!_activeRooms.TryGetValue(normalized, out var room))
                throw new KeyNotFoundException("Mã phòng không tồn tại hoặc đã đóng.");

            if (room.IsStarted)
                throw new InvalidOperationException("Phòng đã đủ 2 người chơi.");

            room.GuestPlayer = guest;
            return room;
        }

        // 3. Ghép cặp ngẫu nhiên (tìm phòng đang chờ, nếu không có thì tự tạo phòng chờ)
        public static RoomSession MatchRandom(PlayerProfile player)
        {
            var waitingRoom = _activeRooms.Values.FirstOrDefault(r => !r.IsStarted && r.HostPlayer.Username != player.Username);
            if (waitingRoom != null)
            {
                waitingRoom.GuestPlayer = player;
                return waitingRoom;
            }
            return CreateRoom(player);
        }

        public static void CloseRoom(string roomCode) => _activeRooms.Remove(roomCode.Trim().ToUpper());
    }
}