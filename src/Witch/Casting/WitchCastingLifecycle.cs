using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.Gameplay.Skills;
using Sims3.Gameplay.Utilities;
using Sims3.Gameplay;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal static class WitchCastingLifecycle
    {
        private static readonly FieldInfo SkillMultipliers = typeof(InteractionInstance).GetField("mSkillMultipliers",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<ulong, WeakReference> practice = new Dictionary<ulong, WeakReference>();
        private static EventListener instantiated;
        private static EventListener practiced;
        private static EventListener bought;
        private static AlarmHandle recovery = AlarmHandle.kInvalidHandle;
        private static AlarmHandle upkeep = AlarmHandle.kInvalidHandle;

        internal static bool CastingInstalled { get { return SpellKnowledgeGates.Installed; } }

        internal static void Prepare()
        {
            if (GameUtils.IsInstalled(ProductVersion.EP7))
            {
                WitchMagicRecovery.RegisterFizzle();
            }
        }

        internal static void Start()
        {
            if (!GameUtils.IsInstalled(ProductVersion.EP7))
            {
                return;
            }

            WitchMagicRecovery.RegisterFizzle();
            SpellKnowledgeGates.Prepare();
            WitchNativeCosts.Start();

            instantiated = EventTracker.AddListener(EventTypeId.kSimInstantiated, OnInstantiated);
            practiced = EventTracker.AddListener(EventTypeId.kPracticedSpellcasting, OnPractice);
            bought = EventTracker.AddListener(EventTypeId.kBoughtObject, OnBought);
            foreach (Sims3.Gameplay.Abstracts.GameObject target in Sims3.Gameplay.Queries.GetObjects<Sims3.Gameplay.Abstracts.GameObject>())
            {
                SpellKnowledgeGates.InstallFor(target);
            }
            SpellKnowledgeGates.InstallFor(Sims3.Gameplay.Core.Terrain.Singleton);
            GenieRain.Restore();
            WitchMagicRecovery.RefreshAll();
            recovery = AlarmManager.Global.AddAlarmRepeating(5f, TimeUnit.Minutes,
                WitchMagicRecovery.RefreshAll, 5f, TimeUnit.Minutes, "Arcana Witch regeneration", AlarmType.NeverPersisted, null);
            upkeep = AlarmManager.Global.AddAlarmRepeating(5f, TimeUnit.Minutes, RunUpkeep, 5f, TimeUnit.Minutes,
                "Arcana magic upkeep", AlarmType.NeverPersisted, null);
        }

        internal static void Stop()
        {
            RemoveListener(ref instantiated);
            RemoveListener(ref practiced);
            RemoveListener(ref bought);
            RemoveAlarm(ref recovery);
            RemoveAlarm(ref upkeep);
            WitchMagicRecovery.RestoreAll();
            WitchNativeCosts.Stop();
            practice.Clear();
            GenieRain.Clear();
        }
        internal static void RemoveListener(ref EventListener listener)
        {
            if (listener != null) EventTracker.RemoveListener(listener);
            listener = null;
        }

        internal static void RemoveAlarm(ref AlarmHandle alarm)
        {
            if (alarm != AlarmHandle.kInvalidHandle && AlarmManager.Global != null) AlarmManager.Global.RemoveAlarm(alarm);
            alarm = AlarmHandle.kInvalidHandle;
        }

        private static void RunUpkeep()
        {
            try
            {
                Sim[] sims = Sims3.Gameplay.Queries.GetObjects<Sim>();
                GeniePower.RegenerateAll(5f, sims);
                MagicCasters.UpdateAllHands(sims);
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce("Arcana magic upkeep", error);
            }
        }

        private static ListenerAction OnInstantiated(Event e)
        {
            Sim sim = e.Actor as Sim ?? e.TargetObject as Sim;
            if (sim != null)
            {
                JournalBootstrap.RunLater(delegate { SpellKnowledgeGates.InstallFor(sim); });
            }
            return ListenerAction.Keep;
        }

        private static ListenerAction OnBought(Event e)
        {
            Sims3.Gameplay.Abstracts.GameObject target = e.TargetObject as Sims3.Gameplay.Abstracts.GameObject;
            if (target != null)
            {
                JournalBootstrap.RunLater(delegate { SpellKnowledgeGates.InstallFor(target); });
            }
            return ListenerAction.Keep;
        }

        private static ListenerAction OnPractice(Event e)
        {
            Sim sim = e.Actor as Sim;
            if (!WitchCastingContext.IsWitch(sim) || SkillMultipliers == null)
            {
                return ListenerAction.Keep;
            }

            MagicWand.PracticeSpellCasting interaction = sim.CurrentInteraction as MagicWand.PracticeSpellCasting;
            if (interaction == null || !interaction.HasCommodityUpdate(CommodityKind.SkillSpellcasting))
            {
                return ListenerAction.Keep;
            }

            ulong id = sim.SimDescription.SimDescriptionId;
            WeakReference previous;
            if (practice.TryGetValue(id, out previous) && previous.Target == interaction)
            {
                return ListenerAction.Keep;
            }

            Dictionary<SkillNames, float> multipliers = SkillMultipliers.GetValue(interaction) as Dictionary<SkillNames, float>;
            float current;
            if (multipliers != null && multipliers.TryGetValue(SkillNames.Spellcasting, out current))
            {
                interaction.AlterSkillUpdateMultiplier(CommodityKind.SkillSpellcasting, current * (CastingRules.PracticeExperience - 1f));
                practice[id] = new WeakReference(interaction);
            }
            return ListenerAction.Keep;
        }
    }
}
