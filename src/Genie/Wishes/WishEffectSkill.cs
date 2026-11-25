using System;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Careers;
using Sims3.Gameplay.Skills;
using Sims3.SimIFace;
using Sims3.SimIFace.CAS;

namespace SupernaturalExpanded.Journal
{
    [Persistable]
    public sealed class WishEffectSkill : Skill
    {
        internal const SkillNames WishGuid = (SkillNames)0xA9C4B6E3;
        private const int StreamVersion = 5;
        private WishRecord record = new WishRecord();
        private bool restoreTravelBuffs;
        private float wrongMinutes, beautyMinutes, loveMinutes, joyMinutes;
        private int wrongMood;
        private ulong loveTargetId;

        internal void RestoreTravelBuffs(Sims3.Gameplay.Actors.Sim sim)
        {
            if (!restoreTravelBuffs) return;
            WishBuffs.RestoreTravel(sim, wrongMinutes, wrongMood, beautyMinutes, loveMinutes, loveTargetId, joyMinutes);
            restoreTravelBuffs = false;
        }

        internal WishRecord Record { get { if (record == null) record = new WishRecord(); return record; } }
        public WishEffectSkill() : this(WishGuid) { }
        public WishEffectSkill(SkillNames guid) : base(guid) { }
        internal static WishEffectSkill Prototype()
        {
            WishEffectSkill skill = new WishEffectSkill();
            NonPersistableSkillData data = new NonPersistableSkillData();
            data.Name = "Arcana.Genie.WishRecord"; data.Description = "Arcana.Genie.WishRecord";
            data.MaxSkillLevel = 1; data.PointsForNextLevel = new int[] { int.MaxValue };
            data.SkillCategory = SkillCategory.Hidden; data.SkillProductVersion = ProductVersion.EP6;
            data.AvailableAgeSpecies = CASAGSAvailabilityFlags.HumanAgeMask;
            skill.mNonPersistableData = data;
            return skill;
        }
        public override Skill Clone(SimDescription owner)
        {
            WishEffectSkill copy = (WishEffectSkill)base.Clone(owner);
            copy.record = Record.Clone(); return copy;
        }
        public override void OnSkillAddition(bool suppressTNS) { }
        public override bool OnLoadFixup() { SkillGainRate = 0f; return base.OnLoadFixup(); }
        public override bool ExportContent(IPropertyStreamWriter writer)
        {
            if (!base.ExportContent(writer)) return false;
            Sims3.Gameplay.Actors.Sim sim = SkillOwner == null ? null : SkillOwner.CreatedSim;
            if (sim != null && sim.BuffManager != null)
                WishBuffs.CaptureTravel(sim, out wrongMinutes, out wrongMood, out beautyMinutes, out loveMinutes, out loveTargetId, out joyMinutes);
            WishRecord value = Record;
            return writer.WriteBool(0xAC520001, value.CheatDeath)
                && writer.WriteInt32(0xAC520002, value.Escapes)
                && writer.WriteInt64(0xAC520003, value.JoyEnds)
                && writer.WriteInt64(0xAC520004, value.SadEnds)
                && writer.WriteBool(0xAC520005, value.CareerPending)
                && writer.WriteUint64(0xAC520006, (ulong)value.CareerGuid)
                && writer.WriteInt64(0xAC520007, value.CareerExpires)
                && writer.WriteBool(0xAC520008, value.WorkStarted)
                && writer.WriteBool(0xAC520009, value.WaitForCurrentShift)
                && writer.WriteFloat(0xAC52000A, value.PenaltyRemaining)
                && writer.WriteFloat(0xAC52000B, value.LastPerformance)
                && writer.WriteInt32(0xAC52000C, value.LastCareerLevel)
                && writer.WriteBool(0xAC52000D, value.CheatDeathAttempted)
                && writer.WriteFloat(0xAC520020, wrongMinutes)
                && writer.WriteInt32(0xAC520021, wrongMood)
                && writer.WriteFloat(0xAC520022, beautyMinutes)
                && writer.WriteFloat(0xAC520023, loveMinutes)
                && writer.WriteUint64(0xAC520024, loveTargetId)
                && writer.WriteFloat(0xAC520025, joyMinutes)
                && writer.WriteInt32(0xAC520026, StreamVersion)
                && writer.WriteInt64(0xAC520027, value.LoveRevealAt)
                && writer.WriteBool(0xAC520028, value.ConcealedJoy)
                && writer.WriteUint64(0xAC520029, value.TemporaryLoveTarget)
                && writer.WriteBool(0xAC52002A, value.TemporaryLoveHadNativeFlag)
                && writer.WriteInt64(0xAC52002B, value.WorldEnds)
                && writer.WriteInt32(0xAC52002C, value.WorldKind)
                && writer.WriteBool(0xAC52002D, value.WorldOwnsTrait)
                && writer.WriteInt64(0xAC52002F, value.FateSealEnds)
                && writer.WriteUint64(0xAC520030, value.FateVictim)
                && writer.WriteBool(0xAC520031, value.YouthPending)
                && writer.WriteUint64(0xAC520032, value.YouthViewer)
                && writer.WriteUint64(0xAC520033, value.FateDeathIssuer)
                && writer.WriteInt64(0xAC520034, value.FateDeathUntil)
                && writer.WriteBool(0xAC520035, value.FateDeathVisible)
                && WriteBlessings(writer, value);
        }

        private static bool WriteBlessings(IPropertyStreamWriter writer, WishRecord value)
        {
            long[] ends = value.Blessings();
            for (uint index = 0; index < ends.Length; index++)
                if (!writer.WriteInt64(0xAC520036 + index, ends[index])) return false;
            return writer.WriteInt32(0xAC52003C, value.Desire)
                && writer.WriteUint64(0xAC52003D, value.DesireGenie)
                && writer.WriteInt64(0xAC52003E, value.DesireAsked)
                && writer.WriteBool(0xAC52003F, value.DesireOpen)
                && writer.WriteFloat(0xAC520040, value.AthleticBonus)
                && writer.WriteBool(0xAC520041, value.InLamp)
                && writer.WriteInt64(0xAC520042, value.BlessingCooldownEnds);
        }
        public override bool ImportContent(IPropertyStreamReader reader)
        {
            if (!base.ImportContent(reader)) return false;
            record = new WishRecord();
            reader.ReadBool(0xAC520001, out record.CheatDeath, false);
            reader.ReadInt32(0xAC520002, out record.Escapes, 0);
            reader.ReadInt64(0xAC520003, out record.JoyEnds, 0);
            reader.ReadInt64(0xAC520004, out record.SadEnds, 0);
            reader.ReadBool(0xAC520005, out record.CareerPending, false);
            ulong career; reader.ReadUint64(0xAC520006, out career, 0); record.CareerGuid = (OccupationNames)career;
            reader.ReadInt64(0xAC520007, out record.CareerExpires, 0);
            reader.ReadBool(0xAC520008, out record.WorkStarted, false);
            reader.ReadBool(0xAC520009, out record.WaitForCurrentShift, false);
            reader.ReadFloat(0xAC52000A, out record.PenaltyRemaining, 0f);
            reader.ReadFloat(0xAC52000B, out record.LastPerformance, 0f);
            reader.ReadInt32(0xAC52000C, out record.LastCareerLevel, 0);
            reader.ReadBool(0xAC52000D, out record.CheatDeathAttempted, record.CheatDeath);
            reader.ReadFloat(0xAC520020, out wrongMinutes, 0f);
            reader.ReadInt32(0xAC520021, out wrongMood, -25);
            reader.ReadFloat(0xAC520022, out beautyMinutes, 0f);
            reader.ReadFloat(0xAC520023, out loveMinutes, 0f);
            reader.ReadUint64(0xAC520024, out loveTargetId, 0);
            reader.ReadFloat(0xAC520025, out joyMinutes, 0f);
            int version;
            reader.ReadInt32(0xAC520026, out version, 0);
            if (version >= 1)
            {
                reader.ReadInt64(0xAC520027, out record.LoveRevealAt, 0);
                reader.ReadBool(0xAC520028, out record.ConcealedJoy, false);
            }
            if (version >= 2)
            {
                reader.ReadUint64(0xAC520029, out record.TemporaryLoveTarget, 0);
                reader.ReadBool(0xAC52002A, out record.TemporaryLoveHadNativeFlag, false);
                reader.ReadInt64(0xAC52002B, out record.WorldEnds, 0);
                reader.ReadInt32(0xAC52002C, out record.WorldKind, 0);
                reader.ReadBool(0xAC52002D, out record.WorldOwnsTrait, false);
                reader.ReadInt64(0xAC52002F, out record.FateSealEnds, 0);
                reader.ReadUint64(0xAC520030, out record.FateVictim, 0);
                reader.ReadBool(0xAC520031, out record.YouthPending, false);
                reader.ReadUint64(0xAC520032, out record.YouthViewer, 0);
            }
            if (version >= 3)
            {
                reader.ReadUint64(0xAC520033, out record.FateDeathIssuer, 0);
                reader.ReadInt64(0xAC520034, out record.FateDeathUntil, 0);
                reader.ReadBool(0xAC520035, out record.FateDeathVisible, false);
            }
            if (version >= 4)
            {
                long[] ends = record.Blessings();
                for (uint index = 0; index < ends.Length; index++) reader.ReadInt64(0xAC520036 + index, out ends[index], 0);
                reader.ReadInt32(0xAC52003C, out record.Desire, 0);
                reader.ReadUint64(0xAC52003D, out record.DesireGenie, 0);
                reader.ReadInt64(0xAC52003E, out record.DesireAsked, 0);
                reader.ReadBool(0xAC52003F, out record.DesireOpen, false);
                reader.ReadFloat(0xAC520040, out record.AthleticBonus, 0f);
                reader.ReadBool(0xAC520041, out record.InLamp, false);
            }
            if (version >= 5) reader.ReadInt64(0xAC520042, out record.BlessingCooldownEnds, 0);
            restoreTravelBuffs = wrongMinutes > 0f || loveMinutes > 0f || joyMinutes > 0f;
            record.Escapes = Math.Max(0, record.Escapes); SkillGainRate = 0f; return true;
        }
    }

    internal sealed class WishEffectRegistration : SkillManager
    {
        internal static void Register()
        {
            Skill existing;
            if (sDictionary.TryGetValue((ulong)WishEffectSkill.WishGuid, out existing))
            {
                if (!(existing is WishEffectSkill)) throw new InvalidOperationException("Arcana wish record skill ID collision.");
                return;
            }
            sDictionary.Add((ulong)WishEffectSkill.WishGuid, WishEffectSkill.Prototype());
            sSkillEnumValues.AddNewEnumValue("ArcanaWishEffects", WishEffectSkill.WishGuid);
        }
    }
}
