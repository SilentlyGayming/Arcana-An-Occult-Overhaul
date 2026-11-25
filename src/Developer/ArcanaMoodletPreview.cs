using System;
using System.Collections.Generic;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Gameflow = Sims3.Gameplay.Gameflow;

namespace SupernaturalExpanded.Journal
{
    internal static class ArcanaMoodletPreview
    {
        private const int BatchSize = 5;
        [Persistable(false)] private static readonly Dictionary<BuffNames, Origin> Catalog = new Dictionary<BuffNames, Origin>();
        [Persistable(false)] private static readonly Queue<BuffNames> Pending = new Queue<BuffNames>();
        [Persistable(false)] private static readonly Dictionary<BuffNames, BuffInstance> Owned = new Dictionary<BuffNames, BuffInstance>();
        [Persistable(false)] private static readonly List<BuffNames> Applied = new List<BuffNames>();
        [Persistable(false)] private static MoodletPreviewInteraction SaveOwner;
        [Persistable(false)] private static Sim subject;
        [Persistable(false)] private static AlarmHandle alarm = AlarmHandle.kInvalidHandle;
        [Persistable(false)] private static bool saveLocked;

        // Registers a gameplay moodlet with the preview catalog when the moodlet itself is registered.
        internal static void Track(BuffNames id, Origin origin)
        { Catalog[id] = origin; }

        internal static int Toggle()
        {
            if (subject != null) { Clear(); return 1; }
            Sim sim = Sim.ActiveActor;
            if (sim == null || sim.HasBeenDestroyed || sim.BuffManager == null || Catalog.Count == 0) return -1;
            if (sim.InteractionQueue == null) return -1;
            MoodletPreviewInteraction job = MoodletPreviewInteraction.Singleton.CreateInstance(sim, sim,
                new InteractionPriority(InteractionPriorityLevel.UserDirected), false, true) as MoodletPreviewInteraction;
            if (job == null) return -1;
            subject = sim;
            SaveOwner = job;
            try
            {
                if (sim.InteractionQueue.AddNext(job)) return 1;
                Clear();
                return -1;
            }
            catch { Clear(); throw; }
        }

        internal static bool IsCurrent(MoodletPreviewInteraction job)
        { return SaveOwner == job && subject != null && !subject.HasBeenDestroyed && subject.InWorld && subject == Sim.ActiveActor; }

        internal static bool Begin(MoodletPreviewInteraction job)
        {
            if (!IsCurrent(job) || subject.CurrentInteraction != job || subject.BuffManager == null) return false;
            List<BuffNames> ids = new List<BuffNames>(Catalog.Keys);
            ids.Sort();
            foreach (BuffNames id in ids) Pending.Enqueue(id);
            Gameflow.Singleton.DisableSave(job, "Arcana.Cheat.Preview.SaveLock");
            saveLocked = true;
            Advance();
            if (IsCurrent(job))
                alarm = AlarmManager.Global.AddAlarmRepeating(30f, TimeUnit.Minutes, Advance, 30f, TimeUnit.Minutes,
                    "Arcana moodlet previews", AlarmType.NeverPersisted, null);
            return IsCurrent(job);
        }

        internal static void Finish(MoodletPreviewInteraction job)
        { if (SaveOwner == job) Clear(); }

        private static void Advance()
        {
            try
            {
                RemoveBatch();
                if (SaveOwner == null || !IsCurrent(SaveOwner) || subject.CurrentInteraction != SaveOwner || subject.BuffManager == null || Pending.Count == 0)
                { Clear(); return; }
                for (int i = 0; i < BatchSize && Pending.Count > 0; i++)
                {
                    BuffNames original = Pending.Dequeue();
                    BuffInstance template;
                    if (!BuffManager.BuffDictionary.TryGetValue((ulong)original, out template))
                    { JournalBootstrap.ReportOnce("Missing preview moodlet " + original, new InvalidOperationException()); continue; }
                    BuffNames alias = (BuffNames)ResourceUtils.HashString64("ArcanaPreview:" + ((ulong)original).ToString());
                    BuffInstance registered;
                    if (BuffManager.BuffDictionary.TryGetValue((ulong)alias, out registered))
                    {
                        BuffInstance owned;
                        if (!Owned.TryGetValue(alias, out owned) || !object.ReferenceEquals(owned, registered))
                        { JournalBootstrap.ReportOnce("Preview identity collision " + alias, new InvalidOperationException()); continue; }
                    }
                    else
                    {
                        const string prefix = "Gameplay/Excel/buffs/BuffList:";
                        string description = template.Description;
                        if (description.StartsWith(prefix)) description = description.Substring(prefix.Length);
                        Buff.BuffData data = WishBuffs.Data(alias, template.mBuff.BuffIdNonLocalized, description,
                            template.EffectValue, template.TimeoutCount / 60f, false);
                        data.mProductVersion = ProductVersion.BaseGame;
                        data.mThumbKey = template.ThumbKey;
                        data.mThumbString = template.ThumbString;
                        data.mPolarityOverride = template.mBuff.PolarityOverride;
                        data.mMoodletColor = template.mBuff.MoodletColor;
                        BuffInstance preview = new Buff(data).CreateBuffInstance();
                        BuffManager.BuffDictionary.Add((ulong)alias, preview);
                        Owned[alias] = preview;
                    }
                    if (subject.BuffManager.HasElement(alias)) continue;
                    Applied.Add(alias);
                    float duration = template.PermaMoodlet ? -1f : template.TimeoutCount;
                    if (!subject.BuffManager.AddElementIgnoreDelay(alias, template.EffectValue, duration, Catalog[original]))
                        JournalBootstrap.ReportOnce("Could not display preview moodlet " + original, new InvalidOperationException());
                }
            }
            catch { Clear(); throw; }
        }

        private static void RemoveBatch()
        {
            if (subject != null && !subject.HasBeenDestroyed && subject.BuffManager != null)
                foreach (BuffNames id in Applied) subject.BuffManager.RemoveElement(id);
            Applied.Clear();
        }

        internal static void Clear()
        {
            MoodletPreviewInteraction owner = SaveOwner;
            SaveOwner = null;
            try
            {
                WitchCastingLifecycle.RemoveAlarm(ref alarm);
                RemoveBatch();
            }
            finally
            {
                Pending.Clear();
                subject = null;
                if (saveLocked)
                {
                    saveLocked = false;
                    Gameflow.Singleton.EnableSave(owner);
                }
            }
        }
    }

    public sealed class MoodletPreviewInteraction : Interaction<Sim, Sim>
    {
        internal static readonly InteractionDefinition Singleton = new Definition();

        protected override bool Run()
        {
            try
            {
                if (Actor.HasExitReason(ExitReason.Default) || !ArcanaMoodletPreview.Begin(this)) return false;
                while (ArcanaMoodletPreview.IsCurrent(this) && !Actor.HasExitReason(ExitReason.Default))
                    Simulator.Sleep(30);
                return true;
            }
            finally { ArcanaMoodletPreview.Finish(this); }
        }

        public override void Cleanup()
        {
            try { ArcanaMoodletPreview.Finish(this); }
            finally { base.Cleanup(); }
        }

        [DoesntRequireTuning]
        private sealed class Definition : InteractionDefinition<Sim, Sim, MoodletPreviewInteraction>
        {
            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            { return !autonomous && actor != null && actor == target && actor == Sim.ActiveActor; }

            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            { return JournalText.Get("Arcana.Cheat.Preview.SaveLock"); }
        }
    }
}
