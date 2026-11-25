using Sims3.Gameplay.Core;
using System;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects;
using Sims3.Gameplay.Scuba;
using Sims3.Gameplay.Interfaces;
using Sims3.Gameplay.Objects.Gardening;
using Sims3.Gameplay.Services;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal static class WishDeath
    {
        private static readonly FieldInfo normal = typeof(GrimReaperSituation.ReapSoul).GetField("Singleton");
        private static readonly FieldInfo diving = typeof(GrimReaperSituation.ReapSoulDiving).GetField("DivingSingleton");
        private static InteractionDefinition originalNormal, originalDiving;
        internal static void Install()
        {
            Install(normal, new WishReapSoul.Definition(), ref originalNormal);
            Install(diving, new WishReapSoulDiving.DivingDefinition(), ref originalDiving);
        }

        private static void Install(FieldInfo field, InteractionDefinition replacement, ref InteractionDefinition original)
        {
            if (field == null) throw new InvalidOperationException("Missing native reaping interaction.");
            InteractionDefinition current = field.GetValue(null) as InteractionDefinition;
            if (current == null || current.GetType().DeclaringType != field.DeclaringType) return;
            original = current;
            field.SetValue(null, replacement);
        }

        internal static void Restore()
        {
            if (normal != null && normal.GetValue(null) is WishReapSoul.Definition) normal.SetValue(null, originalNormal);
            if (diving != null && diving.GetValue(null) is WishReapSoulDiving.DivingDefinition) diving.SetValue(null, originalDiving);
            originalNormal = originalDiving = null;
        }
    }

    [Persistable]
    public sealed class WishReapSoul : GrimReaperSituation.ReapSoul
    {
        private bool rolled;
        private bool protectedEscape;
        private bool counted;

        protected override bool Run()
        {
            WishRecord record = WishState.Read(Target.SimDescription, false);
            bool natural = Target.HasTrait(TraitNames.Unlucky) || Target.Inventory != null && Target.Inventory.Find<DeathFlower>() != null ||
                Target.SimDescription.DeathStyle == SimDescription.DeathType.Ranting && !Target.HasTrait(TraitNames.ThereAndBackAgain);
            if (record == null || !record.CheatDeath || natural || Target.SimDescription.DeathStyle == SimDescription.DeathType.OldAge)
                return base.Run();
            if (!rolled)
            {
                rolled = true;
                protectedEscape = RandomUtil.RandomChance(WishRules.DeathChance(record.Escapes));
            }
            if (!protectedEscape) return base.Run();
            if (!CreateGraveStone()) return false;
            mSMCDeath = mSituation.SMCDeath;
            mSMCDeath.SetActor("y", Target);
            mSMCDeath.SetActor("grave", mGrave);
            mSMCDeath.EnterState("y", "Enter");
            mSMCDeath.EnterState("x", "Enter");
            WishDeathNativeFields.MarkReaping(mSituation);
            PlaceGraveStone();
            mDeathProgress = DeathProgress.GravePlaced;
            Actor.RouteTurnToFace(Target.Position);
            mSMCDeath.RequestState(true, "x", "CreateTombstone");
            mSMCDeath.RequestState(true, "x", "ReaperPointAtGrave");
            mSMCDeath.RequestState(false, "x", "ReaperFloating");
            mGhostPosition = GetPositionForGhost(Target, mGrave);
            Target.SetPosition(mGhostPosition);
            mGrave.GhostSetup(Target, false);
            Target.SetHiddenFlags(HiddenFlags.Nothing); Target.FadeIn();
            string name = Localization.LocalizeString(Target.IsFemale, "Gameplay/Actors/Sim/ReapSoul:InteractionName");
            BeginSocialInteraction(new SocialInteractionB.DefinitionDeathInteraction(name, false), true, 1.25f, false);
            mDeathProgress = DeathProgress.UnluckyStarted;
            mSMCDeath.AddOneShotScriptEventHandler(101u, RestoreLife);
            mSMCDeath.RequestState(false, "x", "Unlucky");
            mSMCDeath.RequestState(true, "y", "Unlucky");
            mSMCDeath.RequestState(false, "x", "Exit");
            mSMCDeath.RequestState(true, "y", "Exit");
            GrimReaperPostSequenceCleanup();
            if (mDeathProgress == DeathProgress.UnluckyPostEvent) mDeathProgress = DeathProgress.Complete;
            return true;
        }

        private void RestoreLife(StateMachineClient sender, IEvent e)
        {
            if (mDeathProgress != DeathProgress.UnluckyStarted) return;
            mGrave.GhostToSim(Target, false, false);
            Actor.ClearSynchronizationData();
            mGrave.FadeOut(false, true);
            Target.SimDescription.IsNeverSelectable = false;
            Target.SimDescription.ShowSocialsOnSim = true;
            Target.Autonomy.DecrementAutonomyDisabled();
            mDeathProgress = DeathProgress.UnluckyPostEvent;
            CountEscape();
        }

        private void CountEscape()
        {
            if (counted || !protectedEscape || Target.SimDescription.DeathStyle != SimDescription.DeathType.None) return;
            counted = true;
            WishRecord record = WishState.Read(Target.SimDescription, false);
            if (record != null) record.Escapes++;
            SpellKnowledge.Notify(Target, "Arcana.Genie.Wishes.DeathEscaped", Target.SimDescription.FirstName);
        }

        public override void Cleanup()
        {
            try { base.Cleanup(); }
            finally { CountEscape(); }
        }

        [DoesntRequireTuning]
        public sealed class Definition : InteractionDefinition<Sim, Sim, WishReapSoul>, Sims3.Gameplay.Controllers.IUsableDuringFire
        {
            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            { return actor.Service is GrimReaper && target.SimDescription.DeathStyle != SimDescription.DeathType.None; }
            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            { return Localization.LocalizeString(target.IsFemale, "Gameplay/Actors/Sim/ReapSoul:InteractionName"); }
        }
    }
    internal static class WishDeathNativeFields
    {
        private static readonly FieldInfo First = typeof(GrimReaperSituation).GetField("mIsFirstSim", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ScubaJig = typeof(GrimReaperSituation).GetField("mScubaDeathJig", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static void MarkReaping(GrimReaperSituation situation)
        { if (First != null) First.SetValue(situation, false); }
        internal static SocialJig Jig(GrimReaperSituation situation)
        { return ScubaJig == null ? null : ScubaJig.GetValue(situation) as SocialJig; }
    }

    [Persistable]
    public sealed class WishReapSoulDiving : GrimReaperSituation.ReapSoulDiving
    {
        private bool rolled;
        private bool protectedEscape;
        private bool counted;
        protected override bool Run()
        {
            WishRecord record = WishState.Read(Target.SimDescription, false);
            if (record == null || !record.CheatDeath || Target.Inventory != null && Target.Inventory.Find<DeathFlower>() != null ||
                Target.SimDescription.DeathStyle == SimDescription.DeathType.OldAge) return base.Run();
            if (!rolled) { rolled = true; protectedEscape = RandomUtil.RandomChance(WishRules.DeathChance(record.Escapes)); }
            if (!protectedEscape) return base.Run();
            GrimReaperSituation situation = ServiceSituation.FindServiceSituationInvolving(Actor) as GrimReaperSituation;
            SocialJig jig = situation == null ? null : WishDeathNativeFields.Jig(situation);
            if (jig == null) return base.Run();
            Type linkedType = typeof(GrimReaperSituation).GetNestedType("BeReapedDiving", BindingFlags.NonPublic);
            FieldInfo field = linkedType == null ? null : linkedType.GetField("Singleton");
            FieldInfo flowerFlag = linkedType == null ? null : linkedType.GetField("mHadDeathFlower");
            InteractionDefinition native = field == null ? null : field.GetValue(null) as InteractionDefinition;
            if (native == null || flowerFlag == null) return base.Run();
            InteractionInstance linked = native.CreateInstance(Actor, Target, new InteractionPriority(InteractionPriorityLevel.MaxDeath, 0f), false, false);
            if (linked == null) return base.Run();
            if (!CreateGraveStone()) return false;
            WishDeathNativeFields.MarkReaping(situation);
            mSMCDeath = mSituation.SMCDeath;
            mSMCDeath.SetActor("y", Target); mSMCDeath.SetActor("grave", mGrave);
            mSMCDeath.EnterState("y", "Enter"); mSMCDeath.EnterState("x", "Enter");
            ScubaDiving posture = Target.Posture as ScubaDiving;
            if (posture != null) posture.StopBubbleEffects();
            Target.FadeOut(false, false, 2f);
            PlaceGraveStone(); mDeathProgress = DeathProgress.GravePlaced;
            mSMCDeath.RequestState(true, "x", "CreateTombstone");
            mSMCDeath.RequestState(false, "x", "ReaperFloating");
            mGhostPosition = jig.GetPositionOfSlot(SocialJigTwoPerson.RoutingSlots.SimB);
            mGhostForward = jig.GetForwardOfSlot(SocialJigTwoPerson.RoutingSlots.SimB);
            mSMCDeath.AddOneShotScriptEventHandler(101u, EventCallbackSimToGhostEffectNoFadeOut);
            mSMCDeath.AddOneShotScriptEventHandler(102u, EventCallbackFadeBodyFromPose);
            bool shark = Target.SimDescription.DeathStyle == SimDescription.DeathType.Shark;
            mSMCDeath.RequestState(true, "y", shark ? "PoseShark" : "PoseDrown");
            mSMCDeath.RequestState(true, "y", shark ? "SharkToFloat" : "DrownToFloat");
            mSMCDeath.RequestState(false, "y", "GhostFloating");
            LinkedInteractionInstance = linked;
            flowerFlag.SetValue(linked, true);
            Target.AddExitReason(ExitReason.HigherPriorityNext);
            Target.InteractionQueue.AddNext(linked);
            StartSync(true, true, null, 0f, false);
            mDeathProgress = DeathProgress.UnluckyStarted;
            mSMCDeath.AddOneShotScriptEventHandler(101u, RestoreLife);
            mSMCDeath.RequestState(false, "x", "DeathFlower"); mSMCDeath.RequestState(true, "y", "DeathFlower");
            mSMCDeath.RequestState(false, "x", "Exit"); mSMCDeath.RequestState(true, "y", "Exit");
            FinishLinkedInteraction(true);
            if (mDeathProgress == DeathProgress.UnluckyPostEvent) mDeathProgress = DeathProgress.Complete;
            return true;
        }
        private void RestoreLife(StateMachineClient sender, IEvent e)
        {
            if (mDeathProgress != DeathProgress.UnluckyStarted) return;
            mGrave.GhostToSim(Target, false, false); Actor.ClearSynchronizationData();
            mGrave.FadeOut(false, true);
            Target.SimDescription.IsNeverSelectable = false; Target.SimDescription.ShowSocialsOnSim = true;
            Target.Autonomy.DecrementAutonomyDisabled(); Target.FadeOut(false, false, 0.4f);
            mDeathProgress = DeathProgress.UnluckyPostEvent; CountEscape();
        }
        private void CountEscape()
        {
            if (counted || !protectedEscape || Target.SimDescription.DeathStyle != SimDescription.DeathType.None) return;
            counted = true;
            WishRecord record = WishState.Read(Target.SimDescription, false);
            if (record != null) record.Escapes++;
            SpellKnowledge.Notify(Target, "Arcana.Genie.Wishes.DeathEscaped", Target.SimDescription.FirstName);
        }
        public override void Cleanup() { try { base.Cleanup(); } finally { CountEscape(); } }
        [DoesntRequireTuning]
        public sealed class DivingDefinition : InteractionDefinition<Sim, Sim, WishReapSoulDiving>, Sims3.Gameplay.Controllers.IUsableDuringFire, IScubaDivingInteractionDefinition
        {
            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            { return actor.Service is GrimReaper && target.SimDescription.DeathStyle != SimDescription.DeathType.None; }
        }
    }

}
