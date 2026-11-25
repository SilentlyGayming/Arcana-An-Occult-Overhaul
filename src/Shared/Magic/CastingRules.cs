using System;

namespace SupernaturalExpanded.Journal
{
    [Flags]
    internal enum MagicSpellKind
    {
        Spell = 0,
        Curse = 1,
        Ritual = 2,
        Charm = 4
    }

    internal struct MagicSpell
    {
        internal readonly int Tier;
        internal readonly MagicSpellKind Kind;

        internal MagicSpell(int tier, MagicSpellKind kind)
        {
            Tier = tier;
            Kind = kind;
        }

        internal bool IsCurse { get { return (Kind & MagicSpellKind.Curse) != 0; } }
        internal bool IsRitual { get { return (Kind & MagicSpellKind.Ritual) != 0; } }

        internal static MagicSpell For(SpellEntry entry)
        {
            return new MagicSpell(entry.Tier, KindOf(entry.Category));
        }

        internal static MagicSpellKind KindOf(SpellCategory category)
        {
            switch (category)
            {
                case SpellCategory.Charm: return MagicSpellKind.Charm;
                case SpellCategory.Curse: return MagicSpellKind.Curse;
                case SpellCategory.Ritual: return MagicSpellKind.Ritual;
                default: return MagicSpellKind.Spell;
            }
        }
    }

    internal struct SpellAccuracy
    {
        internal const float Minimum = 0f;
        internal const float Maximum = 95f;

        internal float Starting;
        internal float RankBonus;
        internal float Overreach;
        internal float Wand;
        internal float Mood;
        internal float Needs;
        internal float DarkArts;
        internal float FinePrint;
        internal float Ascendancy;
        internal float RisingTide;
        internal float Coven;
        internal float Limit;
        internal float Ceiling;
        internal float Total;
    }

    internal struct PowerRecovery
    {
        internal static readonly PowerRecovery Baseline = new PowerRecovery(1f, 1f, 1f, 1f, 1f, 1f);

        internal readonly float Rank;
        internal readonly float Rest;
        internal readonly float Hour;
        internal readonly float Coven;
        internal readonly float Fizzle;
        internal readonly float Genie;

        internal PowerRecovery(float rank, float rest, float hour, float coven, float fizzle, float genie)
        {
            Rank = rank;
            Rest = rest;
            Hour = hour;
            Coven = coven;
            Fizzle = fizzle;
            Genie = genie;
        }

        internal float Rate { get { return Rank * Rest * Hour * Coven * Fizzle * Genie; } }
    }

    internal struct SpellCostFactors
    {
        internal float Apprentice, Lessons, HighWitch, Familiar, Hexweaver, OldWays, Eidetic, Innate, FinePrint, Path;

        internal float Multiplier
        {
            get { return Apprentice * Lessons * HighWitch * Familiar * Hexweaver * OldWays * Eidetic * Innate * FinePrint * Path; }
        }
    }

    internal struct CastingRules
    {
        internal const int Legendary = 5;
        internal const float PracticeExperience = 1.10f;
        internal const float FamiliarProtection = 40f;
        internal const float DarkArtsRefundChance = 25f;
        internal static float NeedPenalty(float remaining)
        {
            if (remaining <= 0.05f) return 35f;
            if (remaining <= 0.20f) return 25f;
            if (remaining <= 0.35f) return 10f;
            return 0f;
        }
        internal const float MaxEmotionPenalty = 30f;
        internal const float MagicHandsAccuracyBonus = 20f;
        internal const float LessonsDiscount = 0.25f;
        internal const float FamiliarDiscount = 0.15f;
        internal const float HexweaverDiscount = 0.25f;
        internal const float HexweaverDuration = 1.25f;
        internal const float RestPerUpgrade = 0.05f;
        internal const float WitchingHourRegen = 0.25f;
        internal const float CovenRegen = 0.20f;
        internal const float CovenFailureReduction = 0.10f;
        internal const int CovenMaximumTier = (int)Rank.Adept;
        internal const float DarkArtsPenaltyReduction = 0.50f;
        internal const float AscendancyCapacityBonus = 0.20f;
        internal const float AscendancyAccuracyBonus = 10f;
        internal const float OldWaysDiscount = 0.25f;
        internal const float OldWaysAccuracyBonus = 15f;
        internal const float OverreachPenaltyPerTier = 45f;
        internal const float UnboundCapacityBonus = 0.15f;
        internal const float AncientSorceryCost = 0.75f;
        internal const float UnboundAbilityCost = 0.10f;
        internal const float UnboundBacklashDuration = 1.25f;
        internal const float LampboundBacklashCut = 0.15f;
        internal const float LampboundAbilityDiscount = 0.10f;
        internal const float LampboundSpellCost = 0.10f;
        internal const float RisingTidePerStack = 0.05f;
        internal const int RisingTideMostStacks = 5;
        internal const float RisingTideAccuracy = 10f;
        internal const float HomeLampRegen = 0.25f;
        internal const float FinePrintAccuracy = 5f;
        internal const float FinePrintCost = 1.15f;
        internal const float DomesticDjinnDiscount = 0.25f;
        internal const float SilkenEnsorcelDiscount = 0.25f;
        internal const float SilkenEnsorcelLonger = 0.50f;
        internal const float SilkenBanishRefund = 0.50f;
        internal const float FugitiveReturn = 0.25f;
        internal const float UnleashedBacklash = 0.15f;
        internal const float UnleashedRedirectChance = 50f;
        internal const float GenieLawWishBonus = 20f;
        internal const float ExhaustionRecoveryLimit = 0.75f;
        internal const float ExhaustionRegeneration = 0.50f;
        internal const float ExhaustionDays = 7f;
        internal const float ChainsHours = 24f;
        internal const float MisfortuneHours = 24f;
        internal const float DebtShare = 0.25f;
        internal const int WishFortune = 100000;
        internal const float RedirectRange = 12f;
        internal const float RetreatRegeneration = 0.10f;
        internal const float RainRegen = 0.50f;
        internal const float RainBacklashCut = 0.25f;

        internal static float WellspringBonus(int upgrade)
        {
            return 0.10f * Math.Max(0, Math.Min(3, upgrade));
        }

        internal static float NightsBonus(int upgrade)
        {
            return 0.10f * Math.Max(0, Math.Min(3, upgrade));
        }

        internal static float EideticDiscount(int upgrade)
        {
            return upgrade <= 0 ? 0f : upgrade == 1 ? 0.07f : upgrade == 2 ? 0.15f : 0.30f;
        }

        internal static float RestBonus(int upgrade)
        {
            return RestPerUpgrade * Math.Max(0, Math.Min(3, upgrade));
        }

        internal static float ScapegoatProtection(int upgrade)
        {
            return upgrade <= 0 ? 0f : upgrade == 1 ? 25f : upgrade == 2 ? 40f : 55f;
        }

        internal float GeneralAccuracyBonus
        {
            get { return Ascendancy ? AscendancyAccuracyBonus : 0f; }
        }

        internal float SpecialtyAccuracyBonus(MagicSpell spell)
        {
            return OldWays && !Ascendancy && (spell.IsCurse || spell.IsRitual || spell.Tier == Legendary)
                ? OldWaysAccuracyBonus : 0f;
        }

        internal readonly Rank Rank;
        internal readonly CasterKind Caster;
        internal bool Lessons;
        internal bool Familiar;
        internal bool Hexweaver;
        internal bool DarkArts;
        internal bool InCoven;
        internal bool Ascendancy;
        internal bool OldWays;
        internal int Eidetic;
        internal int Rest;
        internal int Nights;
        internal bool WitchingHour;
        internal int Scapegoat;
        internal bool Unbound;
        internal bool Lampbound;
        internal int Wellspring;
        internal int Tide;
        internal bool AtHomeLamp;
        internal bool InLamp;
        internal bool Exhausted;
        internal bool FateSealed;
        internal bool FortuneRain;
        internal bool FinePrint;
        internal int ExtraTiers;
        internal bool RisingTide;
        internal bool DomesticDjinn;
        internal bool SilkenCommand;
        internal bool Fugitive;
        internal bool Unleashed;
        internal bool GenieLaw;
        internal bool Counterattack;
        internal bool LawOfTheLamp;

        internal CastingRules(Rank rank) : this()
        {
            Rank = rank;
        }

        internal CastingRules(Rank rank, CasterKind caster) : this()
        {
            Rank = rank;
            Caster = caster;
        }

        internal CasterProfile Profile { get { return CasterProfile.For(Caster); } }
        internal bool Innate { get { return Profile.Innate; } }
        internal float BaseAccuracy { get { return Profile.BaseAccuracy; } }
        internal float InnateAccuracy { get { return Profile.InnateAccuracy; } }

        internal static Rank RankForSkill(int level)
        {
            return (Rank)Math.Max(0, Math.Min((int)Rank.HighWitch, level / 2));
        }

        internal float BaseCapacity { get { return Profile.Capacity(Rank.Novice); } }
        internal float RankCapacity { get { return Profile.Capacity(Rank); } }
        internal float Capacity { get { return RankCapacity * (Ascendancy ? 1f + AscendancyCapacityBonus : 1f) * GenieCapacity; } }
        internal float GenieCapacity
        {
            get
            {
                return 1f + (Unbound ? UnboundCapacityBonus : 0f) + WellspringBonus(Wellspring);
            }
        }
        internal float FizzleDuration { get { return Profile.FizzleDuration(Rank) * (Unbound ? UnboundBacklashDuration : 1f); } }
        internal float AbilityCostFactor
        {
            get { return (Unbound ? 1f + UnboundAbilityCost : 1f) * (Lampbound ? 1f - LampboundAbilityDiscount : 1f); }
        }

        internal float OverreachPenalty { get { return OverreachPenaltyPerTier; } }

        internal int HighestLearnableTier
        {
            get { return Math.Min(Legendary - 1, Profile.HighestLearnableTier + ExtraTiers); }
        }
        internal bool HasBacklash { get { return Profile.Backlash; } }

        internal float Regeneration(bool asleep, bool witchingHour)
        {
            return RegenerationFactors(asleep, witchingHour, false).Rate;
        }

        internal PowerRecovery RegenerationFactors(bool asleep, bool witchingHour, bool fizzled)
        {
            float rankBonus = Profile.RankRegeneration(Rank);
            float sleepBonus = asleep ? 1f + RestBonus(Rest) + NightsBonus(Nights) : 1f;
            float hourBonus = !asleep && witchingHour && WitchingHour ? 1f + WitchingHourRegen : 1f;
            return new PowerRecovery(rankBonus, sleepBonus, hourBonus, InCoven ? 1f + CovenRegen : 1f, fizzled ? 0.80f : 1f, GenieRegeneration);
        }

        internal float GenieRegeneration
        {
            get
            {
                if (FateSealed) return 0f;
                int stacks = RisingTide ? Math.Max(0, Math.Min(RisingTideMostStacks, Tide)) : 0;
                float regeneration = 1f + stacks * RisingTidePerStack + (AtHomeLamp ? HomeLampRegen : 0f) + (FortuneRain ? RainRegen : 0f) + (InLamp ? RetreatRegeneration : 0f);
                return Exhausted ? regeneration * (1f - ExhaustionRegeneration) : regeneration;
            }
        }

        internal float FatigueDrain(float powerCost, float nativeMinimum, float nativeMaximum)
        {
            return powerCost * Math.Max(0f, nativeMaximum - nativeMinimum) / Capacity;
        }

        internal static float PowerFraction(float fatigue, float nativeMinimum, float nativeMaximum)
        {
            if (nativeMaximum <= nativeMinimum)
            {
                return 0f;
            }
            return Math.Max(0f, Math.Min(1f, (nativeMaximum - fatigue) / (nativeMaximum - nativeMinimum)));
        }

        internal float Accuracy(MagicSpell spell, float wand, float emotionPenalty, float needPenalty, int emptyNeeds)
        {
            return AccuracyFor(spell, wand, emotionPenalty, needPenalty, emptyNeeds).Total;
        }

        internal SpellAccuracy AccuracyFor(MagicSpell spell, float wand, float emotionPenalty, float needPenalty, int emptyNeeds)
        {
            SpellAccuracy result = new SpellAccuracy();
            result.Starting = BaseAccuracy;
            result.RankBonus = Profile.RankAccuracy(Rank);
            result.Wand = wand;
            result.Mood = -Math.Max(0f, Math.Min(MaxEmotionPenalty, emotionPenalty));
            result.Needs = -Math.Max(0f, needPenalty);
            result.DarkArts = DarkArts ? -DarkArtsPenaltyReduction * result.Mood : 0f;
            result.FinePrint = FinePrint ? FinePrintAccuracy : 0f;
            result.RisingTide = RisingTide && Tide > 0 ? RisingTideAccuracy : 0f;
            result.Ascendancy = GeneralAccuracyBonus;
            result.Ceiling = emptyNeeds >= 2 ? 5f : emptyNeeds == 1 ? 15f : SpellAccuracy.Maximum;
            if (spell.Tier == Legendary)
            {
                result.Total = float.NaN;
                return result;
            }
            int above = spell.Tier - (int)Rank;
            float tierBonus = above > 0 ? 0f : 10f * -above;
            result.Overreach = above > 0 ? -OverreachPenalty * above : 0f;
            float curseOrRitual = SpecialtyAccuracyBonus(spell);
            float raw = result.Starting + result.RankBonus + tierBonus + result.Overreach + wand;
            raw += result.Mood + result.DarkArts + result.Needs + result.FinePrint;
            raw += result.Ascendancy + curseOrRitual + result.RisingTide;
            float bounded = Math.Max(SpellAccuracy.Minimum, Math.Min(SpellAccuracy.Maximum, raw));
            float withCoven = InCoven && CovenEligible(spell) ? 100f - (100f - bounded) * (1f - CovenFailureReduction) : bounded;
            result.Coven = withCoven - bounded;
            result.Total = Math.Min(result.Ceiling, withCoven);
            result.Limit = result.Total - raw - result.Coven;
            return result;
        }

        internal static bool CovenEligible(MagicSpell spell)
        {
            return spell.Tier >= 0 && spell.Tier <= CovenMaximumTier && !spell.IsRitual;
        }

        internal SpellCostFactors CostFactors(MagicSpell spell)
        {
            bool earlyTier = spell.Tier <= (int)Rank.Apprentice;
            SpellCostFactors factors = new SpellCostFactors();
            factors.Apprentice = earlyTier ? 1f - Profile.EarlyTierDiscount(Rank) : 1f;
            factors.Lessons = earlyTier && Lessons ? 1f - LessonsDiscount : 1f;
            factors.HighWitch = 1f - Profile.RankDiscount(Rank);
            factors.Familiar = Familiar ? 1f - FamiliarDiscount : 1f;
            factors.Hexweaver = Hexweaver && spell.IsCurse ? 1f - HexweaverDiscount : 1f;
            factors.OldWays = OldWays && !Ascendancy && (spell.IsRitual || spell.Tier == Legendary) ? 1f - OldWaysDiscount : 1f;
            factors.Eidetic = spell.Tier < (int)Rank && Eidetic > 0 ? 1f - EideticDiscount(Eidetic) : 1f;
            factors.Innate = Unbound ? AncientSorceryCost : Profile.InnateSpellCost;
            factors.FinePrint = FinePrint ? FinePrintCost : 1f;
            factors.Path = Lampbound ? 1f + LampboundSpellCost : 1f;
            return factors;
        }

        internal float CostMultiplier(MagicSpell spell)
        {
            return CostFactors(spell).Multiplier;
        }

        internal float FinalCost(float baseCost, MagicSpell spell, float drainMultiplier)
        {
            return baseCost * CostMultiplier(spell) * drainMultiplier;
        }

        internal float NativeCorrection(float baseCost, MagicSpell spell, float nativeDrain, float drainMultiplier,
            float nativeMinimum, float nativeMaximum)
        {
            return FatigueDrain(FinalCost(baseCost, spell, drainMultiplier), nativeMinimum, nativeMaximum) - nativeDrain * drainMultiplier;
        }

        internal void CostRange(MagicSpellKind kind, out float minimum, out float maximum)
        {
            minimum = maximum = CostMultiplier(new MagicSpell(0, kind));
            for (int tier = 1; tier <= HighestTier; tier++)
            {
                float cost = CostMultiplier(new MagicSpell(tier, kind));
                minimum = Math.Min(minimum, cost);
                maximum = Math.Max(maximum, cost);
            }
        }

        internal int FirstOverreachTier
        {
            get
            {
                int tier = (int)Rank + 1;
                return tier <= HighestLearnableTier ? tier : -1;
            }
        }

        internal int HighestTier
        {
            get { return Math.Min((int)Rank, HighestLearnableTier); }
        }

        internal float ReboundChance(MagicSpell spell)
        {
            return spell.Tier == Legendary ? 0f : Profile.ReboundChance(spell.Tier);
        }

        internal float BacklashCut
        {
            get { return Lampbound ? LampboundBacklashCut : 0f; }
        }

        internal float BacklashChance(MagicSpell spell, float accuracy)
        {
            if (!HasBacklash || float.IsNaN(accuracy)) return 0f;
            float success = Math.Max(0f, Math.Min(100f, accuracy));
            float chance = NaturalBacklash(spell, success) * (1f - BacklashCut) * (FortuneRain ? 1f - RainBacklashCut : 1f) * (Unleashed ? 1f + UnleashedBacklash : 1f);
            return Math.Min(100f, Math.Max(Profile.BacklashFloor(spell.Tier), chance));
        }

        internal float NaturalBacklash(MagicSpell spell, float success)
        {
            return 100f - success + success * ReboundChance(spell) / 100f;
        }

        internal float RefundFraction(MagicSpell spell)
        {
            return spell.IsCurse ? 0.85f : 0.70f;
        }

        internal float ScapegoatChance
        {
            get { return ScapegoatProtection(Scapegoat); }
        }
    }
}
