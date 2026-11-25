using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Careers;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Interfaces;
using Sims3.Gameplay.Objects;
using Sims3.Gameplay.Objects.CookingObjects;
using Sims3.Gameplay.Objects.Electronics;
using Sims3.Gameplay.Objects.HobbiesSkills;
using Sims3.Gameplay.Services;
using Sims3.Gameplay.Skills;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI.Controller;

namespace SupernaturalExpanded.Journal
{
    public static class FightBlessing
    {
        private const string NativeName = "DecideWinnerOfFight";
        private static readonly FieldInfo Callback =
            typeof(SocialRuleRHS).GetField("mProceduralEffectBeforeUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Rules = typeof(SocialRuleRHS).GetField("sDictionary", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo Native = typeof(SocialCallback).GetMethod(NativeName, BindingFlags.Static | BindingFlags.Public);
        private static readonly MethodInfo Blessed = typeof(FightBlessing).GetMethod("DecideWinner", BindingFlags.Static | BindingFlags.Public);
        [Persistable(false)] private static readonly List<SocialRuleRHS> Swapped = new List<SocialRuleRHS>();

        // Points the fight socials' winner callback at DecideWinner, which runs the game's own roll first.
        internal static void Install()
        {
            Remove();
            if (Callback == null || Rules == null || Native == null || Blessed == null)
                throw new InvalidOperationException("Missing native fight callback API.");
            IDictionary all = Rules.GetValue(null) as IDictionary;
            if (all == null) return;
            foreach (object list in all.Values)
            {
                IEnumerable rules = list as IEnumerable;
                if (rules == null) continue;
                foreach (object item in rules)
                {
                    SocialRuleRHS rule = item as SocialRuleRHS;
                    MethodInfo current = rule == null ? null : Callback.GetValue(rule) as MethodInfo;
                    if (current == null || current.Name != NativeName || current.DeclaringType != typeof(SocialCallback)) continue;
                    Callback.SetValue(rule, Blessed);
                    Swapped.Add(rule);
                }
            }
        }

        internal static void Remove()
        {
            foreach (SocialRuleRHS rule in Swapped)
                if (Callback.GetValue(rule) == (object)Blessed) Callback.SetValue(rule, Native);
            Swapped.Clear();
        }

        public static void DecideWinner(Sim actor, Sim target, string interaction, ActiveTopic topic, InteractionInstance i)
        {
            SocialCallback.DecideWinnerOfFight(actor, target, interaction, topic, i);
            try { Favor(actor, target, i as SocialInteractionA); }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Applying the Blessing of Vitality to a fight", error); }
        }

        // Lifts the blessed Sim's native odds by the bonus, up to the cap, by re-rolling only a lost fight.
        private static void Favor(Sim actor, Sim target, SocialInteractionA fight)
        {
            if (fight == null || actor == null || target == null) return;
            bool actorBlessed = GenieBlessings.Has(actor, Blessing.Vitality);
            if (actorBlessed == GenieBlessings.Has(target, Blessing.Vitality)) return;
            float chance = ActorChance(actor, target);
            if (float.IsNaN(chance) || fight.ActorWinsFight == actorBlessed) return;
            float odds = actorBlessed ? chance : 1f - chance;
            float raised = Math.Min(odds + BlessingRules.FightBonus, BlessingRules.FightCap);
            if (raised <= odds) return;
            if (RandomUtil.RandomChance01((raised - odds) / (1f - odds))) fight.ActorWinsFight = actorBlessed;
        }

        // Mirrors SocialCallback.DecideWinnerOfFightGeneral; NaN marks the fights the game decides without a roll.
        private static float ActorChance(Sim actor, Sim target)
        {
            if (actor.SimDescription.IsBonehilda || target.SimDescription.IsBonehilda) return float.NaN;
            if ((Level(actor, SkillNames.MartialArts) > 0) != (Level(target, SkillNames.MartialArts) > 0)) return float.NaN;
            if (actor.HasTrait(TraitNames.Loser) != target.HasTrait(TraitNames.Loser)) return float.NaN;
            if (BodyBuilder(actor) != BodyBuilder(target)) return float.NaN;
            float chance = 0.5f + (Math.Max(0, Level(actor, SkillNames.Athletic)) - Math.Max(0, Level(target, SkillNames.Athletic)))
                * Conversation.AthleticSkillFightModifier;
            if (Tough(actor)) chance += Conversation.TraitSimFightModifier;
            if (Tough(target)) chance -= Conversation.TraitSimFightModifier;
            if (actor.HasTrait(TraitNames.CanApprehendBurglar)) chance += Police.PoliceFightModifier;
            return Math.Max(0f, Math.Min(1f, chance));
        }

        private static int Level(Sim sim, SkillNames skill)
        {
            return sim.SkillManager == null ? 0 : sim.SkillManager.GetSkillLevel(skill);
        }

        private static bool BodyBuilder(Sim sim)
        {
            Athletic athletic = sim.SkillManager == null ? null : sim.SkillManager.GetSkill<Athletic>(SkillNames.Athletic);
            return athletic != null && athletic.IsBodyBuilder();
        }

        private static bool Tough(Sim sim)
        {
            return sim.HasTrait(TraitNames.Brave) || sim.BuffManager.HasElement(BuffNames.OddlyPowerful) ||
                sim.HasTrait(TraitNames.Evil) || sim.HasTrait(TraitNames.MeanSpirited);
        }
    }

    internal static class CreativeWork
    {
        private static readonly FieldInfo PaintTime = typeof(Easel).GetField("mPaintTimeSpent", BindingFlags.Instance | BindingFlags.NonPublic);

        // Finds the painting or novel the Sim is actively working on and how far along it is.
        internal static bool Read(Sim sim, out object work, out float progress)
        {
            work = null;
            progress = float.NaN;
            InteractionInstance current = sim.CurrentInteraction;
            if (current is Easel.Paint)
            {
                Easel easel = current.Target as Easel;
                if (easel == null || PaintTime == null) return false;
                work = easel;
                progress = (float)PaintTime.GetValue(easel);
                return true;
            }
            if (!(current is Computer.WriteNovel)) return false;
            work = Novel(sim);
            if (work == null) return false;
            progress = ((WrittenBookData)work).PercentComplete;
            return true;
        }

        internal static WrittenBookData Novel(Sim sim)
        {
            Writing writing = sim.SkillManager == null ? null : sim.SkillManager.GetSkill<Writing>(SkillNames.Writing);
            return writing == null ? null : writing.CurrentWriting;
        }

        internal static void Write(object work, float progress)
        {
            Easel easel = work as Easel;
            if (easel != null)
            {
                PaintTime.SetValue(easel, progress);
                return;
            }
            WrittenBookData book = work as WrittenBookData;
            if (book != null) book.PercentComplete = Math.Min(progress, 100f);
        }
    }

    internal static class PaintingBlessing
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string RandomArt = "Sims3.Gameplay.Objects.HobbiesSkills.Easel+Paint+Definition";
        private static readonly FieldInfo PictureKey = typeof(ImageObject).GetField("mPictureKey", Hidden);
        private static readonly FieldInfo Quality = typeof(ImageObject).GetField("mQuality", Hidden);
        private static readonly FieldInfo Stage = typeof(ImageObject).GetField("mPaintingStage", Hidden);
        private static readonly MethodInfo Redraw = typeof(ImageObject).GetMethod("UpdatePaintingStage", Hidden);

        private enum Artwork
        {
            Unknown,
            Random,
            Chosen
        }

        // Gives a blessed painter's new canvas a chance at a second native quality roll and keeps the better result.
        internal static void Improve(Sim sim, EaselCanvas canvas)
        {
            if (PictureKey == null || Quality == null || Stage == null || Redraw == null)
                throw new InvalidOperationException("Missing native painting API.");
            if (sim == null || sim.SimDescription == null || canvas.PainterSimId != sim.SimDescription.SimDescriptionId) return;
            Artwork artwork = Kind(sim);
            if (artwork == Artwork.Unknown || (uint)Stage.GetValue(canvas) != 0u) return;
            if (!RandomUtil.RandomChance(BlessingRules.CreativeQualityChance)) return;
            PaintingSkill.PaintingQuality current = (PaintingSkill.PaintingQuality)Quality.GetValue(canvas);
            PaintingSkill.PaintingQuality reroll = PaintingSkill.PickPaintingQuality(sim, SkillNames.Painting, (OccupationNames)0);
            if (reroll <= current) return;
            if (artwork == Artwork.Random)
            {
                ResourceKey key;
                PaintingSkill.PaintingQuality picked;
                int level = reroll == PaintingSkill.PaintingQuality.Masterpiece ? 11 : 10;
                if (!PaintingSkill.PickPainting(sim, SkillNames.Painting, (OccupationNames)0, canvas.Size, out key, out picked, level) || picked != reroll)
                    return;
                PictureKey.SetValue(canvas, key);
                World.SetIsPaintingTextureInUse(key, true, false);
            }
            Quality.SetValue(canvas, reroll);
            PaintingSkill skill = sim.SkillManager == null ? null : sim.SkillManager.GetSkill<PaintingSkill>(SkillNames.Painting);
            canvas.CalculateInitialValue((skill == null ? 0 : skill.GetNumPaintingsPaintedPerSize(canvas.Size)) + 1);
            Redraw.Invoke(canvas, null);
        }

        // Random art comes only from the plain Paint menu; still lifes, memories and saved images keep their chosen picture.
        private static Artwork Kind(Sim sim)
        {
            Easel.Paint paint = sim.CurrentInteraction as Easel.Paint;
            if (paint == null || paint.InteractionDefinition == null) return Artwork.Unknown;
            object definition = paint.InteractionDefinition;
            if (definition.GetType().FullName != RandomArt) return Artwork.Chosen;
            FieldInfo level = definition.GetType().GetField("Level", BindingFlags.Instance | BindingFlags.Public);
            return level != null && (int)level.GetValue(definition) == -1 ? Artwork.Random : Artwork.Unknown;
        }
    }

    internal static class MealBlessing
    {
        // Marks food the blessed Sim is preparing with the game's own Born to Cook bonus, which never stacks with the trait.
        internal static void Season(Sim sim)
        {
            Lot lot = sim.LotCurrent;
            if (lot == null || sim.SimDescription == null) return;
            ulong cook = sim.SimDescription.SimDescriptionId;
            foreach (IPartOfCookingProcess part in lot.GetObjects<IPartOfCookingProcess>())
            {
                CookingProcess process = part == null ? null : part.CookingProcess;
                if (process != null && !process.BornToCookEnhanced && process.PreparerId == cook) process.BornToCookEnhanced = true;
            }
        }
    }

    public static class HarmonyBlessing
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo Callback = typeof(SocialRuleRHS).GetField("mProceduralEffectAfterUpdate", Hidden);
        private static readonly FieldInfo Rules = typeof(SocialRuleRHS).GetField("sDictionary", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo Highest = typeof(LongTermRelationship).GetField("mHighestLiking", Hidden);
        private static readonly MethodInfo Haunting = typeof(LongTermRelationship).GetMethod("UpdateHauntedSimGhostRelationship", Hidden);
        private static readonly MethodInfo Blessed = typeof(HarmonyBlessing).GetMethod("Settle", BindingFlags.Static | BindingFlags.Public);
        private static readonly long Stale = SimClock.ConvertToTicks(1f, TimeUnit.Hours);
        [Persistable(false)] private static readonly Dictionary<SocialRuleRHS, MethodInfo> Originals = new Dictionary<SocialRuleRHS, MethodInfo>();
        [Persistable(false)] private static readonly Dictionary<LongTermRelationship, Baseline> Baselines = new Dictionary<LongTermRelationship, Baseline>();
        [Persistable(false)] private static EventListener socials;
        [Persistable(false)] private static bool installed;

        private sealed class Baseline
        {
            internal float Liking;
            internal LongTermRelationshipTypes State;
            internal long Stamp;
        }

        // Hooks the social rules only while a Sim in the world holds the Blessing of Harmony.
        internal static void Sync(Sim[] sims)
        {
            bool wanted = false;
            foreach (Sim sim in sims)
            {
                if (sim != null && !sim.HasBeenDestroyed && GenieBlessings.Has(sim, Blessing.Harmony))
                {
                    wanted = true;
                    break;
                }
            }
            if (wanted && !installed) Install();
            else if (!wanted && installed) Remove();
        }

        // Routes each social's after-update callback through Settle, which corrects the liking change and then runs the original.
        private static void Install()
        {
            if (Callback == null || Rules == null || Highest == null || Haunting == null || Blessed == null)
                throw new InvalidOperationException("Missing native relationship API.");
            installed = true;
            IDictionary all = Rules.GetValue(null) as IDictionary;
            if (all == null) return;
            foreach (object list in all.Values)
            {
                IEnumerable rules = list as IEnumerable;
                if (rules == null) continue;
                foreach (object item in rules)
                {
                    SocialRuleRHS rule = item as SocialRuleRHS;
                    if (rule == null || Originals.ContainsKey(rule)) continue;
                    Originals[rule] = Callback.GetValue(rule) as MethodInfo;
                    Callback.SetValue(rule, Blessed);
                }
            }
            socials = EventTracker.AddListener(EventTypeId.kSocialInteraction, OnSocial);
        }

        // Restores every callback still pointing at Settle; a rule another mod has wrapped since keeps forwarding through Settle.
        internal static void Remove()
        {
            if (Callback != null)
            {
                List<SocialRuleRHS> restored = new List<SocialRuleRHS>();
                foreach (KeyValuePair<SocialRuleRHS, MethodInfo> pair in Originals)
                {
                    if (Callback.GetValue(pair.Key) != (object)Blessed) continue;
                    Callback.SetValue(pair.Key, pair.Value);
                    restored.Add(pair.Key);
                }
                foreach (SocialRuleRHS rule in restored) Originals.Remove(rule);
            }
            Baselines.Clear();
            installed = false;
            WitchCastingLifecycle.RemoveListener(ref socials);
        }

        public static void Settle(Sim actor, Sim target, string interaction, ActiveTopic topic, InteractionInstance i)
        {
            SocialInteractionA social = i as SocialInteractionA;
            SocialRule effect = social == null ? null : social.TargetEffect;
            try { Correct(actor, target, effect); }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Applying the Blessing of Harmony to a relationship", error); }
            MethodInfo original;
            if (effect == null || effect.RHS == null || !Originals.TryGetValue(effect.RHS, out original) || original == null) return;
            SocialRuleRHS.ProceduralEffectDelegate call =
                Delegate.CreateDelegate(typeof(SocialRuleRHS.ProceduralEffectDelegate), original) as SocialRuleRHS.ProceduralEffectDelegate;
            if (call != null) call(actor, target, interaction, topic, i);
        }

        private static ListenerAction OnSocial(Event e)
        {
            try
            {
                Sim actor = e.Actor as Sim;
                Sim target = e.TargetObject as Sim;
                if (actor == null || target == null || (!GenieBlessings.Has(actor, Blessing.Harmony) && !GenieBlessings.Has(target, Blessing.Harmony)))
                    return ListenerAction.Keep;
                Relationship relation = Relationship.Get(actor, target, false);
                if (relation == null || relation.LTR == null) return ListenerAction.Keep;
                Prune();
                Baseline baseline = new Baseline();
                baseline.Liking = relation.LTR.Liking;
                baseline.State = relation.LTR.CurrentLTR;
                baseline.Stamp = SimClock.CurrentTicks;
                Baselines[relation.LTR] = baseline;
            }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Recording a Blessing of Harmony relationship baseline", error); }
            return ListenerAction.Keep;
        }

        // Scales the liking change this social just made, once, and leaves anything it cannot attribute untouched.
        private static void Correct(Sim actor, Sim target, SocialRule effect)
        {
            if (actor == null || target == null) return;
            Relationship relation = Relationship.Get(actor, target, false);
            LongTermRelationship ltr = relation == null ? null : relation.LTR;
            Baseline baseline;
            if (ltr == null || !Baselines.TryGetValue(ltr, out baseline)) return;
            Baselines.Remove(ltr);
            if (effect == null || effect.LHS == null || effect.LHS.LTROverride != 0 || SimClock.CurrentTicks - baseline.Stamp > Stale) return;
            if (!GenieBlessings.Has(actor, Blessing.Harmony) && !GenieBlessings.Has(target, Blessing.Harmony)) return;
            float change = ltr.LastChange;
            if (!BlessingRules.Attributed(baseline.Liking, change, ltr.Liking)) return;
            if (change < 0f && ltr.CurrentLTR != baseline.State) return;
            float liking = BlessingRules.Harmonized(baseline.Liking, change);
            ltr.SetLiking(liking);
            if (liking > (float)Highest.GetValue(ltr)) Highest.SetValue(ltr, liking);
            if (liking >= 0f) ltr.RemoveInteractionBit(LongTermRelationship.InteractionBits.MakeEnemy);
            Haunting.Invoke(ltr, new object[] { relation.SimDescriptionA.CreatedSim, relation.SimDescriptionB.CreatedSim });
        }

        private static void Prune()
        {
            if (Baselines.Count < 32) return;
            long now = SimClock.CurrentTicks;
            List<LongTermRelationship> old = new List<LongTermRelationship>();
            foreach (KeyValuePair<LongTermRelationship, Baseline> pair in Baselines)
                if (now - pair.Value.Stamp > Stale) old.Add(pair.Key);
            foreach (LongTermRelationship ltr in old) Baselines.Remove(ltr);
        }
    }
}
