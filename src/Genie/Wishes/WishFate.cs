using System.Collections.Generic;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.EventSystem;
using Sims3.UI;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal enum FateOutcome { DeathsDue, ThingYouLoveMost, ForgottenLove, ChainsOfOld, FortunesDebt, Misfortune, SlapOnTheWrist }

    internal static class WishFate
    {
        internal static FateOutcome Select(float draw)
        {
            if (draw < 1f) return FateOutcome.DeathsDue;
            if (draw < 3f) return FateOutcome.ThingYouLoveMost;
            if (draw < 10f) return FateOutcome.ForgottenLove;
            if (draw < 25f) return FateOutcome.ChainsOfOld;
            if (draw < 45f) return FateOutcome.FortunesDebt;
            if (draw < 70f) return FateOutcome.Misfortune;
            return FateOutcome.SlapOnTheWrist;
        }

        internal static string Key(FateOutcome outcome)
        {
            switch (outcome)
            {
                case FateOutcome.DeathsDue: return "Arcana.Genie.Fate.DeathsDue";
                case FateOutcome.ThingYouLoveMost: return "Arcana.Genie.Fate.ThingYouLoveMost";
                case FateOutcome.ForgottenLove: return "Arcana.Genie.Fate.ForgottenLove";
                case FateOutcome.ChainsOfOld: return "Arcana.Genie.Fate.ChainsOfOld";
                case FateOutcome.FortunesDebt: return "Arcana.Genie.Fate.FortunesDebt";
                case FateOutcome.Misfortune: return "Arcana.Genie.Fate.FatesMisfortune";
                default: return "Arcana.Genie.Fate.SlapOnTheWrist";
            }
        }

        internal static bool Apply(Sim genie, FateOutcome outcome)
        {
            SimDescription desc = genie.SimDescription;
            SimDescription subject = desc;
            switch (outcome)
            {
                case FateOutcome.DeathsDue:
                    return RequestDeath(genie, genie);
                case FateOutcome.ThingYouLoveMost:
                    Relationship strongest = Strongest(desc);
                    if (strongest == null) return false;
                    subject = strongest.GetOtherSimDescription(desc);
                    if (subject.CreatedSim == null)
                    {
                        WishRecord pending = WishState.Read(desc, true);
                        if (pending == null || pending.FateVictim != 0) return false;
                        pending.FateVictim = subject.SimDescriptionId;
                        return true;
                    }
                    return RequestDeath(genie, subject.CreatedSim);
                case FateOutcome.ForgottenLove:
                    Relationship forgotten = Strongest(desc);
                    if (forgotten == null) return false;
                    subject = forgotten.GetOtherSimDescription(desc);
                    WishConsequences.RemoveRomance(desc, subject, forgotten, 0f);
                    break;
                case FateOutcome.ChainsOfOld:
                    WishRecord state = WishState.Read(desc, true);
                    if (state == null) return false;
                    state.FateSealEnds = WishState.After(CastingRules.ChainsHours);
                    GenieOutcomeBuffs.Give(genie, "Chains", CastingRules.ChainsHours);
                    break;
                case FateOutcome.FortunesDebt:
                    Household household = desc.Household;
                    if (household == null) return false;
                    household.ModifyFamilyFunds(-(int)(household.FamilyFunds * CastingRules.DebtShare));
                    break;
                case FateOutcome.Misfortune:
                    GenieWishes.GiveMoodlet(genie, BuffNames.FeelingUnlucky, CastingRules.MisfortuneHours);
                    break;
            }
            WishFeedback.Show(genie, subject.CreatedSim, Key(outcome), desc.FirstName, subject.FirstName);
            JournalBootstrap.RefreshMeter();
            return true;
        }

        private static bool RequestDeath(Sim genie, Sim victim)
        {
            if (victim == null || victim.HasBeenDestroyed || victim.SimDescription.IsDead || !victim.CanBeKilled() ||
                victim.InteractionQueue == null || victim.InteractionQueue.HasInteractionOfType(Sims3.Gameplay.Objects.Urnstone.KillSim.Singleton)) return false;
            WishRecord marker = WishState.Read(victim.SimDescription, true);
            if (marker == null || marker.FateDeathUntil > SimClock.CurrentTicks) return false;
            marker.FateDeathIssuer = genie.SimDescription.SimDescriptionId;
            marker.FateDeathVisible = genie.IsSelectable;
            marker.FateDeathUntil = WishState.After(4f);
            if (victim.Kill(SimDescription.DeathType.Electrocution)) return true;
            marker.FateDeathUntil = 0;
            return false;
        }

        internal static ListenerAction OnDeath(Event e)
        {
            SimDescriptionEvent death = e as SimDescriptionEvent;
            if (death == null || death.SimDescription == null) return ListenerAction.Keep;
            SimDescription victim = death.SimDescription;
            WishRecord marker = WishState.Read(victim, false);
            if (marker == null || marker.FateDeathUntil <= SimClock.CurrentTicks ||
                victim.DeathStyle != SimDescription.DeathType.Electrocution) return ListenerAction.Keep;
            marker.FateDeathUntil = 0;
            if (!marker.FateDeathVisible) return ListenerAction.Keep;
            SimDescription genie = SimDescription.Find(marker.FateDeathIssuer);
            bool self = victim.SimDescriptionId == marker.FateDeathIssuer;
            if (genie == null && !self) return ListenerAction.Keep;
            string key = Key(self ? FateOutcome.DeathsDue : FateOutcome.ThingYouLoveMost);
            string text = JournalText.Format(key, self ? victim.FirstName : genie.FirstName, victim.FirstName);
            Sim portrait = victim.CreatedSim;
            StyledNotification.Show(new StyledNotification.Format(text, ObjectGuid.InvalidObjectGuid,
                portrait == null || portrait.HasBeenDestroyed ? ObjectGuid.InvalidObjectGuid : portrait.ObjectId,
                StyledNotification.NotificationStyle.kGameMessagePositive));
            return ListenerAction.Keep;
        }

        private static Relationship Strongest(SimDescription genie)
        {
            IEnumerable<Relationship> relationships = Relationship.Get(genie);
            if (relationships == null) return null;
            Relationship strongest = null;
            ulong bestId = ulong.MaxValue;
            foreach (Relationship relationship in relationships)
            {
                SimDescription other = relationship.GetOtherSimDescription(genie);
                if (other == null || other == genie || !other.IsHuman || other.IsDead || !other.IsValidDescription) continue;
                bool partner = genie.Partner == other;
                bool bestPartner = strongest != null && genie.Partner == strongest.GetOtherSimDescription(genie);
                if (strongest == null || partner && !bestPartner || partner == bestPartner &&
                    (relationship.LTR.Liking > strongest.LTR.Liking ||
                    relationship.LTR.Liking == strongest.LTR.Liking && other.SimDescriptionId < bestId))
                {
                    strongest = relationship;
                    bestId = other.SimDescriptionId;
                }
            }
            return strongest;
        }

        internal static void Update(Sim genie, WishRecord state)
        {
            long now = SimClock.CurrentTicks;
            if (state.FateDeathUntil > now && state.FateDeathUntil - now < SimClock.ConvertToTicks(3.9f, TimeUnit.Hours) &&
                !genie.SimDescription.IsDead && genie.SimDescription.DeathStyle == SimDescription.DeathType.None &&
                genie.DeathReactionBroadcast == null && genie.InteractionQueue != null &&
                !genie.InteractionQueue.HasInteractionOfType(Sims3.Gameplay.Objects.Urnstone.KillSim.Singleton))
                state.FateDeathUntil = 0;
            if (state.FateSealEnds > now)
            {
                BuffNames chains = WishBuffs.Id("Chains");
                if (!genie.BuffManager.HasElement(chains))
                    GenieOutcomeBuffs.Give(genie, "Chains", SimClock.ConvertFromTicks(state.FateSealEnds - SimClock.CurrentTicks, TimeUnit.Hours));
            }
            if (state.FateVictim == 0) return;
            SimDescription victim = SimDescription.Find(state.FateVictim);
            if (victim == null || victim.CreatedSim == null && !victim.IsDead) return;
            state.FateVictim = 0;
            if (!victim.IsDead) RequestDeath(genie, victim.CreatedSim);
        }
    }
}
