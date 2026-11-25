using System;
using System.Collections;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.CelebritySystem;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace.Enums;
using Sims3.UI.Hud;
using Sims3.UI.Controller;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal static class WishConsequences
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo NativeLove = typeof(Relationship).GetField("mIsGenieWishRelationship", PrivateInstance);
        private static readonly FieldInfo Scandals = typeof(CelebrityManager).GetField("mDisgracefulActionQueue", PrivateInstance);

        internal static bool CanStartLove(Sim wisher, Sim other)
        {
            WishRecord own = WishState.Read(wisher.SimDescription, false);
            WishRecord theirs = WishState.Read(other.SimDescription, false);
            return (own == null || own.LoveRevealAt == 0) &&
                (theirs == null || theirs.TemporaryLoveTarget != wisher.SimDescription.SimDescriptionId);
        }

        internal static bool Love(Sim wisher, Sim other, bool succeeds, WishCommit commit)
        {
            Relationship relationship = Relationship.Get(wisher, other, false);
            if (relationship == null) return false;
            if (!succeeds)
            {
                WishRecord record = WishState.Read(wisher.SimDescription, true);
                if (record == null || NativeLove == null || !CanStartLove(wisher, other)) return false;
                record.TemporaryLoveHadNativeFlag = (bool)NativeLove.GetValue(relationship);
                relationship.GenieWishForLove();
                if (!relationship.AreRomantic()) return false;
                record.TemporaryLoveTarget = other.SimDescription.SimDescriptionId;
                record.ScheduleLoveReveal(WishState.After(24f));
            }
            else
                relationship.GenieWishForLove();
            commit.EffectApplied();
            GenieWishes.GiveMoodlet(wisher, BuffNames.WishForLove, 24f);
            GenieWishes.GiveMoodlet(other, BuffNames.WishForLove, 24f);
            EventTracker.SendEvent(EventTypeId.kWishForLove, wisher, other);
            return true;
        }

        internal static bool EndLove(Sim wisher, WishRecord state)
        {
            if (state.TemporaryLoveTarget == 0) return true;
            SimDescription other = SimDescription.Find(state.TemporaryLoveTarget);
            if (other == null) return false;
            Relationship relationship = Relationship.Get(wisher.SimDescription, other, false);
            if (relationship != null && !other.IsDead)
            {
                RemoveRomance(wisher.SimDescription, other, relationship, -100f);
                if (NativeLove != null) NativeLove.SetValue(relationship, state.TemporaryLoveHadNativeFlag);
            }
            state.TemporaryLoveTarget = 0;
            return true;
        }

        internal static void RemoveRomance(SimDescription left, SimDescription right, Relationship relationship, float liking)
        {
            if (relationship.AreRomantic() || left.Partner == right || right.Partner == left)
            {
                SocialCallback.BreakUpDescriptionsShared(left, right);
                relationship.ForceChangeState(liking < 0f ? LongTermRelationshipTypes.Enemy : LongTermRelationshipTypes.Acquaintance);
                if (NativeLove != null) NativeLove.SetValue(relationship, false);
            }
            relationship.LTR.SetLiking(liking);
        }

        internal static bool World(Sim sim, bool success, WishCommit commit)
        {
            WishRecord state = WishState.Read(sim.SimDescription, true);
            if (state == null || state.WorldEnds > 0) return false;
            bool misery = sim.HasTrait(TraitNames.Evil) == success;
            TraitNames trait = WorldTrait(misery);
            state.WorldOwnsTrait = !sim.HasTrait(trait);
            if (state.WorldOwnsTrait) sim.TraitManager.AddHiddenElement(trait);
            if (!sim.HasTrait(trait))
            {
                state.WorldOwnsTrait = false;
                return false;
            }
            state.WorldKind = (misery ? 2 : 1) + (success ? 0 : 2);
            state.WorldEnds = WishState.After(72f);
            commit.EffectApplied();
            UpdateWorld(sim, state, SimClock.CurrentTicks);
            if (success)
            {
                ActiveTopic.AddToSim(sim, "TalkAboutWorldPeaceMisery");
                EventTracker.SendEvent(misery ? EventTypeId.kWishForWorldMisery : EventTypeId.kWishForWorldPeace, sim);
            }
            return true;
        }

        private static TraitNames WorldTrait(bool misery)
        { return misery ? TraitNames.WishedWorldMisery : TraitNames.WishedWorldPeace; }

        internal static void UpdateWorld(Sim sim, WishRecord state, long now)
        {
            if (state.WorldEnds <= 0 || state.WorldKind < 1 || state.WorldKind > 4) return;
            bool misery = state.WorldKind % 2 == 0;
            bool success = state.WorldKind <= 2;
            BuffNames native = misery ? BuffNames.WishForWorldMisery : BuffNames.WishForWorldPeace;
            string privateName = misery ? "MiseryTurnedSour" : "PeaceTurnedSour";
            BuffNames display = success ? native : WishBuffs.Id(privateName);
            if (state.WorldEnds <= now)
            {
                if (state.WorldOwnsTrait) sim.TraitManager.RemoveElement(WorldTrait(misery));
                sim.BuffManager.RemoveElement(display);
                state.WorldEnds = 0;
                state.WorldKind = 0;
                state.WorldOwnsTrait = false;
                return;
            }
            if (!success && state.WorldOwnsTrait) sim.BuffManager.RemoveElement(native);
            float remaining = SimClock.ConvertFromTicks(state.WorldEnds - now, TimeUnit.Hours);
            if (!sim.BuffManager.HasElement(display))
            {
                if (success) GenieWishes.GiveMoodlet(sim, native, remaining);
                else GenieOutcomeBuffs.Give(sim, privateName, remaining);
            }
            BuffInstance active = sim.BuffManager.GetElement(display);
            if (active != null && active.TimeoutCount > remaining * 60f + 1f)
                active.TimeoutCount = remaining * 60f;
        }

        internal static bool Accuse(Sim sim)
        {
            if (Scandals == null) return false;
            DisgracefulActionEvent allegation = new DisgracefulActionEvent(EventTypeId.kSimCommittedDisgracefulAction, sim, DisgracefulActionType.FalselyAccused);
            EventTracker.SendEvent(allegation);
            IList queued = Scandals.GetValue(sim.CelebrityManager) as IList;
            return queued != null && queued.Contains(allegation);
        }
    }
}
