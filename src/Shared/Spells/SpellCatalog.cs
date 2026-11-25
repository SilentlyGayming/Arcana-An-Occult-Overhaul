using System;
using System.Collections.Generic;

namespace SupernaturalExpanded.Journal
{
    internal enum SpellLearning { Study, Starting, NativeDisabled }

    internal enum SpellCategory { Spell, Charm, Curse, Ritual, Ability }

    internal sealed class SpellEntry
    {
        internal readonly string Id, NativeClass;
        internal readonly int Tier, MotiveIndex;
        internal readonly SpellCategory Category;
        internal readonly SpellLearning Learning;
        internal readonly float Cost;

        internal SpellEntry(string id, int tier, SpellCategory category, SpellLearning learning, string nativeClass, int motiveIndex, float cost)
        {
            Id = id; Tier = tier; Category = category; Learning = learning; NativeClass = nativeClass; MotiveIndex = motiveIndex; Cost = cost;
        }
    }

    internal static class SpellCatalog
    {
        internal const int LegendaryTier = 5;
        internal const float ConjureAppleCost = 6f;
        internal static readonly SpellEntry[] Entries = Create();
        private static readonly Dictionary<string, SpellEntry> ById = Index(Entries);

        private static SpellEntry[] Create()
        {
            List<SpellEntry> entries = new List<SpellEntry>();
            Add(entries, "good_luck", 0, SpellCategory.Charm, SpellLearning.Study, "CastGoodLuckCharm", 30f);
            Add(entries, "love", 2, SpellCategory.Charm, SpellLearning.Study, "CastLoveCharm", 48f);
            Add(entries, "sunlight", 3, SpellCategory.Charm, SpellLearning.Study, "CastSunlightCharm", 72f);
            Add(entries, "pestilence", 3, SpellCategory.Curse, SpellLearning.Study, "CastPestilenceCurse", 88f);
            Add(entries, "fire_blast", 0, SpellCategory.Spell, SpellLearning.Starting, "CastFireBlast", 12f);
            Add(entries, "ice_blast", 0, SpellCategory.Spell, SpellLearning.Starting, "CastIceBlast", 16f);
            Add(entries, "toadification", 2, SpellCategory.Curse, SpellLearning.Study, "CastToadify", 52f);
            Add(entries, "haunting", 2, SpellCategory.Curse, SpellLearning.Study, "CastHauntingCurse", 62f);
            Add(entries, "upgrade", 1, SpellCategory.Spell, SpellLearning.Study, "MagicallyUpgrade", 30f);
            Add(entries, "conversion", 1, SpellCategory.Ritual, SpellLearning.Study, "CastConvert", 45f);
            Add(entries, "restoration", 4, SpellCategory.Ritual, SpellLearning.Study, "CastRestoration", 100f);
            Add(entries, "reanimation", 4, SpellCategory.Ritual, SpellLearning.Study, "CastReanimate", 110f);
            string[] motives = { "bladder", "hunger", "energy", "hygiene", "fun", "social" };
            float[] charmCosts = { 30f, 34f, 0f, 30f, 0f, 0f };
            float[] curseCosts = { 36f, 40f, 0f, 36f, 0f, 0f };
            for (int i = 0; i < motives.Length; i++)
            {
                SpellLearning learning = i == 0 || i == 1 || i == 3 ? SpellLearning.Study : SpellLearning.NativeDisabled;
                string motive = motives[i];
                entries.Add(new SpellEntry("charm_" + motive, 2, SpellCategory.Charm, learning, "CastMotiveCharm", i, charmCosts[i]));
                entries.Add(new SpellEntry("curse_" + motive, 3, SpellCategory.Curse, learning, "CastMotiveCurse", i, curseCosts[i]));
            }
            return entries.ToArray();
        }

        private static void Add(List<SpellEntry> entries, string id, int tier, SpellCategory category, SpellLearning learning,
            string nativeClass, float cost)
        {
            entries.Add(new SpellEntry(id, tier, category, learning, nativeClass, -1, cost));
        }

        internal static SpellEntry Find(string id)
        {
            SpellEntry entry;
            return id != null && ById.TryGetValue(id, out entry) ? entry : null;
        }

        private static Dictionary<string, SpellEntry> Index(SpellEntry[] entries)
        {
            Dictionary<string, SpellEntry> result = new Dictionary<string, SpellEntry>(StringComparer.Ordinal);
            foreach (SpellEntry entry in entries) result.Add(entry.Id, entry);
            return result;
        }

        internal static bool CheatUnlockable(SpellEntry entry)
        {
            return entry != null && entry.Learning != SpellLearning.NativeDisabled && entry.Tier >= 0
                && entry.Tier < LegendaryTier;
        }

        internal static bool InnateDiscoverable(SpellEntry entry, int highestTier, bool known, bool castable)
        {
            return entry != null && castable && entry.Learning != SpellLearning.NativeDisabled && !known && entry.Tier >= 0
                && entry.Tier < LegendaryTier && entry.Tier <= highestTier;
        }

        internal static bool Discoverable(SpellEntry entry, int highestTier, bool known)
        {
            return entry != null && entry.Learning == SpellLearning.Study && !known && entry.Tier >= 0
                && entry.Tier < LegendaryTier && entry.Tier <= highestTier;
        }
    }
}
