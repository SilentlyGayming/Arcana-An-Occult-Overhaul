using System;
using System.Reflection;
using System.Collections.Generic;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Rewards;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.SimIFace.CAS;

namespace SupernaturalExpanded.Journal
{
    internal static class WishAging
    {
        internal static bool LoseDays(Sim sim, float days, bool nextStage)
        {
            SimDescription desc = sim.SimDescription;
            AgingState state = desc.AgingState;
            AgingManager manager = AgingManager.Singleton;
            if (manager == null || !manager.Enabled || state == null || state.IsAgingInProgress() || desc.IsPregnant) return false;
            if (!desc.Elder)
            {
                float remaining = manager.AgingYearsToSimDays(manager.GetCurrentAgingStageLength(desc) - desc.YearsSinceLastAgeTransition);
                if (nextStage || remaining < days)
                {
                    CASAgeGenderFlags oldAge = desc.Age;
                    manager.AgeTransitionWithoutCake(sim);
                    return desc.Age != oldAge || state.IsAgingInProgress() || sim.InteractionQueue.HasInteractionOfType(AgeUp.Singleton);
                }
            }
            state.ExtendAgingState(-days);
            return true;
        }

        [Persistable(false)] private static readonly List<WishYouthPayment> Pending = new List<WishYouthPayment>();

        internal static bool IsReserved(Sim first, Sim second)
        {
            foreach (WishYouthPayment payment in Pending)
                if (payment.Involves(first) || payment.Involves(second)) return true;
            return false;
        }

        internal static bool IsSponsor(Sim sim)
        {
            foreach (WishYouthPayment payment in Pending)
                if (payment.Mode != WishMode.Lamp && payment.Genie == sim) return true;
            return false;
        }

        internal static bool QueueYouth(Sim actor, Sim genie, Sim sim, WishMode mode, GenieLamp lamp)
        {
            if (!WishAgeDown.CanRun || sim.InteractionQueue == null || IsReserved(genie, sim) ||
                sim.InteractionQueue.HasInteractionOfType(AgeDown.Singleton)) return false;
            WishRecord record = WishState.Read(sim.SimDescription, false);
            if (record != null && record.YouthPending) return false;
            WishAgeDown job = WishAgeDown.Singleton.CreateInstance(sim, sim, new InteractionPriority(InteractionPriorityLevel.Aging), false, false) as WishAgeDown;
            if (job == null) return false;
            WishYouthPayment payment = new WishYouthPayment(job, actor, genie, sim, mode, lamp);
            job.Payment = payment;
            Pending.Add(payment);
            try
            {
                Sims3.Gameplay.Gameflow.Singleton.DisableSave(payment.Job, "Arcana.Genie.Wish.Dialog.Youth");
                if (sim.InteractionQueue.Add(job)) return true;
            }
            catch
            {
                Release(payment);
                throw;
            }
            Release(payment);
            return false;
        }

        internal static bool IsPending(WishYouthPayment payment) { return Pending.Contains(payment); }

        internal static void Release(WishYouthPayment payment)
        {
            if (Pending.Remove(payment))
                Sims3.Gameplay.Gameflow.Singleton.EnableSave(payment.Job);
        }

        internal static void Clear()
        {
            foreach (WishYouthPayment payment in Pending.ToArray()) Release(payment);
        }

        internal static void Tick()
        {
            foreach (WishYouthPayment payment in Pending.ToArray())
            {
                Sim sim = payment.Wisher;
                if (payment.Job.NativeExecuting || sim.CurrentInteraction == payment.Job) continue;
                if (sim.HasBeenDestroyed || sim.InteractionQueue == null || SimClock.CurrentTicks >= payment.Expires || !IsQueued(sim, payment.Job))
                    Release(payment);
            }
        }

        private static bool IsQueued(Sim sim, InteractionInstance job)
        {
            foreach (InteractionInstance queued in sim.InteractionQueue.InteractionList)
                if (queued == job) return true;
            return false;
        }

        internal static void Poll(Sim sim, WishRecord state)
        {
            if (!state.YouthPending) return;
            if (sim.SimDescription.YoungAdult)
            {
                state.YouthPending = false;
                SimDescription viewer = SimDescription.Find(state.YouthViewer);
                WishFeedback.Show(viewer == null || viewer.CreatedSim == null ? sim : viewer.CreatedSim, sim,
                    WishFeedback.SuccessKey(WishKind.Youth, false), sim.SimDescription.FirstName);
            }
            else if (!(sim.CurrentInteraction is WishAgeDown) && sim.InteractionQueue != null &&
                !sim.InteractionQueue.HasInteractionOfType(WishAgeDown.Singleton)) state.YouthPending = false;
        }
    }

    internal sealed class WishYouthPayment
    {
        internal readonly WishAgeDown Job;
        internal readonly Sim Actor, Genie, Wisher;
        internal readonly WishMode Mode;
        private readonly GenieLamp lamp;
        internal readonly long Expires;
        private readonly WishCommit commit;

        internal WishYouthPayment(WishAgeDown job, Sim actor, Sim genie, Sim wisher, WishMode mode, GenieLamp lamp)
        {
            Job = job; Actor = actor; Genie = genie; Wisher = wisher; Mode = mode; this.lamp = lamp;
            Expires = WishState.After(1f);
            commit = new WishCommit(Settle);
        }

        internal bool Involves(Sim sim) { return sim != null && (sim == Genie || sim == Wisher); }

        internal bool CanStart()
        {
            if (!WishAging.IsPending(this) || Actor.HasBeenDestroyed || Genie.HasBeenDestroyed || Wisher.HasBeenDestroyed || SimClock.CurrentTicks >= Expires) return false;
            if (Mode == WishMode.Lamp) return WishLamp.Lamp(Genie) == lamp && WishLamp.Allowed(Genie, Wisher);
            CastingRules rules = MagicCasters.ReadRules(Genie);
            GenieMagicSkill skill = GeniePower.Read(Genie.SimDescription, false);
            return skill != null && (Mode == WishMode.Guaranteed
                ? WishRules.GuaranteeAvailable(skill.Power, rules.LawOfTheLamp, skill.GuaranteeReady, SimClock.CurrentTicks)
                : WishRules.OrdinaryAvailable(skill.Power, skill.Capacity, skill.IsExhausted));
        }

        internal void Complete()
        {
            if (Wisher.SimDescription.YoungAdult) commit.EffectApplied();
        }

        private void Settle()
        {
            GenieMagicSkill skill = Mode == WishMode.Lamp ? null : GeniePower.Read(Genie.SimDescription, false);
            GenieWishes.Pay(Actor, Genie, Wisher, WishKind.Youth, Mode, lamp, skill, true);
            WishFeedback.Show(Actor, Wisher, WishFeedback.SuccessKey(WishKind.Youth, false), Wisher.SimDescription.FirstName);
            GenieWishes.AfterCommit(Actor, Genie, Wisher, WishKind.Youth, Mode, skill, true, false, string.Empty, 0);
        }
    }

    [Persistable]
    public sealed class WishAgeDown : Interaction<Sim, Sim>
    {
        internal static readonly InteractionDefinition Singleton = new Definition();
        [Persistable(false)] internal WishYouthPayment Payment;
        [Persistable(false)] private bool nativeExecuting;
        internal bool NativeExecuting { get { return nativeExecuting; } }
        private const BindingFlags InternalInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly MethodInfo NativeRun = typeof(AgeDown).GetMethod("RunNonPersistable", InternalInstance);
        private static readonly MethodInfo NativePrivacy = typeof(AgeDown).GetMethod("CheckForPrivacy", InternalInstance);
        private static readonly FieldInfo AgingActive = typeof(AgeDown).GetField("ActiveAgingInteraction", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo OriginalOutfit = typeof(AgingState).GetField("mOriginalOutfitKey", InternalInstance);
        internal static bool CanRun { get { return NativeRun != null && NativePrivacy != null && AgingActive != null && OriginalOutfit != null; } }

        protected override bool Run()
        {
            SimDescription sim = Actor.SimDescription;
            WishRecord legacy = WishState.Read(sim, false);
            if (Payment != null ? !Payment.CanStart() : legacy == null || !legacy.YouthPending) return false;
            if (!CanRun || sim.AgingState == null || sim.IsPregnant || sim.YoungAdult || Actor.BuffManager.HasTransformBuff()) return false;
            AgeDown native = AgeDown.Singleton.CreateInstance(Actor, Actor, GetPriority(), false, false) as AgeDown;
            if (native == null || !(bool)NativePrivacy.Invoke(native, null)) return false;
            while ((bool)AgingActive.GetValue(null))
            {
                if (Actor.HasExitReason(ExitReason.Default) || Payment != null && !Payment.CanStart()) return false;
                Simulator.Sleep(30);
            }
            if (Payment != null && !Payment.CanStart() || sim.IsPregnant || sim.YoungAdult || Actor.BuffManager.HasTransformBuff()) return false;
            if (Actor.InteractionQueue.HasInteractionOfType(AgeDown.Singleton)) return false;
            native.OriginalAge = sim.Age;
            native.NewAge = CASAgeGenderFlags.YoungAdult;
            native.OriginalSimOutfit = Actor.CurrentOutfit;
            OriginalOutfit.SetValue(sim.AgingState, native.OriginalSimOutfit.Key);
            bool legacySaveLock = Payment == null;
            if (legacySaveLock) Sims3.Gameplay.Gameflow.Singleton.DisableSave(this, "Arcana.Genie.Wish.Dialog.Youth");
            AgingActive.SetValue(null, true);
            nativeExecuting = true;
            bool nativeReturned = false;
            bool aged = false;
            try
            {
                aged = (bool)NativeRun.Invoke(native, null);
                nativeReturned = true;
                if (aged && sim.MidlifeCrisisManager != null) sim.MidlifeCrisisManager.ForceEndCrisis(true);
                return aged;
            }
            catch (TargetInvocationException error)
            {
                ResetException reset = error.InnerException as ResetException;
                if (reset != null) throw reset;
                throw;
            }
            finally
            {
                try
                {
                    try { if (nativeReturned && !aged) RemoveNativeFallback(); }
                    finally
                    {
                        if (Payment != null) Payment.Complete();
                        else if (legacy != null) WishAging.Poll(Actor, legacy);
                    }
                }
                finally
                {
                    try { native.Cleanup(); }
                    finally
                    {
                        nativeExecuting = false;
                        if (legacySaveLock) Sims3.Gameplay.Gameflow.Singleton.EnableSave(this);
                        else WishAging.Release(Payment);
                    }
                }
            }
        }

        private void RemoveNativeFallback()
        {
            foreach (InteractionInstance item in new List<InteractionInstance>(Actor.InteractionQueue.InteractionList))
            {
                AgeDown fallback = item as AgeDown;
                if (fallback != null && fallback.CallbackOnCompletion == null)
                    Actor.InteractionQueue.CancelInteraction(fallback, false);
            }
        }

        public override void Cleanup()
        {
            try { if (Payment != null && !nativeExecuting) WishAging.Release(Payment); }
            finally { base.Cleanup(); }
        }

        [Persistable, DoesntRequireTuning]
        public sealed class Definition : InteractionDefinition<Sim, Sim, WishAgeDown>
        {
            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            { return actor == target && actor.SimDescription.AgingState != null; }

            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            { return JournalText.Get("Arcana.Genie.Wish.Dialog.Youth"); }
        }
    }
}
