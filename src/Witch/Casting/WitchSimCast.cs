using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.Gameplay.Skills;
using Sims3.Gameplay.Utilities;
using Sims3.Gameplay;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    public sealed class WitchSimCast : MagicWand.CastSpell
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo NativeJig = typeof(MagicWand.CastSpell).GetField("mJig", InstanceFields);
        private static readonly FieldInfo NativeWand = typeof(MagicWand.CastSpell).GetField("mWand", InstanceFields);
        private static readonly FieldInfo NativeSucceeded = typeof(MagicWand.CastSpell).GetField("mSpellCastingSucceeded", InstanceFields);
        private static readonly FieldInfo SpellEffect = typeof(MagicWand.CastSpell).GetField("mSpellEffect", InstanceFields);
        private static readonly FieldInfo HitEffect = typeof(MagicWand.CastSpell).GetField("mHitEffect", InstanceFields);
        private static readonly FieldInfo NativeEpic = typeof(MagicWand.CastSpell).GetField("mSpellCastingEpiclyFailed", InstanceFields);
        private static readonly Type TargetInteractionType = typeof(MagicWand).GetNestedType("BeTargetOfSpell", BindingFlags.NonPublic);
        private static readonly PropertyInfo TargetCaster = TargetInteractionType.GetProperty("Caster");
        private static readonly PropertyInfo TargetJig = TargetInteractionType.GetProperty("Jig");
        private static readonly FieldInfo TargetSingleton = TargetInteractionType.GetField("Singleton");
        private static readonly FieldInfo CloseRange = typeof(MagicWand.CastSpell).GetField("kCloseEnoughToPlaceJig", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo StepAwayRange = typeof(MagicWand.CastSpell).GetField("kDistanceToForceRouteAway", BindingFlags.Static | BindingFlags.NonPublic);

        [Persistable(false)]
        private MagicWand.CastSpell native;
        [Persistable(false)]
        private TargetSpell spell;
        [Persistable(false)]
        private CastingRules rules;
        private float emotions;
        private float spent;
        private bool fizzlePenalty;
        private MagicHands temporaryHands;

        internal MagicWand ActiveWand { get { return mWand; } }

        protected override MagicWand.SpellType TypeOfSpell
        {
            get { return ((TargetSpell.Definition)InteractionDefinition).Spell.Id; }
        }
        protected override string JazzStateName { get { return spell.JazzState; } }
        protected override string SuccessVfxName { get { return (string)Invoke(spell.SuccessVfx, native, null); } }
        protected override string EpicFailVfxName { get { return (string)Invoke(spell.EpicVfx, native, null); } }
        protected override string HitVfxName { get { return (string)Invoke(spell.HitVfx, native, null); } }

        protected override bool Run()
        {
            TargetSpell.Definition definition = (TargetSpell.Definition)InteractionDefinition;
            spell = definition.Spell;
            if (!SpellKnowledge.Knows(Actor, SpellKnowledgeGates.EntryFor(spell.Stock))) return false;
            native = (MagicWand.CastSpell)spell.Stock.CreateInstance(Target, Actor, GetPriority(), Autonomous, CancellableByPlayer);

            long start = SimClock.CurrentTicks;
            CasterKind kind;
            if (!MagicCasters.TryKind(Actor, out kind) || Actor.SkillManager == null) return false;
            SkillNames skillName = MagicCasters.SkillFor(kind);
            Actor.SkillManager.AddElement(skillName);
            Skill skill = Actor.SkillManager.GetElement(skillName);
            MagicCasters.EnsureHands(Actor);
            mWand = WitchCastingContext.EquippedWand(Actor);
            if (skill == null)
            {
                return false;
            }
            if (mWand == null && MagicCasters.NeedsHands(Actor))
            {
                var hands = GlobalFunctions.CreateObjectOutOfWorld("magicHandsLHR", ProductVersion.EP7);
                temporaryHands = hands as MagicHands;
                if (temporaryHands == null && hands != null)
                {
                    hands.Destroy();
                }
                if (temporaryHands != null) MagicCasters.MakeWorthless(temporaryHands);
                mWand = temporaryHands;
            }
            if (mWand == null)
            {
                return false;
            }

            mWand.PrepareForUse(Actor);
            if (!RouteForCast(start))
            {
                return false;
            }
            if (!ResolveCast())
            {
                return false;
            }

            StandardEntry();
            bool updating = false;
            bool completed = false;
            try
            {
                BeginCommodityUpdates();
                updating = true;
                AnimateCast(skill);
                DrainMotives();
                if (Actor.Motives.GetValue(CommodityKind.Hunger) <= Actor.Motives.GetMin(CommodityKind.Hunger))
                {
                    EventTracker.SendEvent(EventTypeId.kDiedFromCastingSpell, Actor);
                }
                if (mSpellCastingSucceeded)
                {
                    AnimateSim("Success");
                    OnSpellSuccess();
                    if (fizzlePenalty) WitchMagicRecovery.Fizzle(Actor, rules);
                    completed = true;
                }
                else
                {
                    if (fizzlePenalty) WitchMagicRecovery.Fizzle(Actor, rules);
                    AnimateSim(mSpellCastingEpiclyFailed ? "Epic" : "Fail");
                    if (mSpellCastingEpiclyFailed)
                    {
                        OnSpellEpicFailure();
                    }
                }
                EventTracker.SendEvent(EventTypeId.kCastSpell, Actor);
                return completed;
            }
            finally
            {
                try
                {
                    if (updating)
                    {
                        EndCommodityUpdates(completed);
                    }
                }
                finally
                {
                    StandardExit();
                }
            }
        }

        public override void Cleanup()
        {
            try
            {
                base.Cleanup();
            }
            finally
            {
                if (temporaryHands != null)
                {
                    if (Actor != null && Actor.Inventory != null) Actor.Inventory.TryToRemove(temporaryHands);
                    temporaryHands.Destroy();
                    temporaryHands = null;
                }
            }
        }

        private bool RouteForCast(long start)
        {
            bool solo = Actor == Target;
            InteractionInstance follower = null;
            float close = (float)CloseRange.GetValue(null);
            if (!solo)
            {
                if (Actor.HasExitReason())
                {
                    return false;
                }
                InteractionDefinition targetDefinition = (InteractionDefinition)TargetSingleton.GetValue(null);
                follower = targetDefinition.CreateInstance(Actor, Target, GetPriority(), false, false);
                follower.LinkedInteractionInstance = this;
                TargetCaster.SetValue(follower, Actor, null);
                if (!Target.InteractionQueue.AddNext(follower))
                {
                    return false;
                }
                if (Actor.GetDistanceToObjectSquared(Target) > close * close && !Actor.RouteToPointRadius(Target.Position, close))
                {
                    return false;
                }
            }

            SocialJig jig = GlobalFunctions.CreateObjectOutOfWorld(solo ? "practiceSpell_jig" : "castSpell_jig", ProductVersion.EP7) as SocialJig;
            if (jig == null)
            {
                return false;
            }
            NativeJig.SetValue(this, jig);
            jig.RegisterParticipants(Actor, solo ? null : Target);
            jig.SetOpacity(0f, 0f);
            Vector3 position = Actor.Position;
            Vector3 forward = Actor.ForwardVector;
            if (!GlobalFunctions.FindGoodLocationNearby(jig, ref position, ref forward))
            {
                return false;
            }
            jig.SetPosition(position);
            jig.SetForward(forward);
            jig.AddToWorld();

            if (follower != null)
            {
                TargetJig.SetValue(follower, jig, null);
                float stepAway = (float)StepAwayRange.GetValue(null);
                while (!jig.RouteComplete && !Actor.HasExitReason(ExitReason.Default))
                {
                    if (SimClock.ConvertFromTicks(SimClock.CurrentTicks - start, TimeUnit.Minutes) > 15f &&
                        Actor.GetDistanceToObject(jig) < stepAway)
                    {
                        Actor.RouteToObjectRadialRange(jig, stepAway, stepAway);
                    }
                    Simulator.Sleep(0);
                }
            }
            return Actor.DoRoute(jig.RouteToJigA(Actor));
        }

        private bool ResolveCast()
        {
            rules = MagicCasters.ReadRules(Actor);
            float needPenalty;
            int empty;
            WitchCastingContext.CountNeeds(Actor, out needPenalty, out empty, null);
            emotions = WitchCastingContext.EmotionPenalty(Actor);
            float accuracy = rules.Accuracy(spell.Category, rules.Innate ? rules.InnateAccuracy : WitchCastingContext.WandBonus(mWand, false),
                emotions, needPenalty, empty);
            mSpellCastingSucceeded = RandomUtil.RandomChance(accuracy);
            fizzlePenalty = MagicCasters.ResolveBacklash(Actor, rules, spell.Category, accuracy, false, ref mSpellCastingSucceeded);
            NativeWand.SetValue(native, mWand);
            NativeSucceeded.SetValue(native, mSpellCastingSucceeded);
            mSpellCastingEpiclyFailed = !mSpellCastingSucceeded &&
                (bool)Invoke(spell.NativeEpicAllowed, native, null) && RandomUtil.RandomChance(mWand.EpicFailChance(Actor));
            NativeEpic.SetValue(native, mSpellCastingEpiclyFailed);
            return (bool)Invoke(spell.NativePreparation, native, null);
        }

        private void AnimateCast(Skill skill)
        {
            EnterStateMachine(Actor == Target ? "SpellcastingSolo" : "SpellcastingSocial", "Enter", "x");
            if (Actor != Target)
            {
                SetActor("y", Target);
            }
            SetParameter("noWand", mWand is MagicHands);
            SetActor("wand", mWand);
            SetParameter("isSkilled", skill.SkillLevel >= MagicWand.kExpertLevel);
            AddOneShotScriptEventHandler(101, ShowSuccessVfx);
            AddOneShotScriptEventHandler(102, ShowFailVfx);
            AddOneShotScriptEventHandler(103, ShowEpicFailVfx);
            AnimateSim(JazzStateName);
        }

        protected override void ShowSuccessVfx(StateMachineClient sender, IEvent evt)
        {
            if (spell.NativeSuccessVisual.DeclaringType == typeof(MagicWand.CastSpell))
            {
                base.ShowSuccessVfx(sender, evt);
                return;
            }

            try
            {
                Invoke(spell.NativeSuccessVisual, native, new object[] { sender, evt });
            }
            finally
            {
                SpellEffect.SetValue(this, SpellEffect.GetValue(native));
                HitEffect.SetValue(this, HitEffect.GetValue(native));
                SpellEffect.SetValue(native, null);
                HitEffect.SetValue(native, null);
            }
        }

        protected override void DrainMotives()
        {
            if (rules.Innate)
            {
                spent = MagicCasters.SpendInnate(Actor, rules, rules.FinalCost(spell.BaseCost, spell.Category, 1f));
                return;
            }
            float before = Actor.Motives.GetValue(CommodityKind.MagicFatigue);
            float logicalCost = rules.FinalCost(spell.BaseCost, spell.Category, 1f);
            float drain = rules.FatigueDrain(logicalCost, Actor.Motives.GetMin(CommodityKind.MagicFatigue),
                Actor.Motives.GetMax(CommodityKind.MagicFatigue));
            mWand.DrainMotive(Actor, CommodityKind.MagicFatigue, -drain);
            spent = Math.Max(0f, Actor.Motives.GetValue(CommodityKind.MagicFatigue) - before);
        }

        protected override void OnSpellSuccess()
        {
            if (GenieBlessings.Resists(Actor, Target, spell.CatalogEntry)) return;
            object shield = GenieBlessings.ShieldSpell(Actor, Target, spell.CatalogEntry);
            BuffInstance prior = rules.Hexweaver && spell.TimedCurse != BuffNames.Undefined
                ? Target.BuffManager.GetElement(spell.TimedCurse) : null;
            float before = prior == null ? 0f : prior.TimeoutCount;
            Invoke(spell.NativeSuccess, native, null);
            if (rules.Hexweaver && spell.TimedCurse != BuffNames.Undefined)
            {
                BuffInstance applied = Target.BuffManager.GetElement(spell.TimedCurse);
                if (applied != null && applied.TimeoutCount > 0f && (applied != prior || applied.TimeoutCount > before))
                {
                    applied.TimeoutCount *= CastingRules.HexweaverDuration;
                }
            }
            GenieBlessings.ShortenSpell(shield, Target);
            if (rules.DarkArts && emotions > 0f && spent > 0f && RandomUtil.RandomChance(CastingRules.DarkArtsRefundChance))
            {
                Actor.Motives.ChangeValue(CommodityKind.MagicFatigue, -spent * rules.RefundFraction(spell.Category));
            }
        }

        protected override void OnSpellEpicFailure()
        {
            if (!WitchBackfires.TryIntercept(Actor, spell, rules))
            {
                Invoke(spell.NativeFailure, native, null);
            }
        }

        internal static object Invoke(MethodInfo method, object instance, object[] args)
        {
            try
            {
                return method.Invoke(instance, args);
            }
            catch (TargetInvocationException error)
            {
                throw error.InnerException ?? error;
            }
        }
    }

    internal static class WitchBackfires
    {
        internal static bool TryIntercept(Sim caster, TargetSpell spell, CastingRules rules)
        {
            BuffInstance template;
            if (spell.Backfire == BuffNames.Undefined || !BuffManager.BuffDictionary.TryGetValue((ulong)spell.Backfire, out template) ||
                !Accepts(caster, template))
            {
                return false;
            }

            if (rules.Familiar && RandomUtil.RandomChance(CastingRules.FamiliarProtection))
            {
                return true;
            }
            if (rules.ScapegoatChance == 0f || caster.LotCurrent == null)
            {
                return false;
            }

            List<Sim> eligible = new List<Sim>();
            foreach (Sim sim in caster.LotCurrent.GetSims())
            {
                if (WitchCastingContext.Nearby(caster, sim) && Accepts(sim, template) &&
                    !sim.BuffManager.HasElement(spell.Backfire))
                {
                    eligible.Add(sim);
                }
            }
            if (eligible.Count == 0 || !RandomUtil.RandomChance(rules.ScapegoatChance))
            {
                return false;
            }

            eligible.Sort(delegate(Sim a, Sim b)
            {
                return caster.GetDistanceToObjectSquared(a).CompareTo(caster.GetDistanceToObjectSquared(b));
            });
            foreach (Sim sim in eligible)
            {
                if (sim.BuffManager.AddElement(spell.Backfire, Origin.FromSpell))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Accepts(Sim sim, BuffInstance template)
        {
            return sim.SimDescription.IsHuman && sim.SimDescription.TeenOrAbove &&
                !sim.SimDescription.IsDead && !sim.SimDescription.IsGhost &&
                sim.BuffManager != null && (sim.SimDescription.GetCASAGSAvailabilityFlags() & template.mBuff.AvailabilityFlags) != 0 &&
                template.mBuff.ShouldAdd(sim.BuffManager, MoodAxis.None, template.EffectValue);
        }
    }
}
