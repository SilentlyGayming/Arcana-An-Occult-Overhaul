using System;
using System.Collections.Generic;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Skills;
using Sims3.Gameplay;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal sealed class StatusField
    {
        internal string Label = "";
        internal string Caption = "";
        internal float Fraction;
        internal UIImage Icon;
        internal bool Available;
        internal OverviewTip Tip;

        internal void Clear()
        {
            Label = "";
            Caption = "";
            Fraction = 0f;
            Icon = null;
            Available = false;
            Tip = null;
        }

        internal void Set(string label, string caption, float fraction, UIImage icon)
        {
            Label = label;
            Caption = caption;
            Fraction = float.IsNaN(fraction) || float.IsInfinity(fraction) || fraction < 0f
                ? 0f : (fraction > 1f ? 1f : fraction);
            Icon = icon;
            Available = true;
            Tip = null;
        }
    }

    internal sealed class StatusSource
    {
        internal readonly ObjectGuid Id;
        internal readonly PowerSelection Selection;
        internal SimInfo Info;
        private Sim sim;

        internal StatusSource(ObjectGuid id, SimInfo info, PowerSelection selection)
        {
            Id = id;
            Info = info;
            Selection = selection;
        }

        internal Sim LiveSim
        {
            get
            {
                if (sim == null && Id.IsValid)
                {
                    sim = GlobalFunctions.ConvertGuidToObject<Sim>(Id);
                }
                return sim;
            }
        }
    }

    internal abstract class OccultStatusProvider
    {
        internal readonly OccultTypes Type;

        protected OccultStatusProvider(OccultTypes type)
        {
            Type = type;
        }

        internal uint Accent { get { return OccultAssetData.Accent(Type); } }

        internal abstract void ReadProgress(StatusSource source, StatusField field);
        internal abstract void ReadPower(StatusSource source, StatusField field);

        private static readonly OccultStatusProvider[] Providers =
        {
            new WitchStatus(), new FairyStatus(), new WerewolfStatus(), new VampireStatus(),
            new GenieStatus(), new MermaidStatus(), new GhostStatus()
        };

        internal static OccultStatusProvider For(OccultTypes primary)
        {
            foreach (OccultStatusProvider provider in Providers)
            {
                if (provider.Type == primary)
                {
                    return provider;
                }
            }
            return null;
        }
    }

    internal static class StatusReaders
    {
        private const float HudMeterEmpty = -100f;
        private const float HudMeterScale = 0.005f;
        private static readonly Dictionary<SkillNames, UIImage> skillIcons = new Dictionary<SkillNames, UIImage>();
        private static readonly Dictionary<ulong, UIImage> statusIcons = new Dictionary<ulong, UIImage>();

        internal static void ClearIcons()
        {
            skillIcons.Clear();
            statusIcons.Clear();
        }

        internal static UIImage StatusIcon(ulong instance)
        {
            UIImage image;
            if (statusIcons.TryGetValue(instance, out image))
            {
                return image;
            }

            image = UIManager.LoadUIImage(new ResourceKey(instance, 0x2F7D0004, 0));
            if (!NativeUi.HasImage(image))
            {
                return null;
            }
            statusIcons[instance] = image;
            return image;
        }

        internal static bool ReadSkill(StatusSource source, SkillNames name, string label, StatusField field)
        {
            Skill template = SkillManager.GetStaticSkill(name);
            if (template == null || template.MaxSkillLevel <= 0)
            {
                return false;
            }

            Sim sim = source.LiveSim;
            SkillManager skills = sim == null ? null : sim.SkillManager;
            Skill skill = skills == null ? null : skills.GetElement(name);
            if (skill == null)
            {
                if (sim == null)
                {
                    return false;
                }
                field.Set(label, JournalText.Format("Arcana.Status.Level", 0), 0f, SkillIcon(name, template));
                return true;
            }

            int level = skill.SkillLevel;
            int max = skill.MaxSkillLevel;
            if (level < 0 || max <= 0)
            {
                return false;
            }
            if (level > max)
            {
                level = max;
            }

            int[] thresholds = skill.PointsForNextLevel;
            float fraction = 1f;
            if (level < max)
            {
                if (thresholds == null || level >= thresholds.Length ||
                    (level > 0 && level - 1 >= thresholds.Length))
                {
                    return false;
                }
                float lower = level == 0 ? 0f : thresholds[level - 1];
                float upper = thresholds[level];
                if (upper <= lower || float.IsNaN(skill.SkillPoints) ||
                    float.IsInfinity(skill.SkillPoints))
                {
                    return false;
                }
                fraction = (skill.SkillPoints - lower) / (upper - lower);
            }
            field.Set(label, JournalText.Format("Arcana.Status.Level", level), fraction, SkillIcon(name, skill));
            return true;
        }

        private static UIImage SkillIcon(SkillNames name, Skill skill)
        {
            UIImage image;
            if (skillIcons.TryGetValue(name, out image))
            {
                return image;
            }

            image = null;
            ResourceKey key = skill.IconKey;
            if (key.InstanceId != 0)
            {
                image = UIManager.LoadUIImage(key);
                if (!NativeUi.HasImage(image))
                {
                    image = null;
                }
            }
            skillIcons[name] = image;
            return image;
        }

        internal static bool ReadMotive(StatusSource source, CommodityKind kind, string label, StatusField field)
        {
            float meter;
            Sim sim = source.LiveSim;
            Motives motives = sim == null ? null : sim.Motives;
            Motive motive = motives == null ? null : motives.GetMotive(kind);
            if (motive != null)
            {
                meter = kind == CommodityKind.MagicFatigue ? -motive.UIValue : motive.UIValue;
            }
            else if (source.Info != null && source.Info.OccultInfo != null)
            {
                meter = source.Info.OccultInfo.MagicMotiveValue;
            }
            else
            {
                return false;
            }

            float fraction = (meter - HudMeterEmpty) * HudMeterScale;
            fraction = fraction < 0f ? 0f : (fraction > 1f ? 1f : fraction);
            field.Set(label, JournalText.Format("Arcana.Common.Ratio", (int)Math.Round(fraction * 100f), 100), fraction,
                OccultMeter.MeterIcon(MotiveOccult(kind)));
            return true;
        }

        private static OccultTypes MotiveOccult(CommodityKind kind)
        {
            return kind == CommodityKind.AuraPower ? OccultTypes.Fairy : OccultTypes.Witch;
        }

        internal static bool ReadEnergy(StatusSource source, OccultTypes type, string label, StatusField field)
        {
            OccultEnergy energy = OccultEnergy.For(type);
            if (energy == null)
            {
                return false;
            }
            float value = energy.Get(source.Id);
            field.Set(label, JournalText.Format("Arcana.Common.Ratio", (int)Math.Round(value), (int)OccultEnergy.Max), value / OccultEnergy.Max,
                OccultMeter.MeterIcon(type));
            return true;
        }
    }

    internal sealed class WitchStatus : OccultStatusProvider
    {
        internal WitchStatus() : base(OccultTypes.Witch) { }

        internal override void ReadProgress(StatusSource source, StatusField field)
        {
            PowerSelection selection = source.Selection;
            if (selection != null && selection.Available)
            {
                field.Set(JournalText.Get("Arcana.Status.Label.MagicalProficiency"), WitchTree.Tree.RankLabel((int)selection.Rank), selection.Proficiency,
                    StatusReaders.StatusIcon(OccultAssetData.WitchSkillStatus));
            }
        }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            if (StatusReaders.ReadMotive(source, CommodityKind.MagicFatigue, JournalText.Get("Arcana.Status.Label.MagicalPower"), field))
            {
                Sim sim = source.LiveSim;
                if (sim != null && WitchCastingLifecycle.CastingInstalled)
                {
                    CastingRules rules = WitchCastingContext.ReadCapacityRules(sim);
                    const CommodityKind kind = CommodityKind.MagicFatigue;
                    Motives motives = sim.Motives;
                    if (motives != null && motives.GetMotive(kind) != null)
                    {
                        field.Fraction = CastingRules.PowerFraction(motives.GetValue(kind), motives.GetMin(kind), motives.GetMax(kind));
                    }
                    field.Caption = JournalText.Format("Arcana.Common.Ratio", (int)Math.Round(field.Fraction * rules.Capacity),
                        (int)Math.Round(rules.Capacity));
                    field.Tip = CasterAttributes.PowerTooltip(field.Label, rules);
                }
                UIImage powerIcon = StatusReaders.StatusIcon(OccultAssetData.WitchPowerStatus);
                if (powerIcon != null)
                {
                    field.Icon = powerIcon;
                }
            }
        }
    }

    internal sealed class FairyStatus : OccultStatusProvider
    {
        internal FairyStatus() : base(OccultTypes.Fairy) { }

        internal override void ReadProgress(StatusSource source, StatusField field)
        {
            StatusReaders.ReadSkill(source, SkillNames.FairyMagic, JournalText.Get("Arcana.Status.Label.FairyMagic"), field);
        }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            string label = JournalText.Get("Arcana.Status.Label.MagicalPower");
            if (StatusReaders.ReadMotive(source, CommodityKind.AuraPower, label, field) && source.LiveSim != null && WitchCastingLifecycle.CastingInstalled)
            {
                field.Tip = CasterAttributes.PowerTooltip(label, MagicCasters.ReadRules(source.LiveSim));
            }
        }
    }

    internal sealed class WerewolfStatus : OccultStatusProvider
    {
        internal WerewolfStatus() : base(OccultTypes.Werewolf) { }

        internal override void ReadProgress(StatusSource source, StatusField field)
        {
            StatusReaders.ReadSkill(source, SkillNames.Lycanthropy, JournalText.Get("Arcana.Status.Label.Lycanthropy"), field);
        }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            StatusReaders.ReadEnergy(source, Type, JournalText.Get("Arcana.Status.Label.LycanthropicPower"), field);
        }
    }

    internal sealed class VampireStatus : OccultStatusProvider
    {
        internal VampireStatus() : base(OccultTypes.Vampire) { }

        internal override void ReadProgress(StatusSource source, StatusField field) { }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            StatusReaders.ReadEnergy(source, Type, JournalText.Get("Arcana.Status.Label.VampiricPower"), field);
        }
    }

    internal sealed class GenieStatus : OccultStatusProvider
    {
        internal GenieStatus() : base(OccultTypes.Genie) { }

        internal override void ReadProgress(StatusSource source, StatusField field)
        {
            if (!StatusReaders.ReadSkill(source, GenieMagicSkill.GenieGuid, JournalText.Get("Arcana.Status.Label.GenieMagic"), field)) return;
            Sim sim = source.LiveSim;
            GenieMagicSkill skill = sim == null ? null : GeniePower.Read(sim.SimDescription, false);
            if (skill == null) return;
            Rank rank = CastingRules.RankForSkill(skill.SkillLevel);
            field.Set(JournalText.Get("Arcana.Status.Label.MagicalProficiency"), GenieRanks.FullName(rank),
                GenieRanks.Progress(skill.SkillLevel, field.Fraction), field.Icon);
        }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            Sim sim = source.LiveSim;
            if (sim == null || !WitchCastingLifecycle.CastingInstalled)
            {
                StatusReaders.ReadEnergy(source, Type, JournalText.Get("Arcana.Status.Label.MagicalPower"), field);
                return;
            }
            CastingRules rules = MagicCasters.ReadRules(sim);
            float power = Math.Min(GeniePower.Get(sim.SimDescription), rules.Capacity);
            string label = JournalText.Get("Arcana.Status.Label.MagicalPower");
            field.Set(label, JournalText.Format("Arcana.Common.Ratio", (int)Math.Round(power), (int)Math.Round(rules.Capacity)),
                rules.Capacity <= 0f ? 0f : power / rules.Capacity, OccultMeter.MeterIcon(Type));
            field.Tip = CasterAttributes.PowerTooltip(label, rules);
        }
    }

    internal sealed class MermaidStatus : OccultStatusProvider
    {
        internal MermaidStatus() : base(OccultTypes.Mermaid) { }

        internal override void ReadProgress(StatusSource source, StatusField field) { }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            StatusReaders.ReadEnergy(source, Type, JournalText.Get("Arcana.Status.Label.MermadicPower"), field);
        }
    }

    internal sealed class GhostStatus : OccultStatusProvider
    {
        internal GhostStatus() : base(OccultTypes.Ghost) { }

        internal override void ReadProgress(StatusSource source, StatusField field) { }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            StatusReaders.ReadEnergy(source, Type, JournalText.Get("Arcana.Status.Label.PhantasmalPower"), field);
        }
    }
}
