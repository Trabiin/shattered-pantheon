// Default team placement, used by the simulator and by the Auto button on the team screen.
using System.Collections.Generic;
using System.Linq;

namespace ShatteredPantheon.Battle
{
    public static class Formation
    {
        static readonly string[] FrontRoles = { "Tank", "Warrior" };

        // Tanks then warriors in front, everyone else behind. Uses 3-2 when there are three or more
        // front-liners and at most two others, otherwise 2-3. The sturdiest back hero stands in the middle.
        public static List<TeamSlot> AutoPlace(GameData data, IList<string> ids)
        {
            var hs = ids.Select(id => data.Hero(id)).Where(h => h != null).ToList();
            var fronts = hs.Where(h => FrontRoles.Contains(h.Role)).OrderBy(h => h.Role == "Tank" ? 0 : 1).ThenByDescending(h => h.Stats.Get("hp")).ToList();
            string formation = fronts.Count >= 3 && hs.Count - 3 <= 2 ? "3-2" : "2-3";
            int frontCount = formation == "3-2" ? 3 : 2, backCount = 5 - frontCount;
            var front = fronts.Take(frontCount).ToList();
            var rest = hs.Where(h => !front.Contains(h)).ToList();
            while (front.Count < frontCount && rest.Count > backCount) { front.Add(rest[0]); rest.RemoveAt(0); }
            return Place(front, Battle.SlotsOf(formation).Where(s => s.StartsWith("front")).ToList())
                .Concat(Place(rest, Battle.SlotsOf(formation).Where(s => s.StartsWith("back")).ToList())).ToList();
        }

        // Fills a row from the middle outwards, sturdiest first.
        static IEnumerable<TeamSlot> Place(List<UnitDef> heroes, List<string> slots)
        {
            var order = slots.Count == 3 ? new[] { 1, 0, 2 } : new[] { 0, 1 };
            var byToughness = heroes.OrderByDescending(h => h.Stats.Get("hp") + 10 * h.Stats.Get("armour")).ToList();
            for (int i = 0; i < byToughness.Count && i < order.Length; i++)
                yield return new TeamSlot(byToughness[i].Id, slots[order[i]]);
        }
    }
}
