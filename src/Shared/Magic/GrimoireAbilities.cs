using System.Collections.Generic;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;

namespace SupernaturalExpanded.Journal
{
    internal static class InnateAbilities
    {
        private static readonly string[][] Fairy =
        {
            new string[] { "fairy_aura", "Arcana.Ability.fairy_aura.Name", "Arcana.Ability.fairy_aura.Description" },
            new string[] { "fairy_trick", "Arcana.Ability.fairy_trick.Name", "Arcana.Ability.fairy_trick.Description" },
            new string[] { "fairy_boon", "Arcana.Ability.fairy_boon.Name", "Arcana.Ability.fairy_boon.Description" },
            new string[] { "fairy_trap", "Arcana.Ability.fairy_trap.Name", "Arcana.Ability.fairy_trap.Description" },
            new string[] { "fairy_repair", "Arcana.Ability.fairy_repair.Name", "Arcana.Ability.fairy_repair.Description" }
        };
        private const int WishTier = 4;
        private static readonly string[] GenieLampRow = { "genie_lamp", "Arcana.Ability.genie_lamp.Name", "Arcana.Ability.genie_lamp.Description" };

        internal static List<GrimoireEntry> Entries(SimIdentity identity, SpellPowerQuote quote)
        {
            Sim actor = identity == null || identity.Description == null ? null : identity.Description.CreatedSim;
            CasterKind kind;
            if (!MagicCasters.TryKind(actor, out kind)) return new List<GrimoireEntry>();
            if (kind == CasterKind.Witch) return WitchAbilities.Entries(identity);
            List<GrimoireEntry> entries = new List<GrimoireEntry>();
            if (kind == CasterKind.Genie)
            {
                CastingRules rules = MagicCasters.ReadRules(actor);
                foreach (GenieAbility ability in GenieAbilities.All)
                {
                    if (rules.Rank < ability.Rank || ability.Id == "genie_release") continue;
                    string tier = ability.CanFail ? OverviewProviders.GetRomanNumeral(ability.Tier + 1) : "";
                    entries.Add(new GrimoireEntry(ability.Id, JournalText.Get(ability.NameKey), tier, ability.Tier,
                        ability.CostFor(rules), JournalText.Get(ability.DescriptionKey)));
                }
                AddPerkAbilities(entries, actor, rules);
                return entries;
            }
            foreach (string[] ability in Fairy) entries.Add(Entry(ability, float.NaN));
            return entries;
        }

        private static void AddPerkAbilities(List<GrimoireEntry> entries, Sim actor, CastingRules rules)
        {
            if (LampRetreat.Unlocked(actor, rules)) entries.Add(Entry(GenieLampRow, 0f, 2));
            if (GeniePerks.Owns(actor.SimDescription, "HeartsDesire") && rules.Lampbound)
            {
                for (int index = 0; index < BlessingRules.Count; index++)
                {
                    string name = BlessingRules.Names[index];
                    entries.Add(new GrimoireEntry("genie_blessing_" + name.ToLowerInvariant(), GenieBlessings.Name((Blessing)index),
                        OverviewProviders.GetRomanNumeral(4), 3, BlessingRules.CostFor(rules),
                        JournalText.Get("Arcana.Genie.Blessing." + name + ".Description")));
                }
            }
            if (rules.Rank < Rank.HighWitch || (!rules.GenieLaw && !rules.Counterattack)) return;
            WishMode mode = rules.Counterattack ? WishMode.Own : WishMode.Other;
            bool own = mode == WishMode.Own;
            GenieMagicSkill skill = GeniePower.Read(actor.SimDescription, false);
            entries.Add(new GrimoireEntry(own ? "genie_own_wish" : "genie_wish",
                JournalText.Get(own ? "Arcana.Genie.Wish.Menu.Own" : "Arcana.Genie.Wish.Menu.Other"),
                "V", WishTier, GenieWishes.Cost(actor, mode),
                JournalText.Get(own ? "Arcana.Genie.Wish.OwnDescription" : "Arcana.Genie.Wish.OtherDescription"),
                GenieWishes.CanPay(mode, rules, skill)));
        }

        private static GrimoireEntry Entry(string[] row, float cost)
        {
            return new GrimoireEntry(row[0], JournalText.Get(row[1]), "", 0, cost, JournalText.Get(row[2]));
        }

        private static GrimoireEntry Entry(string[] row, float cost, int tier)
        {
            return new GrimoireEntry(row[0], JournalText.Get(row[1]), OverviewProviders.GetRomanNumeral(tier + 1), tier, cost, JournalText.Get(row[2]));
        }
    }

    internal static class WitchAbilities
    {
        internal const string AppleId = "conjure_apple";

        internal static List<GrimoireEntry> Entries(SimIdentity identity)
        {
            List<GrimoireEntry> entries = new List<GrimoireEntry>();
            Sim actor = identity == null || identity.Description == null ? null : identity.Description.CreatedSim;
            if (WitchCastingContext.IsWitch(actor))
            {
                bool evil = actor.TraitManager != null && actor.TraitManager.HasElement(TraitNames.Evil);
                entries.Add(new GrimoireEntry(AppleId,
                    JournalText.Get(evil ? "Arcana.Ability.ConjurePoisonedApple.Name" : "Arcana.Ability.ConjureApple.Name"),
                    "I", 0, SpellCatalog.ConjureAppleCost, JournalText.Get(evil
                        ? "Arcana.Ability.ConjurePoisonedApple.Description" : "Arcana.Ability.ConjureApple.Description")));
            }
            return entries;
        }
    }
}
