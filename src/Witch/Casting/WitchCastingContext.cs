using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.Gameplay.Skills;
using Sims3.Gameplay.Socializing;
using Sims3.SimIFace;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal static class WitchCastingContext
    {
        internal const float NearbyRadius = 10f;
        private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Dictionary<Type, FieldInfo> nativeWandFields = new Dictionary<Type, FieldInfo>();
        private static readonly FieldInfo LunarDrain = typeof(MagicWand).GetField("kMoonPhaseMotiveDrainMultiplier", StaticFields);
        private static readonly FieldInfo FamiliarDrain = typeof(MagicWand).GetField("kFamiliarMotiveMultiplier", StaticFields);
        private static readonly FieldInfo HandsDrainField = typeof(MagicHands).GetField("kMotiveDrainMultiplier", StaticFields);

        private static readonly CommodityKind[] Needs =
        {
            CommodityKind.Hunger, CommodityKind.VampireThirst, CommodityKind.Bladder,
            CommodityKind.Energy, CommodityKind.Fun, CommodityKind.Social, CommodityKind.Hygiene
        };

        internal static bool IsWitch(Sim sim)
        {
            return sim != null && sim.OccultManager != null && sim.OccultManager.HasOccultType(OccultTypes.Witch);
        }

        internal static CastingRules ReadRules(Sim sim)
        {
            Rank rank;
            PerkRecord record = PerkProgressSource.Witch.ReadGameplay(sim.SimDescription, out rank);
            CastingRules rules = new CastingRules(rank);
            if (record == null)
            {
                return rules;
            }

            rules.Lessons = record.TierOf("LessonsInTheUnseenArts") > 0;
            rules.Familiar = record.TierOf("FamiliarBond") > 0 && HasFamiliar(sim);
            rules.Hexweaver = record.TierOf("Hexweaver") > 0;
            rules.DarkArts = record.TierOf("DarkArts") > 0;
            rules.InCoven = record.TierOf("ThePowerOfThree") > 0 && HasCoven(sim);
            rules.Ascendancy = record.TierOf("ArcaneAscendancy") > 0;
            rules.OldWays = !rules.Ascendancy && record.TierOf("TheOldWays") > 0;
            rules.Eidetic = Math.Min(3, record.TierOf("EideticWitchary"));
            rules.Rest = Math.Min(3, record.TierOf("RestForTheWicked"));
            rules.WitchingHour = record.TierOf("TheWitchingHour") > 0;
            rules.Scapegoat = Math.Min(3, record.TierOf("Scapegoat"));
            return rules;
        }

        internal static bool Nearby(Sim caster, Sim other)
        {
            return other != null && caster != other && other.InWorld && !other.HasBeenDestroyed &&
                caster.GetDistanceToObjectSquared(other) <= NearbyRadius * NearbyRadius;
        }

        private static bool HasCoven(Sim sim)
        {
            if (sim.LotCurrent == null)
            {
                return false;
            }

            int count = 0;
            foreach (Sim other in sim.LotCurrent.GetSims())
            {
                if (!Nearby(sim, other) || !IsWitch(other))
                {
                    continue;
                }
                Relationship relationship = Relationship.Get(sim, other, false);
                if (relationship != null && relationship.AreFriends() && ++count == 2)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasFamiliar(Sim sim)
        {
            if (sim.LotCurrent == null || sim.BuffManager == null || !sim.BuffManager.HasElement(BuffNames.AnimalFamiliar))
            {
                return false;
            }
            foreach (Sim other in sim.LotCurrent.GetSims())
            {
                if (Nearby(sim, other) && other.SimDescription.IsCat)
                {
                    Relationship relationship = Relationship.Get(sim, other, false);
                    if (relationship != null && relationship.AreFriends())
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static bool HasPracticallyMagic(Sim sim)
        {
            if (!IsWitch(sim))
            {
                return false;
            }
            PerkRecord record = WitchProgressStore.Ledger.ReadCommitted(sim.SimDescription.SimDescriptionId);
            return record != null && record.TierOf("PracticallyMagic") > 0;
        }

        internal static MagicWand EquippedWand(Sim sim)
        {
            SpellcastingSkill skill = sim.SkillManager == null ? null :
                sim.SkillManager.GetElement(SkillNames.Spellcasting) as SpellcastingSkill;
            return MagicWand.GetWandToUse(sim, skill);
        }

        internal static MagicWand WandForAccuracy(Sim sim)
        {
            InteractionInstance interaction = sim.CurrentInteraction;
            WitchSimCast arcanaCast = interaction as WitchSimCast;
            if (arcanaCast != null && arcanaCast.ActiveWand != null)
            {
                return arcanaCast.ActiveWand;
            }
            MagicWand wand = arcanaCast == null ? NativeWand(interaction) : null;
            return wand ?? EquippedWand(sim);
        }

        internal static MagicWand NativeWand(InteractionInstance interaction)
        {
            if (interaction == null)
            {
                return null;
            }
            Type type = interaction.GetType();
            FieldInfo field;
            if (!nativeWandFields.TryGetValue(type, out field))
            {
                for (Type owner = type; owner != null; owner = owner.BaseType)
                {
                    if (owner.DeclaringType == typeof(MagicWand))
                    {
                        field = owner.GetField("mWand", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        if (field != null && field.FieldType == typeof(MagicWand))
                        {
                            break;
                        }
                        field = null;
                    }
                }
                nativeWandFields.Add(type, field);
            }
            return field == null ? null : field.GetValue(interaction) as MagicWand;
        }

        internal static float SituationalDrain(Sim sim)
        {
            float result = 1f;
            float[] lunar = (float[])LunarDrain.GetValue(null);
            int phase = (int)World.GetLunarPhase();
            if (lunar != null && phase >= 0 && phase < lunar.Length)
            {
                result *= lunar[phase];
            }
            if (sim.BuffManager != null && sim.BuffManager.HasElement(BuffNames.AnimalFamiliar))
            {
                result *= (float)FamiliarDrain.GetValue(null);
            }
            return result;
        }

        internal static float HandsDrain
        {
            get { return (float)HandsDrainField.GetValue(null); }
        }

        internal static float WandBonus(MagicWand wand, bool previewMagicHands)
        {
            if (wand is MagicHands || (wand == null && previewMagicHands))
            {
                return CastingRules.MagicHandsAccuracyBonus;
            }
            if (wand is ElegantWand) return 25f;
            if (wand is IvoryWand || wand is IridescentWand) return 20f;
            if (wand is VerdantWand || wand is CrystalWand) return 15f;
            if (wand is ArgentWand || wand is AzureWand || wand is CrimsonWand) return 10f;
            return wand is DefaultWand ? 5f : 0f;
        }

        internal static float EmotionPenalty(Sim sim)
        {
            float penalty = 0f;
            if (sim.BuffManager == null)
            {
                return penalty;
            }

            Motives motives = sim.Motives;
            foreach (BuffInstance instance in sim.BuffManager.Buffs)
            {
                if (instance == null || instance.mBuff == null || instance.EffectValue >= 0 ||
                    IsNeedMoodlet(instance, motives))
                {
                    continue;
                }
                penalty -= instance.EffectValue;
                if (penalty >= CastingRules.MaxEmotionPenalty)
                {
                    return CastingRules.MaxEmotionPenalty;
                }
            }
            return penalty;
        }

        private static bool IsNeedMoodlet(BuffInstance buff, Motives motives)
        {
            foreach (CommodityKind need in Needs)
            {
                if (buff.mBuff.SolveCommodity == need)
                {
                    return true;
                }
                Motive motive = motives == null ? null : motives.GetMotive(need);
                if (motive == null || motive.Tuning == null || motive.Tuning.BuffTriggers == null)
                {
                    continue;
                }
                foreach (MotiveTuning.MotiveBuffTrigger trigger in motive.Tuning.BuffTriggers)
                {
                    if (trigger.mAddBuff == buff.mBuffGuid)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static void CountNeeds(Sim sim, out float penalty, out int empty, List<TooltipModifier> details)
        {
            penalty = 0f;
            empty = 0;
            Motives motives = sim.Motives;
            if (motives == null)
            {
                return;
            }

            bool thirst = motives.GetMotive(CommodityKind.VampireThirst) != null;
            foreach (CommodityKind need in Needs)
            {
                if ((thirst && need == CommodityKind.Hunger) || motives.GetMotive(need) == null)
                {
                    continue;
                }

                float min = motives.GetMin(need);
                float span = motives.GetMax(need) - min;
                if (span <= 0f)
                {
                    continue;
                }

                float aboveMinimum = motives.GetValue(need) - min;
                float amount = CastingRules.NeedPenalty(aboveMinimum / span);
                penalty += amount;
                if (aboveMinimum <= 0f)
                {
                    empty++;
                }
                if (amount > 0f && details != null)
                {
                    CasterAttributes.AddPercentModifier(details, CasterAttributes.NeedName(need), -amount);
                }
            }
        }
        internal static CastingRules ReadCapacityRules(Sim sim)
        {
            if (!IsWitch(sim)) return MagicCasters.ReadRules(sim);
            Rank rank;
            PerkRecord record = PerkProgressSource.Witch.ReadGameplay(sim.SimDescription, out rank);
            CastingRules rules = new CastingRules(rank);
            rules.Ascendancy = record != null && record.TierOf("ArcaneAscendancy") > 0;
            rules.WitchingHour = record != null && record.TierOf("TheWitchingHour") > 0;
            return rules;
        }
    }
}
