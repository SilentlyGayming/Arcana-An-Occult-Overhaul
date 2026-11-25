using System;
using System.Collections.Generic;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Utilities;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Objects.Alchemy;

namespace SupernaturalExpanded.Journal
{
    internal static class CasterAttributes
    {
        internal static OverviewTip PowerTooltip(string title, CastingRules rules)
        {
            List<TooltipModifier> lines = new List<TooltipModifier>();
            AddPowerLine(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Rank"), rules.RankCapacity - rules.BaseCapacity);
            if (rules.Ascendancy)
            {
                AddPowerLine(lines, JournalText.Get("Arcana.Witch.Perk.ArcaneAscendancy.Name"), rules.Capacity - rules.RankCapacity);
            }
            if (rules.Unbound)
            {
                AddPowerLine(lines, JournalText.Get("Arcana.Genie.Perk.Unbound.Name"), CastingRules.UnboundCapacityBonus * rules.RankCapacity);
            }
            AddPowerLine(lines, JournalText.Get("Arcana.Genie.Perk.Wellspring.Name"), CastingRules.WellspringBonus(rules.Wellspring) * rules.RankCapacity);
            string baseline = JournalText.Format("Arcana.Journal.Overview.Power.Base", (int)rules.BaseCapacity);
            return new OverviewTip(title, JournalText.Get("Arcana.Journal.Overview.Power.Tagline"), baseline, lines.ToArray());
        }

        private static void AddPowerLine(List<TooltipModifier> lines, string label, float amount)
        {
            int rounded = (int)Math.Round(amount);
            if (rounded > 0)
            {
                lines.Add(new TooltipModifier(JournalText.Format("Arcana.Common.ModifierLine", label, "+" + rounded), true));
            }
        }

        internal static ModifierReading ReadRegeneration(OverviewContext context)
        {
            Sim sim = context.Identity.Description == null ? null : context.Identity.Description.CreatedSim;
            if (!WitchCastingLifecycle.CastingInstalled || !WitchCastingContext.IsWitch(sim) || sim.BuffManager == null || sim.Motives == null)
            {
                return ModifierReading.None;
            }

            Motive motive = sim.Motives.GetMotive(CommodityKind.MagicFatigue);
            PowerRecovery regeneration = WitchMagicRecovery.AppliedRegeneration(motive);
            List<TooltipModifier> lines = new List<TooltipModifier>();
            AddPercentModifier(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Rank"), (regeneration.Rank - 1f) * 100f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.RestForTheWicked.Name"), (regeneration.Rest - 1f) * 100f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.TheWitchingHour.Name"), (regeneration.Hour - 1f) * 100f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.ThePowerOfThree.Name"), (regeneration.Coven - 1f) * 100f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Fizzle.Name"), (regeneration.Fizzle - 1f) * 100f);
            OverviewTip tip = new OverviewTip(JournalText.Get("Arcana.Journal.Overview.Attributes.PowerRegeneration"),
                JournalText.Get("Arcana.Journal.Overview.Regeneration.Tagline"),
                JournalText.Get("Arcana.Journal.Overview.Regeneration.Base"), lines.ToArray());
            return new ModifierReading((regeneration.Rate - 1f) * 100f, "", tip);
        }

        internal static ModifierReading ReadInnateRegeneration(OverviewContext context)
        {
            Sim sim = context.Identity.Description == null ? null : context.Identity.Description.CreatedSim;
            CasterKind kind;
            if (!WitchCastingLifecycle.CastingInstalled || !MagicCasters.TryKind(sim, out kind) || kind == CasterKind.Witch)
            {
                return ModifierReading.None;
            }
            PowerRecovery recovery = kind == CasterKind.Genie ? GeniePower.Recovery(sim) : PowerRecovery.Baseline;
            CastingRules rules = MagicCasters.ReadRules(sim);
            GenieMagicSkill skill = kind == CasterKind.Genie ? GeniePower.Read(sim.SimDescription, false) : null;
            bool stopped = skill != null && skill.IsExhausted && skill.Power >= skill.RecoveryLimit;
            List<TooltipModifier> lines = new List<TooltipModifier>();
            AddPercentModifier(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Rank"), (recovery.Rank - 1f) * 100f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Genie.Perk.AThousandAndOneNights.Name"), (recovery.Rest - 1f) * 100f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Genie.Perk.RisingTide.Name"),
                Math.Min(rules.Tide, CastingRules.RisingTideMostStacks) * CastingRules.RisingTidePerStack * 100f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Genie.Perk.PocketPalace.Name"), rules.AtHomeLamp ? CastingRules.HomeLampRegen * 100f : 0f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Ability.genie_lamp.Name"),
                kind == CasterKind.Genie && LampRetreat.IsResting(sim) ? CastingRules.RetreatRegeneration * 100f : 0f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Genie.Rain.Name"), rules.FortuneRain ? CastingRules.RainRegen * 100f : 0f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Genie.Exhaustion.Name"), stopped ? -100f : rules.Exhausted ? -CastingRules.ExhaustionRegeneration * 100f : 0f);
            WishRecord fate = kind == CasterKind.Genie ? WishState.Read(sim.SimDescription, false) : null;
            AddPercentModifier(lines, JournalText.Get("Arcana.Genie.WishBuff.Chains.Name"),
                fate != null && fate.FateSealEnds > SimClock.CurrentTicks ? -100f : 0f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Fizzle.Name"), (recovery.Fizzle - 1f) * 100f);
            OverviewTip tip = new OverviewTip(JournalText.Get("Arcana.Journal.Overview.Attributes.PowerRegeneration"),
                JournalText.Get("Arcana.Journal.Overview.Regeneration.Tagline"),
                JournalText.Get("Arcana.Journal.Overview.Regeneration.Base"), lines.ToArray());
            return new ModifierReading(stopped ? -100f : (recovery.Rate - 1f) * 100f, "", tip);
        }

        internal static ModifierReading ReadBacklash(OverviewContext context)
        {
            Sim sim = context.Identity.Description == null ? null : context.Identity.Description.CreatedSim;
            if (!WitchCastingLifecycle.CastingInstalled || !MagicCasters.IsCaster(sim))
            {
                return ModifierReading.None;
            }
            CastingRules rules = MagicCasters.ReadRules(sim);
            if (!rules.HasBacklash)
            {
                return ModifierReading.None;
            }
            float needPenalty;
            int empty;
            WitchCastingContext.CountNeeds(sim, out needPenalty, out empty, null);
            float emotions = WitchCastingContext.EmotionPenalty(sim);
            List<TooltipModifier> lines = new List<TooltipModifier>();
            float current = 0f;
            for (int tier = 0; tier <= rules.HighestTier; tier++)
            {
                MagicSpell spell = new MagicSpell(tier, MagicSpellKind.Spell);
                current = rules.BacklashChance(spell, rules.Accuracy(spell, rules.InnateAccuracy, emotions, needPenalty, empty));
                string name = JournalText.Format("Arcana.Journal.Overview.Cost.TierSingle", OverviewProviders.GetRomanNumeral(tier + 1));
                lines.Add(new TooltipModifier(JournalText.Format("Arcana.Journal.Overview.Cost.TierTotal", name, PercentText.Format(current, false)), true, true));
            }
            AddCut(lines, JournalText.Get("Arcana.Genie.Perk.Lampbound.Name"), rules.Lampbound ? CastingRules.LampboundBacklashCut : 0f);
            AddCut(lines, JournalText.Get("Arcana.Genie.Rain.Name"), rules.FortuneRain ? CastingRules.RainBacklashCut : 0f);
            if (rules.Unleashed)
            {
                lines.Add(new TooltipModifier(JournalText.Format("Arcana.Common.ModifierLine", JournalText.Get("Arcana.Genie.Perk.Unleashed.Name"),
                    PercentText.Format(CastingRules.UnleashedBacklash * 100f, true)), false));
            }
            lines.Add(new TooltipModifier(JournalText.Format("Arcana.Journal.Overview.Backlash.Floor",
                PercentText.Format(rules.Profile.BacklashFloor(rules.HighestTier), false)), false));
            OverviewTip tip = new OverviewTip(JournalText.Get("Arcana.Journal.Overview.Attributes.BacklashChance"),
                JournalText.Get("Arcana.Journal.Overview.Backlash.Tagline"), JournalText.Get("Arcana.Journal.Overview.Backlash.Base"), lines.ToArray());
            return new ModifierReading(current, "", tip, PercentText.Format(current, false));
        }

        private static void AddCut(List<TooltipModifier> lines, string label, float cut)
        {
            if (cut > 0f)
            {
                lines.Add(new TooltipModifier(JournalText.Format("Arcana.Common.ModifierLine", label, PercentText.Format(-cut * 100f, true)), true));
            }
        }

        internal static ModifierReading ReadSpellCost(OverviewContext context)
        {
            Sim sim = context.Identity.Description == null ? null : context.Identity.Description.CreatedSim;
            if (!WitchCastingLifecycle.CastingInstalled || !MagicCasters.IsCaster(sim))
            {
                return ModifierReading.None;
            }

            CastingRules rules = MagicCasters.ReadRules(sim);
            int tier = rules.HighestTier;
            MagicSpell current = new MagicSpell(tier, MagicSpellKind.Spell);
            SpellCostFactors ordinary = rules.CostFactors(current);
            List<TooltipModifier> lines = new List<TooltipModifier>();
            AddCostFactor(lines, JournalText.Get("Arcana.Journal.Overview.Cost.HighWitch"), ordinary.HighWitch);
            AddCostFactor(lines, JournalText.Get("Arcana.Witch.Perk.FamiliarBond.Name"), ordinary.Familiar);
            AddCostFactor(lines, JournalText.Get(rules.Unbound ? "Arcana.Journal.Overview.Cost.Ancient" : "Arcana.Journal.Overview.Cost.Borrowed"), ordinary.Innate);
            AddCostFactor(lines, JournalText.Get("Arcana.Genie.DelicateCasting.Name"), ordinary.FinePrint);
            AddCostFactor(lines, JournalText.Get(rules.Unbound ? "Arcana.Genie.Perk.Unbound.Name" : "Arcana.Genie.Perk.Lampbound.Name"), ordinary.Path);

            AddCostCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Spells"), rules, current);
            AddCostCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Charms"), rules, new MagicSpell(tier, MagicSpellKind.Charm));
            AddCostCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Curses"), rules, new MagicSpell(tier, MagicSpellKind.Curse));
            AddCostCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Rituals"), rules, new MagicSpell(tier, MagicSpellKind.Ritual));
            if (!rules.Innate)
            {
                AddCostCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Legendary"), rules, new MagicSpell(CastingRules.Legendary, MagicSpellKind.Spell));
            }

            OverviewTip tip = new OverviewTip(JournalText.Get("Arcana.Journal.Overview.Attributes.SpellCost"),
                JournalText.Get("Arcana.Journal.Overview.Cost.Tagline"), JournalText.Get("Arcana.Journal.Overview.Cost.Base"), lines.ToArray());
            float minimum, maximum;
            rules.CostRange(MagicSpellKind.Spell, out minimum, out maximum);
            string range = FormatCostRange((maximum - 1f) * 100f, (minimum - 1f) * 100f, true);
            return new ModifierReading((rules.CostMultiplier(current) - 1f) * 100f, "", tip, range);
        }

        private static void AddCostCategory(List<TooltipModifier> lines, string label, CastingRules rules, MagicSpell spell)
        {
            SpellCostFactors factors = rules.CostFactors(spell);
            bool legendary = spell.Tier == CastingRules.Legendary;
            float minimum = factors.Multiplier;
            float maximum = minimum;
            if (!legendary)
            {
                rules.CostRange(spell.Kind, out minimum, out maximum);
            }
            string range = FormatCostRange(minimum * 100f, maximum * 100f, false);
            lines.Add(new TooltipModifier(JournalText.Format("Arcana.Common.ModifierLine", label, range), true, true, false, true));
            if (!legendary && PercentText.AbsoluteTenths(minimum * 100f) != PercentText.AbsoluteTenths(maximum * 100f))
            {
                AddCostTierGroups(lines, rules, spell.Kind);
            }
            AddCostFactor(lines, JournalText.Get("Arcana.Witch.Perk.Hexweaver.Name"), factors.Hexweaver);
            AddCostFactor(lines, JournalText.Get("Arcana.Witch.Perk.TheOldWays.Name"), factors.OldWays);

            if (spell.Tier == CastingRules.Legendary)
            {
                return;
            }
            SpellCostFactors lower = rules.CostFactors(new MagicSpell((int)Rank.Novice, spell.Kind));
            string earlyTiers = rules.Rank == Rank.Novice
                ? JournalText.Get("Arcana.Journal.Overview.Cost.FirstTier")
                : JournalText.Get("Arcana.Journal.Overview.Cost.EarlyTiers");
            AddCostFactor(lines, JournalText.Get("Arcana.Journal.Overview.Cost.Apprentice"), lower.Apprentice, earlyTiers);
            AddCostFactor(lines, JournalText.Get("Arcana.Witch.Perk.LessonsInTheUnseenArts.Name"), lower.Lessons, earlyTiers);
            if (lower.Eidetic != 1f && rules.Rank > Rank.Novice)
            {
                string last = OverviewProviders.GetRomanNumeral((int)rules.Rank);
                string lowerTiers = rules.Rank == Rank.Apprentice
                    ? JournalText.Get("Arcana.Journal.Overview.Cost.FirstTier")
                    : JournalText.Format("Arcana.Journal.Overview.Cost.TierRange", last);
                AddCostFactor(lines, JournalText.Get("Arcana.Witch.Perk.EideticWitchary.Name"), lower.Eidetic, lowerTiers);
            }
        }

        private static string FormatCostRange(float first, float last, bool signed)
        {
            string a = PercentText.Format(first, signed);
            string b = PercentText.Format(last, signed);
            if (a == b) return a;
            return signed ? JournalText.Format("Arcana.Journal.Overview.Cost.SignedRange", a, b)
                : JournalText.Format("Arcana.Journal.Overview.Cost.Range", a, b);
        }

        private static void AddCostTierGroups(List<TooltipModifier> lines, CastingRules rules, MagicSpellKind kind)
        {
            int start = 0;
            string amount = PercentText.Format(rules.CostMultiplier(new MagicSpell(0, kind)) * 100f, false);
            for (int tier = 1; tier <= rules.HighestTier + 1; tier++)
            {
                string next = tier <= rules.HighestTier
                    ? PercentText.Format(rules.CostMultiplier(new MagicSpell(tier, kind)) * 100f, false) : null;
                if (next == amount)
                {
                    continue;
                }
                string first = OverviewProviders.GetRomanNumeral(start + 1);
                string last = OverviewProviders.GetRomanNumeral(tier);
                string name = start == tier - 1
                    ? JournalText.Format("Arcana.Journal.Overview.Cost.TierSingle", first)
                    : JournalText.Format("Arcana.Journal.Overview.Cost.TierSpan", first, last);
                lines.Add(new TooltipModifier(JournalText.Format("Arcana.Journal.Overview.Cost.TierTotal", name, amount), true, true));
                start = tier;
                amount = next;
            }
        }

        private static void AddCostFactor(List<TooltipModifier> lines, string label, float factor)
        {
            AddCostFactor(lines, label, factor, null);
        }

        private static void AddCostFactor(List<TooltipModifier> lines, string label, float factor, string qualifier)
        {
            float percent = (factor - 1f) * 100f;
            if (PercentText.AbsoluteTenths(percent) != 0)
            {
                string text = JournalText.Format("Arcana.Common.ModifierLine", label, PercentText.Format(percent, true));
                if (!string.IsNullOrEmpty(qualifier))
                {
                    text = JournalText.Format("Arcana.Common.WithQualifier", text, qualifier);
                }
                lines.Add(new TooltipModifier(text, percent < 0f));
            }
        }

        internal static AccuracyReading ReadAccuracy(OverviewContext context)
        {
            Sim sim = context.Identity.Description == null ? null : context.Identity.Description.CreatedSim;
            if (!WitchCastingLifecycle.CastingInstalled || !MagicCasters.IsCaster(sim))
            {
                return null;
            }

            CastingRules rules = MagicCasters.ReadRules(sim);
            MagicWand wand = rules.Innate ? null : WitchCastingContext.WandForAccuracy(sim);
            bool previewMagicHands = !rules.Innate && wand == null && WitchCastingContext.HasPracticallyMagic(sim);
            float wandBonus = rules.Innate ? rules.InnateAccuracy : WitchCastingContext.WandBonus(wand, previewMagicHands);
            float needPenalty;
            int empty;
            List<TooltipModifier> needLines = new List<TooltipModifier>();
            WitchCastingContext.CountNeeds(sim, out needPenalty, out empty, needLines);
            float emotions = WitchCastingContext.EmotionPenalty(sim);
            MagicSpell currentRank = new MagicSpell(rules.HighestTier, MagicSpellKind.Spell);
            SpellAccuracy accuracy = rules.AccuracyFor(currentRank, wandBonus, emotions, needPenalty, empty);
            List<TooltipModifier> lines = new List<TooltipModifier>();
            string baseLine = JournalText.Format("Arcana.Common.ModifierLine",
                JournalText.Get("Arcana.Journal.Overview.Accuracy.Base"), PercentText.Format(rules.BaseAccuracy, false));
            string wandName = rules.Innate ? JournalText.Get("Arcana.Journal.Overview.Accuracy.InnatePower")
                : previewMagicHands || wand is MagicHands
                ? JournalText.Get("Arcana.Journal.Overview.Accuracy.MagicHands")
                : wand != null && accuracy.Wand != 0f ? wand.GetLocalizedName() : null;
            if (rules.FirstOverreachTier >= 0)
            {
                string above = JournalText.Format("Arcana.Journal.Overview.Accuracy.AboveRank",
                    OverviewProviders.GetRomanNumeral(rules.FirstOverreachTier + 1));
                AddPercentModifier(lines, above, -rules.OverreachPenalty);
            }
            AddPercentModifier(lines, RankLabel(rules), accuracy.RankBonus);
            AddPercentModifier(lines, string.IsNullOrEmpty(wandName)
                ? JournalText.Get("Arcana.Journal.Overview.Accuracy.Wand") : wandName, accuracy.Wand);
            AddPercentModifier(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Mood"), accuracy.Mood);
            if (PercentText.AbsoluteTenths(accuracy.DarkArts) != 0)
            {
                string offset = JournalText.Format("Arcana.Common.ModifierLine",
                    JournalText.Get("Arcana.Witch.Perk.DarkArts.Name"), PercentText.Format(accuracy.DarkArts, true));
                lines.Add(new TooltipModifier(offset, true, false, true));
            }
            lines.AddRange(needLines);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.ArcaneAscendancy.Name"), accuracy.Ascendancy);
            AddPercentModifier(lines, JournalText.Get("Arcana.Genie.DelicateCasting.Name"), accuracy.FinePrint);
            AddPercentModifier(lines, JournalText.Get("Arcana.Genie.Perk.RisingTide.Name"), accuracy.RisingTide);
            AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Spells"), accuracy.Total);
            AddCategoryModifiers(lines, accuracy, rules.SpecialtyAccuracyBonus(currentRank));
            int tier = rules.HighestTier;
            MagicSpell charmSpell = new MagicSpell(tier, MagicSpellKind.Charm);
            MagicSpell curseSpell = new MagicSpell(tier, MagicSpellKind.Curse);
            MagicSpell ritualSpell = new MagicSpell(tier, MagicSpellKind.Ritual);
            MagicSpell legendarySpell = new MagicSpell(CastingRules.Legendary, MagicSpellKind.Spell);
            SpellAccuracy charm = rules.AccuracyFor(charmSpell, wandBonus, emotions, needPenalty, empty);
            SpellAccuracy curse = rules.AccuracyFor(curseSpell, wandBonus, emotions, needPenalty, empty);
            SpellAccuracy ritual = rules.AccuracyFor(ritualSpell, wandBonus, emotions, needPenalty, empty);
            SpellAccuracy legendary = rules.AccuracyFor(legendarySpell, wandBonus, emotions, needPenalty, empty);
            AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Charms"), charm.Total);
            AddCategoryModifiers(lines, charm, rules.SpecialtyAccuracyBonus(charmSpell));
            AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Curses"), curse.Total);
            AddCategoryModifiers(lines, curse, rules.SpecialtyAccuracyBonus(curseSpell));
            AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Rituals"), ritual.Total);
            AddCategoryModifiers(lines, ritual, rules.SpecialtyAccuracyBonus(ritualSpell));
            if (!rules.Innate)
            {
                AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Legendary"), legendary.Total);
                AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.TheOldWays.Name"), rules.SpecialtyAccuracyBonus(legendarySpell));
            }
            return new AccuracyReading(accuracy.Total, baseLine, lines.ToArray());
        }

        private static string RankLabel(CastingRules rules)
        {
            return JournalText.Get(rules.Caster == CasterKind.Fairy ? "Arcana.Journal.Overview.Accuracy.InnateMagic" : "Arcana.Journal.Overview.Accuracy.Rank");
        }

        private static void AddCategory(List<TooltipModifier> lines, string label, float value)
        {
            string text = JournalText.Format("Arcana.Common.ModifierLine", label,
                float.IsNaN(value) ? JournalText.Get("Arcana.Common.UnavailableValue") : PercentText.Format(value, false));
            lines.Add(new TooltipModifier(text, true, true, false, true));
        }

        private static void AddCategoryModifiers(List<TooltipModifier> lines, SpellAccuracy category, float specialty)
        {
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.TheOldWays.Name"), specialty);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.ThePowerOfThree.Name"), category.Coven);
            AddLimit(lines, category);
        }

        private static void AddLimit(List<TooltipModifier> lines, SpellAccuracy category)
        {
            if (PercentText.AbsoluteTenths(category.Limit) == 0 || category.Limit > 0f) return;
            string label;
            if (category.Ceiling < SpellAccuracy.Maximum)
                label = JournalText.Format("Arcana.Journal.Overview.Accuracy.NeedsCap", PercentText.Format(category.Ceiling, false));
            else
                label = JournalText.Format("Arcana.Journal.Overview.Accuracy.Maximum", PercentText.Format(SpellAccuracy.Maximum, false));
            AddPercentModifier(lines, label, category.Limit);
        }

        internal static void AddPercentModifier(List<TooltipModifier> lines, string label, float value)
        {
            if (PercentText.AbsoluteTenths(value) != 0)
            {
                string text = JournalText.Format("Arcana.Common.ModifierLine", label, PercentText.Format(value, true));
                lines.Add(new TooltipModifier(text, value > 0f));
            }
        }

        internal static string NeedName(CommodityKind need)
        {
            switch (need)
            {
                case CommodityKind.Hunger: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Hunger");
                case CommodityKind.VampireThirst: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Thirst");
                case CommodityKind.Bladder: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Bladder");
                case CommodityKind.Energy: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Energy");
                case CommodityKind.Fun: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Fun");
                case CommodityKind.Social: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Social");
                case CommodityKind.Hygiene: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Hygiene");
                default: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Needs");
            }
        }
    }

    internal sealed class SpellPowerQuote
    {
        private readonly bool caster;
        private readonly CastingRules rules;
        private readonly float wandDrain;
        private readonly float handsDrain;
        private readonly float situational;

        internal SpellPowerQuote(Sim actor)
        {
            CasterKind kind;
            caster = MagicCasters.TryKind(actor, out kind);
            if (!caster)
            {
                return;
            }
            rules = MagicCasters.ReadRules(actor);
            if (kind != CasterKind.Witch)
            {
                wandDrain = handsDrain = situational = 1f;
                return;
            }
            MagicWand wand = WitchCastingContext.EquippedWand(actor);
            wandDrain = wand == null ? 1f : wand.MotiveDrainMultiplier;
            handsDrain = wand == null && WitchCastingContext.HasPracticallyMagic(actor) ? WitchCastingContext.HandsDrain : wandDrain;
            situational = WitchCastingContext.SituationalDrain(actor);
        }

        internal float For(SpellEntry entry)
        {
            bool hands = SpellKnowledgeGates.UsesArcanaCasting(entry.NativeClass);
            return Quote(entry, hands ? handsDrain : wandDrain);
        }

        internal float NativeVariant(SpellEntry entry)
        {
            return Quote(entry, wandDrain);
        }

        private float Quote(SpellEntry entry, float drain)
        {
            if (!caster) return entry.Cost;
            return rules.FinalCost(entry.Cost, MagicSpell.For(entry), drain * situational);
        }
    }
}
