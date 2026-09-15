using System;

namespace WildGlow
{
    internal static class StructureFilter
    {
        internal static bool Excluded(string name, bool buildingComponent, bool doorComponent, bool collectibleComponent)
        {
            // Bone rib decorations are destructible props, not necessarily Door/Piece components.
            if (name.Equals("lox_ribs", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("lox_ribs_", StringComparison.OrdinalIgnoreCase)) return true;
            if (doorComponent) return true;
            // These village props drop resources, which must not classify them as resource deposits.
            foreach (string family in new[] { "goblin_roof", "goblin_woodwall", "goblin_fence", "goblin_pole",
                "goblin_stairs", "goblin_stepladder", "goblin_bed", "goblin_banner", "goblin_totempole",
                "goblin_strawpile", "goblin_trashpile" })
                if (name.Equals(family, StringComparison.OrdinalIgnoreCase) || name.StartsWith(family + "_", StringComparison.OrdinalIgnoreCase)) return true;
            // World chests, hives, pickables and mineral nodes remain eligible even when implemented
            // as Pieces or parented beneath a building. Their existing ownership rules still apply.
            if (collectibleComponent) return false;
            if (buildingComponent) return true;
            // Destructible architectural props without building components (including dungeon doors).
            foreach (string part in name.Split('_', '-', ' '))
            {
                string word = part.TrimEnd('0','1','2','3','4','5','6','7','8','9').ToLowerInvariant();
                switch (word)
                {
                    case "roof": case "wall": case "woodwall": case "floor": case "beam": case "pole":
                    case "fence": case "stairs": case "stair": case "door": case "gate": case "irongate":
                    case "arch": case "pillar": case "column": case "bridge": case "hut": case "house":
                    case "ruin": case "ruins": case "furniture": case "bed": case "chair": case "table":
                        return true;
                }
            }
            return false;
        }
    }
}
