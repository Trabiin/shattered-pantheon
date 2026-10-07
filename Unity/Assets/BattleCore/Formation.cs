// Default team placement, used by the simulator and until the formation picker exists.
using System.Collections.Generic;
using System.Linq;

namespace ShatteredPantheon.Battle
{
    public static class Formation
    {
        // Tanks and melee in front (up to two, tanks first), everyone else behind.
        public static List<TeamSlot> AutoPlace(GameData data, IList<string> ids)
        {
            var hs = ids.Select(id => data.Heroes.First(h => h.Id == id)).ToList();
            var front = hs.Where(h => (h.Role == "Tank" || h.Role == "Warrior") && !h.Backline)
                          .OrderBy(h => h.Role == "Tank" ? 0 : 1).Take(2).ToList();
            var rest = hs.Where(h => !front.Contains(h)).ToList();
            while (front.Count < 2 && rest.Count > 3) { front.Add(rest[0]); rest.RemoveAt(0); }
            return front.Select((h, i) => new TeamSlot(h.Id, "front", "front" + i))
                .Concat(rest.Select((h, i) => new TeamSlot(h.Id, "back", "back" + i))).ToList();
        }
    }
}
