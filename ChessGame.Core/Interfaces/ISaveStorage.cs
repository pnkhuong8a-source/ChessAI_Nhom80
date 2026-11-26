namespace ChessGame.Core.Interfaces
{
    public record GameSaveDto(string LevelId, string FenString, int ElapsedSeconds, int HintUsedCount, DateTime SavedAt);
    public record LevelConfigDto(string LevelId, string Title, string Difficulty, string InitialFen, int BaseScore, int TimeLimitSeconds);
    public record HighScoreEntryDto(string PlayerName, string LevelId, int Score, DateTime AchievedAt);

    public interface ISaveStorage
    {
        void SaveGame(string filePath, GameSaveDto saveData);
        GameSaveDto LoadGame(string filePath);
        List<LevelConfigDto> LoadAllLevels(string folderPath);
        void SaveHighScore(string filePath, HighScoreEntryDto entry);
        List<HighScoreEntryDto> LoadTopHighScores(string filePath, int top = 10);
    }
}