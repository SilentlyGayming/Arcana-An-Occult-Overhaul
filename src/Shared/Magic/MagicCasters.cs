using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.Gameplay.Skills;
using Sims3.SimIFace;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal static class MagicCasters
    {
        [PersistableStatic] internal static List<MagicHands> GeneratedHands;
        private static readonly FieldInfo PurchasedPrice = typeof(GameObject).GetField("mPurchasedPrice", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CatalogPrice = typeof(GameObject).GetField("mIntPrice", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool TryKind(Sim sim, out CasterKind kind)
        {
            kind = CasterKind.Witch;
            if (sim == null || sim.OccultManager == null) return false;
            if (sim.OccultManager.HasOccultType(OccultTypes.Witch)) return true;
            kind = CasterKind.Fairy;
            if (sim.OccultManager.HasOccultType(OccultTypes.Fairy)) return true;
            kind = CasterKind.Genie;
            return sim.OccultManager.HasOccultType(OccultTypes.Genie);
        }

        internal static bool IsCaster(Sim sim)
        {
            CasterKind kind;
            return TryKind(sim, out kind);
        }

        internal static bool IsInnateCaster(Sim sim)
        {
            CasterKind kind;
            return TryKind(sim, out kind) && kind != CasterKind.Witch;
        }

        internal static SkillNames SkillFor(CasterKind kind)
        {
            return kind == CasterKind.Fairy ? SkillNames.FairyMagic : kind == CasterKind.Genie ? GenieMagicSkill.GenieGuid : SkillNames.Spellcasting;
        }

        internal static int SkillLevel(Sim sim, CasterKind kind)
        {
            if (sim == null || sim.SkillManager == null) return 0;
            if (kind == CasterKind.Genie)
            {
                GenieMagicSkill genie = GeniePower.Read(sim.SimDescription, false);
                return genie == null ? 0 : genie.SkillLevel;
            }
            return Math.Max(0, sim.SkillManager.GetSkillLevel(SkillFor(kind)));
        }

        internal static CastingRules ReadRules(Sim sim)
        {
            CasterKind kind;
            if (!TryKind(sim, out kind)) return new CastingRules(Rank.Novice);
            if (kind == CasterKind.Witch) return WitchCastingContext.ReadRules(sim);
            CastingRules rules = new CastingRules(CastingRules.RankForSkill(SkillLevel(sim, kind)), kind);
            return kind == CasterKind.Genie ? GeniePerks.Apply(sim, rules) : rules;
        }

        internal static float RealPower(Sim sim, CastingRules rules)
        {
            if (sim == null || sim.Motives == null) return 0f;
            switch (rules.Caster)
            {
                case CasterKind.Fairy:
                    return Fraction(sim, CommodityKind.AuraPower) * rules.Capacity;
                case CasterKind.Genie:
                    return Math.Min(GeniePower.Get(sim.SimDescription), rules.Capacity);
                default:
                    float minimum = sim.Motives.GetMin(CommodityKind.MagicFatigue);
                    float maximum = sim.Motives.GetMax(CommodityKind.MagicFatigue);
                    return rules.Capacity * CastingRules.PowerFraction(sim.Motives.GetValue(CommodityKind.MagicFatigue), minimum, maximum);
            }
        }

        internal static float AvailablePower(Sim sim, CastingRules rules)
        {
            return rules.Caster == CasterKind.Genie && WishAging.IsSponsor(sim) ? 0f : RealPower(sim, rules);
        }

        internal static float SpendInnate(Sim sim, CastingRules rules, float cost)
        {
            if (rules.Caster == CasterKind.Genie && WishAging.IsSponsor(sim)) return 0f;
            float spent = Math.Min(Math.Max(0f, cost), RealPower(sim, rules));
            if (spent <= 0f) return 0f;
            if (rules.Caster == CasterKind.Fairy)
            {
                float span = sim.Motives.GetMax(CommodityKind.AuraPower) - sim.Motives.GetMin(CommodityKind.AuraPower);
                sim.Motives.ChangeValue(CommodityKind.AuraPower, -spent / rules.Capacity * span);
                JournalBootstrap.RefreshMeter();
            }
            else if (rules.Caster == CasterKind.Genie)
            {
                GeniePower.Change(sim.SimDescription, -spent);
                GeniePerks.Return(sim, rules, spent);
            }
            return spent;
        }

        internal static bool ResolveBacklash(Sim caster, CastingRules rules, MagicSpell spell, float accuracy, bool ability, ref bool succeeded)
        {
            if (!rules.HasBacklash) return !succeeded;
            if (ability && rules.GenieLaw)
            {
                succeeded = true;
                return false;
            }
            bool backlash = RollBacklash(rules, spell, accuracy, succeeded);
            if (!ability) RisingTide.Record(caster, rules, succeeded, backlash);
            if (!backlash) return false;
            succeeded = !ability && rules.Unleashed;
            GeniePerks.AfterBacklash(caster);
            return !GeniePerks.TryRedirect(caster, rules);
        }

        private static bool RollBacklash(CastingRules rules, MagicSpell spell, float accuracy, bool succeeded)
        {
            if (float.IsNaN(accuracy)) return false;
            float success = Math.Max(0f, Math.Min(100f, accuracy));
            float natural = rules.NaturalBacklash(spell, success) / 100f;
            float target = rules.BacklashChance(spell, success) / 100f;
            bool occurs = !succeeded || RandomUtil.RandomChance(rules.ReboundChance(spell));
            if (occurs) return !RandomUtil.RandomChance(natural > target && natural > 0f ? (1f - target / natural) * 100f : 0f);
            return RandomUtil.RandomChance(target > natural && natural < 1f ? (target - natural) / (1f - natural) * 100f : 0f);
        }

        private static float Fraction(Sim sim, CommodityKind kind)
        {
            float minimum = sim.Motives.GetMin(kind);
            float span = sim.Motives.GetMax(kind) - minimum;
            return span <= 0f ? 0f : Math.Max(0f, Math.Min(1f, (sim.Motives.GetValue(kind) - minimum) / span));
        }

        internal static bool NeedsHands(Sim sim)
        {
            if (sim == null || sim.HasBeenDestroyed) return false;
            return IsInnateCaster(sim) || (WitchCastingContext.IsWitch(sim) && WitchCastingContext.HasPracticallyMagic(sim));
        }

        internal static bool EnsureHands(Sim sim)
        {
            if (!NeedsHands(sim) || sim.Inventory == null) return false;
            if (sim.Inventory.FindAll<MagicWand>(false).Count > 0) return true;
            GameObject hands = Sims3.Gameplay.GlobalFunctions.CreateObject("magicHandsLHR", ProductVersion.EP7, Vector3.OutOfWorld, 0,
                Vector3.UnitZ, null, (Simulator.ObjectInitParameters)null) as GameObject;
            if (!(hands is MagicHands))
            {
                if (hands != null) hands.Destroy();
                return false;
            }
            MakeWorthless((MagicHands)hands);
            if (sim.Inventory.TryToAdd(hands))
            {
                if (GeneratedHands == null) GeneratedHands = new List<MagicHands>();
                GeneratedHands.Add((MagicHands)hands);
                return true;
            }
            hands.Destroy();
            return false;
        }

        internal static void UpdateHands(Sim sim)
        {
            if (sim == null || sim.Inventory == null) return;
            if (NeedsHands(sim))
            {
                EnsureHands(sim);
                return;
            }
            if (GeneratedHands == null || GeneratedHands.Count == 0) return;
            foreach (MagicHands hands in sim.Inventory.FindAll<MagicHands>(false))
            {
                if (!GeneratedHands.Contains(hands) || !sim.Inventory.TryToRemove(hands)) continue;
                GeneratedHands.Remove(hands);
                hands.Destroy();
            }
        }

        internal static void UpdateAllHands(Sim[] sims)
        {
            if (GeneratedHands != null) GeneratedHands.RemoveAll(delegate(MagicHands item) { return item == null || item.HasBeenDestroyed; });
            foreach (Sim sim in sims) UpdateHands(sim);
        }

        internal static void MakeWorthless(MagicHands hands)
        {
            PurchasedPrice.SetValue(hands, 0);
            CatalogPrice.SetValue(hands, 0);
        }


        internal static void EnsureSkill(Sim sim)
        {
            CasterKind kind;
            if (TryKind(sim, out kind) && kind == CasterKind.Genie) GeniePower.Read(sim.SimDescription, true);
        }
    }
}
