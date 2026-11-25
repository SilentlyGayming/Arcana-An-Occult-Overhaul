using Sims3.Gameplay.CAS;
using System;
using System.Collections.Generic;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Services;
using Sims3.Gameplay.Situations;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    [Persistable]
    internal sealed class WishRepoRequest
    {
        internal Lot Home;
        internal GameObject Target;
        internal long Deadline;
    }

    internal static class WishRepossession
    {
        [PersistableStatic] internal static List<WishRepoRequest> sRequests;
        internal static bool Request(Sim wisher)
        {
            Lot home = wisher.LotHome;
            Repoman service = Repoman.Instance;
            if (home == null || home.Household != wisher.Household || service == null) return false;
            if (service.IsServiceRequested(home) || service.IsAnySimAssignedToLot(home)) return false;
            if (sRequests == null) sRequests = new List<WishRepoRequest>();
            foreach (WishRepoRequest existing in sRequests) if (existing.Home == home) return false;
            GameObject item = RepomanSituation.FindObjectToRepossess(home, 1000, new GetRepoCandidatesFunctor(new List<GameObject>()));
            if (item == null) return false;
            WishRepoRequest request = new WishRepoRequest();
            request.Home = home; request.Target = item; request.Deadline = WishState.After(24f);
            sRequests.Add(request);
            try { service.MakeServiceRequest(home, true, wisher.ObjectId, true); }
            catch { sRequests.Remove(request); throw; }
            return true;
        }

        internal static void Tick()
        {
            if (sRequests == null || sRequests.Count == 0 || Repoman.Instance == null) return;
            foreach (WishRepoRequest request in new List<WishRepoRequest>(sRequests))
            {
                if (request.Home == null || request.Home.Household == null) { sRequests.Remove(request); continue; }
                Sim worker = Repoman.Instance.GetSimActiveOnLot(request.Home);
                RepomanSituation situation = worker == null ? null : ServiceSituation.FindServiceSituationInvolving(worker) as RepomanSituation;
                if (situation != null)
                {
                    situation.SetState(new WishRepoArrival(situation, request.Target));
                    sRequests.Remove(request);
                }
                else if (SimClock.CurrentTicks >= request.Deadline)
                {
                    Repoman.Instance.MakeServiceRequest(request.Home, false, ObjectGuid.InvalidObjectGuid);
                    sRequests.Remove(request);
                    JournalBootstrap.ReportOnce("Wish Repoman could not be assigned", new InvalidOperationException("The native service did not supply a Repoman within 24 Sim hours."));
                }
            }
        }
        internal static void ResetSaved() { sRequests = null; }
    }

    [Persistable]
    public sealed class WishRepoArrival : ChildSituation<RepomanSituation>
    {
        private GameObject item;
        public WishRepoArrival() { }
        public WishRepoArrival(RepomanSituation parent, GameObject target) : base(parent) { item = target; }
        public override void Init(RepomanSituation parent)
        {
            if (item == null || item.HasBeenDestroyed || !item.CanBeRepossessed(true)) { parent.SetToLeave(); return; }
            parent.OnServiceStarting();
            parent.Worker.SetHiddenFlags(HiddenFlags.Nothing);
            parent.Worker.FadeIn();
            ForceSituationSpecificInteraction(parent.Lot, parent.Worker, ServiceSituation.WalkToLot<RepomanSituation, RepomanSituation.InitialRepomanNotifications>.DoWalkToLot.Singleton,
                null, Arrived, Failed);
        }
        private void Arrived(Sim worker, float value)
        {
            if (item == null || item.HasBeenDestroyed || item.LotCurrent != Parent.Lot || !item.CanBeRepossessed(true)) { Parent.SetToLeave(); return; }
            DebtFreeRepossess interaction = ForceSituationSpecificInteraction(item, worker, DebtFreeRepossess.Singleton,
                null, Finished, Failed) as DebtFreeRepossess;
            if (interaction == null) { Parent.SetToLeave(); return; }
            interaction.RepomanSituation = Parent;
        }
        private void Finished(Sim worker, float value) { Parent.SetToLeave(); }
        private void Failed(Sim worker, float value) { Parent.SetToLeave(); }
    }

    public sealed class DebtFreeRepossess : RepomanSituation.Repossess
    {
        public static readonly InteractionDefinition Singleton = new Definition();
        protected override bool RouteToRepossess()
        { return RepomanSituation != null && (RepomanSituation.RouteRepomanToObject(Target) || RepomanSituation.TeleportRepomanToObject(Target)); }
        protected override bool Run()
        {
            Household household = Target.LotCurrent == null ? null : Target.LotCurrent.Household;
            if (household == null) return false;
            bool inWorld = Target.InWorld;
            int debt = household.DelinquentFunds;
            int value = Target.Value;
            Sims3.Gameplay.Gameflow.Singleton.DisableSave(this, "Gameplay/Services/Repoman:ServiceType");
            try { return base.Run(); }
            finally
            {
                try
                {
                    if (inWorld && !Target.InWorld && household.ComeAndSeeOutOfWorldObjects.Contains(Target))
                        household.DelinquentFunds += WishRules.RepossessionCredit(debt, household.DelinquentFunds, value);
                }
                finally { Sims3.Gameplay.Gameflow.Singleton.EnableSave(this); }
            }
        }
        [DoesntRequireTuning]
        public sealed class Definition : InteractionDefinition<Sim, GameObject, DebtFreeRepossess>
        {
            protected override bool Test(Sim actor, GameObject target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            { return target != null && target.CanBeRepossessed(true); }
        }
    }
}
