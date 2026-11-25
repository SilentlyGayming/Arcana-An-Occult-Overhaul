using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Utilities;
using Sims3.Gameplay;
using Sims3.SimIFace.CAS;
using Sims3.SimIFace;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal static class WitchMagicRecovery
    {
        private const float FizzleMinutes = 300f;
        private const int FizzleMood = -10;
        private const string NativeBuffPrefix = "Gameplay/Excel/buffs/BuffList:";
        private const string FizzleName = "Arcana.Witch.Fizzle.Name";
        private const string FizzleDescription = "Arcana.Witch.Fizzle.Description";
        internal static readonly BuffNames FizzleBuff = (BuffNames)ResourceUtils.HashString64("ArcanaWitchFizzle");
        private static readonly Dictionary<Motive, DecayOverride> overrides = new Dictionary<Motive, DecayOverride>();

        internal static void RegisterFizzle()
        {
            ArcanaMoodletPreview.Track(FizzleBuff, Origin.FromSpell);
            ulong id = (ulong)FizzleBuff;
            if (!BuffManager.BuffDictionary.ContainsKey(id))
            {
                Buff.BuffData data = new Buff.BuffData();
                data.mBuffGuid = FizzleBuff;
                data.mBuffName = FizzleName;
                data.mDescription = FizzleDescription;
                data.mEffectValue = FizzleMood;
                data.mHelpText = "";
                data.mAxisEffected = MoodAxis.Uncomfortable;
                data.mPolarityOverride = Polarity.Negative;
                data.mAvailabilityFlags = CASAGSAvailabilityFlags.HumanAgeMask;
                data.mIncreasedEffectivenessList = new List<SocialManager.SocialEffectiveness>();
                data.mReducedEffectivenessList = new List<SocialManager.SocialEffectiveness>();
                data.mDisallowedOccults = new List<OccultTypes>();
                data.mNeededTraitList = new List<TraitNames>();
                data.mFacialIdles = new string[0];
                data.mVersion = 1.0;
                BuffInstance icon;
                if (BuffManager.BuffDictionary.TryGetValue((ulong)BuffNames.DepletedMagic, out icon))
                {
                    data.mThumbKey = icon.ThumbKey;
                    data.mThumbString = icon.ThumbString;
                }
                data.mTimeoutSimMinutes = FizzleMinutes;
                data.mBuffCategory = BuffCategory.Physical;
                data.mProductVersion = ProductVersion.EP7;
                BuffManager.BuffDictionary.Add(id, new Buff(data).CreateBuffInstance());
            }
        }

        internal static void Fizzle(Sim sim, CastingRules rules)
        {
            Give(sim, rules.FizzleDuration);
            if (!rules.Innate) Refresh(sim, rules);
        }

        internal static void Give(Sim sim, float duration)
        {
            sim.BuffManager.RemoveElement(FizzleBuff);
            sim.BuffManager.AddElementIgnoreDelay(FizzleBuff, FizzleMood, FizzleMinutes * duration, Origin.FromSpell);
        }

        internal static PowerRecovery AppliedRegeneration(Motive motive)
        {
            DecayOverride current;
            return motive != null && overrides.TryGetValue(motive, out current) && motive.Tuning == current.Current
                ? current.Regeneration : PowerRecovery.Baseline;
        }

        internal static void RefreshAll()
        {
            Dictionary<Motive, bool> present = new Dictionary<Motive, bool>();
            foreach (Sim sim in Sims3.Gameplay.Queries.GetObjects<Sim>())
            {
                if (!WitchCastingContext.IsWitch(sim) || sim.Motives == null || sim.BuffManager == null)
                {
                    continue;
                }
                Motive motive = Refresh(sim, WitchCastingContext.ReadRules(sim));
                if (motive != null)
                {
                    present[motive] = true;
                }
            }

            List<Motive> old = new List<Motive>();
            foreach (KeyValuePair<Motive, DecayOverride> pair in overrides)
            {
                if (!present.ContainsKey(pair.Key))
                {
                    pair.Value.Restore(pair.Key);
                    old.Add(pair.Key);
                }
            }
            foreach (Motive motive in old)
            {
                overrides.Remove(motive);
            }
        }

        internal static void RestoreAll()
        {
            foreach (KeyValuePair<Motive, DecayOverride> pair in overrides)
            {
                pair.Value.Restore(pair.Key);
            }
            overrides.Clear();
        }

        private static Motive Refresh(Sim sim, CastingRules rules)
        {
            Motive motive = sim.Motives.GetMotive(CommodityKind.MagicFatigue);
            if (motive == null || motive.Tuning == null)
            {
                return null;
            }

            BuffInstance fizzle = sim.BuffManager.GetElement(FizzleBuff);
            if (fizzle != null)
            {
                fizzle.mBuffName = NativeBuffPrefix + FizzleName;
                fizzle.mDescription = NativeBuffPrefix + FizzleDescription;
            }
            PowerRecovery regeneration = rules.RegenerationFactors(sim.IsSleeping, SimClock.Hours24 < 4, fizzle != null);
            float rate = regeneration.Rate;
            DecayOverride previous;
            if (overrides.TryGetValue(motive, out previous))
            {
                if (motive.Tuning == previous.Current && rate == previous.Regeneration.Rate)
                {
                    previous.Regeneration = regeneration;
                    return motive;
                }
                previous.Restore(motive);
                overrides.Remove(motive);
            }
            if (rate == 1f)
            {
                if (regeneration.Hour != 1f || regeneration.Fizzle != 1f || regeneration.Coven != 1f ||
                    regeneration.Rank != 1f || regeneration.Rest != 1f)
                {
                    overrides.Add(motive, new DecayOverride(motive.Tuning, motive.Tuning, regeneration));
                }
                return motive;
            }

            MotiveTuning original = motive.Tuning;
            MotiveTuning adjusted = MotiveDecay.Scaled(original, rate);
            if (adjusted == null)
            {
                return motive;
            }

            motive.Tuning = adjusted;
            overrides.Add(motive, new DecayOverride(original, adjusted, regeneration));
            return motive;
        }

        private sealed class DecayOverride
        {
            internal readonly MotiveTuning Current;
            internal PowerRecovery Regeneration;
            private readonly MotiveTuning original;

            internal DecayOverride(MotiveTuning original, MotiveTuning current, PowerRecovery regeneration)
            {
                this.original = original;
                Current = current;
                Regeneration = regeneration;
            }

            internal void Restore(Motive motive)
            {
                if (motive.Tuning == Current)
                {
                    motive.Tuning = original;
                }
            }
        }
    }

    internal static class MotiveDecay
    {
        private static readonly MethodInfo CloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DecayCurve = typeof(MotiveTuning).GetField("mMotiveDecayCurve", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo BuffTriggers = typeof(MotiveTuning).GetField("mBuffTriggers", BindingFlags.Instance | BindingFlags.NonPublic);

        // Returns a private copy of the tuning whose decay is multiplied by the factor, or null when the motive has no scalable decay.
        internal static MotiveTuning Scaled(MotiveTuning original, float factor)
        {
            MotiveTuning adjusted = (MotiveTuning)CloneMethod.Invoke(original, null);
            if (original.DecayType == DecayType.VariableDecayByMoodlet && original.BuffTriggers != null)
            {
                List<MotiveTuning.MotiveBuffTrigger> triggers = new List<MotiveTuning.MotiveBuffTrigger>();
                foreach (MotiveTuning.MotiveBuffTrigger item in original.BuffTriggers)
                {
                    MotiveTuning.MotiveBuffTrigger copy = (MotiveTuning.MotiveBuffTrigger)CloneMethod.Invoke(item, null);
                    if (copy.mDecay > 0f)
                    {
                        copy.mDecay *= factor;
                    }
                    triggers.Add(copy);
                }
                BuffTriggers.SetValue(adjusted, triggers);
                return adjusted;
            }
            if ((original.DecayType == DecayType.FixedDecay || original.DecayType == DecayType.DecayToZero) && original.MotiveDecayCurve != null)
            {
                Curve curve = new Curve();
                curve.Points = new List<Vector2>();
                foreach (Vector2 point in original.MotiveDecayCurve.Points)
                {
                    curve.Points.Add(new Vector2(point.x, point.y < 0f ? point.y * factor : point.y));
                }
                DecayCurve.SetValue(adjusted, curve);
                return adjusted;
            }
            return null;
        }
    }
}
