using System.Collections.Generic;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    public sealed class LampRetreat : Interaction<Sim, Sim>
    {
        public static readonly InteractionDefinition Singleton = new Definition();
        [Persistable(false)] private static readonly List<LampRetreat> Active = new List<LampRetreat>();

        [Persistable(false)] private bool hidden;
        [Persistable(false)] private bool resting;
        [Persistable(false)] private readonly List<MotiveDelta> recovery = new List<MotiveDelta>();
        [Persistable(false)] private bool moodletApplied;
        private static readonly CommodityKind[] Needs =
            { CommodityKind.Energy, CommodityKind.Hygiene, CommodityKind.Hunger, CommodityKind.Bladder, CommodityKind.Fun, CommodityKind.Social };
        private static readonly float[] Gain = { 21.33f, 15f, 12f, 18f, 12f, 10f };

        internal static bool Unlocked(Sim actor, CastingRules rules)
        {
            return rules.Rank >= Rank.Adept && rules.Lampbound && GeniePerks.Owns(actor.SimDescription, "PocketPalace");
        }

        internal static bool IsResting(Sim sim)
        {
            LampRetreat retreat = sim.CurrentInteraction as LampRetreat;
            return retreat != null && retreat.resting;
        }

        internal static void ReleaseAll()
        {
            foreach (LampRetreat retreat in Active.ToArray())
            {
                try { retreat.Release(false); }
                catch (System.Exception error) { JournalBootstrap.ReportOnce("Ending lamp rest", error); }
            }
        }

        // A Genie saved inside the lamp resumes the rest after loading, or steps out if the rest can no longer continue.
        internal static void Check(Sim sim, WishRecord record)
        {
            if (!record.InLamp || IsRunning(sim) || sim.InteractionQueue == null || sim.InteractionQueue.HasInteractionOfType(Singleton)) return;
            if (CanRest(sim, sim))
            {
                InteractionInstance resume = Singleton.CreateInstance(sim, sim, new InteractionPriority(InteractionPriorityLevel.UserDirected), false, true);
                if (resume != null && sim.InteractionQueue.AddNext(resume)) return;
            }
            record.InLamp = false;
            Show(sim, true);
        }

        private static bool IsRunning(Sim sim)
        {
            foreach (LampRetreat retreat in Active)
                if (retreat.Actor == sim) return true;
            return false;
        }

        protected override bool Run()
        {
            WishRecord record = WishState.Read(Actor.SimDescription, true);
            bool resume = record != null && record.InLamp;
            if (record == null || !CanRest(Actor, Target) || (!resume && ModelHidden(Actor)) || Actor.HasExitReason(ExitReason.Default)) return false;
            StandardEntry(false);
            try
            {
                for (int index = 0; index < Needs.Length; index++)
                    if (Actor.Motives.HasMotive(Needs[index])) recovery.Add(AddMotiveDelta(Needs[index], Gain[index]));
                if (!resume)
                {
                    Actor.PlaySoloAnimation("a_genie_clap_x", true, ProductVersion.EP6);
                    if (Actor.HasExitReason(ExitReason.Default) || !CanRest(Actor, Target)) return false;
                }
                Active.Add(this);
                hidden = true;
                record.InLamp = true;
                if (resume) Actor.SetHiddenFlags(Actor.GetHiddenFlags() | HiddenFlags.Model);
                else
                {
                    PlayDeparture(Actor);
                    Actor.FadeOut(true, false);
                    if (!hidden || Actor.HasExitReason(ExitReason.Default)) return false;
                    Actor.SetHiddenFlags(Actor.GetHiddenFlags() | HiddenFlags.Model);
                }
                moodletApplied = Actor.BuffManager.HasElement(WishBuffs.HomeSweetLamp) ||
                    Actor.BuffManager.AddElementIgnoreDelay(WishBuffs.HomeSweetLamp, 0, -1f, Origin.None);
                resting = true;
                JournalBootstrap.RefreshMeter();
                while (resting && Actor.IsSelectable && !Actor.HasBeenDestroyed && Actor.InWorld && !Actor.HasExitReason(ExitReason.Default))
                    Simulator.Sleep(30);
                return false;
            }
            finally
            {
                bool emerge = hidden && Actor != null && Actor.HasExitReason(ExitReason.UserCanceled);
                Sim actor = Actor;
                try { Release(emerge); }
                finally { StandardExit(false, false); }
                if (emerge) JournalBootstrap.RunLater(delegate { PlayEmergence(actor); });
            }
        }

        public override void Cleanup()
        {
            try { Release(false); }
            finally { base.Cleanup(); }
        }

        private void Release(bool smoke)
        {
            bool wasResting = resting;
            resting = false;
            try
            {
                foreach (MotiveDelta delta in recovery) RemoveMotiveDelta(delta);
                recovery.Clear();
                if (moodletApplied)
                {
                    moodletApplied = false;
                    if (Actor != null && Actor.BuffManager != null) Actor.BuffManager.RemoveElement(WishBuffs.HomeSweetLamp);
                }
            }
            finally
            {
                try
                {
                    if (hidden)
                    {
                        hidden = false;
                        if (Actor != null && !Actor.HasBeenDestroyed)
                        {
                            WishRecord record = WishState.Read(Actor.SimDescription, false);
                            if (record != null) record.InLamp = false;
                            Show(Actor, smoke);
                        }
                    }
                }
                finally
                {
                    Active.Remove(this);
                    if (smoke && wasResting) JournalBootstrap.RefreshMeter();
                }
            }
        }

        private static void Show(Sim sim, bool fade)
        {
            if (sim == null || sim.HasBeenDestroyed) return;
            sim.SetHiddenFlags(sim.GetHiddenFlags() & ~HiddenFlags.Model);
            sim.FadeIn(false, fade ? GameObject.kGlobalObjectFadeTime : 0f);
            if (sim.BuffManager != null) sim.BuffManager.RemoveElement(WishBuffs.HomeSweetLamp);
        }

        private static bool ModelHidden(Sim sim)
        {
            return (sim.GetHiddenFlags() & HiddenFlags.Model) != 0;
        }

        private static void PlayDeparture(Sim sim)
        {
            VisualEffect effect = VisualEffect.Create("ep6GenieTargetSimDisappear_main");
            if (effect == null) return;
            effect.ParentTo(sim, Sim.FXJoints.Spine0);
            effect.SubmitOneShotEffect(VisualEffect.TransitionType.SoftTransition);
        }

        private static void PlayEmergence(Sim sim)
        {
            if (sim == null || sim.HasBeenDestroyed || !sim.InWorld || ModelHidden(sim)) return;
            VisualEffect effect = VisualEffect.Create("ep6GenieAppearSmoke_main");
            if (effect == null) return;
            effect.SetPosAndOrient(sim.Position, Vector3.UnitX, Vector3.UnitZ);
            effect.SubmitOneShotEffect(VisualEffect.TransitionType.SoftTransition);
        }

        private static bool CanRest(Sim actor, Sim target)
        {
            return actor != null && actor == target && actor.IsSelectable && !actor.HasBeenDestroyed && actor.InWorld &&
                actor.Motives != null && actor.BuffManager != null && !actor.IsSleeping &&
                GenieAbilities.IsGenie(actor) && WishLamp.Lamp(actor) == null && Unlocked(actor, MagicCasters.ReadRules(actor));
        }

        public sealed class Definition : InteractionDefinition<Sim, Sim, LampRetreat>
        {
            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            { return JournalText.Get("Arcana.Ability.genie_lamp.Name"); }

            public override string[] GetPath(bool female)
            { return new[] { GenieMenu.Root(female) }; }

            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                if (autonomous || !CanRest(actor, target)) return false;
                WishRecord record = WishState.Read(actor.SimDescription, false);
                return !ModelHidden(actor) || record != null && record.InLamp;
            }
        }
    }
}
