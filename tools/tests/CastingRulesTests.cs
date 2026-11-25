using System;
using System.Collections.Generic;

namespace SupernaturalExpanded.Journal
{
    internal static class CastingRulesTests
    {
        private static readonly MagicSpell TierOne = new MagicSpell(0, MagicSpellKind.Spell);
        private static readonly MagicSpell TierTwo = new MagicSpell(1, MagicSpellKind.Spell);
        private static readonly MagicSpell TierThree = new MagicSpell(2, MagicSpellKind.Spell);
        private static readonly MagicSpell TierFour = new MagicSpell(3, MagicSpellKind.Spell);
        private static readonly MagicSpell TierFive = new MagicSpell(4, MagicSpellKind.Spell);
        private static readonly MagicSpell Legendary = new MagicSpell(5, MagicSpellKind.Ritual);
        private static int checks;
        private static readonly List<string> failures = new List<string>();

        public static void Main()
        {
            Research();
            Accuracy();
            NeedsSeverity();
            PerkAccuracy();
            AccuracyBreakdown();
            GroupedAccuracy();
            LoadedWitchRank();
            PercentRounding();
            ProgressionAndCost();
            CostBreakdown();
            CostRanges();
            PowerPoolConversion();
            RecoveryAndProtection();
            CharmClassification();
            FixedCosts();
            InnateCasters();
            GeniePerkRules();
            if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures.ToArray()));
            Console.WriteLine("PASS: " + checks + " deterministic research, casting and progression checks");
        }

        private static void Research()
        {
            SpellResearchSettings settings = SpellResearchSettings.Default;
            Equal(30f, settings.IntervalMinutes, "base research interval");
            Equal(25f, settings.DiscoveryChancePercent, "base discovery odds");
            Equal(0.4f, settings.ExperienceFraction, "study XP is a separate fraction");
            foreach (CasterKind caster in new[] { CasterKind.Witch, CasterKind.Fairy, CasterKind.Genie })
            {
                for (int rank = 0; rank < 5; rank++)
                {
                    CastingRules rules = new CastingRules((Rank)rank, caster);
                    float baseline = caster == CasterKind.Witch ? 1f : 0.5f;
                    float bonus = caster == CasterKind.Witch && rank >= 3 ? 1.1f : caster == CasterKind.Genie && rank >= 2 ? 1.25f : 1f;
                    Equal(baseline * bonus, settings.Speed(rules, false), "species and rank research factor");
                    Equal(baseline * bonus, settings.Speed(rules, true), "midnight alone grants no perk");
                    rules.WitchingHour = true;
                    Equal(baseline * bonus, settings.Speed(rules, false), "night perk does not work by day");
                    Equal(baseline * bonus * (caster == CasterKind.Witch ? 1.2f : 1f), settings.Speed(rules, true), "only a Witch's purchased night perk applies");
                    rules.Ascendancy = true;
                    rules.Unbound = true;
                    rules.Lampbound = true;
                    rules.FinePrint = true;
                    Equal(baseline * bonus, settings.Speed(rules, false), "casting perks do not silently amplify research");
                }
            }
            CastingRules master = new CastingRules(Rank.Master);
            master.WitchingHour = true;
            Equal(1.32f, settings.Speed(master, true), "Master and night bonuses multiply once");
            Equal(30f / 1.32f, settings.IntervalMinutes / settings.Speed(master, true), "Master night roll interval");
            CastingRules ascendant = new CastingRules(Rank.Adept, CasterKind.Genie);
            Equal(48f, settings.IntervalMinutes / settings.Speed(ascendant, false), "Ascendant roll interval");
            SpellResearchSettings tuned = new SpellResearchSettings(20f, 50f, 0.2f, 2f, 0.8f, 0.6f, 30f, 40f, 50f);
            Equal(3.9f, tuned.Speed(master, true), "custom Witch, Master and night tunings are live");
            Equal(0.84f, tuned.Speed(ascendant, true), "custom Genie and Ascendant tunings are live");
            Equal(0.8f, tuned.Speed(new CastingRules(Rank.HighWitch, CasterKind.Fairy), true), "custom Fairy tuning is live");
            Equal(50f, tuned.DiscoveryChancePercent, "discovery odds remain independently tunable");
            Equal(0.2f, tuned.ExperienceFraction, "XP remains independently tunable");
            SpellResearchSettings invalid = new SpellResearchSettings(float.NaN, 0f, 2f, float.PositiveInfinity, -1f, 0f, -1f, float.NaN, float.PositiveInfinity);
            Equal(30f, invalid.IntervalMinutes, "invalid interval falls back");
            Equal(25f, invalid.DiscoveryChancePercent, "invalid chance falls back");
            Equal(0.4f, invalid.ExperienceFraction, "invalid XP falls back");
            Equal(1.32f, invalid.Speed(master, true), "invalid Witch tuning falls back");
            Equal(0.625f, invalid.Speed(ascendant, true), "invalid Genie tuning falls back");
            Equal(0.5f, invalid.FairySpeed, "invalid Fairy tuning falls back");
            SpellResearchSettings disabled = new SpellResearchSettings(30f, 25f, 0.4f, 1f, 0.5f, 0.5f, 0f, 0f, 0f);
            Equal(1f, disabled.Speed(master, true), "zero bonuses are allowed");
            Equal(0.5f, disabled.Speed(ascendant, true), "zero Ascendant bonus is allowed");
            True(SpellResearchSettings.IsWitchingHour(0f), "night begins at midnight");
            True(SpellResearchSettings.IsWitchingHour(3.999f), "night lasts until four");
            True(!SpellResearchSettings.IsWitchingHour(4f), "night ends at four");
            True(!SpellResearchSettings.IsWitchingHour(23.999f), "night does not start early");
            True(!SpellResearchSettings.IsWitchingHour(float.NaN), "invalid hour grants no bonus");

            SpellResearchClock clock = new SpellResearchClock();
            clock.Start(1f, 30f);
            True(!clock.Advance(15f, 1.2f, 30f), "half progress survives entering the night window");
            True(!clock.Advance(27.4f, 1.2f, 30f), "night roll does not occur early");
            True(clock.Advance(27.5f, 1.2f, 30f), "new speed applies only to remaining work");
            True(!clock.Advance(27.5f, 1.2f, 30f), "same timestamp never duplicates a roll");
            clock.Start(1.2f, 30f);
            True(!clock.Advance(10f, 1f, 30f), "leaving the night window preserves prior work");
            True(!clock.Advance(27.9f, 1f, 30f), "daytime remaining work is not shortened");
            True(clock.Advance(28f, 1f, 30f), "daytime roll uses the remaining work");
            clock.Start(0.5f, 30f);
            True(!clock.Advance(30f, 0.625f, 30f), "mid-study Ascendant rank preserves half progress");
            True(clock.Advance(54f, 0.625f, 30f), "Ascendant speeds the remaining half once");
            True(!clock.Advance(float.PositiveInfinity, 1f, 30f), "infinite callback time is ignored");
            True(!clock.Advance(53f, 1f, 30f), "backward callback time is ignored");
            True(!clock.Advance(54f, 0.625f, 30f), "bad callback time did not corrupt the clock");
            clock.Start(1f, 30f);
            True(!clock.Advance(15f, 1f, 60f), "interval tuning changes preserve partial work");
            True(clock.Advance(45f, 1f, 60f), "new interval applies prospectively");
        }

        private static void Accuracy()
        {
            CastingRules novice = new CastingRules(Rank.Novice);
            Equal(50f, novice.Accuracy(TierOne, 0f, 0f, 0, 0), "base");
            foreach (float wand in new[] { 5f, 10f, 15f, 20f, 25f })
            {
                Equal(50f + wand, novice.Accuracy(TierOne, wand, 0f, 0, 0), "wand bonus");
            }
            Equal(30f, novice.Accuracy(TierTwo, 25f, 0f, 0, 0), "overreach");
            Equal(40f, novice.Accuracy(TierOne, 0f, 0f, 10f, 0), "low Need");
            Equal(20f, novice.Accuracy(TierOne, 0f, 60f, 0, 0), "emotions cap");
            Equal(0f, novice.Accuracy(TierOne, 0f, 30f, 30f, 0), "negative totals show 0%");
            True(float.IsNaN(novice.Accuracy(Legendary, 0f, 0f, 0, 0)), "Legendary special");
            CastingRules master = new CastingRules(Rank.Master);
            Equal(65f, master.Accuracy(TierFour, 0f, 0f, 0, 0), "rank bonus");
            SpellAccuracy hands = master.AccuracyFor(TierFour, CastingRules.MagicHandsAccuracyBonus, 0f, 0, 0);
            Equal(20f, hands.Wand, "Magic Hands breakdown");
            Equal(85f, hands.Total, "Magic Hands chance at Master rank");
            Equal(85f, master.Accuracy(TierTwo, 0f, 0f, 0, 0), "tier bonus");
            Equal(95f, master.Accuracy(TierTwo, 20f, 0f, 0, 0), "ceiling");
            Equal(15f, master.Accuracy(TierTwo, 20f, 0f, 10f, 1), "one empty Need");
            Equal(5f, master.Accuracy(TierTwo, 20f, 0f, 20f, 2), "two empty Needs");
        }

        private static void NeedsSeverity()
        {
            Equal(0f, CastingRules.NeedPenalty(0.36f), "healthy Need");
            Equal(10f, CastingRules.NeedPenalty(0.35f), "low Need boundary");
            Equal(10f, CastingRules.NeedPenalty(0.21f), "low Need");
            Equal(25f, CastingRules.NeedPenalty(0.20f), "very low Need boundary");
            Equal(25f, CastingRules.NeedPenalty(0.06f), "very low Need");
            Equal(35f, CastingRules.NeedPenalty(0.05f), "critical Need boundary");
            Equal(35f, CastingRules.NeedPenalty(0f), "empty Need");
            CastingRules high = new CastingRules(Rank.HighWitch);
            high.DarkArts = true;
            Equal(40f, high.Accuracy(TierFive, 20f, 30f, 35f, 0), "critical Hunger screenshot example");
            high.OldWays = true;
            Equal(55f, high.Accuracy(new MagicSpell(4, MagicSpellKind.Curse), 20f, 30f, 35f, 0), "curse specialty");
            Equal(55f, high.Accuracy(new MagicSpell(4, MagicSpellKind.Ritual), 20f, 30f, 35f, 0), "ritual specialty");
            Equal(15f, high.Accuracy(TierFive, 20f, 30f, 35f, 1), "empty Need cap");
            Equal(5f, high.Accuracy(TierFive, 20f, 30f, 70f, 2), "two empty Needs cap a positive total at 5%");
            Equal(0f, high.Accuracy(TierFive, 20f, 30f, 100f, 2), "two empty Needs with a negative total");
            high.OldWays = false;
            high.Ascendancy = true;
            Equal(50f, high.Accuracy(TierFive, 20f, 30f, 35f, 0), "Ascendancy with critical Hunger");
            Equal(10f, high.GeneralAccuracyBonus, "general capstone modifier available to future Legendary resolver");
        }

        private static void PerkAccuracy()
        {
            CastingRules adept = new CastingRules(Rank.Adept);
            Equal(30f, adept.Accuracy(TierThree, 0f, 20f, 10f, 0), "unbought Dark Arts");
            adept.DarkArts = true;
            Equal(40f, adept.Accuracy(TierThree, 0f, 20f, 10f, 0), "Dark Arts halves emotions only");
            adept.InCoven = true;
            Equal(82f, adept.Accuracy(TierThree, 20f, 0f, 0, 0), "relative coven failure reduction");
            Equal(80f, adept.Accuracy(new MagicSpell(2, MagicSpellKind.Ritual), 20f, 0f, 0, 0), "no coven ritual bonus");
            Equal(40f, adept.Accuracy(TierFour, 25f, 0f, 0, 0), "above-rank accuracy retains the 45-point penalty; coven gives no bonus");
            Equal(15f, adept.Accuracy(TierThree, 25f, 0f, 10f, 1), "coven empty Need cap");
            CastingRules covenHigh = new CastingRules(Rank.HighWitch);
            covenHigh.InCoven = true;
            Equal(91f, covenHigh.Accuracy(TierThree, 0f, 0f, 0, 0), "coven tier III");
            Equal(91f, covenHigh.Accuracy(new MagicSpell(2, MagicSpellKind.Curse), 0f, 0f, 0, 0), "coven tier III curse");
            Equal(80f, covenHigh.Accuracy(TierFour, 0f, 0f, 0, 0), "no coven tier IV bonus");
            Equal(70f, covenHigh.Accuracy(TierFive, 0f, 0f, 0, 0), "no coven tier V bonus");
            Equal(90f, covenHigh.Accuracy(new MagicSpell(2, MagicSpellKind.Ritual), 0f, 0f, 0, 0), "no coven tier III ritual bonus");

            CastingRules high = new CastingRules(Rank.HighWitch);
            high.Ascendancy = true;
            Equal(80f, high.Accuracy(TierFive, 0f, 0f, 0, 0), "Ascendancy accuracy");
            Equal(180f, high.Capacity, "Ascendancy capacity");
            high.Ascendancy = false;
            high.OldWays = true;
            Equal(70f, high.Accuracy(TierFive, 0f, 0f, 0, 0), "Old Ways category gate");
            Equal(85f, high.Accuracy(new MagicSpell(4, MagicSpellKind.Curse | MagicSpellKind.Ritual), 0f, 0f, 0, 0), "Old Ways applies once");
            Equal(15f, high.SpecialtyAccuracyBonus(Legendary), "Legendary specialist modifier available");
            high.Ascendancy = true;
            Equal(0f, high.SpecialtyAccuracyBonus(Legendary), "Conflicting capstones do not double-apply");
            high.Ascendancy = false;
            True(float.IsNaN(high.Accuracy(Legendary, 0f, 0f, 0, 0)), "Legendary success still separate");
            Equal(95f, high.Accuracy(new MagicSpell(4, MagicSpellKind.Curse), 25f, 0f, 0, 0), "Old Ways respects cap");
            Equal(15f, high.Accuracy(new MagicSpell(4, MagicSpellKind.Curse), 25f, 0f, 10f, 1), "Old Ways respects empty Need");
            Equal(0.85f, high.RefundFraction(new MagicSpell(2, MagicSpellKind.Curse)), "curse refund");
            Equal(0.70f, high.RefundFraction(TierThree), "ordinary refund");
        }

        private static void AccuracyBreakdown()
        {
            CastingRules master = new CastingRules(Rank.Master);
            SpellAccuracy accuracy = master.AccuracyFor(TierFour, 20f, 10f, 0, 0);
            Equal(15f, accuracy.RankBonus, "current rank breakdown");
            Equal(20f, accuracy.Wand, "wand breakdown");
            Equal(-10f, accuracy.Mood, "mood breakdown");
            Equal(75f, accuracy.Total, "current-rank total");
            Equal(95f, master.Accuracy(TierTwo, 20f, 10f, 0, 0), "lower-tier gameplay bonus remains in spell rolls");

            CastingRules high = new CastingRules(Rank.HighWitch);
            high.Ascendancy = true;
            high.DarkArts = true;
            high.InCoven = true;
            accuracy = high.AccuracyFor(TierFive, 25f, 20f, 10f, 1);
            Equal(10f, accuracy.Ascendancy, "Ascendancy breakdown");
            Equal(10f, accuracy.DarkArts, "Dark Arts breakdown");
            Equal(-10f, accuracy.Needs, "Needs breakdown");
            Equal(0f, accuracy.Coven, "no coven breakdown at Tier V");
            Equal(15f, accuracy.Total, "empty Need still caps current-rank accuracy");
            Equal(accuracy.Total, accuracy.Starting + accuracy.RankBonus + accuracy.Wand + accuracy.Mood + accuracy.Needs
                + accuracy.DarkArts + accuracy.Ascendancy + accuracy.Coven + accuracy.Limit, "listed modifiers sum to the total");
        }

        private static void GroupedAccuracy()
        {
            CastingRules high = new CastingRules(Rank.HighWitch);
            high.DarkArts = true;
            high.OldWays = true;
            MagicSpell curse = new MagicSpell(4, MagicSpellKind.Curse);
            MagicSpell ritual = new MagicSpell(4, MagicSpellKind.Ritual);
            SpellAccuracy baseline = high.AccuracyFor(TierFive, 20f, 30f, 25f, 0);
            Equal(50f, baseline.Total, "grouped spells example");
            Equal(65f, high.Accuracy(curse, 20f, 30f, 25f, 0), "grouped curses example");
            Equal(65f, high.Accuracy(ritual, 20f, 30f, 25f, 0), "grouped rituals example");
            foreach (MagicSpell spell in new[] { curse, ritual })
            {
                foreach (int empty in new[] { 0, 1, 2 })
                {
                    SpellAccuracy a = high.AccuracyFor(TierFive, 25f, 0f, 35f * empty, empty);
                    SpellAccuracy b = high.AccuracyFor(spell, 25f, 0f, 35f * empty, empty);
                    Equal(b.Total, a.Total + high.SpecialtyAccuracyBonus(spell) + b.Coven - a.Coven + b.Limit - a.Limit,
                        "category adjustments reconcile with total, including caps");
                }
            }
            CastingRules adept = new CastingRules(Rank.Adept);
            adept.InCoven = true;
            MagicSpell adeptRitual = new MagicSpell(2, MagicSpellKind.Ritual);
            SpellAccuracy regular = adept.AccuracyFor(TierThree, 20f, 0f, 0f, 0);
            SpellAccuracy ritualReading = adept.AccuracyFor(adeptRitual, 20f, 0f, 0f, 0);
            Equal(ritualReading.Total, regular.Total + ritualReading.Coven - regular.Coven + ritualReading.Limit - regular.Limit,
                "ritual category accounts for absent coven bonus");
            foreach (MagicSpell spell in new[] { TierFive, curse, ritual, Legendary })
            {
                SpellAccuracy reading = high.AccuracyFor(spell, 20f, 30f, 35f, 1);
                Equal(-30f, reading.Mood, "all categories use emotional penalty");
                Equal(15f, reading.DarkArts, "all categories use Dark Arts mood offset");
                Equal(-35f, reading.Needs, "all categories use Needs penalty");
                Equal(15f, reading.Ceiling, "all categories expose empty Need ceiling");
                if (spell.Tier != CastingRules.Legendary)
                    Equal(15f, reading.Total, "implemented category enforces empty Need cap");
            }
            True(float.IsNaN(high.Accuracy(Legendary, 20f, 30f, 25f, 0)), "no invented Legendary total");
        }

        private static void LoadedWitchRank()
        {
            int[] cumulative = { 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000 };
            long[] thresholds = PerkProgression.RankThresholds(cumulative, 10, PerkProgression.WitchRankStarts);
            long loaded = PerkProgression.ExperienceForSkill(0f, 8, cumulative);
            Equal((int)Rank.Master, PerkProgression.GetRankState(loaded, thresholds, 0).Rank, "loaded Master with stale XP");
            long experienced = PerkProgression.ExperienceForSkill(850f, 8, cumulative);
            Equal(850000f, experienced, "earned XP above its level floor");
            long novice = PerkProgression.ExperienceForSkill(0f, 0, cumulative);
            Equal((int)Rank.Novice, PerkProgression.GetRankState(novice, thresholds, 0).Rank, "new Novice");
            Equal((int)Rank.HighWitch, PerkProgression.GetRankState(0L, thresholds, PerkProgression.AllMask).Rank, "saved milestone rank floor");
        }

        private static void PercentRounding()
        {
            Equal(438f, PercentText.AbsoluteTenths(43.75), "the reported fractional regeneration bonus");
            Equal(438f, PercentText.AbsoluteTenths(-43.75), "negative adjustment magnitude");
            Equal(0f, PercentText.AbsoluteTenths(-0.04), "no signed negative zero");
            Equal(150f, PercentText.AbsoluteTenths(14.99), "rounds across a whole percentage");
        }

        private static void ProgressionAndCost()
        {
            Equal(40f, PerkProgression.PointsIn(PerkProgression.AllMask), "lifetime points");
            Equal(13f, PerkProgression.MilestoneCount, "milestones");
            Equal(1.1f, CastingRules.PracticeExperience, "Practice Magic XP");
            for (int rank = 0; rank < PerkProgression.RankCount; rank++)
            {
                Equal(50f + 25f * rank, new CastingRules((Rank)rank).Capacity, "rank capacity");
            }

            CastingRules apprentice = new CastingRules(Rank.Apprentice);
            Equal(0.95f, apprentice.CostMultiplier(TierOne), "Apprentice tier I");
            Equal(0.95f, apprentice.CostMultiplier(TierTwo), "Apprentice tier II");
            Equal(1f, apprentice.CostMultiplier(TierThree), "Apprentice tier III");
            CastingRules high = new CastingRules(Rank.HighWitch);
            Equal(0.95f * 0.95f, high.CostMultiplier(TierTwo), "rank discounts stack");
            Equal(0.95f, high.CostMultiplier(TierFive), "High Witch discount");
            high.Lessons = true;
            high.Familiar = true;
            high.Eidetic = 3;
            high.Hexweaver = true;
            high.OldWays = true;
            Equal(0.95f * 0.75f * 0.95f * 0.85f * 0.75f * 0.70f,
                high.CostMultiplier(new MagicSpell(1, MagicSpellKind.Curse)), "compatible discounts");
            Equal(0.95f * 0.85f * 0.75f,
                high.CostMultiplier(new MagicSpell(4, MagicSpellKind.Ritual)), "Old Ways ritual once");
            Equal(0.95f * 0.85f, high.CostMultiplier(TierFive), "ordinary Tier V no specialty discount");
            Equal(0.95f * 0.85f * 0.75f, high.CostMultiplier(Legendary), "Legendary ritual discount once");
            Equal(0.95f * 0.85f * 0.75f, high.CostMultiplier(new MagicSpell(5, MagicSpellKind.Spell)), "Legendary ordinary discount");
        }

        private static void CostBreakdown()
        {
            for (int rank = 0; rank <= (int)Rank.HighWitch; rank++)
            for (int tier = 0; tier <= CastingRules.Legendary; tier++)
            foreach (MagicSpellKind kind in new[] { MagicSpellKind.Spell, MagicSpellKind.Curse, MagicSpellKind.Ritual,
                MagicSpellKind.Curse | MagicSpellKind.Ritual, MagicSpellKind.Charm })
            for (int perks = 0; perks < 32; perks++)
            for (int eidetic = 0; eidetic <= 3; eidetic++)
            {
                CastingRules rules = new CastingRules((Rank)rank);
                rules.Lessons = (perks & 1) != 0;
                rules.Familiar = (perks & 2) != 0;
                rules.Hexweaver = (perks & 4) != 0;
                rules.OldWays = (perks & 8) != 0;
                rules.Ascendancy = (perks & 16) != 0;
                rules.Eidetic = eidetic;
                MagicSpell spell = new MagicSpell(tier, kind);
                float expected = 1f;
                if (tier <= 1 && rank >= 1) expected *= 0.95f;
                if (tier <= 1 && rules.Lessons) expected *= 0.75f;
                if (rank == 4) expected *= 0.95f;
                if (rules.Familiar) expected *= 0.85f;
                if (rules.Hexweaver && spell.IsCurse) expected *= 0.75f;
                if (rules.OldWays && !rules.Ascendancy && (spell.IsRitual || tier == 5)) expected *= 0.75f;
                if (tier < rank && eidetic > 0) expected *= 1f - (eidetic == 1 ? 0.07f : eidetic == 2 ? 0.15f : 0.30f);
                Equal(expected, rules.CostMultiplier(spell), "cost breakdown preserves original cost");
                Equal(expected, rules.CostFactors(spell).Multiplier, "tooltip factors match cast");
            }
        }

        private static void CostRanges()
        {
            CastingRules high = new CastingRules(Rank.HighWitch);
            high.Lessons = true;
            high.Eidetic = 1;
            high.Hexweaver = true;
            float minimum, maximum;
            high.CostRange(MagicSpellKind.Spell, out minimum, out maximum);
            Equal(0.95f * 0.95f * 0.75f * 0.93f, minimum, "ordinary lowest-tier cost");
            Equal(0.95f, maximum, "ordinary current-rank cost");
            high.CostRange(MagicSpellKind.Curse, out minimum, out maximum);
            Equal(0.95f * 0.95f * 0.75f * 0.75f * 0.93f, minimum, "curse lowest-tier cost");
            Equal(0.95f * 0.75f, maximum, "curse current-rank cost");
            high.OldWays = true;
            high.CostRange(MagicSpellKind.Ritual, out minimum, out maximum);
            Equal(0.95f * 0.95f * 0.75f * 0.75f * 0.93f, minimum, "ritual lowest-tier cost");
            Equal(0.95f * 0.75f, maximum, "ritual current-rank cost");
            CastingRules novice = new CastingRules(Rank.Novice);
            novice.Lessons = true;
            novice.CostRange(MagicSpellKind.Spell, out minimum, out maximum);
            Equal(0.75f, minimum, "novice cost");
            Equal(minimum, maximum, "single-tier range collapses");
        }

        private static void PowerPoolConversion()
        {
            CastingRules novice = new CastingRules(Rank.Novice);
            Equal(50f, novice.Capacity, "Novice power pool");
            Equal(40f, novice.FatigueDrain(10f, -100f, 100f), "10 power on a 200-point native range");
            Equal(40f, CastingRules.PowerFraction(-60f, -100f, 100f) * novice.Capacity, "remaining power after Good Luck Charm");
            Equal(20f, novice.FatigueDrain(10f, 0f, 100f), "10 power on a 100-point native range");
            Equal(40f, CastingRules.PowerFraction(20f, 0f, 100f) * novice.Capacity, "same power with a different native range");
            Equal(1f, CastingRules.PowerFraction(-110f, -100f, 100f), "full power clamps to capacity");
            Equal(0f, CastingRules.PowerFraction(110f, -100f, 100f), "depleted power clamps to zero");

            CastingRules high = new CastingRules(Rank.HighWitch);
            high.Ascendancy = true;
            Equal(30f, high.Capacity - high.RankCapacity, "capacity tooltip Ascendancy contribution");
            Equal(10f, high.FatigueDrain(10f, -100f, 80f), "cast cost scales to the perk-adjusted pool");
            Equal(170f, CastingRules.PowerFraction(-90f, -100f, 80f) * high.Capacity, "remaining power with the perk");
        }

        private static void RecoveryAndProtection()
        {
            CastingRules apprentice = new CastingRules(Rank.Apprentice);
            Equal(1f, apprentice.Regeneration(false, false), "early regen");
            Equal(1f, SpellResearchSettings.Default.Speed(apprentice, false), "early study");
            Equal(1f, apprentice.FizzleDuration, "early fizzle");
            CastingRules adept = new CastingRules(Rank.Adept);
            Equal(1.1f, adept.Regeneration(false, false), "Adept regen");
            CastingRules master = new CastingRules(Rank.Master);
            Equal(1.1f, SpellResearchSettings.Default.Speed(master, false), "Master study");
            Equal(0.8f, master.FizzleDuration, "Master fizzle");
            CastingRules high = new CastingRules(Rank.HighWitch);
            Equal(1.15f, high.Regeneration(false, false), "High Witch regen");
            high.InCoven = true;
            high.Rest = 3;
            high.WitchingHour = true;
            Equal(1.15f * 1.15f * 1.20f, high.Regeneration(true, true), "stacked sleep and coven");
            Equal(1.15f * 1.25f * 1.20f, high.Regeneration(false, true), "stacked witching hour and coven");
            PowerRecovery regen = high.RegenerationFactors(false, true, true);
            Equal(1.15f, regen.Rank, "rank regeneration factor");
            Equal(0.80f, regen.Fizzle, "Fizzle regeneration factor");
            Equal(1.15f * 1.25f * 1.20f * 0.80f, regen.Rate, "regeneration includes active Fizzle");
            CastingRules apprenticeWithHour = new CastingRules(Rank.Apprentice);
            apprenticeWithHour.WitchingHour = true;
            Equal(1f, apprenticeWithHour.RegenerationFactors(false, true, true).Rate, "opposing effects can cancel");
            Equal(40f, CastingRules.FamiliarProtection, "Familiar Bond protection");
            for (int upgrade = 1; upgrade <= 3; upgrade++)
            {
                Equal(0.05f * upgrade, CastingRules.RestBonus(upgrade), "Rest tooltip and gameplay total");
                Equal(new[] { 0.07f, 0.15f, 0.30f }[upgrade - 1], CastingRules.EideticDiscount(upgrade), "Eidetic tooltip and gameplay total");
            }
            float[] chances = { 0f, 25f, 40f, 55f };
            for (int i = 0; i < chances.Length; i++)
            {
                high.Scapegoat = i;
                Equal(chances[i], high.ScapegoatChance, "Scapegoat total chance");
            }
        }

        private static void CharmClassification()
        {
            True(MagicSpell.KindOf(SpellCategory.Spell) == MagicSpellKind.Spell, "Spell category maps to Spell");
            True(MagicSpell.KindOf(SpellCategory.Charm) == MagicSpellKind.Charm, "Charm category maps to Charm");
            True(MagicSpell.KindOf(SpellCategory.Curse) == MagicSpellKind.Curse, "Curse category maps to Curse");
            True(MagicSpell.KindOf(SpellCategory.Ritual) == MagicSpellKind.Ritual, "Ritual category maps to Ritual");
            True((MagicSpellKind.Charm & (MagicSpellKind.Curse | MagicSpellKind.Ritual)) == 0, "Charm flag overlaps Curse or Ritual");
            True((int)MagicSpellKind.Curse == 1 && (int)MagicSpellKind.Ritual == 2, "Existing kind values changed");
            for (int tier = 0; tier <= 5; tier++)
            {
                MagicSpell charm = new MagicSpell(tier, MagicSpell.KindOf(SpellCategory.Charm));
                True(!charm.IsCurse && !charm.IsRitual, "Charm satisfies a Curse or Ritual test");
            }
            float[] wands = { 0f, 5f, 20f, 25f };
            for (int rank = 0; rank <= 4; rank++)
            for (int tier = 0; tier <= 5; tier++)
            for (int perks = 0; perks < 256; perks++)
            {
                CastingRules rules = new CastingRules((Rank)rank);
                rules.Lessons = (perks & 1) != 0;
                rules.Familiar = (perks & 2) != 0;
                rules.Hexweaver = (perks & 4) != 0;
                rules.OldWays = (perks & 8) != 0;
                rules.Ascendancy = (perks & 16) != 0;
                rules.InCoven = (perks & 32) != 0;
                rules.DarkArts = (perks & 64) != 0;
                rules.Eidetic = (perks & 128) != 0 ? 3 : 0;
                MagicSpell charm = new MagicSpell(tier, MagicSpellKind.Charm);
                MagicSpell spell = new MagicSpell(tier, MagicSpellKind.Spell);
                Equal(rules.CostMultiplier(spell), rules.CostMultiplier(charm), "Charm cost differs from the ordinary baseline");
                SpellCostFactors factors = rules.CostFactors(charm);
                Equal(1f, factors.Hexweaver, "Hexweaver discounted a charm");
                Equal(0.70f, rules.RefundFraction(charm), "Charm refund is not the ordinary spell refund");
                True(CastingRules.CovenEligible(charm) == (tier <= 2), "Power of Three eligibility for Tier I-III charms");
                True(CastingRules.CovenEligible(charm) == CastingRules.CovenEligible(spell), "Charm and spell coven eligibility differ");
                Equal(rules.AccuracyFor(spell, 0f, 0f, 0f, 0).Overreach, rules.AccuracyFor(charm, 0f, 0f, 0f, 0).Overreach, "Charm rank penalty differs");
                if (tier == CastingRules.Legendary)
                {
                    continue;
                }
                Equal(1f, factors.OldWays, "The Old Ways discounted an ordinary charm");
                Equal(0f, rules.SpecialtyAccuracyBonus(charm), "Specialty accuracy applied to an ordinary charm");
                foreach (float wand in wands)
                {
                    SpellAccuracy a = rules.AccuracyFor(charm, wand, 18f, 10f, 0);
                    SpellAccuracy b = rules.AccuracyFor(spell, wand, 18f, 10f, 0);
                    Equal(b.Total, a.Total, "Charm accuracy differs from the ordinary baseline");
                    Equal(b.Coven, a.Coven, "Charm coven adjustment differs");
                    Equal(b.DarkArts, a.DarkArts, "Charm Dark Arts offset differs");
                    Equal(b.Ascendancy, a.Ascendancy, "Charm Ascendancy bonus differs");
                }
            }
            CastingRules covenAdept = new CastingRules(Rank.Adept);
            covenAdept.InCoven = true;
            SpellAccuracy coven = covenAdept.AccuracyFor(new MagicSpell(2, MagicSpellKind.Charm), 0f, 0f, 0, 0);
            True(coven.Coven > 0f, "The Power of Three skipped a Tier III charm");
            Equal(0f, covenAdept.AccuracyFor(new MagicSpell(2, MagicSpellKind.Ritual), 0f, 0f, 0, 0).Coven, "The Power of Three reached a ritual");
            CastingRules hex = new CastingRules(Rank.HighWitch);
            hex.Hexweaver = true;
            Equal(0.75f, hex.CostFactors(new MagicSpell(2, MagicSpellKind.Curse)).Hexweaver, "Hexweaver lost its curse discount");
            CastingRules oldWays = new CastingRules(Rank.HighWitch);
            oldWays.OldWays = true;
            Equal(CastingRules.OldWaysAccuracyBonus, oldWays.SpecialtyAccuracyBonus(new MagicSpell(4, MagicSpellKind.Curse)), "The Old Ways lost its curse accuracy");
            Equal(0.75f, oldWays.CostFactors(new MagicSpell(4, MagicSpellKind.Ritual)).OldWays, "The Old Ways lost its ritual discount");
            Equal(0.75f, oldWays.CostFactors(new MagicSpell(5, MagicSpellKind.Charm)).OldWays, "The Old Ways lost its Legendary discount");
        }

        private static void FixedCosts()
        {
            foreach (SpellEntry entry in SpellCatalog.Entries)
            {
                if (entry.Learning == SpellLearning.NativeDisabled) continue;
                MagicSpell spell = MagicSpell.For(entry);
                for (int rank = 0; rank <= (int)Rank.HighWitch; rank++)
                for (int ascendancy = 0; ascendancy < 2; ascendancy++)
                {
                    CastingRules rules = new CastingRules((Rank)rank);
                    rules.Ascendancy = ascendancy == 1;
                    float multiplier = rules.CostMultiplier(spell);
                    Equal(entry.Cost * multiplier, rules.FinalCost(entry.Cost, spell, 1f), "final cost is base times perks: " + entry.Id);
                    Equal(entry.Cost * multiplier * 1.3f, rules.FinalCost(entry.Cost, spell, 1.3f), "wand and moon multiply the final cost");
                    float final = rules.FinalCost(entry.Cost, spell, 1.15f);
                    float drain = rules.FatigueDrain(final, -100f, 100f);
                    Equal(final, drain / 200f * rules.Capacity, "power drained equals the quoted cost: " + entry.Id);
                    foreach (float native in new[] { 0f, 10f, 25f, 60f })
                    {
                        float correction = rules.NativeCorrection(entry.Cost, spell, native, 1.15f, -100f, 100f);
                        Equal(drain, native * 1.15f + correction, "native drain is corrected, not doubled: " + entry.Id);
                    }
                }
            }
            SpellEntry conversion = SpellCatalog.Find("conversion");
            for (int rank = 0; rank <= (int)Rank.HighWitch; rank++)
            {
                CastingRules rules = new CastingRules((Rank)rank);
                Equal(45f * rules.CostMultiplier(MagicSpell.For(conversion)), rules.FinalCost(conversion.Cost, MagicSpell.For(conversion), 1f),
                    "Conversion stays 45 before modifiers at every rank");
            }
            CastingRules novice = new CastingRules(Rank.Novice);
            Equal(45f, novice.FinalCost(conversion.Cost, MagicSpell.For(conversion), 1f), "Novice Conversion costs exactly 45");
            CastingRules high = new CastingRules(Rank.HighWitch);
            high.Lessons = true;
            high.Familiar = true;
            Equal(30f * 0.95f * 0.95f * 0.75f * 0.85f, high.FinalCost(30f, MagicSpell.For(SpellCatalog.Find("good_luck")), 1f),
                "discounts apply to the fixed cost");
            Equal(110f * 0.95f * 0.85f, high.FinalCost(110f, MagicSpell.For(SpellCatalog.Find("reanimation")), 1f),
                "Tier V ritual skips lower-tier discounts");
            Equal(FullDrain(high, 30f, MagicSpell.For(SpellCatalog.Find("upgrade"))),
                high.NativeCorrection(30f, MagicSpell.For(SpellCatalog.Find("upgrade")), 0f, 1f, -100f, 100f),
                "Magically Upgrade receives its whole cost from the correction");
        }

        private static void InnateCasters()
        {
            Equal(0f, (float)CastingRules.RankForSkill(0), "skill level 0 is Tier I");
            Equal(1f, (float)CastingRules.RankForSkill(3), "skill level 3 is Tier II");
            Equal(4f, (float)CastingRules.RankForSkill(10), "skill level 10 is the top rank");
            CastingRules witch = new CastingRules(Rank.Novice);
            CastingRules fairy = new CastingRules(Rank.Novice, CasterKind.Fairy);
            CastingRules genie = new CastingRules(Rank.Novice, CasterKind.Genie);
            Equal(50f, witch.AccuracyFor(TierOne, 0f, 0f, 0f, 0).Total, "Witch base accuracy");
            Equal(60f, genie.AccuracyFor(TierOne, genie.InnateAccuracy, 0f, 0f, 0).Total, "Genie base accuracy");
            Equal(85f, fairy.AccuracyFor(TierOne, fairy.InnateAccuracy, 0f, 0f, 0).Total, "Fairy base accuracy with Innate Power");
            Equal(60f, fairy.AccuracyFor(TierOne, 0f, 0f, 0f, 0).Starting, "breakdown reports the innate base");
            Equal(1f, fairy.CostMultiplier(TierOne), "Fairy pays the fixed cost");
            Equal(1.25f, genie.CostMultiplier(TierOne), "Genie borrowed spells cost more");
            Equal(30f * 1.25f, genie.FinalCost(30f, TierOne, 1f), "Genie Good Luck Charm cost");
            Equal(100f, fairy.Capacity, "Fairy power pool");
            Equal(100f, new CastingRules(Rank.HighWitch, CasterKind.Fairy).Capacity, "Fairy pool ignores rank");
            for (int rank = 0; rank <= (int)Rank.HighWitch; rank++)
                Equal(100f + 50f * rank, new CastingRules((Rank)rank, CasterKind.Genie).Capacity, "Genie pool doubles the Witch pool");
            Equal(100f, genie.BaseCapacity, "Genie base pool");
            Equal(0.5f, SpellResearchSettings.Default.Speed(genie, false), "half-speed study");
            Equal(0.5f, SpellResearchSettings.Default.Speed(new CastingRules(Rank.Apprentice, CasterKind.Genie), false), "Awakened has no learning bonus");
            Equal(0.5f * 1.25f, SpellResearchSettings.Default.Speed(new CastingRules(Rank.Adept, CasterKind.Genie), false), "Ascendant learns 25% faster");
            Equal(0.5f * 1.25f, SpellResearchSettings.Default.Speed(new CastingRules(Rank.Master, CasterKind.Genie), false), "Transcendent does not grant the bonus again");
            Equal(0.5f * 1.25f, SpellResearchSettings.Default.Speed(new CastingRules(Rank.HighWitch, CasterKind.Genie), false), "Almighty does not grant the bonus again");
            Equal(0.5f, SpellResearchSettings.Default.Speed(new CastingRules(Rank.HighWitch, CasterKind.Fairy), false), "Fairies keep half-speed study");
            Equal(1f, new CastingRules(Rank.Master, CasterKind.Fairy).FizzleDuration, "no Master fizzle reduction for Fairies");
            for (int rank = 0; rank <= (int)Rank.HighWitch; rank++)
            {
                CastingRules ranked = new CastingRules((Rank)rank, CasterKind.Genie);
                Equal(1f, ranked.FizzleDuration, "ranks never shorten Backlash");
                Equal(1f, ranked.AbilityCostFactor, "ranks never cut Genie ability cost");
                Equal(1f, ranked.Regeneration(false, false), "ranks never raise regeneration");
                Equal(0f, ranked.BacklashCut, "ranks never cut Backlash chance");
                Equal(2f * rank, ranked.AccuracyFor(TierOne, 0f, 0f, 0f, 0).RankBonus, "Genie ranks add 2 accuracy points each");
                Equal(5f * rank, new CastingRules((Rank)rank).AccuracyFor(TierOne, 0f, 0f, 0f, 0).RankBonus, "Witch ranks keep 5 accuracy points each");
            }
            Equal(0f, fairy.BacklashChance(TierOne, 60f), "Fairies have no Backlash");
            Equal(0f, witch.BacklashChance(TierOne, 50f), "Witches fizzle without Backlash");
            Equal(40f, genie.BacklashChance(TierOne, 60f), "every failure rebounds");
            Equal(35f + 65f * 0.05f, genie.BacklashChance(TierTwo, 65f), "Tier II spells that work can rebound");
            Equal(5f, genie.BacklashChance(TierOne, 100f), "Tier I Backlash never drops below 5%");
            Equal(8f, genie.BacklashChance(TierTwo, 100f), "Tier II Backlash never drops below 8%");
            Equal(12f, genie.BacklashChance(TierThree, 100f), "Tier III Backlash never drops below 12%");
            Equal(16f, genie.BacklashChance(TierFour, 100f), "Tier IV Backlash never drops below 16%");
            Equal(20f, genie.BacklashChance(TierFive, 100f), "Tier V Backlash never drops below 20%");
            CastingRules lamp = new CastingRules(Rank.HighWitch, CasterKind.Genie);
            Equal(20f + 80f * 0.20f, lamp.BacklashChance(new MagicSpell(4, MagicSpellKind.Spell), 80f), "Almighty cancels none of the Backlash");
            Equal(0f, lamp.ReboundChance(new MagicSpell(CastingRules.Legendary, MagicSpellKind.Spell)), "Legendary magic has no rebound table");
            CastingRules top = new CastingRules(Rank.HighWitch, CasterKind.Genie);
            Equal(0f, top.AccuracyFor(new MagicSpell(3, MagicSpellKind.Curse), 0f, 0f, 0f, 0).Overreach, "Tier IV casts without a penalty");
            Equal(0f, top.AccuracyFor(new MagicSpell(4, MagicSpellKind.Ritual), 0f, 0f, 0f, 0).Overreach, "Tier V casts without a penalty at the top skill rank");
            Equal(2f, top.HighestTier, "highest learnable innate tier is III");
            Equal(1.25f, top.CostMultiplier(TierTwo), "no Witch rank discounts for a Genie");
            Equal(8f, top.AccuracyFor(new MagicSpell(3, MagicSpellKind.Curse), 0f, 0f, 0f, 0).RankBonus, "Almighty adds 8 accuracy points in total");
            Equal(78f, top.AccuracyFor(new MagicSpell(3, MagicSpellKind.Curse), 0f, 0f, 0f, 0).Total, "Genie Tier IV accuracy at level 10");
            Equal(-45f, new CastingRules(Rank.Novice, CasterKind.Fairy).AccuracyFor(TierTwo, 0f, 0f, 0f, 0).Overreach, "skill tier penalizes higher spells");
            Equal(1f, new CastingRules(Rank.Novice).FirstOverreachTier, "Novice penalty starts at Tier II");
            Equal(-1f, new CastingRules(Rank.HighWitch).FirstOverreachTier, "High Witch has no penalized learnable tier");
            Equal(2f, new CastingRules(Rank.Apprentice, CasterKind.Genie).FirstOverreachTier, "Innate penalty reaches Tier III");
            Equal(-1f, new CastingRules(Rank.Adept, CasterKind.Fairy).FirstOverreachTier, "Innate casters cannot learn past Tier III");
            CastingRules novice = new CastingRules(Rank.Novice);
            SpellAccuracy reach = novice.AccuracyFor(TierTwo, 0f, 0f, 0f, 0);
            Equal(-45f, reach.Overreach, "one tier above rank costs 45% accuracy");
            Equal(5f, reach.Total, "Novice Tier II accuracy is low but possible");
            Equal(-90f, novice.AccuracyFor(new MagicSpell(2, MagicSpellKind.Spell), 0f, 0f, 0f, 0).Overreach, "penalty scales per tier");
            Equal(0f, novice.AccuracyFor(new MagicSpell(2, MagicSpellKind.Spell), 0f, 0f, 0f, 0).Total, "accuracy never drops below 0%");
            Equal(5f + 25f, novice.AccuracyFor(TierTwo, 25f, 0f, 0f, 0).Total, "bonuses still apply above rank");
            Equal(0f, novice.AccuracyFor(TierOne, 0f, 0f, 0f, 0).Overreach, "no penalty at or below rank");
            float minimum, maximum;
            top.CostRange(MagicSpellKind.Spell, out minimum, out maximum);
            Equal(1.25f, minimum, "Genie cost range keeps the borrowed-spell factor without Witch rank discounts");
        }

        private static float FullDrain(CastingRules rules, float cost, MagicSpell spell)
        {
            return rules.FatigueDrain(rules.FinalCost(cost, spell, 1f), -100f, 100f);
        }

        private static void GeniePerkRules()
        {
            CastingRules unbound = new CastingRules(Rank.Master, CasterKind.Genie);
            unbound.Unbound = true;
            Equal(250f * 1.15f, unbound.Capacity, "Unbound adds 15% power");
            unbound.Wellspring = 3;
            Equal(250f * 1.45f, unbound.Capacity, "Wellspring's third upgrade adds 30% on top of Unbound");
            Equal(250f * 1.45f, WithExhaustion(unbound).Capacity, "Wish Exhaustion preserves the true maximum");
            Equal(1f * 1.25f, unbound.FizzleDuration, "Unbound lengthens Genie Backlash by 25%; Witch rank reductions do not apply");
            Equal(1.1f, unbound.AbilityCostFactor, "Unbound raises ability costs by 10%");
            Equal(0.75f, unbound.CostFactors(TierTwo).Innate, "Unbound retains the 25% Ancient Sorcery discount");
            Equal(1f, unbound.CostFactors(TierTwo).Path, "Unbound does not apply an additional spell cost discount");
            Equal(0.75f, unbound.CostMultiplier(TierTwo), "Unbound only applies the 25% Ancient Sorcery discount");
            unbound.RisingTide = true;
            unbound.Tide = 9;
            unbound.AtHomeLamp = true;
            Equal(1f + 0.25f + 0.25f, unbound.GenieRegeneration, "Rising Tide stops at five stacks and Pocket Palace adds 25% at home");
            unbound.Exhausted = true;
            Equal(0.5f * 1.5f, unbound.GenieRegeneration, "Wish Exhaustion halves regeneration");
            CastingRules retreat = new CastingRules(Rank.Adept, CasterKind.Genie);
            retreat.InLamp = true;
            retreat.AtHomeLamp = true;
            retreat.FortuneRain = true;
            retreat.Nights = 2;
            Equal(1.85f, retreat.GenieRegeneration, "Lamp recovery stacks with Pocket Palace and weather");
            Equal(1.85f * 1.2f, retreat.RegenerationFactors(true, false, false).Rate, "Lamp recovery counts as sleep for perks");
            retreat.Exhausted = true;
            Equal(1.85f * 1.2f * .5f, retreat.RegenerationFactors(true, false, false).Rate, "Lamp recovery respects exhaustion");
            retreat.FateSealed = true;
            Equal(0f, retreat.RegenerationFactors(true, false, false).Rate, "Chains overrides recovery with all bonuses active");
            unbound.Exhausted = false;
            unbound.RisingTide = false;
            Equal(1.25f, unbound.GenieRegeneration, "streaks do nothing without Rising Tide");
            unbound.FortuneRain = true;
            Equal(1.75f, unbound.GenieRegeneration, "Fortune's Rain adds 50% regeneration");
            Equal(40f * 0.75f, unbound.BacklashChance(TierOne, 60f), "Fortune's Rain cuts Backlash chance by a quarter");
            Equal(5f, unbound.BacklashChance(TierOne, 100f), "Fortune's Rain never cuts Backlash below its floor");
            unbound.FortuneRain = false;
            unbound.RisingTide = true;
            Equal(10f, unbound.AccuracyFor(TierOne, 0f, 0f, 0f, 0).RisingTide, "Rising Tide adds 10% accuracy while a streak is running");
            unbound.Tide = 0;
            Equal(0f, unbound.AccuracyFor(TierOne, 0f, 0f, 0f, 0).RisingTide, "no streak, no accuracy");
            Equal(-45f, unbound.AccuracyFor(TierFive, 0f, 0f, 0f, 0).Overreach, "the above-rank penalty is never removed by a perk");
            unbound.ExtraTiers = 1;
            Equal(3f, unbound.HighestLearnableTier, "Fugitive of Old reaches Tier IV");
            unbound.ExtraTiers = 2;
            Equal(4f, unbound.HighestLearnableTier, "Unleashed reaches Tier V");

            CastingRules lamp = new CastingRules(Rank.HighWitch, CasterKind.Genie);
            lamp.Lampbound = true;
            Equal(0.15f, lamp.BacklashCut, "Lampbound alone cuts Backlash by 15%");
            Equal(0.9f, lamp.AbilityCostFactor, "Lampbound cuts ability costs by 10%");
            Equal(1.25f * 1.1f, lamp.CostMultiplier(TierTwo), "Lampbound raises spell costs by 10%");
            lamp.FinePrint = true;
            Equal(1.25f * 1.1f * 1.15f, lamp.CostMultiplier(TierTwo), "Delicate Casting costs 15% more");
            Equal(5f, lamp.AccuracyFor(TierOne, 0f, 0f, 0f, 0).FinePrint, "Delicate Casting adds 5% accuracy");
            Equal(5f, lamp.BacklashChance(TierOne, 100f), "Lampbound still leaves the Tier I floor");
            Equal(16f, lamp.BacklashChance(TierFour, 100f), "the floor rises with the spell's tier");
            Equal(50f * 0.85f, lamp.BacklashChance(TierOne, 50f), "a cast at 50% accuracy backlashes after Lampbound's cut");

            CastingRules unleashed = new CastingRules(Rank.HighWitch, CasterKind.Genie);
            unleashed.Unbound = true;
            Equal(50f, unleashed.BacklashChance(TierOne, 50f), "Unbound removes no Backlash");
            unleashed.Unleashed = true;
            Equal(50f * 1.15f, unleashed.BacklashChance(TierOne, 50f), "Unleashed raises Backlash by 15%");
            Equal(5f, unleashed.BacklashChance(TierOne, 100f), "Unleashed never removes the floor");

            CastingRules sleeper = new CastingRules(Rank.Novice, CasterKind.Genie);
            sleeper.Nights = 3;
            Equal(1f + 0.30f, sleeper.RegenerationFactors(true, false, false).Rest, "A Thousand and One Nights adds 30% sleeping regeneration");
            Equal(1f, sleeper.RegenerationFactors(false, false, false).Rest, "Nights only works asleep");
        }

        private static CastingRules WithExhaustion(CastingRules source)
        {
            CastingRules copy = source;
            copy.Exhausted = true;
            return copy;
        }

        private static void Equal(float expected, float actual, string check)
        {
            True(Math.Abs(actual - expected) < 0.001f, check + " (" + expected + " expected, got " + actual + ")");
        }

        private static void True(bool pass, string check)
        {
            if (!pass)
            {
                failures.Add(check);
            }
            checks++;
        }
    }
}
