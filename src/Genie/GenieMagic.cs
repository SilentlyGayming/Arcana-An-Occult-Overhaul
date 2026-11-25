using System;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Skills;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace.CAS;
using Sims3.SimIFace;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    [Persistable]
    public sealed class GenieMagicSkill : Skill
    {
        internal const SkillNames GenieGuid = (SkillNames)0xA9C4B6E2;
        internal const int MaxLevel = 10;
        private float power = float.NaN;
        private bool delicateCasting;
        private long exhaustEnds;
        private long redirectReady;
        private long guaranteeReady;

        public GenieMagicSkill() : this(GenieGuid) { }
        public GenieMagicSkill(SkillNames guid) : base(guid) { }

        internal float Capacity
        {
            get { return CasterProfile.Genie.Capacity(CastingRules.RankForSkill(SkillLevel)) * GeniePerks.CapacityFactor(SkillOwner); }
        }

        internal bool DelicateCasting
        {
            get { return delicateCasting; }
            set { delicateCasting = value; }
        }

        internal long ExhaustEnds { get { return exhaustEnds; } set { exhaustEnds = value; } }
        internal long RedirectReady { get { return redirectReady; } set { redirectReady = value; } }
        internal long GuaranteeReady { get { return guaranteeReady; } set { guaranteeReady = value; } }

        internal float Power
        {
            get { return float.IsNaN(power) ? Capacity : Math.Max(0f, Math.Min(Capacity, power)); }
            set { power = Math.Max(0f, Math.Min(Capacity, value)); }
        }

        internal bool IsExhausted { get { return ExhaustEnds > SimClock.CurrentTicks; } }
        internal float RecoveryLimit { get { return GeniePowerPolicy.RecoveryLimit(Capacity, IsExhausted); } }

        internal void ChangePower(float amount)
        {
            if (amount > 0f)
            {
                WishRecord fate = WishState.Read(SkillOwner, false);
                if (fate != null && fate.FateSealEnds > SimClock.CurrentTicks) return;
            }
            Power = GeniePowerPolicy.Change(Power, amount, Capacity, IsExhausted);
        }

        public override bool ExportContent(IPropertyStreamWriter writer)
        {
            if (!base.ExportContent(writer)) return false;
            return writer.WriteFloat(0xAC530001, power)
                && writer.WriteBool(0xAC530002, delicateCasting)
                && writer.WriteInt64(0xAC530003, exhaustEnds)
                && writer.WriteInt64(0xAC530006, redirectReady)
                && writer.WriteInt64(0xAC530007, guaranteeReady);
        }
        public override bool ImportContent(IPropertyStreamReader reader)
        {
            if (!base.ImportContent(reader)) return false;
            reader.ReadFloat(0xAC530001, out power, float.NaN);
            reader.ReadBool(0xAC530002, out delicateCasting, false);
            reader.ReadInt64(0xAC530003, out exhaustEnds, 0);
            reader.ReadInt64(0xAC530006, out redirectReady, 0);
            reader.ReadInt64(0xAC530007, out guaranteeReady, 0);
            if (float.IsInfinity(power)) power = float.NaN;
            return true;
        }

        internal const string ArtName = "SupernaturalExpanded.LifeStateArt.Genie";

        // Builds Genie Magic on the Spellcasting progression with the Genie's own life-state art, so it never shows the Witch icon.
        internal static GenieMagicSkill Prototype()
        {
            Skill casting = SkillManager.GetStaticSkill(SkillNames.Spellcasting);
            if (casting == null || casting.NonPersistableData == null) return null;
            NonPersistableSkillData source = casting.NonPersistableData;
            NonPersistableSkillData data = new NonPersistableSkillData();
            data.Name = "Arcana.Skill.GenieMagic.Name";
            data.Description = "Arcana.Skill.GenieMagic.Description";
            data.Commodity = Sims3.Gameplay.Autonomy.CommodityKind.None;
            data.MaxSkillLevel = MaxLevel;
            data.PointsForNextLevel = source.PointsForNextLevel != null && source.PointsForNextLevel.Length > MaxLevel
                ? (int[])source.PointsForNextLevel.Clone() : new int[] { 0, 100, 300, 600, 1000, 1500, 2100, 2800, 3600, 4500, 5500 };
            data.SkillCategory = source.SkillCategory;
            data.AvailableAgeSpecies = source.AvailableAgeSpecies;
            data.AlwaysDisplayLevelUpTns = source.AlwaysDisplayLevelUpTns;
            ResourceKey icon = new ResourceKey(OccultAssetData.GenieLifeStateArt, 0x2F7D0004, 0);
            data.DreamsAndPromisesIcon = ArtName;
            data.DreamsAndPromisesIconKey = icon;
            data.IconKey = icon;
            data.SkillUIIconKey = icon;
            data.SkillProductVersion = source.SkillProductVersion;
            data.SkillVersion = source.SkillVersion;
            string[] messages = new string[MaxLevel + 1];
            for (int i = 1; i < messages.Length; i++) messages[i] = "Arcana.Skill.GenieMagic.LevelUp";
            data.LevelUpStrings = messages;
            GenieMagicSkill skill = new GenieMagicSkill();
            skill.mNonPersistableData = data;
            return skill;
        }
    }

    internal sealed class GenieMagicRegistration : SkillManager
    {
        internal static void Register()
        {
            Skill existing;
            if (sDictionary.TryGetValue((ulong)GenieMagicSkill.GenieGuid, out existing))
            {
                if (!(existing is GenieMagicSkill)) throw new InvalidOperationException("Arcana Genie Magic skill ID collision.");
                return;
            }
            GenieMagicSkill prototype = GenieMagicSkill.Prototype();
            if (prototype == null) return;
            sDictionary.Add((ulong)GenieMagicSkill.GenieGuid, prototype);
            sSkillEnumValues.AddNewEnumValue("ArcanaGenieMagic", GenieMagicSkill.GenieGuid);
        }
    }

    internal static class GeniePower
    {
        internal const float RegenerationPerHour = 10f;
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo NativeCurrent = typeof(OccultMagicPointSystem).GetField("mCurrentMagicPointValue", InstanceFields);
        private static readonly FieldInfo NativeMaximum = typeof(OccultMagicPointSystem).GetField("mMaxMagicPoints", InstanceFields);
        private static readonly FieldInfo NativeShortage = typeof(OccultMagicPointSystem).GetField("mPowerShortageBuff", InstanceFields);
        private static readonly FieldInfo NativeFailure = typeof(OccultMagicPointSystem).GetField("mPowerFailureBuff", InstanceFields);

        internal static GenieMagicSkill Read(SimDescription sim, bool create)
        {
            if (sim == null || sim.SkillManager == null) return null;
            GenieMagicSkill skill = sim.SkillManager.GetElement(GenieMagicSkill.GenieGuid) as GenieMagicSkill;
            if (skill == null && create)
            {
                GenieMagicRegistration.Register();
                sim.SkillManager.AddElement(GenieMagicSkill.GenieGuid);
                skill = sim.SkillManager.GetElement(GenieMagicSkill.GenieGuid) as GenieMagicSkill;
            }
            return skill;
        }

        internal static float Get(SimDescription sim)
        {
            GenieMagicSkill skill = Read(sim, false);
            return skill == null ? CapacityOf(sim) : skill.Power;
        }

        internal static float CapacityOf(SimDescription sim)
        {
            GenieMagicSkill skill = Read(sim, false);
            return skill == null ? CasterProfile.Genie.Capacity(Rank.Novice) : skill.Capacity;
        }

        internal static void Change(SimDescription sim, float amount)
        {
            GenieMagicSkill skill = Read(sim, true);
            if (skill == null || amount == 0f) return;
            skill.ChangePower(amount);
            JournalBootstrap.RefreshMeter();
        }

        internal static void RegenerateAll(float minutes, Sim[] sims)
        {
            bool changed = false;
            GenieRain.Tick();
            foreach (Sim sim in sims)
            {
                CasterKind kind;
                if (!MagicCasters.TryKind(sim, out kind) || kind != CasterKind.Genie) continue;
                RefillNativePoints(sim);
                GenieMagicSkill skill = Read(sim.SimDescription, false);
                if (skill == null || skill.Power >= skill.RecoveryLimit) continue;
                skill.ChangePower(RegenerationPerHour * Recovery(sim).Rate * minutes / 60f);
                changed = true;
            }
            if (changed) JournalBootstrap.RefreshMeter();
        }

        internal static PowerRecovery Recovery(Sim sim)
        {
            bool fizzled = sim.BuffManager != null && sim.BuffManager.HasElement(WitchMagicRecovery.FizzleBuff);
            CastingRules rules = MagicCasters.ReadRules(sim);
            rules.InLamp = LampRetreat.IsResting(sim);
            WishRecord fate = WishState.Read(sim.SimDescription, false);
            rules.FateSealed = fate != null && fate.FateSealEnds > SimClock.CurrentTicks;
            return rules.RegenerationFactors(sim.IsSleeping || rules.InLamp, false, fizzled);
        }

        internal static void RefillNativePoints(Sim sim)
        {
            OccultGenie genie = sim == null || sim.OccultManager == null ? null : sim.OccultManager.GetOccultType(OccultTypes.Genie) as OccultGenie;
            OccultMagicPointSystem points = genie == null ? null : genie.MagicPoints;
            if (points == null) return;
            NativeCurrent.SetValue(points, NativeMaximum.GetValue(points));
            if (sim.BuffManager == null) return;
            sim.BuffManager.RemoveElement((BuffNames)NativeShortage.GetValue(points));
            sim.BuffManager.RemoveElement((BuffNames)NativeFailure.GetValue(points));
        }
    }

    internal static class GenieRanks
    {
        private const string Placeholder = "{Name}";
        private static readonly string[] Full =
        {
            "Arcana.Genie.Rank.Full.UnChained", "Arcana.Genie.Rank.Full.Awakened", "Arcana.Genie.Rank.Full.Ascendant",
            "Arcana.Genie.Rank.Full.Transcendent", "Arcana.Genie.Rank.Full.Almighty"
        };
        private static readonly string[] Descriptions =
        {
            "Arcana.Genie.Rank.Description.UnChained", "Arcana.Genie.Rank.Description.Awakened", "Arcana.Genie.Rank.Description.Ascendant",
            "Arcana.Genie.Rank.Description.Transcendent", "Arcana.Genie.Rank.Description.Almighty"
        };

        internal static bool TryRead(SimDescription sim, out Rank rank)
        {
            GenieMagicSkill skill = GeniePower.Read(sim, false);
            rank = skill == null ? Rank.Novice : CastingRules.RankForSkill(skill.SkillLevel);
            return skill != null;
        }

        internal static float Progress(int level, float levelFraction)
        {
            int start = 2 * (int)CastingRules.RankForSkill(level);
            return Math.Max(0f, Math.Min(1f, (level - start + levelFraction) / 2f));
        }

        internal static string FullName(Rank rank)
        {
            return JournalText.Get(Full[Index(rank)]);
        }

        internal static string Description(Rank rank, string firstName)
        {
            string name = string.IsNullOrEmpty(firstName) ? JournalText.Get("Arcana.Common.ThisSim") : firstName;
            return JournalText.Get(Descriptions[Index(rank)]).Replace(Placeholder, name);
        }

        private static int Index(Rank rank)
        {
            return Math.Max(0, Math.Min(Full.Length - 1, (int)rank));
        }
    }

    internal static class GeniePowerPolicy
    {
        internal static float RecoveryLimit(float capacity, bool exhausted)
        { return exhausted ? capacity * CastingRules.ExhaustionRecoveryLimit : capacity; }

        // Recovery never lowers power already held above the exhaustion ceiling.
        internal static float Change(float power, float amount, float capacity, bool exhausted)
        {
            if (amount <= 0f) return Math.Max(0f, power + amount);
            return Math.Max(power, Math.Min(RecoveryLimit(capacity, exhausted), power + amount));
        }
    }
}
