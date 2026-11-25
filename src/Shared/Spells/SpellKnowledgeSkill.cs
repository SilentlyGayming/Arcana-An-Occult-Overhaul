using System;
using System.Collections.Generic;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Skills;
using Sims3.SimIFace.CAS;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    [Persistable]
    public sealed class SpellKnowledgeSkill : Skill
    {
        internal const SkillNames KnowledgeGuid = (SkillNames)0xA9C4B6E1;
        private const uint VersionKey = 0xAC510001;
        private const uint SpellsKey = 0xAC510002;
        private int version = 1;
        private List<string> known = new List<string>();

        public SpellKnowledgeSkill() : this(KnowledgeGuid) { }
        public SpellKnowledgeSkill(SkillNames guid) : base(guid) { }

        internal static SpellKnowledgeSkill Prototype()
        {
            SpellKnowledgeSkill skill = new SpellKnowledgeSkill();
            skill.mNonPersistableData = new NonPersistableSkillData();
            skill.mNonPersistableData.Name = "Arcana.SpellKnowledge";
            skill.mNonPersistableData.Description = "Arcana.SpellKnowledge";
            skill.mNonPersistableData.MaxSkillLevel = 1;
            skill.mNonPersistableData.PointsForNextLevel = new int[] { int.MaxValue };
            skill.mNonPersistableData.SkillCategory = SkillCategory.Hidden;
            skill.mNonPersistableData.SkillProductVersion = ProductVersion.BaseGame;
            skill.mNonPersistableData.AvailableAgeSpecies = (CASAGSAvailabilityFlags)ulong.MaxValue;
            return skill;
        }

        public override Skill Clone(SimDescription owner)
        {
            SpellKnowledgeSkill copy = (SpellKnowledgeSkill)base.Clone(owner);
            copy.known = SpellKnowledgeRecord.Normalize(known);
            return copy;
        }

        public override void OnSkillAddition(bool suppressTNS) { }

        public override bool OnLoadFixup()
        {
            known = SpellKnowledgeRecord.Normalize(known);
            if (version < 1) version = 1;
            SkillGainRate = 0f;
            return base.OnLoadFixup();
        }

        internal bool Knows(string id) { return known != null && known.Contains(id); }
        internal bool Learn(string id)
        {
            if (version > 1 || string.IsNullOrEmpty(id) || Knows(id)) return false;
            if (known == null) known = new List<string>();
            known.Add(id);
            return true;
        }

        internal int LearnAllOrdinary()
        {
            if (version > 1) return -1;
            int learned = 0;
            foreach (SpellEntry entry in SpellCatalog.Entries)
                if (SpellCatalog.CheatUnlockable(entry) && Learn(entry.Id)) learned++;
            return learned;
        }

        public override bool ExportContent(IPropertyStreamWriter writer)
        {
            if (!base.ExportContent(writer)) return false;
            return writer.WriteInt32(VersionKey, version)
                && writer.WriteString(SpellsKey, SpellKnowledgeRecord.Normalize(known).ToArray());
        }

        public override bool ImportContent(IPropertyStreamReader reader)
        {
            if (!base.ImportContent(reader)) return false;
            reader.ReadInt32(VersionKey, out version, 1);
            if (version < 1) version = 1;
            string[] ids;
            reader.ReadString(SpellsKey, out ids);
            known = SpellKnowledgeRecord.Normalize(ids);
            SkillGainRate = 0f;
            return true;
        }
    }

    internal sealed class SpellKnowledgeRegistration : SkillManager
    {
        private const string Message = "Arcana.Magic.ProficiencyIncreased";
        private static Skill.NonPersistableSkillData originalData;
        private static string description;
        private static string[] originalMessages, messages;

        internal static void Restore()
        {
            if (originalData == null) return;
            if (originalData.Description == Message) originalData.Description = description;
            if (originalData.LevelUpStrings == messages) originalData.LevelUpStrings = originalMessages;
            originalData = null;
            originalMessages = messages = null;
        }
        internal static void Register()
        {
            Skill casting = SkillManager.GetStaticSkill(SkillNames.Spellcasting);
            Skill.NonPersistableSkillData data = casting == null ? null : casting.NonPersistableData;
            if (data != null && originalData == null)
            {
                originalData = data;
                description = data.Description;
                originalMessages = data.LevelUpStrings;
                if (originalMessages != null)
                {
                    messages = (string[])originalMessages.Clone();
                    for (int i = 1; i < messages.Length; i++)
                        if (!string.IsNullOrEmpty(messages[i])) messages[i] = Message;
                    data.LevelUpStrings = messages;
                }
                data.Description = Message;
            }
            Skill existing;
            if (sDictionary.TryGetValue((ulong)SpellKnowledgeSkill.KnowledgeGuid, out existing))
            {
                if (!(existing is SpellKnowledgeSkill)) throw new InvalidOperationException("Arcana Spell Knowledge skill ID collision.");
                return;
            }
            sDictionary.Add((ulong)SpellKnowledgeSkill.KnowledgeGuid, SpellKnowledgeSkill.Prototype());
            sSkillEnumValues.AddNewEnumValue("ArcanaSpellKnowledge", SpellKnowledgeSkill.KnowledgeGuid);
        }
    }

    internal static class SpellKnowledgeRecord
    {
        internal static List<string> Normalize(IEnumerable<string> incoming)
        {
            List<string> result = new List<string>();
            if (incoming != null)
            {
                foreach (string id in incoming)
                {
                    if (string.IsNullOrEmpty(id) || id.Length > 128 || result.Contains(id)) continue;
                    result.Add(id);
                }
            }
            return result;
        }
    }
}
