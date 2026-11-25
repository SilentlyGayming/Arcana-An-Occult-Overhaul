using System;

namespace SupernaturalExpanded.Journal
{
    internal struct SpellResearchSettings
    {
        internal static readonly SpellResearchSettings Default = new SpellResearchSettings(30f, 25f, 0.40f, 1f, 0.5f, 0.5f, 10f, 25f, 20f);
        internal readonly float IntervalMinutes, DiscoveryChancePercent, ExperienceFraction;
        internal readonly float WitchSpeed, FairySpeed, GenieSpeed;
        internal readonly float WitchMasterBonusPercent, GenieAscendantBonusPercent, WitchingHourBonusPercent;

        internal SpellResearchSettings(float interval, float chance, float experience, float witch, float fairy, float genie,
            float masterBonus, float ascendantBonus, float nightBonus)
        {
            IntervalMinutes = Valid(interval, 30f, 1f, 1440f);
            DiscoveryChancePercent = Valid(chance, 25f, 0.01f, 100f);
            ExperienceFraction = Valid(experience, 0.40f, 0.001f, 1f);
            WitchSpeed = Valid(witch, 1f, 0.01f, 100f);
            FairySpeed = Valid(fairy, 0.5f, 0.01f, 100f);
            GenieSpeed = Valid(genie, 0.5f, 0.01f, 100f);
            WitchMasterBonusPercent = Valid(masterBonus, 10f, 0f, 1000f);
            GenieAscendantBonusPercent = Valid(ascendantBonus, 25f, 0f, 1000f);
            WitchingHourBonusPercent = Valid(nightBonus, 20f, 0f, 1000f);
        }

        private static float Valid(float value, float fallback, float minimum, float maximum)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum ? fallback : value;
        }

        internal static bool IsWitchingHour(float hour)
        {
            return hour >= 0f && hour < 4f;
        }

        internal float RankBonusPercent(CastingRules rules)
        {
            if (rules.Caster == CasterKind.Witch && rules.Rank >= Rank.Master) return WitchMasterBonusPercent;
            if (rules.Caster == CasterKind.Genie && rules.Rank >= Rank.Adept) return GenieAscendantBonusPercent;
            return 0f;
        }

        internal float Speed(CastingRules rules, bool night)
        {
            float baseline = rules.Caster == CasterKind.Witch ? WitchSpeed : rules.Caster == CasterKind.Fairy ? FairySpeed : GenieSpeed;
            float perk = rules.Caster == CasterKind.Witch && rules.WitchingHour && night ? 1f + WitchingHourBonusPercent / 100f : 1f;
            return baseline * (1f + RankBonusPercent(rules) / 100f) * perk;
        }
    }

    [Sims3.SimIFace.Persistable]
    internal struct SpellResearchClock
    {
        private float lastElapsed, previousRate, progress;

        internal void Start(float speed, float interval)
        {
            lastElapsed = 0f;
            progress = 0f;
            previousRate = speed / interval;
        }

        internal bool Advance(float elapsed, float speed, float interval)
        {
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < lastElapsed) return false;
            progress += (elapsed - lastElapsed) * previousRate;
            lastElapsed = elapsed;
            previousRate = speed / interval;
            if (progress + 0.000001f < 1f) return false;
            progress = Math.Max(0f, progress - (float)Math.Floor(progress + 0.000001f));
            return true;
        }
    }
}
