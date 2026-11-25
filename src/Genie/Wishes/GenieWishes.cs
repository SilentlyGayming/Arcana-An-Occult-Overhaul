using System;
using System.Collections.Generic;
using Sims3.Gameplay;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Objects.Rewards;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.SimIFace.Enums;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal static class GenieWishes
    {
        private static readonly MagicSpell WishSpell = new MagicSpell(4, MagicSpellKind.Spell);

        internal static float SuccessChance(Sim caster, CastingRules rules)
        {
            float needPenalty;
            int empty;
            WitchCastingContext.CountNeeds(caster, out needPenalty, out empty, null);
            float accuracy = rules.Accuracy(WishSpell, rules.InnateAccuracy,
                WitchCastingContext.EmotionPenalty(caster), needPenalty, empty);
            return WishRules.PlayerAccuracy(accuracy, rules.GenieLaw);
        }

        internal static float Cost(Sim genie, WishMode mode)
        {
            SimDescription sim = genie.SimDescription;
            return WishRules.Cost(mode, GeniePower.Get(sim), GeniePower.CapacityOf(sim));
        }

        internal static bool Allowed(Sim actor, Sim target, WishMode mode, ref GreyedOutTooltipCallback tooltip)
        {
            if (WishAging.IsReserved(actor, target))
            {
                tooltip = GenieMenu.SilentUnavailable;
                return false;
            }
            if (mode == WishMode.Lamp)
            {
                bool onCooldown;
                if (!WishLamp.Allowed(target, actor, out onCooldown))
                {
                    if (onCooldown && actor != null)
                        tooltip = InteractionInstance.CreateTooltipCallback(Sims3.Gameplay.Utilities.Localization.LocalizeString(actor.IsFemale,
                            "Gameplay/ActorSystems/OccultGenie/MakeWish:MustWaitTillTomorrow", actor));
                    return false;
                }
                return true;
            }
            if (actor == null || target == null || actor.HasBeenDestroyed || actor.SimDescription == null ||
                !GenieAbilities.IsGenie(actor) || WishLamp.Lamp(actor) != null || !WishEffects.CanReceive(target)) return false;
            CastingRules rules = MagicCasters.ReadRules(actor);
            if (rules.Rank < Rank.HighWitch) return false;
            switch (mode)
            {
                case WishMode.Own:
                    if (!rules.Counterattack || actor != target) return false;
                    break;
                case WishMode.Other:
                    if (actor == target || !rules.GenieLaw) return false;
                    break;
                default:
                    return false;
            }
            GenieMagicSkill skill = GeniePower.Read(actor.SimDescription, false);
            if (CanPay(mode, rules, skill)) return true;
            tooltip = GenieMenu.SilentUnavailable;
            return false;
        }

        internal static bool CanPay(WishMode mode, CastingRules rules, GenieMagicSkill skill)
        {
            if (skill == null) return false;
            return ResolveMode(mode, rules, skill) == WishMode.Guaranteed ||
                WishRules.OrdinaryAvailable(skill.Power, skill.Capacity, skill.IsExhausted);
        }

        private static WishMode ResolveMode(WishMode mode, CastingRules rules, GenieMagicSkill skill)
        { return WishRules.ResolveMode(mode, skill.Power, rules.LawOfTheLamp, skill.GuaranteeReady, SimClock.CurrentTicks); }

        internal static bool Grant(Sim actor, Sim target, WishKind kind, WishMode mode, WishSelection selection,
            GenieLamp reservedLamp, WishCommitSignal markCommitted)
        {
            GreyedOutTooltipCallback tooltip = null;
            if (!Allowed(actor, target, mode, ref tooltip)) return false;
            Sim genie = mode == WishMode.Lamp ? target : actor;
            Sim wisher = mode == WishMode.Lamp ? actor : target;
            if (mode == WishMode.Lamp && (reservedLamp == null || reservedLamp != WishLamp.Lamp(genie))) return false;
            if (!WishEffects.Eligible(genie, wisher, kind, mode) || !WishEffects.ValidateSelection(wisher, kind, selection)) return false;

            GenieMagicSkill skill = mode == WishMode.Lamp ? null : GeniePower.Read(genie.SimDescription, false);
            CastingRules rules = mode == WishMode.Lamp ? default(CastingRules) : MagicCasters.ReadRules(genie);
            if (skill != null) mode = ResolveMode(mode, rules, skill);
            bool succeeded = WishRules.RequiredSuccess(mode);
            if (mode == WishMode.Other)
            {
                succeeded = WishRules.OrdinarySucceeds(SuccessChance(genie, rules), RandomUtil.GetFloat(100f));
            }
            else if (mode == WishMode.Lamp)
            {
                Relationship relationship = Relationship.Get(wisher, genie, false);
                float liking = relationship == null || relationship.LTR == null ? 0f : relationship.LTR.Liking;
                int mood = wisher.MoodManager == null ? 0 : wisher.MoodManager.MoodValue;
                succeeded = RandomUtil.RandomChance(WishRules.NpcAccuracy(liking, mood));
            }

            bool forced;
            if (ArcanaPresentationCheats.TryWish(actor, kind, mode, out forced)) succeeded = forced;
            SimDescription selectedPerson = selection.Lover != null ? selection.Lover.SimDescription
                : selection.Grave == null ? null : selection.Grave.DeadSimsDescription;
            string person = selectedPerson == null ? string.Empty : selectedPerson.FirstName;
            int before = wisher.Household == null ? 0 : wisher.Household.FamilyFunds;
            bool scandal = kind == WishKind.Fame && wisher.CelebrityManager.Level > 0;
            if (succeeded && kind == WishKind.Youth)
            {
                return WishAging.QueueYouth(actor, genie, wisher, mode, reservedLamp);
            }
            WishCommit commit = new WishCommit(delegate
            {
                markCommitted();
                Pay(actor, genie, wisher, kind, mode, reservedLamp, skill, succeeded);
            });
            try
            {
                if (!WishEffects.Apply(genie, wisher, kind, succeeded, selection, commit) && !commit.Applied)
                    SpellKnowledge.Notify(actor, "Arcana.Genie.Wishes.Unavailable");
                return commit.Applied;
            }
            finally
            {
                if (commit.Applied)
                    AfterCommit(actor, genie, wisher, kind, mode, skill, succeeded, scandal, person,
                        kind == WishKind.MoreWishes && reservedLamp != null ? reservedLamp.mRemainingWishes :
                        wisher.Household == null ? 0 : wisher.Household.FamilyFunds - before);
            }
        }

        internal static void Pay(Sim actor, Sim genie, Sim wisher, WishKind kind, WishMode mode, GenieLamp lamp,
            GenieMagicSkill skill, bool succeeded)
        {
            ArcanaPresentationCheats.ConsumeWish(actor, kind, mode);
            if (mode == WishMode.Lamp)
            {
                WishLamp.Commit(lamp, genie, wisher, kind, succeeded);
                return;
            }
            skill.Power = 0f;
            if (mode == WishMode.Guaranteed) skill.GuaranteeReady = WishState.After(WishRules.GuaranteeHours);
            else skill.ExhaustEnds = WishState.After(CastingRules.ExhaustionDays * 24f);
        }

        internal static void AfterCommit(Sim actor, Sim genie, Sim wisher, WishKind kind, WishMode mode,
            GenieMagicSkill skill, bool succeeded, bool scandal, string person, int amount)
        {
            try
            {
                if (skill != null)
                {
                    WishBuffs.SyncExhaustion(genie, skill);
                    JournalBootstrap.RefreshMeter();
                }
                if (mode != WishMode.Lamp) JournalSounds.Play("sting_make_wish");
                WishFeedback.Result(actor, wisher, kind, succeeded, scandal, person, amount);
            }
            finally
            {
                if (mode == WishMode.Own) WishFate.Apply(genie, ArcanaPresentationCheats.TakeFate(genie, RandomUtil.GetFloat(100f)));
            }
        }

        internal static void GiveMoodlet(Sim sim, BuffNames buff, float hours)
        {
            if (sim == null || sim.BuffManager == null) return;
            BuffInstance applied = sim.BuffManager.GetElement(buff);
            if (applied == null && LuckMoodlet.Covers(buff))
            {
                LuckMoodlet.Add(sim, buff, hours * 60f, Origin.FromGenie);
                return;
            }
            sim.BuffManager.AddElement(buff, Origin.FromGenie);
            applied = sim.BuffManager.GetElement(buff);
            if (applied != null) applied.TimeoutCount = hours * 60f;
        }

    }

    [Persistable]
    public sealed class GrantWish : Interaction<Sim, Sim>
    {
        public static readonly InteractionDefinition[] Singletons =
        {
            new Definition(WishMode.Other), new Definition(WishMode.Own)
        };
        public static readonly InteractionDefinition LampSingleton = new Definition(WishMode.Lamp);
        private bool committed;

        protected override bool Run()
        {
            Definition definition = InteractionDefinition as Definition;
            if (definition == null || !definition.UsePicker || !WishRules.IsMenuMode(definition.Mode) || committed) return false;
            GreyedOutTooltipCallback tooltip = null;
            if (!GenieWishes.Allowed(Actor, Target, definition.Mode, ref tooltip)) return false;
            Sim genie = definition.Mode == WishMode.Lamp ? Target : Actor;
            Sim wisher = definition.Mode == WishMode.Lamp ? Actor : Target;
            using (WishReservation reservation = WishReservation.TryAcquire(genie.SimDescription.SimDescriptionId, wisher.SimDescription.SimDescriptionId))
            {
                if (reservation == null) return false;
                return RunReserved(definition.Mode, genie, wisher);
            }
        }

        private bool RunReserved(WishMode mode, Sim genie, Sim wisher)
        {
            GenieLamp lamp = mode == WishMode.Lamp ? WishLamp.Lamp(genie) : null;
            GenieWishChoice choice = GenieWishDialog.Show(genie, wisher, mode);
            if (choice == null || choice.IsDisabled()) return false;
            WishKind kind = choice.Kind;
            if (!WishEffects.Eligible(genie, wisher, kind, mode)) return false;
            WishSelection selection = WishEffects.Select(wisher, kind);
            if (selection == null || Actor.HasExitReason(ExitReason.Default)) return false;
            bool entry = false;
            bool synced = false;
            bool updates = false;
            WishParticipant participant = null;
            Sims3.Gameplay.Gameflow.Singleton.DisableSave(this, "Gameplay/ActorSystems/OccultGenie/MakeWish:InteractionName");
            try
            {
                if (Actor != Target)
                {
                    participant = WishParticipant.Singleton.CreateInstance(Actor, Target, GetPriority(), Autonomous, CancellableByPlayer) as WishParticipant;
                    if (participant == null) return false;
                    participant.ApproachGenie = mode != WishMode.Lamp;
                    LinkedInteractionInstance = participant;
                    if (!Target.InteractionQueue.AddNext(participant)) return false;
                    if (!StartSync(true, false, null, 0f, false)) return false;
                    synced = true;
                    Actor.LoopIdle();
                    if (mode == WishMode.Lamp && !WishRendezvous.Approach(Actor, Target)) return false;
                    Actor.SynchronizationLevel = Sim.SyncLevel.Routed;
                    if (!Actor.WaitForSynchronizationLevelWithSim(Target, Sim.SyncLevel.Committed, 60f)) return false;
                    Actor.SynchronizationLevel = Sim.SyncLevel.Committed;
                }
                if (!WishEffects.Eligible(genie, wisher, kind, mode) || !WishEffects.ValidateSelection(wisher, kind, selection)) return false;
                StandardEntry(); entry = true;
                BeginCommodityUpdates(); updates = true;
                if (Actor != Target && !Actor.RouteTurnToFace(Target.Position)) return false;
                AnimateWish(mode, kind, genie, wisher);
                if (Actor.HasExitReason(ExitReason.Default) || wisher.HasExitReason(ExitReason.Default)) return false;
                return GenieWishes.Grant(Actor, Target, kind, mode, selection, lamp, delegate { committed = true; });
            }
            catch (ResetException) { throw; }
            catch (Exception error)
            {
                if (!WishRules.Concealed(kind)) JournalBootstrap.Report("Granting Genie wish", error);
                if (!committed) SpellKnowledge.Notify(Actor, "Arcana.Genie.Wishes.Unavailable");
                return committed;
            }
            finally
            {
                try
                {
                    if (participant != null)
                    {
                        if (synced)
                        {
                            FinishLinkedInteraction(true);
                            if (Simulator.CheckYieldingContext(false)) WaitForSyncComplete(10f);
                        }
                        else if (Target != null && Target.InteractionQueue != null)
                            Target.InteractionQueue.CancelInteraction(participant, false);
                    }
                }
                finally
                {
                    try
                    {
                        if (synced) Actor.ClearSynchronizationData();
                        if (updates) EndCommodityUpdates(committed);
                        if (entry) StandardExit();
                    }
                    finally
                    {
                        Sims3.Gameplay.Gameflow.Singleton.EnableSave(this);
                    }
                }
            }
        }

        private void AnimateWish(WishMode mode, WishKind kind, Sim genie, Sim wisher)
        {
            GenieWishTypes native;
            if (mode == WishMode.Lamp && GenieWishDialog.TryNativeAnimation(kind, wisher.HasTrait(TraitNames.Evil), out native))
            {
                EnterStateMachine("MakeWish", "Enter", "x", "y");
                SetParameter("WishType", native);
                AnimateSim("Exit");
                return;
            }
            genie.PlaySoloAnimation("a_genie_clap_x", true, ProductVersion.EP6);
            if (Actor.HasExitReason(ExitReason.Default)) return;
            VisualEffect effect = VisualEffect.Create("ep6GenieAppearSmoke_main");
            if (effect == null) return;
            effect.SetPosAndOrient(wisher.Position, Vector3.UnitX, Vector3.UnitZ);
            effect.SubmitOneShotEffect(VisualEffect.TransitionType.SoftTransition);
        }

        public override void Cleanup()
        {
            Sims3.Gameplay.Gameflow.Singleton.EnableSave(this);
            base.Cleanup();
        }

        [Persistable, DoesntRequireTuning]
        public sealed class Definition : InteractionDefinition<Sim, Sim, GrantWish>
        {
            internal WishKind Kind = WishKind.Fortune;
            internal WishMode Mode;
            internal bool UsePicker;
            public Definition() { }
            internal Definition(WishMode mode) { Mode = mode; UsePicker = true; }

            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            {
                if (Mode == WishMode.Lamp)
                {
                    GenieLamp lamp = WishLamp.Lamp(target);
                    return lamp == null ? JournalText.Get("Arcana.Genie.Wish.Dialog.MakeWish") : lamp.GetMakeWishInteractionName();
                }
                if (Mode == WishMode.Own) return JournalText.Get("Arcana.Genie.Wish.Menu.Own");
                return JournalText.Get("Arcana.Genie.Wish.Menu.Other");
            }

            public override string[] GetPath(bool female)
            {
                if (Mode == WishMode.Lamp) return new string[0];
                return new[] { GenieMenu.Root(female) };
            }

            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                if (autonomous || !UsePicker || !WishRules.IsMenuMode(Mode) || !GenieWishes.Allowed(actor, target, Mode, ref tooltip)) return false;
                Sim genie = Mode == WishMode.Lamp ? target : actor;
                Sim wisher = Mode == WishMode.Lamp ? actor : target;
                return GenieWishDialog.HasAvailable(genie, wisher, Mode);
            }
        }
    }
}
