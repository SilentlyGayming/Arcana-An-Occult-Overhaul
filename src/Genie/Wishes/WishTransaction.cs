using System;
using System.Collections.Generic;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    // Holds a Genie and wisher only while a wish runs, so a queued or loaded wish must reserve again.
    internal sealed class WishReservation : IDisposable
    {
        [Persistable(false)] private static readonly Dictionary<ulong, bool> Participants = new Dictionary<ulong, bool>();
        private readonly ulong genie;
        private readonly ulong wisher;
        private bool released;

        private WishReservation(ulong genie, ulong wisher)
        {
            this.genie = genie;
            this.wisher = wisher;
        }

        internal static WishReservation TryAcquire(ulong genie, ulong wisher)
        {
            if (genie == 0 || wisher == 0 || Participants.ContainsKey(genie) || Participants.ContainsKey(wisher)) return null;
            Participants.Add(genie, true);
            if (wisher != genie) Participants.Add(wisher, true);
            return new WishReservation(genie, wisher);
        }

        public void Dispose()
        {
            if (released) return;
            released = true;
            Participants.Remove(genie);
            Participants.Remove(wisher);
        }
    }

    internal delegate void WishCommitSignal();

    internal sealed class WishCommit
    {
        private readonly WishCommitSignal pay;
        internal bool Applied { get; private set; }

        internal WishCommit(WishCommitSignal pay)
        { this.pay = pay; }

        // Marks the outcome as applied before paying, so a failing payment notice can never repeat the effect.
        internal void EffectApplied()
        {
            if (Applied) return;
            Applied = true;
            pay();
        }
    }

    internal static class WishRendezvous
    {
        internal static bool Approach(Sim wisher, Sim genie)
        {
            if (wisher == null || genie == null || wisher.HasBeenDestroyed || genie.HasBeenDestroyed) return false;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (wisher.HasExitReason(ExitReason.Default) || genie.HasBeenDestroyed || genie.LotCurrent == null) return false;
                Route route = wisher.CreateRoute();
                route.PlanToPointRadialRange(genie.Position, 1f, 3.5f, RouteDistancePreference.PreferNearestToRouteDestination,
                    RouteOrientationPreference.TowardsObject, genie.LotCurrent.LotId, new int[] { genie.RoomId });
                route.DoRouteFail = false;
                if (wisher.DoRoute(route)) return true;
                wisher.RemoveExitReason(ExitReason.RouteFailed);
            }
            return false;
        }
    }

    [Persistable]
    public sealed class WishParticipant : Interaction<Sim, Sim>
    {
        public static readonly InteractionDefinition Singleton = new Definition();
        internal bool ApproachGenie;
        protected override bool Run()
        {
            bool synced = false;
            try
            {
                if (!StartSync(false, false, null, 0f, false)) return false;
                synced = true;
                if (ApproachGenie && !WishRendezvous.Approach(Actor, Target)) return false;
                Actor.LoopIdle();
                Actor.SynchronizationLevel = Sim.SyncLevel.Committed;
                if (!Actor.WaitForSynchronizationLevelWithSim(Target, Sim.SyncLevel.Committed, 60f)) return false;
                FinishLinkedInteraction(false);
                WaitForSyncComplete(10f);
                return true;
            }
            finally
            {
                if (synced) Actor.ClearSynchronizationData();
            }
        }
        public override void Cleanup()
        {
            if (Actor != null && Actor.SynchronizationTarget == Target) Actor.ClearSynchronizationData();
            base.Cleanup();
        }
        [DoesntRequireTuning]
        private sealed class Definition : InteractionDefinition<Sim, Sim, WishParticipant>
        {
            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip) { return true; }
            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            { return Localization.LocalizeString(actor.IsFemale, "Gameplay/Actors/Sim/GoMakeWish:InteractionName"); }
        }
    }

    internal static class WishFeedback
    {
        internal static string SuccessKey(WishKind kind, bool evil)
        {
            switch (kind)
            {
                case WishKind.WorldPeace: return evil ? "Arcana.Genie.Wish.Success.Misery" : "Arcana.Genie.Wish.Success.Peace";
                case WishKind.Resurrect: return "Arcana.Genie.Wish.Success.Resurrect";
                case WishKind.MoreWishes: return "Arcana.Genie.Wish.Success.More";
                case WishKind.Love: return "Arcana.Genie.Wish.Success.Love";
                case WishKind.LongLife: return "Arcana.Genie.Wish.Success.LongLife";
                case WishKind.LargeFamily: return "Arcana.Genie.Wish.Success.Family";
                case WishKind.Happiness: return "Arcana.Genie.Wish.Success.Happiness";
                case WishKind.Fortune: return "Arcana.Genie.Wish.Success.Fortune";
                case WishKind.Beauty: return "Arcana.Genie.Wish.Success.Beauty";
                case WishKind.FreeGenie: return "Arcana.Genie.Wish.Success.Freedom";
                case WishKind.CheatDeath: return "Arcana.Genie.Wish.Success.CheatDeath";
                case WishKind.Youth: return "Arcana.Genie.Wish.Success.Youth";
                case WishKind.Fame: return "Arcana.Genie.Wish.Success.Fame";
                default: return null;
            }
        }

        internal static void Show(Sim viewer, Sim subject, string key, params object[] args)
        {
            if (key == null || viewer == null || viewer.HasBeenDestroyed) return;
            ObjectGuid portrait = subject == null || subject.HasBeenDestroyed ? viewer.ObjectId : subject.ObjectId;
            viewer.ShowTNSIfSelectable(JournalText.Format(key, args), StyledNotification.NotificationStyle.kGameMessagePositive, portrait);
        }

        internal static void Result(Sim viewer, Sim wisher, WishKind kind, bool succeeded, bool scandal,
            string person, int amount)
        {
            if (succeeded && kind == WishKind.Youth) return;
            bool evil = wisher.HasTrait(Sims3.Gameplay.ActorSystems.TraitNames.Evil);
            string failure = succeeded ? null : kind == WishKind.WorldPeace && evil
                ? "Arcana.Genie.Wish.Failure.WorldMisery" : WishRules.FailureKey(kind, scandal);
            object detail = kind == WishKind.MoreWishes || kind == WishKind.Fortune ? (object)amount : person;
            Show(viewer, wisher, failure ?? SuccessKey(kind, evil), wisher.SimDescription.FirstName, detail);
        }
    }
}
