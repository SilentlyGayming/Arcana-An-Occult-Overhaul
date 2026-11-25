using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal static class GenieTree
    {
        internal const uint Unbound = 0xFFC23B3B;
        internal const uint Lampbound = 0xFF2874C6;
        internal const uint Shared = 0xFFAA8C35;

        internal static readonly OccultTree Tree = Create();

        private static OccultTree Create()
        {
            OccultTree tree = new OccultTree(0x53E14000, "Arcana.Genie.Perk.", GenieProgressStore.Ledger,
                JournalText.Get("Arcana.LifeState.Name.Genie"), JournalText.Get("Arcana.Journal.Perks.Points"),
                JournalText.Get("Arcana.Genie.Perks.PointsHelp"),
                new[]
                {
                    JournalText.Get("Arcana.Genie.Rank.Short.UnChained"),
                    JournalText.Get("Arcana.Genie.Rank.Short.Awakened"),
                    JournalText.Get("Arcana.Genie.Rank.Short.Ascendant"),
                    JournalText.Get("Arcana.Genie.Rank.Short.Transcendent"),
                    JournalText.Get("Arcana.Genie.Rank.Short.Almighty"),
                },
                new[]
                {
                    JournalText.Get("Arcana.Genie.Rank.Full.UnChained"),
                    JournalText.Get("Arcana.Genie.Rank.Full.Awakened"),
                    JournalText.Get("Arcana.Genie.Rank.Full.Ascendant"),
                    JournalText.Get("Arcana.Genie.Rank.Full.Transcendent"),
                    JournalText.Get("Arcana.Genie.Rank.Full.Almighty"),
                },
                CreatePerks());
            tree.RankUnlocks = new[]
            {
                "Arcana.Genie.Rank.Unlock.1", "Arcana.Genie.Rank.Unlock.2", "Arcana.Genie.Rank.Unlock.3",
                "Arcana.Genie.Rank.Unlock.4", "Arcana.Genie.Rank.Unlock.5"
            };
            tree.Benefits = GeniePerkBenefits.For;
            tree.Paths = new[]
            {
                new PerkPath(JournalText.Get("Arcana.Genie.Path.Power"), Find(tree.Perks, "Unbound")),
                new PerkPath(JournalText.Get("Arcana.Genie.Path.Genie"), Find(tree.Perks, "Lampbound"))
            };
            tree.Connections = new[]
            {
                Link(tree, Shared, "DomesticDjinn,AThousandAndOneNights,FinePrint", "Unbound,Lampbound"),
                Link(tree, Unbound, "Unbound", "RisingTide,Wellspring"),
                Link(tree, Unbound, "RisingTide,Wellspring", "FugitiveOfOld"),
                Link(tree, Unbound, "FugitiveOfOld", "Unleashed"),
                new PerkConnection(Find(tree.Perks, "Unleashed"), Find(tree.Perks, "Counterattack"), Unbound),
                Link(tree, Lampbound, "Lampbound", "PocketPalace,SilkenCommand"),
                Link(tree, Lampbound, "PocketPalace,SilkenCommand", "HeartsDesire"),
                Link(tree, Lampbound, "HeartsDesire", "GenieLaw"),
                new PerkConnection(Find(tree.Perks, "GenieLaw"), Find(tree.Perks, "LawOfTheLamp"), Lampbound)
            };
            return tree;
        }

        private static Perk[] CreatePerks()
        {
            Perk[] perks = new Perk[]
            {
                Power("DomesticDjinn", Rank.Novice, 0.5f, JournalText.Get("Arcana.Genie.Perk.DomesticDjinn.Name"), 2),
                Power("AThousandAndOneNights", Rank.Novice, 1.5f, JournalText.Get("Arcana.Genie.Perk.AThousandAndOneNights.Name"), 1, 1, 1),
                Power("FinePrint", Rank.Novice, 2.5f, JournalText.Get("Arcana.Genie.Perk.FinePrint.Name"), 1),
                Power("Unbound", Rank.Apprentice, 0f, JournalText.Get("Arcana.Genie.Perk.Unbound.Name"), 2),
                Power("Lampbound", Rank.Apprentice, 3f, JournalText.Get("Arcana.Genie.Perk.Lampbound.Name"), 3),
                Power("RisingTide", Rank.Adept, -0.5f, JournalText.Get("Arcana.Genie.Perk.RisingTide.Name"), 3),
                Power("Wellspring", Rank.Adept, 0.5f, JournalText.Get("Arcana.Genie.Perk.Wellspring.Name"), 3, 3, 3),
                Power("PocketPalace", Rank.Adept, 2.5f, JournalText.Get("Arcana.Genie.Perk.PocketPalace.Name"), 4),
                Power("SilkenCommand", Rank.Adept, 3.5f, JournalText.Get("Arcana.Genie.Perk.SilkenCommand.Name"), 5),
                Power("FugitiveOfOld", Rank.Master, 0f, JournalText.Get("Arcana.Genie.Perk.FugitiveOfOld.Name"), 4),
                Power("HeartsDesire", Rank.Master, 3f, JournalText.Get("Arcana.Genie.Perk.HeartsDesire.Name"), 6),
                Power("Unleashed", Rank.HighWitch, 0f, JournalText.Get("Arcana.Genie.Perk.Unleashed.Name"), 8),
                Power("Counterattack", Rank.HighWitch, 1.1f, JournalText.Get("Arcana.Genie.Perk.Counterattack.Name"), 8),
                Power("LawOfTheLamp", Rank.HighWitch, 1.9f, JournalText.Get("Arcana.Genie.Perk.LawOfTheLamp.Name"), 8),
                Power("GenieLaw", Rank.HighWitch, 3f, JournalText.Get("Arcana.Genie.Perk.GenieLaw.Name"), 8)
            };

            Perk unbound = Find(perks, "Unbound");
            Perk lampbound = Find(perks, "Lampbound");
            unbound.Conflict = lampbound;
            lampbound.Conflict = unbound;
            Perk[] foundation = new[] { Find(perks, "DomesticDjinn"), Find(perks, "AThousandAndOneNights"), Find(perks, "FinePrint") };
            unbound.RequiresAny = foundation;
            lampbound.RequiresAny = foundation;
            Branch(unbound, Unbound, null);
            Branch(lampbound, Lampbound, null);
            Branch(Find(perks, "RisingTide"), Unbound, unbound);
            Branch(Find(perks, "Wellspring"), Unbound, unbound);
            Branch(Find(perks, "PocketPalace"), Lampbound, lampbound);
            Branch(Find(perks, "SilkenCommand"), Lampbound, lampbound);
            Perk fugitive = Find(perks, "FugitiveOfOld");
            Branch(fugitive, Unbound, null);
            fugitive.RequiresAny = new[] { Find(perks, "RisingTide"), Find(perks, "Wellspring") };
            Perk heart = Find(perks, "HeartsDesire");
            Branch(heart, Lampbound, null);
            heart.RequiresAny = new[] { Find(perks, "PocketPalace"), Find(perks, "SilkenCommand") };
            Branch(Find(perks, "Unleashed"), Unbound, fugitive);
            Branch(Find(perks, "GenieLaw"), Lampbound, heart);
            Branch(Find(perks, "Counterattack"), Unbound, Find(perks, "Unleashed"));
            Branch(Find(perks, "LawOfTheLamp"), Lampbound, Find(perks, "GenieLaw"));
            return perks;
        }

        private static Perk Find(Perk[] perks, string key)
        {
            foreach (Perk perk in perks)
                if (perk.Key == key) return perk;
            throw new ArgumentException("Unknown Genie perk " + key);
        }

        private static PerkConnection Link(OccultTree tree, uint color, string lower, string upper)
        {
            return new PerkConnection(Group(tree.Perks, lower), Group(tree.Perks, upper), color);
        }

        private static Perk[] Group(Perk[] perks, string keys)
        {
            string[] names = keys.Split(',');
            Perk[] group = new Perk[names.Length];
            for (int i = 0; i < names.Length; i++) group[i] = Find(perks, names[i]);
            return group;
        }

        private static void Branch(Perk perk, uint face, Perk requires)
        {
            perk.Face = face;
            perk.Requires = requires;
        }

        private static Perk Power(string key, Rank rank, float lane, string name, params int[] costs)
        {
            int column = (int)Math.Floor(lane);
            return new Perk(key, rank, column, lane - column, name, costs);
        }
    }

    internal static class GeniePerkBenefits
    {
        internal static TooltipModifier[] For(Perk perk, int descriptionIndex)
        {
            int upgrade = perk.PreviewIndex(descriptionIndex) + 1;
            List<TooltipModifier> lines = new List<TooltipModifier>();
            switch (perk.Key)
            {
                case "DomesticDjinn":
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.DomesticCost", Signed(-CastingRules.DomesticDjinnDiscount * 100f)), true);
                    break;
                case "AThousandAndOneNights":
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.SleepRegen", Signed(CastingRules.NightsBonus(upgrade) * 100f)), true);
                    break;
                case "FinePrint":
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.DelicateAccuracy", Signed(CastingRules.FinePrintAccuracy)), true);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.DelicateCost", Signed((CastingRules.FinePrintCost - 1f) * 100f)), false);
                    break;
                case "Unbound":
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.MaxPower", Signed(CastingRules.UnboundCapacityBonus * 100f)), true);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.AncientSorcery", Signed((CastingRules.AncientSorceryCost - 1f) * 100f)), true);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.AbilityCost", Signed(CastingRules.UnboundAbilityCost * 100f)), false);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.BacklashDuration", Signed((CastingRules.UnboundBacklashDuration - 1f) * 100f)), false);
                    break;
                case "Lampbound":
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.BacklashChance", Signed(-CastingRules.LampboundBacklashCut * 100f)), true);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.AbilityCost", Signed(-CastingRules.LampboundAbilityDiscount * 100f)), true);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.SpellCost", Signed(CastingRules.LampboundSpellCost * 100f)), false);
                    break;
                case "RisingTide":
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.TideRegen", Signed(CastingRules.RisingTidePerStack * 100f),
                        Signed(CastingRules.RisingTidePerStack * CastingRules.RisingTideMostStacks * 100f)), true);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.TideAccuracy", Signed(CastingRules.RisingTideAccuracy)), true);
                    Add(lines, JournalText.Get("Arcana.Genie.Benefit.TideHour"), true);
                    Add(lines, JournalText.Get("Arcana.Genie.Benefit.TideBreak"), false);
                    break;
                case "Wellspring":
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.MaxPower", Signed(CastingRules.WellspringBonus(upgrade) * 100f)), true);
                    break;
                case "PocketPalace":
                    Add(lines, JournalText.Get("Arcana.Genie.Benefit.LampNeeds"), true);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.RestRegen", Signed(CastingRules.RetreatRegeneration * 100f)), true);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.HomeRegen", Signed(CastingRules.HomeLampRegen * 100f)), true);
                    break;
                case "SilkenCommand":
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.EnsorcelCost", Signed(-CastingRules.SilkenEnsorcelDiscount * 100f)), true);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.EnsorcelDuration", Signed(CastingRules.SilkenEnsorcelLonger * 100f)), true);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.BanishLoss", Signed(-CastingRules.SilkenBanishRefund * 100f)), true);
                    break;
                case "FugitiveOfOld":
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.UnlockWitchTier", OverviewProviders.GetRomanNumeral(4)), true);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.FugitiveReturn", Unsigned(CastingRules.FugitiveReturn * 100f)), true);
                    break;
                case "HeartsDesire":
                    Add(lines, JournalText.Get("Arcana.Genie.Benefit.HeartAsk"), true);
                    Add(lines, JournalText.Get("Arcana.Genie.Benefit.HeartBlessings"), true);
                    Add(lines, JournalText.Get("Arcana.Genie.Benefit.HeartFulfilled"), true);
                    break;
                case "Unleashed":
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.UnlockTier", OverviewProviders.GetRomanNumeral(5)), true);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.BacklashChance", Signed(CastingRules.UnleashedBacklash * 100f)), false);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.Redirect", Unsigned(CastingRules.UnleashedRedirectChance)), true);
                    Add(lines, JournalText.Get("Arcana.Genie.Benefit.NoFail"), true);
                    break;
                case "GenieLaw":
                    Add(lines, JournalText.Get("Arcana.Genie.Benefit.GrantWishes"), true);
                    Add(lines, JournalText.Format("Arcana.Genie.Benefit.WishSuccess", Signed(CastingRules.GenieLawWishBonus)), true);
                    Add(lines, JournalText.Get("Arcana.Genie.Benefit.AbilitiesSafe"), true);
                    break;
                case "Counterattack":
                    Add(lines, JournalText.Get("Arcana.Genie.Benefit.OwnWishes"), true);
                    Add(lines, JournalText.Get("Arcana.Genie.Benefit.FateDue"), true);
                    break;
                case "LawOfTheLamp":
                    Add(lines, JournalText.Get("Arcana.Genie.Benefit.GuaranteedWish"), true);
                    Add(lines, JournalText.Get("Arcana.Genie.Benefit.GuaranteedSafe"), true);
                    break;
            }
            return lines.ToArray();
        }

        private static void Add(List<TooltipModifier> lines, string text, bool positive)
        {
            lines.Add(new TooltipModifier(text, positive, false));
        }

        private static string Signed(float amount) { return PercentText.Format(amount, true); }
        private static string Unsigned(float amount) { return PercentText.Format(amount, false); }
    }

    internal static class GeniePerks
    {
        private static readonly FieldInfo BanishLiking = typeof(OccultGenie.RemoveSim).GetField("kLTRHitFromRemoving",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        [PersistableStatic] private static Dictionary<ulong, long> struck;

        internal static PerkRecord Committed(SimDescription sim)
        {
            return sim == null ? null : GenieProgressStore.Ledger.ReadCommitted(sim.SimDescriptionId);
        }

        internal static bool Owns(SimDescription sim, string key)
        {
            PerkRecord record = Committed(sim);
            return record != null && record.TierOf(key) > 0;
        }

        internal static CastingRules Apply(Sim sim, CastingRules rules)
        {
            if (sim == null || sim.SimDescription == null) return rules;
            rules.FortuneRain = GenieRain.Active;
            Rank rank;
            PerkRecord record = PerkProgressSource.Genie.ReadGameplay(sim.SimDescription, out rank);
            if (record == null) return Penalties(GeniePower.Read(sim.SimDescription, false), rules);
            rules = Apply(record, rules);
            rules.AtHomeLamp = rules.Lampbound && record.TierOf("PocketPalace") > 0 && sim.LotHome != null && sim.LotCurrent == sim.LotHome;
            rules.Tide = rules.RisingTide ? RisingTide.Stacks(sim.SimDescription) : 0;
            GenieMagicSkill skill = GeniePower.Read(sim.SimDescription, false);
            rules.FinePrint = record.TierOf("FinePrint") > 0 && skill != null && skill.DelicateCasting;
            return Penalties(skill, rules);
        }

        internal static CastingRules Apply(PerkRecord record, CastingRules rules)
        {
            rules.Nights = Math.Min(3, record.TierOf("AThousandAndOneNights"));
            rules.DomesticDjinn = record.TierOf("DomesticDjinn") > 0;
            rules.Unbound = record.TierOf("Unbound") > 0;
            rules.Lampbound = !rules.Unbound && record.TierOf("Lampbound") > 0;
            rules.Wellspring = rules.Unbound ? Math.Min(3, record.TierOf("Wellspring")) : 0;
            rules.RisingTide = rules.Unbound && record.TierOf("RisingTide") > 0;
            rules.Fugitive = rules.Unbound && record.TierOf("FugitiveOfOld") > 0;
            rules.Unleashed = rules.Unbound && record.TierOf("Unleashed") > 0;
            rules.Counterattack = rules.Unleashed && record.TierOf("Counterattack") > 0;
            rules.SilkenCommand = rules.Lampbound && record.TierOf("SilkenCommand") > 0;
            rules.GenieLaw = rules.Lampbound && record.TierOf("GenieLaw") > 0;
            rules.LawOfTheLamp = rules.GenieLaw && record.TierOf("LawOfTheLamp") > 0;
            rules.ExtraTiers = (rules.Fugitive ? 1 : 0) + (rules.Unleashed ? 1 : 0);
            return rules;
        }

        private static CastingRules Penalties(GenieMagicSkill skill, CastingRules rules)
        {
            if (skill == null) return rules;
            long now = SimClock.CurrentTicks;
            rules.Exhausted = skill.ExhaustEnds > now;
            return rules;
        }

        internal static float CapacityFactor(SimDescription sim)
        {
            PerkRecord record = Committed(sim);
            CastingRules rules = new CastingRules(Rank.Novice, CasterKind.Genie);
            if (record != null) rules = Apply(record, rules);
            return Penalties(GeniePower.Read(sim, false), rules).GenieCapacity;
        }

        internal static void AfterAbility(GenieAbility ability, Sim actor, Sim target, CastingRules rules)
        {
            if (!rules.SilkenCommand || target == null) return;
            if (ability.Id == "genie_ensorcel" && target.BuffManager != null)
            {
                BuffInstance ensorcelled = target.BuffManager.GetElement(BuffNames.Ensorcelled);
                if (ensorcelled != null) ensorcelled.TimeoutCount *= 1f + CastingRules.SilkenEnsorcelLonger;
            }
            else if (ability.Id == "genie_banish")
            {
                Relationship relationship = Relationship.Get(actor, target, false);
                if (relationship != null && relationship.LTR != null && BanishLiking != null)
                    relationship.LTR.UpdateLiking(-Convert.ToSingle(BanishLiking.GetValue(null)) * CastingRules.SilkenBanishRefund);
            }
        }

        internal static void AfterBacklash(Sim caster)
        {
            if (caster == null || caster.SimDescription == null) return;
            if (struck == null) struck = new Dictionary<ulong, long>();
            struck[caster.SimDescription.SimDescriptionId] = SimClock.CurrentTicks + SimClock.ConvertToTicks(2f, TimeUnit.Minutes);
        }

        internal static void Return(Sim caster, CastingRules rules, float spent)
        {
            if (caster == null || caster.SimDescription == null) return;
            long expires;
            ulong id = caster.SimDescription.SimDescriptionId;
            if (struck == null || !struck.TryGetValue(id, out expires)) return;
            struck.Remove(id);
            if (rules.Fugitive && expires > SimClock.CurrentTicks) GeniePower.Change(caster.SimDescription, spent * CastingRules.FugitiveReturn);
        }

        internal static bool TryRedirect(Sim caster, CastingRules rules)
        {
            if (!rules.Unleashed || caster == null || caster.LotCurrent == null) return false;
            GenieMagicSkill skill = GeniePower.Read(caster.SimDescription, false);
            long now = SimClock.CurrentTicks;
            if (skill == null || skill.RedirectReady > now || !RandomUtil.RandomChance(CastingRules.UnleashedRedirectChance)) return false;
            Sim nearest = null;
            float best = CastingRules.RedirectRange * CastingRules.RedirectRange;
            foreach (Sim sim in caster.LotCurrent.GetSims())
            {
                if (sim == caster || sim.HasBeenDestroyed || !sim.InWorld || sim.BuffManager == null || sim.SimDescription == null || !sim.SimDescription.IsHuman) continue;
                float distance = caster.GetDistanceToObjectSquared(sim);
                if (distance > best) continue;
                best = distance;
                nearest = sim;
            }
            if (nearest == null) return false;
            WitchMagicRecovery.Give(nearest, rules.FizzleDuration);
            skill.RedirectReady = now + SimClock.ConvertToTicks(24f, TimeUnit.Hours);
            return true;
        }

        internal static void Clear()
        {
            struck = null;
        }
    }

    internal static class RisingTide
    {
        [PersistableStatic] private static Dictionary<ulong, List<long>> streaks;

        internal static int Stacks(SimDescription sim)
        {
            List<long> ends;
            if (sim == null || streaks == null || !streaks.TryGetValue(sim.SimDescriptionId, out ends)) return 0;
            long now = SimClock.CurrentTicks;
            for (int i = ends.Count - 1; i >= 0; i--)
                if (ends[i] <= now) ends.RemoveAt(i);
            if (ends.Count == 0) streaks.Remove(sim.SimDescriptionId);
            return Math.Min(ends.Count, CastingRules.RisingTideMostStacks);
        }

        internal static void Record(Sim sim, CastingRules rules, bool succeeded, bool backlash)
        {
            if (!rules.RisingTide || sim == null || sim.SimDescription == null) return;
            ulong id = sim.SimDescription.SimDescriptionId;
            if (backlash)
            {
                if (streaks != null) streaks.Remove(id);
                return;
            }
            if (!succeeded) return;
            if (streaks == null) streaks = new Dictionary<ulong, List<long>>();
            List<long> ends;
            if (!streaks.TryGetValue(id, out ends))
            {
                ends = new List<long>();
                streaks[id] = ends;
            }
            ends.Add(SimClock.CurrentTicks + SimClock.ConvertToTicks(1f, TimeUnit.Hours));
            if (ends.Count > CastingRules.RisingTideMostStacks) ends.RemoveAt(0);
        }

        internal static void Clear()
        {
            streaks = null;
        }
    }

    public sealed class DelicateCasting : ImmediateInteraction<Sim, Sim>
    {
        public static readonly InteractionDefinition Singleton = new Definition();

        protected override bool Run()
        {
            GenieMagicSkill skill = GeniePower.Read(Actor.SimDescription, true);
            if (skill == null) return false;
            skill.DelicateCasting = !skill.DelicateCasting;
            JournalBootstrap.RefreshMeter();
            return true;
        }

        public sealed class Definition : ImmediateInteractionDefinition<Sim, Sim, DelicateCasting>
        {
            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            {
                GenieMagicSkill skill = actor == null ? null : GeniePower.Read(actor.SimDescription, false);
                return JournalText.Get(skill != null && skill.DelicateCasting
                    ? "Arcana.Genie.DelicateCasting.TurnOff" : "Arcana.Genie.DelicateCasting.TurnOn");
            }

            public override string[] GetPath(bool female)
            { return new[] { GenieMenu.Root(female) }; }

            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                return !autonomous && actor == target && GenieAbilities.IsGenie(actor) && GeniePerks.Owns(actor.SimDescription, "FinePrint");
            }
        }
    }
}
