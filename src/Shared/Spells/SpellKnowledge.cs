using System;
using System.Collections.Generic;
using System.Globalization;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.CAS;
using Sims3.SimIFace;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal static class SpellKnowledge
    {
        internal static event Action<ulong> Changed;

        internal static SpellKnowledgeSkill Read(SimDescription sim, bool create)
        {
            if (sim == null || sim.SkillManager == null) return null;
            SpellKnowledgeSkill data = sim.SkillManager.GetElement(SpellKnowledgeSkill.KnowledgeGuid) as SpellKnowledgeSkill;
            if (data == null && create)
            {
                SpellKnowledgeRegistration.Register();
                sim.SkillManager.AddElement(SpellKnowledgeSkill.KnowledgeGuid);
                data = sim.SkillManager.GetElement(SpellKnowledgeSkill.KnowledgeGuid) as SpellKnowledgeSkill;
            }
            return data;
        }

        internal static void EnsureStarting(Sim actor)
        {
            if (!WitchCastingContext.IsWitch(actor)) return;
            SpellKnowledgeSkill data = Read(actor.SimDescription, true);
            if (data == null) return;
            foreach (SpellEntry entry in SpellCatalog.Entries)
                if (entry.Learning == SpellLearning.Starting) data.Learn(entry.Id);
        }

        internal static bool Knows(Sim actor, SpellEntry entry)
        {
            if (actor == null || entry == null || entry.Learning == SpellLearning.NativeDisabled) return false;
            if (entry.Learning == SpellLearning.Starting) EnsureStarting(actor);
            SpellKnowledgeSkill data = Read(actor.SimDescription, false);
            return data != null && data.Knows(entry.Id);
        }

        internal static List<SpellEntry> Eligible(Sim actor)
        {
            List<SpellEntry> eligible = new List<SpellEntry>();
            if (MagicCasters.IsInnateCaster(actor))
            {
                int highest = MagicCasters.ReadRules(actor).HighestTier;
                SpellKnowledgeSkill known = Read(actor.SimDescription, false);
                foreach (SpellEntry entry in SpellCatalog.Entries)
                    if (SpellCatalog.InnateDiscoverable(entry, highest, known != null && known.Knows(entry.Id), SpellKnowledgeGates.InnateCastable(entry)))
                        eligible.Add(entry);
                return eligible;
            }
            if (!WitchCastingContext.IsWitch(actor)) return eligible;
            EnsureStarting(actor);
            Rank rank;
            PerkProgressSource.Witch.ReadGameplay(actor.SimDescription, out rank);
            SpellKnowledgeSkill data = Read(actor.SimDescription, false);
            foreach (SpellEntry entry in SpellCatalog.Entries)
                if (SpellCatalog.Discoverable(entry, (int)rank, data != null && data.Knows(entry.Id))) eligible.Add(entry);
            return eligible;
        }

        internal static bool Learn(Sim actor, SpellEntry entry)
        {
            if (actor == null || entry == null) return false;
            SpellKnowledgeSkill data = Read(actor.SimDescription, true);
            if (data == null || !data.Learn(entry.Id)) return false;
            RaiseChanged(actor);
            return true;
        }

        internal static int UnlockAllOrdinary(Sim actor)
        {
            if (MagicCasters.IsInnateCaster(actor))
            {
                SpellKnowledgeSkill record = Read(actor.SimDescription, true);
                if (record == null) return -1;
                int count = 0;
                int highest = MagicCasters.ReadRules(actor).HighestLearnableTier;
                foreach (SpellEntry entry in SpellCatalog.Entries)
                    if (SpellCatalog.InnateDiscoverable(entry, highest, record.Knows(entry.Id),
                        SpellKnowledgeGates.InnateCastable(entry)) && record.Learn(entry.Id)) count++;
                RaiseChanged(actor);
                return count;
            }
            if (!WitchCastingContext.IsWitch(actor)) return -1;
            EnsureStarting(actor);
            SpellKnowledgeSkill data = Read(actor.SimDescription, true);
            int learned = data == null ? -1 : data.LearnAllOrdinary();
            if (learned >= 0) RaiseChanged(actor);
            return learned;
        }

        private static void RaiseChanged(Sim actor)
        {
            Action<ulong> handler = Changed;
            if (handler != null)
            {
                try { handler(actor.SimDescription.SimDescriptionId); }
                catch (ResetException) { throw; }
                catch (Exception error) { JournalBootstrap.ReportOnce("Refreshing learned magic in the Journal", error); }
            }
        }

        internal static void Notify(Sim actor, string key, params object[] args)
        {
            actor.ShowTNSIfSelectable(JournalText.Format(key, args),
                StyledNotification.NotificationStyle.kGameMessagePositive, actor.ObjectId);
        }

        internal static string Name(SpellEntry entry) { return JournalText.Get("Arcana.Spell." + entry.Id + ".Name"); }
        internal static string Description(SpellEntry entry) { return JournalText.Get("Arcana.Spell." + entry.Id + ".Description"); }

        private static string Describe(SpellPowerQuote quote, SpellEntry entry, float cost)
        {
            string description = Description(entry);
            string key = entry.Id == "ice_blast" ? "Arcana.Spell.IceOtherTargets"
                : entry.Id == "sunlight" ? "Arcana.Spell.SunlightOtherTargets" : null;
            if (key == null) return description;
            float variant = quote.NativeVariant(entry);
            if (Math.Abs(variant - cost) < 0.005f) return description;
            return description + "\n\n" + JournalText.Format(key, variant.ToString("0.##", CultureInfo.CurrentCulture));
        }

        internal static void Populate(GrimoirePage page, SimIdentity identity)
        {
            Sim actor = identity == null || identity.Description == null ? null : identity.Description.CreatedSim;
            EnsureStarting(actor);
            SpellKnowledgeSkill data = Read(identity == null ? null : identity.Description, false);
            List<GrimoireEntry>[] categories = new List<GrimoireEntry>[GrimoirePage.CategoryCount];
            for (int i = 0; i < categories.Length; i++) categories[i] = new List<GrimoireEntry>();
            try
            {
                SpellPowerQuote quote = new SpellPowerQuote(actor);
                foreach (SpellEntry entry in SpellCatalog.Entries)
                {
                    if (data == null || !data.Knows(entry.Id) || entry.Learning == SpellLearning.NativeDisabled
                        || entry.Category == SpellCategory.Ability) continue;
                    float cost = quote.For(entry);
                    categories[(int)entry.Category].Add(new GrimoireEntry(entry.Id, Name(entry), OverviewProviders.GetRomanNumeral(entry.Tier + 1), entry.Tier,
                        cost, Describe(quote, entry, cost)));
                }
                categories[(int)SpellCategory.Ability] = InnateAbilities.Entries(identity, quote);
            }
            finally
            {
                page.SetAllEntries(categories);
            }
        }
    }
}
