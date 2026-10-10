// The player's save, as it is written to the device: campaign progress (stars per battle, the
// furthest battle reached on each difficulty) and the team last used on each battle.
//
// The file is JSON split into sections ("campaign", "teams"), so later systems add their own section
// (heroes and inventory in E8-F2-S1, currencies and the daily loop in E10) without touching these.
// Reading is forgiving in one direction and strict in the other:
//   - a section or field that's missing gets its default, so an older save loads into a newer game;
//   - a section this version doesn't know is kept as it was and written back unchanged;
//   - anything that is present but malformed makes the whole file unreadable (SaveStore sets it aside).
// When a change can't be expressed by adding fields, bump Version and add an upgrade step to Upgrades.
// Plain C# with no Unity code, so the save tests run without Unity (tools/SaveTests).
namespace ShatteredPantheon.Game
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using ShatteredPantheon.Battle;

    public class SaveData
    {
        public const int CurrentVersion = 2;

        // Upgrades[i] turns a version i + 1 save into version i + 2.
        public static readonly List<Func<Dictionary<string, object>, Dictionary<string, object>>> Upgrades =
            new List<Func<Dictionary<string, object>, Dictionary<string, object>>> { RenameHeroesInTeams };

        // Version 2 (E4-F1-S1): two test heroes were renamed so no hero shares a god's name. Saved teams
        // keep them under their new ids.
        static readonly Dictionary<string, string> RenamedInVersion2 = new Dictionary<string, string> { ["pell"] = "jink", ["varkhul"] = "korvald" };

        static Dictionary<string, object> RenameHeroesInTeams(Dictionary<string, object> root)
        {
            if (!(root.TryGetValue("teams", out var t) && t is Dictionary<string, object> teams)) return root;
            var saved = new List<object> { teams.TryGetValue("last", out var last) ? last : null };
            if (teams.TryGetValue("battles", out var b) && b is Dictionary<string, object> battles) saved.AddRange(battles.Values);
            foreach (var team in saved.OfType<Dictionary<string, object>>())
                if (team.TryGetValue("heroes", out var h) && h is List<object> heroes)
                    foreach (var slot in heroes.OfType<Dictionary<string, object>>())
                        if (slot.TryGetValue("hero", out var id) && id is string old && RenamedInVersion2.TryGetValue(old, out var renamed)) slot["hero"] = renamed;
            return root;
        }

        public int Version = CurrentVersion;
        // Battle id -> the star numbers earned on it (1 = the win; 2 to 5 = challenges, from E7-F5).
        public Dictionary<string, List<int>> Stars = new Dictionary<string, List<int>>();
        // Difficulty -> the id of the furthest battle cleared on it, in campaign order.
        public Dictionary<string, string> Furthest = new Dictionary<string, string>();
        // Battle id -> the team last sent into it; LastTeam is the last team sent anywhere.
        public Dictionary<string, SavedTeam> Teams = new Dictionary<string, SavedTeam>();
        public SavedTeam LastTeam;

        // Sections written by a newer game (or a later system) that this version doesn't read.
        readonly Dictionary<string, object> otherSections = new Dictionary<string, object>();

        static readonly string[] KnownSections = { "version", "campaign", "teams" };

        public string ToJson()
        {
            var root = new Dictionary<string, object>(otherSections)
            {
                ["version"] = Version,
                ["campaign"] = new Dictionary<string, object>
                {
                    ["stars"] = Stars.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => (object)p.Value.Cast<object>().ToList()),
                    ["furthest"] = Furthest.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => (object)p.Value),
                },
                ["teams"] = new Dictionary<string, object>
                {
                    ["last"] = LastTeam?.ToJson(),
                    ["battles"] = Teams.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => (object)p.Value.ToJson()),
                },
            };
            var sb = new StringBuilder();
            Write(sb, root, "");
            return sb.Append('\n').ToString();
        }

        // Throws FormatException if the text isn't a save this version can read.
        public static SaveData FromJson(string text) => FromJson(text, CurrentVersion, Upgrades);

        // The version and upgrade steps are parameters so the tests can try an upgrade before a real one exists.
        public static SaveData FromJson(string text, int currentVersion, IList<Func<Dictionary<string, object>, Dictionary<string, object>>> upgrades)
        {
            try
            {
                if (!(Json.Parse(text) is Dictionary<string, object> root)) throw new FormatException("The save isn't a JSON object.");
                int version = root.TryGetValue("version", out var v) && v is double d && d == Math.Floor(d) ? (int)d : throw new FormatException("The save has no version number.");
                if (version < 1) throw new FormatException($"Unknown save version {version}.");
                if (version > currentVersion) throw new FormatException($"The save is version {version}, newer than this game (version {currentVersion}).");
                for (; version < currentVersion; version++)
                {
                    root = upgrades[version - 1](root);
                    root["version"] = (double)(version + 1);
                }
                return Read(root, currentVersion);
            }
            catch (Exception e) when (!(e is FormatException))
            {
                throw new FormatException("The save is damaged: " + e.Message, e);
            }
        }

        static SaveData Read(Dictionary<string, object> root, int version)
        {
            var save = new SaveData { Version = version };
            var campaign = Section(root, "campaign");
            foreach (var p in Section(campaign, "stars"))
            {
                var stars = ((List<object>)p.Value).Select(s => Convert.ToInt32((double)s)).ToList();
                if (stars.Any(s => s < 1 || s > 5) || stars.Distinct().Count() != stars.Count) throw new FormatException($"Bad stars for battle {p.Key}.");
                save.Stars[p.Key] = stars.OrderBy(s => s).ToList();
            }
            foreach (var p in Section(campaign, "furthest")) save.Furthest[p.Key] = (string)p.Value;
            var teams = Section(root, "teams");
            if (teams.TryGetValue("last", out var last) && last != null) save.LastTeam = SavedTeam.FromJson((Dictionary<string, object>)last);
            foreach (var p in Section(teams, "battles")) save.Teams[p.Key] = SavedTeam.FromJson((Dictionary<string, object>)p.Value);
            foreach (var p in root.Where(p => !KnownSections.Contains(p.Key))) save.otherSections[p.Key] = p.Value;
            return save;
        }

        static Dictionary<string, object> Section(Dictionary<string, object> parent, string name) =>
            parent.TryGetValue(name, out var s) && s != null ? (Dictionary<string, object>)s : new Dictionary<string, object>();

        // Indented JSON with keys in a fixed order, so a save is easy to read and compare when checking one by hand.
        static void Write(StringBuilder sb, object value, string indent)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case Dictionary<string, object> obj:
                    if (obj.Count == 0) { sb.Append("{}"); break; }
                    sb.Append("{\n");
                    int n = 0;
                    foreach (var p in obj)
                    {
                        sb.Append(indent).Append("  ");
                        WriteString(sb, p.Key);
                        sb.Append(": ");
                        Write(sb, p.Value, indent + "  ");
                        sb.Append(++n < obj.Count ? ",\n" : "\n");
                    }
                    sb.Append(indent).Append('}');
                    break;
                case List<object> list:
                    sb.Append('[');
                    for (int k = 0; k < list.Count; k++) { if (k > 0) sb.Append(", "); Write(sb, list[k], indent); }
                    sb.Append(']');
                    break;
                default: throw new ArgumentException("Can't write " + value.GetType().Name + " to the save.");
            }
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            sb.Append('"');
        }
    }

    // A team as saved: its formation and which hero stands in which slot.
    public class SavedTeam
    {
        public string Formation;
        public List<TeamSlot> Heroes = new List<TeamSlot>();

        public SavedTeam(string formation, IEnumerable<TeamSlot> heroes)
        {
            Formation = formation;
            Heroes = heroes.Select(h => new TeamSlot(h.Id, h.Row, h.Slot)).ToList();
        }

        public Dictionary<string, object> ToJson() => new Dictionary<string, object>
        {
            ["formation"] = Formation,
            ["heroes"] = Heroes.Select(h => (object)new Dictionary<string, object> { ["hero"] = h.Id, ["slot"] = h.Slot }).ToList(),
        };

        public static SavedTeam FromJson(Dictionary<string, object> o) => new SavedTeam(
            (string)o["formation"],
            ((List<object>)o["heroes"]).Cast<Dictionary<string, object>>().Select(h => new TeamSlot((string)h["hero"], (string)h["slot"])));
    }
}
