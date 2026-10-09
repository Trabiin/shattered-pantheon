// Where the save lives on the device, and reading and writing it safely:
//   - save.json is replaced in one step (written to save.json.tmp first), so a crash mid-save keeps the old file;
//   - a save that can't be read is renamed to save.damaged-<UTC time>.json for checking, and the game starts new;
//   - no save at all is a new game.
// Plain C# with no Unity code; GameApp passes Unity's Application.persistentDataPath as the folder.
namespace ShatteredPantheon.Game
{
    using System;
    using System.Globalization;
    using System.IO;

    public class SaveStore
    {
        public const string FileName = "save.json";

        public readonly string Folder;
        public string FilePath => Path.Combine(Folder, FileName);

        public SaveStore(string folder) { Folder = folder; }

        // The saved game, or a new one if there is none or it can't be read. problem says what went
        // wrong and where the unreadable file was moved; it's null when the load was fine or there was no save.
        public SaveData Load(out string problem) => Load(out problem, SaveData.FromJson);

        public SaveData Load(out string problem, Func<string, SaveData> parse)
        {
            problem = null;
            if (!File.Exists(FilePath)) return new SaveData();
            try
            {
                return parse(File.ReadAllText(FilePath));
            }
            catch (Exception e) when (e is FormatException || e is IOException || e is UnauthorizedAccessException)
            {
                string kept = SetAside();
                problem = $"{e.Message} Started a new game; the old save was kept as {kept}.";
                return new SaveData();
            }
        }

        public void Save(SaveData save)
        {
            Directory.CreateDirectory(Folder);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, save.ToJson());
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }

        // Removes the save (damaged copies set aside earlier stay).
        public void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }

        string SetAside()
        {
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string name = $"save.damaged-{stamp}.json";
            for (int n = 2; File.Exists(Path.Combine(Folder, name)); n++) name = $"save.damaged-{stamp}-{n}.json";
            File.Move(FilePath, Path.Combine(Folder, name));
            return name;
        }
    }
}
