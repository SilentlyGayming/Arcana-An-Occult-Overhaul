using System;
using System.Collections.Generic;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Careers;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    [Persistable]
    internal sealed class WishRecord
    {
        internal bool CheatDeath;
        internal bool CheatDeathAttempted;
        internal int Escapes;
        internal long JoyEnds;
        internal long SadEnds;
        internal long LoveRevealAt;
        internal bool ConcealedJoy;
        internal ulong TemporaryLoveTarget;
        internal bool TemporaryLoveHadNativeFlag;
        internal long WorldEnds;
        internal int WorldKind;
        internal bool WorldOwnsTrait;
        internal long FateSealEnds;
        internal ulong FateVictim;
        internal ulong FateDeathIssuer;
        internal long FateDeathUntil;
        internal bool FateDeathVisible;
        internal bool YouthPending;
        internal ulong YouthViewer;
        internal bool CareerPending;
        internal OccupationNames CareerGuid;
        internal long CareerExpires;
        internal bool WorkStarted;
        internal bool WaitForCurrentShift;
        internal float PenaltyRemaining;
        internal float LastPerformance;
        internal int LastCareerLevel;
        internal long[] BlessingEnds = new long[BlessingRules.Count];
        internal int Desire;
        internal ulong DesireGenie;
        internal long DesireAsked;
        internal bool DesireOpen;
        internal float AthleticBonus;
        internal bool InLamp;
        internal bool AdviceLent;
        internal long BlessingCooldownEnds;
        internal void ScheduleLoveReveal(long when)
        {
            if (LoveRevealAt == 0 || when < LoveRevealAt) LoveRevealAt = when;
        }

        internal bool TakeLoveReveal(long now)
        {
            if (LoveRevealAt <= 0 || LoveRevealAt > now) return false;
            LoveRevealAt = 0;
            return true;
        }

        internal bool TakeJoyEnd(long now)
        {
            if (JoyEnds <= 0 || JoyEnds > now) return false;
            JoyEnds = 0;
            return true;
        }

        internal WishRecord Clone()
        {
            WishRecord copy = (WishRecord)MemberwiseClone();
            copy.BlessingEnds = (long[])Blessings().Clone();
            copy.AdviceLent = false;
            return copy;
        }

        internal long[] Blessings()
        {
            if (BlessingEnds == null || BlessingEnds.Length != BlessingRules.Count)
            {
                long[] ends = new long[BlessingRules.Count];
                if (BlessingEnds != null) Array.Copy(BlessingEnds, ends, Math.Min(BlessingEnds.Length, ends.Length));
                BlessingEnds = ends;
            }
            return BlessingEnds;
        }
    }

    internal static class WishState
    {
        [Persistable(false)] private static AlarmHandle alarm = AlarmHandle.kInvalidHandle;
        [Persistable(false)] private static EventListener work, death;

        internal static WishRecord Read(SimDescription sim, bool create)
        {
            if (sim == null || sim.SkillManager == null) return null;
            WishEffectSkill skill = sim.SkillManager.GetElement(WishEffectSkill.WishGuid) as WishEffectSkill;
            if (skill == null && create)
            {
                WishEffectRegistration.Register();
                sim.SkillManager.AddElement(WishEffectSkill.WishGuid);
                skill = sim.SkillManager.GetElement(WishEffectSkill.WishGuid) as WishEffectSkill;
            }
            return skill == null ? null : skill.Record;
        }

        internal static long After(float hours)
        { return SimClock.CurrentTicks + SimClock.ConvertToTicks(hours, TimeUnit.Hours); }

        internal static void Prepare()
        {
            Guard("Registering wish records", WishEffectRegistration.Register);
            Guard("Registering wish moodlets", WishBuffs.Register);
            Guard("Installing wish death protection", WishDeath.Install);
            Guard("Installing wish menus", GenieWishMenu.Prepare);
        }
        private static void Guard(string operation, Sims3.UI.Function action)
        {
            try { action(); }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce(operation, error); }
        }
        internal static void Start()
        {
            Stop(); Prepare(); GenieBlessings.Start(); Tick();
            alarm = AlarmManager.Global.AddAlarmRepeating(1f, TimeUnit.Minutes, Tick, 1f, TimeUnit.Minutes,
                "Arcana wish effects", AlarmType.NeverPersisted, null);
            try
            {
                work = EventTracker.AddListener(EventTypeId.kWentToWork, OnWork);
                death = EventTracker.AddListener(EventTypeId.kSimDied, WishFate.OnDeath);
            }
            catch { Stop(); throw; }
        }
        internal static void Stop()
        {
            WitchCastingLifecycle.RemoveAlarm(ref alarm);
            GenieBlessings.Stop();
            WitchCastingLifecycle.RemoveListener(ref work);
            WitchCastingLifecycle.RemoveListener(ref death);
        }

        private static ListenerAction OnWork(Event e)
        {
            Sim sim = e.Actor as Sim;
            if (sim != null) Update(sim);
            return ListenerAction.Keep;
        }

        internal static void Tick()
        {
            WishAging.Tick();
            Sim[] sims = Sims3.Gameplay.Queries.GetObjects<Sim>();
            foreach (Sim sim in sims)
            {
                try { Update(sim); }
                catch (ResetException) { throw; }
                catch (Exception error) { JournalBootstrap.ReportOnce("Updating wish effects", error); }
            }
            Guard("Updating wish repossession", WishRepossession.Tick);
            Guard("Updating wish lamp handoffs", delegate { WishLamp.Tick(sims); });
            Guard("Updating the Blessing of Harmony", delegate { HarmonyBlessing.Sync(sims); });
        }

        private static void Update(Sim sim)
        {
            if (sim == null || sim.HasBeenDestroyed || sim.BuffManager == null) return;
            WishBuffs.SyncExhaustion(sim, GeniePower.Read(sim.SimDescription, false));
            WishBuffs.Refresh(sim);
            WishEffectSkill travel = sim.SkillManager == null ? null : sim.SkillManager.GetElement(WishEffectSkill.WishGuid) as WishEffectSkill;
            if (travel != null) travel.RestoreTravelBuffs(sim);
            WishRecord state = travel == null ? null : travel.Record;
            if (state == null) return;
            state.Blessings();
            GenieBlessings.Update(sim, state);
            LampRetreat.Check(sim, state);
            long now = SimClock.CurrentTicks;
            WishConsequences.UpdateWorld(sim, state, now);
            WishFate.Update(sim, state);
            WishAging.Poll(sim, state);
            if (state.LoveRevealAt > 0 && state.LoveRevealAt <= now && WishConsequences.EndLove(sim, state) && state.TakeLoveReveal(now))
            {
                GenieOutcomeBuffs.Give(sim, "LoveUnwished", 24f);
                WishFeedback.Show(sim, sim, "Arcana.Genie.Wish.Failure.LoveReveal", sim.SimDescription.FirstName);
            }
            EndJoy(sim, state, now);
            if (state.JoyEnds > now && state.ConcealedJoy) WishBuffs.ShowConcealedJoy(sim);
            if (!state.CareerPending) return;
            Occupation career = sim.SimDescription.CareerManager == null ? null : sim.SimDescription.CareerManager.Occupation;
            if (now >= state.CareerExpires || career == null || career.Guid != state.CareerGuid)
            { ClearCareer(sim, state); return; }
            if (state.WaitForCurrentShift)
            {
                if (!career.IsAtWork) state.WaitForCurrentShift = false;
                return;
            }
            if (career.IsAtWork)
            {
                float performance = WishRules.NormalizedPerformance(career.Performance, career is XpBasedCareer);
                if (!state.WorkStarted)
                {
                    state.WorkStarted = true;
                    state.LastPerformance = performance;
                    state.LastCareerLevel = career.Level;
                    state.PenaltyRemaining = WishRules.CareerPenalty;
                    WishBuffs.GiveWrong(sim, 24f, -10);
                }
                if (career.Level != state.LastCareerLevel) { ClearCareer(sim, state); return; }
                float gained = Math.Max(0f, performance - state.LastPerformance);
                float withheld = Math.Min(gained, state.PenaltyRemaining);
                if (withheld > 0f)
                {
                    XpBasedCareer xp = career as XpBasedCareer;
                    float units = xp != null && career.Performance > 0f ? xp.Experience / career.Performance / 100f : 1f;
                    career.UpdatePerformanceOrExperience(-withheld * units);
                    state.PenaltyRemaining -= withheld;
                }
                state.LastPerformance = WishRules.NormalizedPerformance(career.Performance, career is XpBasedCareer);
            }
            else if (state.WorkStarted) ClearCareer(sim, state);
        }

        internal static void JoyExpired(Sim sim)
        {
            WishRecord state = sim == null ? null : Read(sim.SimDescription, false);
            if (state != null && state.JoyEnds > 0) EndJoy(sim, state, SimClock.CurrentTicks);
        }

        private static void EndJoy(Sim sim, WishRecord state, long now)
        {
            if (!state.TakeJoyEnd(now)) return;
            bool concealed = state.ConcealedJoy;
            state.ConcealedJoy = false;
            sim.BuffManager.RemoveElement(WishBuffs.ConcealedHappiness);
            sim.BuffManager.RemoveElement(WishBuffs.BorrowedJoy);
            if (state.SadEnds > now)
            {
                GenieOutcomeBuffs.Give(sim, "JoyGone", SimClock.ConvertFromTicks(state.SadEnds - now, TimeUnit.Hours));
                if (concealed) WishFeedback.Show(sim, sim, "Arcana.Genie.Wish.Failure.HappinessReveal", sim.SimDescription.FirstName);
            }
        }

        private static void ClearCareer(Sim sim, WishRecord state)
        {
            state.CareerPending = false; state.CareerGuid = (OccupationNames)0; state.WorkStarted = false; state.PenaltyRemaining = 0;
            sim.BuffManager.RemoveElement(WishBuffs.CareerSetback);
        }
    }
}
