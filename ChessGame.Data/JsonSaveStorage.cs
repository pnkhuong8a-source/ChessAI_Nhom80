using System.Text.Json;
using ChessGame.Core.Exceptions;
using ChessGame.Core.Interfaces;

namespace ChessGame.Data
{
    public class JsonSaveStorage : ISaveStorage
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public void SaveGame(string filePath, GameSaveDto saveData)
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(saveData, _jsonOptions);
            File.WriteAllText(filePath, json);
        }

        public GameSaveDto LoadGame(string filePath)
        {
            if (!File.Exists(filePath))
                throw new SaveCorruptedException($"Không tìm thấy tệp lưu ván đấu: {filePath}");

            try
            {
                string json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<GameSaveDto>(json);
                if (data == null || string.IsNullOrWhiteSpace(data.FenString))
                    throw new SaveCorruptedException("Dữ liệu ván đấu trong file JSON bị trống hoặc thiếu FEN.");
                return data;
            }
            catch (JsonException ex)
            {
                throw new SaveCorruptedException("Định dạng tệp JSON lưu game bị hỏng.", ex);
            }
        }

        public List<LevelConfigDto> LoadAllLevels(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new SaveCorruptedException($"Không tìm thấy thư mục màn chơi: {folderPath}");

            var levels = new List<LevelConfigDto>();
            foreach (string file in Directory.GetFiles(folderPath, "*.json").OrderBy(f => f))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var lvl = JsonSerializer.Deserialize<LevelConfigDto>(json);
                    if (lvl != null) levels.Add(lvl);
                }
                catch (JsonException ex)
                {
                    throw new SaveCorruptedException($"Lỗi đọc cấu hình màn chơi tại {file}", ex);
                }
            }
            return levels;
        }

        public void SaveHighScore(string filePath, HighScoreEntryDto entry)
        {
            var list = LoadTopHighScores(filePath, 100);
            list.Add(entry);
            var topList = list.OrderByDescending(x => x.Score).Take(10).ToList();

            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, JsonSerializer.Serialize(topList, _jsonOptions));
        }

        public List<HighScoreEntryDto> LoadTopHighScores(string filePath, int top = 10)
        {
            if (!File.Exists(filePath)) return new List<HighScoreEntryDto>();
            try
            {
                string json = File.ReadAllText(filePath);
                var list = JsonSerializer.Deserialize<List<HighScoreEntryDto>>(json) ?? new List<HighScoreEntryDto>();
                return list.OrderByDescending(x => x.Score).Take(top).ToList();
            }
            catch (JsonException ex)
            {
                throw new SaveCorruptedException("Tệp bảng xếp hạng bị hỏng.", ex);
            }
        }
    }
}