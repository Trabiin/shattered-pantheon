// Every change to what the player owns goes through here: items earned and spent, a new game's heroes
// and battle XP. Each change is checked first (whole amounts, never spending more than the player has, no
// hero past their level cap) and saved straight away, so nothing earned is lost if the app closes.
// Plain C# with no Unity code, so the save tests run without Unity (tools/SaveTests).
namespace ShatteredPantheon.Game
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ShatteredPantheon.Battle;

    public class Wallet
    {
        readonly GameData data;
        readonly Growth growth;
        readonly Func<SaveData> save;
        readonly Action write;

        public Wallet(GameData data, Growth growth, Func<SaveData> save, Action write)
        {
            this.data = data;
            this.growth = growth;
            this.save = save;
            this.write = write;
        }

        SaveData Save => save();

        public long Count(string item) => Save.Items.TryGetValue(item, out var n) ? n : 0;

        public void Earn(IReadOnlyDictionary<string, long> items)
        {
            Check(items);
            foreach (var p in items) Save.Items[p.Key] = Count(p.Key) + p.Value;
            write();
        }

        // Takes the whole cost, or nothing if the player is short of any part of it.
        public bool Spend(IReadOnlyDictionary<string, long> cost)
        {
            Check(cost);
            if (cost.Any(p => Count(p.Key) < p.Value)) return false;
            foreach (var p in cost) Save.Items[p.Key] = Count(p.Key) - p.Value;
            write();
            return true;
        }

        static void Check(IReadOnlyDictionary<string, long> items)
        {
            foreach (var p in items)
            {
                if (!Item.All.Contains(p.Key)) throw new ArgumentException("Unknown item " + p.Key);
                if (p.Value < 0) throw new ArgumentException($"A negative amount of {p.Key}");
            }
        }

        public OwnedHero Hero(string id) => Save.Heroes.TryGetValue(id, out var h) ? h : null;
        public bool Owns(string id) => Save.Heroes.ContainsKey(id);

        // A new game's heroes, each at level 1 and their rarity's starting star rank. They aren't saved until
        // something happens: until then a reopened new game gets them again.
        public void StartingHeroes(IEnumerable<string> ids)
        {
            if (Save.Heroes.Count > 0) throw new InvalidOperationException("Starting heroes go to a new game only");
            foreach (string id in ids)
            {
                var def = data.Hero(id) ?? throw new ArgumentException("Unknown hero " + id);
                Save.Heroes[id] = new OwnedHero { Id = id, Stars = growth.StartStars(def.Rarity) };
            }
        }

        public int LevelCap(OwnedHero hero) => growth.LevelCap(hero.Stars);

        // Battle XP after a win (doc 10 section 5): shared evenly by the heroes who fought and can still
        // level. Every time a hero's XP reaches the next level they level up, without gold; a hero who
        // reaches their star rank's cap keeps no XP beyond it.
        public void ShareBattleXp(IEnumerable<string> fighters, long xp)
        {
            if (xp < 0) throw new ArgumentException("Negative battle XP");
            var growing = fighters.Distinct().Select(Hero).Where(h => h != null && h.Level < LevelCap(h)).ToList();
            if (growing.Count == 0) return;
            foreach (var hero in growing) GiveXp(hero, xp / growing.Count);
            write();
        }

        void GiveXp(OwnedHero hero, long xp)
        {
            hero.Xp += xp;
            for (long need = (long)growth.XpToNext(hero.Level); hero.Level < LevelCap(hero) && hero.Xp >= need; need = (long)growth.XpToNext(hero.Level))
            {
                hero.Xp -= need;
                hero.Level++;
            }
            if (hero.Level >= LevelCap(hero)) hero.Xp = 0;
        }
    }
}
