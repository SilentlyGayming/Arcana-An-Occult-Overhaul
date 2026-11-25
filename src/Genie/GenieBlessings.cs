using System;
using System.Collections.Generic;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Careers;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.DreamsAndPromises;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects;
using Sims3.Gameplay.Objects.HobbiesSkills;
using Sims3.Gameplay.Skills;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.SimIFace.Enums;

namespace SupernaturalExpanded.Journal
{
    internal enum Blessing
    {
        Fortune,
        Vitality,
        Inspiration,
        Harmony,
        Protection
    }

    internal static class BlessingRules
    {
        internal const int Count = 5;
        internal const float Cost = 40f;
        internal const float Hours = 12f;
        internal const float DesiredHours = 24f;
        internal const float DesireCooldownDays = 3f;
        internal const float CooldownHours = 48f;
        internal const float DesireLiking = 15f;
        internal const float WrongDesireLiking = -10f;
        internal const float FulfilledHours = 24f;
        internal const float LostInTranslationHours = 6f;
        internal const float IncomeBonus = 0.10f;
        internal const float NeedDecay = 0.75f;
        internal const float AthleticLearning = 0.20f;
        internal const float FatigueBuildup = 0.25f;
        internal const float FatigueRecovery = 2f;
        internal const float LearningBonus = 0.20f;
        internal const float ResearchBonus = 0.20f;
        internal const float SocialMoodletDuration = 0.50f;
        internal const float ResistChance = 40f;
        internal const float MagicMoodletDuration = 0.50f;
        internal const float FightBonus = 0.20f;
        internal const float FightCap = 0.95f;
        internal const float CreativeSpeed = 0.20f;
        internal const float CreativeQualityChance = 50f;
        internal const float NovelLookahead = 3f;
        internal const float HarmonyGain = 1.25f;
        internal const float HarmonyLoss = 0.75f;
        internal const float LikingLimit = 100f;

        internal static bool ShieldsSpell(int tier, bool sameSim)
        {
            return !sameSim && tier < CastingRules.Legendary;
        }

        internal static float Shortened(float before, float after, bool fresh, float factor)
        {
            if (fresh) return after * factor;
            return after > before ? before + (after - before) * factor : after;
        }

        // A novel is armed for its quality bonus once its next few updates would finish it.
        internal static bool NearlyFinished(float progress, float gained)
        {
            return gained > 0f && progress < 100f && progress + gained * NovelLookahead >= 100f;
        }

        // A Genie-origin lucky moodlet that ends with the blessing is the blessing's own, even after a reload forgot who added it.
        internal static bool OwnLuck(bool fromGenie, float timeout, float blessingMinutes)
        {
            return fromGenie && timeout >= 0f && Math.Abs(timeout - blessingMinutes) <= 2f;
        }

        internal static float ClampLiking(float liking)
        {
            return Math.Max(-LikingLimit, Math.Min(LikingLimit, liking));
        }

        // True only when the last native liking change fully explains the move from the baseline to the current liking.
        internal static bool Attributed(float before, float change, float now)
        {
            return change != 0f && Math.Abs(ClampLiking(before + change) - now) < 0.01f;
        }

        internal static float Harmonized(float before, float change)
        {
            return ClampLiking(before + change * (change > 0f ? HarmonyGain : HarmonyLoss));
        }

        internal static readonly string[] Names = { "Fortune", "Vitality", "Inspiration", "Harmony", "Protection" };

        internal static float CostFor(CastingRules rules)
        {
            return Cost * rules.AbilityCostFactor;
        }

        internal static long Duration(bool desired)
        {
            return SimClock.ConvertToTicks(desired ? DesiredHours : Hours, TimeUnit.Hours);
        }

        // A Sim can be blessed again only once the last blessing's cooldown has passed; older saves without one wait for the active blessing to end.
        internal static bool CanBless(long cooldownEnds, long[] ends, long now)
        {
            return cooldownEnds <= now && Survivor(ends, now) < 0;
        }

        internal static long CooldownEnds(long now)
        {
            return now + SimClock.ConvertToTicks(CooldownHours, TimeUnit.Hours);
        }

        // The blessing a Sim keeps when an older save holds several: the one that ends last, then the first in menu order.
        internal static int Survivor(long[] ends, long now)
        {
            int keep = -1;
            for (int index = 0; index < ends.Length; index++)
                if (ends[index] > now && (keep < 0 || ends[index] > ends[keep])) keep = index;
            return keep;
        }

        internal static bool CanReveal(long askedAt, long now)
        {
            return askedAt <= 0 || now >= askedAt + SimClock.ConvertToTicks(DesireCooldownDays * 24f, TimeUnit.Hours);
        }

        internal static int Bonus(int amount)
        {
            return amount <= 0 ? 0 : (int)Math.Round(amount * IncomeBonus, MidpointRounding.AwayFromZero);
        }

        internal static bool EarnedIncome(GotPaidEvent.PayType type)
        {
            return type != GotPaidEvent.PayType.kCareerRetirementPay && type != GotPaidEvent.PayType.kCareerVacationPay;
        }
    }

    internal enum DesireOutcome
    {
        None,
        Correct,
        Incorrect
    }

    internal static class GenieBlessings
    {
        private static readonly CommodityKind[] Needs =
            { CommodityKind.Hunger, CommodityKind.Bladder, CommodityKind.Energy, CommodityKind.Social, CommodityKind.Fun, CommodityKind.Hygiene };
        private static readonly Origin[] SocialOrigins =
        {
            Origin.FromSocialization, Origin.FromFight, Origin.FromFightInPublic, Origin.FromLosingFight, Origin.FromRejectedWooHooOffHome,
            Origin.FromBeingRejectedAsClient, Origin.FromSociallyAwkwardTrait, Origin.FromBartenderArgueGnubb
        };
        private static readonly Origin[] NativeMagicOrigins = { Origin.FromGhostHaunting, Origin.FromFairy, Origin.FromFailedFairyTrap };
        private static readonly string[] HostileSpells = { "ice_blast", "fire_blast", "toadification" };

        [Persistable(false)] private static readonly Dictionary<ulong, Effects> active = new Dictionary<ulong, Effects>();
        [Persistable(false)] private static EventListener paid, buffs, painting;
        [Persistable(false)] private static ulong forcedDesireActor;
        [Persistable(false)] private static Blessing forcedDesire;

        private sealed class Effects
        {
            internal SimDescription Owner;
            internal bool Learning;
            internal float LastFatigue = float.NaN;
            internal object Work;
            internal float Progress = float.NaN;
            internal WrittenBookData Armed;
            internal BuffInstance Luck;
            internal long LuckEnds;
            internal readonly List<PendingBuff> Pending = new List<PendingBuff>();
            internal readonly Dictionary<Motive, KeyValuePair<MotiveTuning, MotiveTuning>> Tunings =
                new Dictionary<Motive, KeyValuePair<MotiveTuning, MotiveTuning>>();
        }

        private sealed class PendingBuff
        {
            internal BuffNames Guid;
            internal BuffInstance Prior;
            internal float Before;
            internal long Stamp;
        }

        private sealed class SpellShield
        {
            internal readonly Dictionary<BuffNames, KeyValuePair<BuffInstance, float>> Before = new Dictionary<BuffNames, KeyValuePair<BuffInstance, float>>();
        }

        internal static BuffNames Moodlet(Blessing kind)
        {
            return WishBuffs.Id("Blessing" + BlessingRules.Names[(int)kind]);
        }

        internal static string Name(Blessing kind)
        {
            return JournalText.Get("Arcana.Genie.Blessing." + BlessingRules.Names[(int)kind] + ".Name");
        }

        internal static void Start()
        {
            Stop();
            paid = EventTracker.AddListener(EventTypeId.kGotPaid, OnPaid);
            buffs = EventTracker.AddListener(EventTypeId.kGotBuff, OnBuff);
            painting = EventTracker.AddListener(EventTypeId.kStartedPainting, OnPainting);
            HeartsDesireLedger.Migrate();
            try { FightBlessing.Install(); }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Installing the Blessing of Vitality fight bonus", error); }
        }

        internal static void Stop()
        {
            WitchCastingLifecycle.RemoveListener(ref paid);
            WitchCastingLifecycle.RemoveListener(ref buffs);
            WitchCastingLifecycle.RemoveListener(ref painting);
            FightBlessing.Remove();
            HarmonyBlessing.Remove();
            foreach (Effects effects in active.Values)
            {
                RestoreTunings(effects);
                Disarm(effects, WishState.Read(effects.Owner, false));
            }
            active.Clear();
            forcedDesireActor = 0;
        }

        internal static bool Has(Sim sim, Blessing kind)
        {
            WishRecord record = sim == null ? null : WishState.Read(sim.SimDescription, false);
            return record != null && BlessingRules.Survivor(record.Blessings(), SimClock.CurrentTicks) == (int)kind;
        }

        internal static bool CanReceive(Sim actor, Sim target)
        {
            return actor != null && target != null && actor != target && !target.HasBeenDestroyed && target.SimDescription != null &&
                target.SimDescription.IsHuman && target.SimDescription.TeenOrAbove && target.BuffManager != null;
        }

        internal static bool Unlocked(Sim actor)
        {
            return GenieAbilities.IsGenie(actor) && actor.SimDescription != null && GeniePerks.Owns(actor.SimDescription, "HeartsDesire") &&
                MagicCasters.ReadRules(actor).Lampbound;
        }

        internal static bool CanBless(Sim target)
        {
            WishRecord record = target == null ? null : WishState.Read(target.SimDescription, false);
            return record == null || BlessingRules.CanBless(record.BlessingCooldownEnds, record.Blessings(), SimClock.CurrentTicks);
        }

        internal static bool Affordable(Sim actor)
        {
            CastingRules rules = MagicCasters.ReadRules(actor);
            return MagicCasters.AvailablePower(actor, rules) + 0.005f >= BlessingRules.CostFor(rules);
        }

        // Applies the moodlet and effects first and only then spends power and settles the desire, so a rejected blessing changes nothing.
        internal static bool Grant(Sim genie, Sim target, Blessing kind)
        {
            CastingRules rules = MagicCasters.ReadRules(genie);
            float cost = BlessingRules.CostFor(rules);
            if (!CanReceive(genie, target) || MagicCasters.AvailablePower(genie, rules) + 0.005f < cost) return false;
            WishRecord record = WishState.Read(target.SimDescription, true);
            if (record == null) return false;
            long now = SimClock.CurrentTicks;
            if (!BlessingRules.CanBless(record.BlessingCooldownEnds, record.Blessings(), now)) return false;
            DesireOutcome outcome = Outcome(record, genie.SimDescription.SimDescriptionId, kind, now);
            int index = (int)kind;
            long[] ends = record.Blessings();
            long[] previous = (long[])ends.Clone();
            Array.Clear(ends, 0, ends.Length);
            ends[index] = now + BlessingRules.Duration(outcome == DesireOutcome.Correct);
            bool applied = false;
            try
            {
                if (SyncMoodlet(target, Moodlet(kind), ends[index] - now))
                {
                    Update(target, record);
                    applied = true;
                }
            }
            finally
            {
                if (!applied) Revert(target, record, previous);
            }
            if (!applied) return false;
            record.BlessingCooldownEnds = BlessingRules.CooldownEnds(now);
            if (outcome != DesireOutcome.None) record.DesireOpen = false;
            MagicCasters.SpendInnate(genie, rules, cost);
            ShowOutcome(genie, target, kind, outcome);
            JournalBootstrap.RefreshMeter();
            return true;
        }

        // Restores every blessing the Sim held before, with its original end, and lets Update re-add its moodlet and effects.
        private static void Revert(Sim target, WishRecord record, long[] previous)
        {
            Array.Copy(previous, record.Blessings(), previous.Length);
            try { Update(target, record); }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Reverting a Genie Blessing", error); }
        }

        private static DesireOutcome Outcome(WishRecord record, ulong genie, Blessing kind, long now)
        {
            if (!record.DesireOpen || record.DesireGenie != genie || BlessingRules.CanReveal(record.DesireAsked, now)) return DesireOutcome.None;
            return record.Desire == (int)kind + 1 ? DesireOutcome.Correct : DesireOutcome.Incorrect;
        }

        private static void ShowOutcome(Sim genie, Sim target, Blessing kind, DesireOutcome outcome)
        {
            string first = target.SimDescription.FirstName;
            if (outcome == DesireOutcome.None)
            {
                WishFeedback.Show(genie, target, "Arcana.Genie.Blessing." + BlessingRules.Names[(int)kind] + ".Granted", first);
                return;
            }
            bool correct = outcome == DesireOutcome.Correct;
            ChangeLiking(genie, target, correct ? BlessingRules.DesireLiking : BlessingRules.WrongDesireLiking);
            GenieOutcomeBuffs.Give(target, correct ? "HeartsDesireFulfilled" : "LostInTranslation",
                correct ? BlessingRules.FulfilledHours : BlessingRules.LostInTranslationHours);
            WishFeedback.Show(genie, target, correct ? "Arcana.Genie.HeartsDesire.Fulfilled" : "Arcana.Genie.HeartsDesire.Mistaken", first);
        }

        private static void ChangeLiking(Sim genie, Sim target, float amount)
        {
            Relationship relationship = Relationship.Get(genie, target, true);
            if (relationship == null || relationship.LTR == null) return;
            relationship.LTR.SetLiking(relationship.LTR.Liking + amount);
        }

        internal static bool CanReveal(Sim target)
        {
            WishRecord record = target == null ? null : WishState.Read(target.SimDescription, false);
            return record == null || BlessingRules.CanReveal(record.DesireAsked, SimClock.CurrentTicks);
        }

        internal static bool Reveal(Sim genie, Sim target)
        {
            WishRecord record = WishState.Read(target.SimDescription, true);
            long now = SimClock.CurrentTicks;
            if (record == null || !BlessingRules.CanReveal(record.DesireAsked, now)) return false;
            Blessing kind = TakeForcedDesire(genie) ? forcedDesire : DesireScores.Pick(target);
            record.Desire = (int)kind + 1;
            record.DesireGenie = genie.SimDescription.SimDescriptionId;
            record.DesireAsked = now;
            record.DesireOpen = true;
            string line = JournalText.Get("Arcana.Genie.HeartsDesire." + BlessingRules.Names[(int)kind] + "." + RandomUtil.GetInt(1, 4));
            WishFeedback.Show(genie, target, "Arcana.Genie.HeartsDesire.Response", target.SimDescription.FirstName, line);
            return true;
        }

        internal static void ForceDesire(Sim actor, Blessing kind)
        {
            forcedDesireActor = actor == null || actor.SimDescription == null ? 0 : actor.SimDescription.SimDescriptionId;
            forcedDesire = kind;
        }

        internal static void ClearForced()
        {
            forcedDesireActor = 0;
        }

        private static bool TakeForcedDesire(Sim genie)
        {
            if (forcedDesireActor == 0 || genie.SimDescription.SimDescriptionId != forcedDesireActor) return false;
            forcedDesireActor = 0;
            return true;
        }

        internal static void ResetDesires()
        {
            foreach (Sim sim in Sims3.Gameplay.Queries.GetObjects<Sim>())
            {
                WishRecord record = WishState.Read(sim.SimDescription, false);
                if (record == null) continue;
                record.DesireAsked = 0;
                record.DesireOpen = false;
            }
        }

        internal static void Update(Sim sim, WishRecord record)
        {
            ulong id = sim.SimDescription.SimDescriptionId;
            long now = SimClock.CurrentTicks;
            long[] ends = record.Blessings();
            int keep = BlessingRules.Survivor(ends, now);
            bool any = false;
            for (int index = 0; index < ends.Length; index++)
            {
                if (index != keep) ends[index] = 0;
                bool on = ends[index] > now;
                if (!SyncMoodlet(sim, Moodlet((Blessing)index), on ? ends[index] - now : 0)) on = false;
                if (!on) ends[index] = 0;
                any |= on;
            }
            if (record.DesireOpen && BlessingRules.CanReveal(record.DesireAsked, now)) record.DesireOpen = false;
            Effects effects;
            active.TryGetValue(id, out effects);
            if (effects != null && effects.Owner != sim.SimDescription)
            {
                active.Remove(id);
                effects = null;
            }
            if (record.AdviceLent && (effects == null || effects.Armed == null)) Reclaim(sim, record);
            if (!any && effects == null && record.AthleticBonus == 0f) return;
            if (effects == null)
            {
                effects = new Effects();
                effects.Owner = sim.SimDescription;
                active[id] = effects;
            }
            bool vitality = ends[(int)Blessing.Vitality] > now;
            bool inspiration = ends[(int)Blessing.Inspiration] > now;
            SettleDurations(sim, effects);
            ApplyLuck(sim, effects, ends[(int)Blessing.Fortune] - now);
            ApplyLearning(sim, effects, record, vitality, inspiration);
            ApplyVitality(sim, effects, vitality);
            ApplyCreativeSpeed(sim, effects, record, inspiration);
            try
            {
                if (inspiration) MealBlessing.Season(sim);
            }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Applying the Blessing of Inspiration to a meal", error); }
            if (!any)
            {
                RestoreTunings(effects);
                active.Remove(id);
            }
        }

        // Returns false when the game refuses the moodlet, so a blessing is never recorded without its moodlet.
        private static bool SyncMoodlet(Sim sim, BuffNames id, long remaining)
        {
            BuffInstance existing = sim.BuffManager.GetElement(id);
            if (remaining <= 0)
            {
                if (existing != null) sim.BuffManager.RemoveElement(id);
                return true;
            }
            float minutes = SimClock.ConvertFromTicks(remaining, TimeUnit.Minutes);
            if (existing != null)
            {
                if (Math.Abs(existing.TimeoutCount - minutes) > 1f) existing.TimeoutCount = minutes;
                return true;
            }
            BuffInstance template;
            if (!BuffManager.BuffDictionary.TryGetValue((ulong)id, out template))
            {
                JournalBootstrap.ReportOnce("Genie Blessing moodlet is not registered: " + id, new InvalidOperationException());
                return false;
            }
            if (sim.BuffManager.AddElementIgnoreDelay(id, template.EffectValue, minutes, Origin.FromGenie) && sim.BuffManager.HasElement(id)) return true;
            JournalBootstrap.ReportOnce("The game refused the Genie Blessing moodlet " + id, new InvalidOperationException());
            return false;
        }

        // Keeps Feeling Lucky for Fortune's duration, remembering whether the blessing created it or only extended a lucky streak the Sim already had.
        private static void ApplyLuck(Sim sim, Effects effects, long remaining)
        {
            if (remaining <= 0)
            {
                ReleaseLuck(sim, effects);
                return;
            }
            if (sim.BuffManager.HasElement(BuffNames.FeelingUnlucky)) return;
            float minutes = SimClock.ConvertFromTicks(remaining, TimeUnit.Minutes);
            BuffInstance lucky = sim.BuffManager.GetElement(BuffNames.FeelingLucky);
            if (lucky == null)
            {
                if (!LuckMoodlet.Add(sim, BuffNames.FeelingLucky, minutes, Origin.FromGenie)) return;
                effects.Luck = sim.BuffManager.GetElement(BuffNames.FeelingLucky);
                effects.LuckEnds = 0;
                return;
            }
            if (lucky != effects.Luck)
            {
                effects.Luck = lucky;
                effects.LuckEnds = BlessingRules.OwnLuck(lucky.BuffOrigin == Origin.FromGenie, lucky.TimeoutCount, minutes)
                    ? 0 : lucky.TimeoutCount < 0f ? -1 : SimClock.CurrentTicks + SimClock.ConvertToTicks(lucky.TimeoutCount, TimeUnit.Minutes);
            }
            if (effects.LuckEnds >= 0 && lucky.TimeoutCount < minutes - 1f) lucky.TimeoutCount = minutes;
        }

        // Takes back only the luck Fortune gave: its own moodlet is removed, a lucky streak the Sim already had returns to its own end.
        private static void ReleaseLuck(Sim sim, Effects effects)
        {
            BuffInstance lucky = effects.Luck;
            long ends = effects.LuckEnds;
            effects.Luck = null;
            effects.LuckEnds = 0;
            if (lucky == null || sim.BuffManager.GetElement(BuffNames.FeelingLucky) != lucky || ends < 0) return;
            long now = SimClock.CurrentTicks;
            if (ends > now) lucky.TimeoutCount = SimClock.ConvertFromTicks(ends - now, TimeUnit.Minutes);
            else sim.BuffManager.RemoveElement(BuffNames.FeelingLucky);
        }

        // The general bonus lives in a modifier the game rebuilds on load; the Athletic bonus is saved and reconciled.
        private static void ApplyLearning(Sim sim, Effects effects, WishRecord record, bool vitality, bool inspiration)
        {
            SkillManager skills = sim.SimDescription.SkillManager;
            if (skills == null) return;
            if (inspiration != effects.Learning)
            {
                skills.mOverallModifier += inspiration ? BlessingRules.LearningBonus : -BlessingRules.LearningBonus;
                effects.Learning = inspiration;
            }
            float athletic = vitality && !inspiration ? BlessingRules.AthleticLearning : 0f;
            if (athletic == record.AthleticBonus) return;
            skills.ChangeSkillGainModifier(SkillNames.Athletic, athletic - record.AthleticBonus);
            record.AthleticBonus = athletic;
        }

        private static void ApplyVitality(Sim sim, Effects effects, bool vitality)
        {
            if (sim.Motives == null) return;
            if (!vitality)
            {
                RestoreTunings(effects);
                effects.LastFatigue = float.NaN;
                return;
            }
            foreach (CommodityKind need in Needs) Scale(sim.Motives.GetMotive(need), effects, BlessingRules.NeedDecay);
            Motive fatigue = sim.Motives.GetMotive(CommodityKind.Fatigue);
            Scale(fatigue, effects, BlessingRules.FatigueRecovery);
            if (fatigue == null) return;
            float value = sim.Motives.GetValue(CommodityKind.Fatigue);
            if (!float.IsNaN(effects.LastFatigue) && value > effects.LastFatigue)
            {
                sim.Motives.ChangeValue(CommodityKind.Fatigue, -(value - effects.LastFatigue) * BlessingRules.FatigueBuildup);
                value = sim.Motives.GetValue(CommodityKind.Fatigue);
            }
            effects.LastFatigue = value;
        }

        // Adds a fifth of the progress made since the last update to the painting or novel the blessed Sim is working on.
        private static void ApplyCreativeSpeed(Sim sim, Effects effects, WishRecord record, bool inspiration)
        {
            object work = null;
            float progress = float.NaN;
            if (inspiration && CreativeWork.Read(sim, out work, out progress) && work == effects.Work && progress > effects.Progress)
            {
                float gained = progress - effects.Progress;
                Arm(effects, record, work as WrittenBookData, progress, gained);
                progress += gained * BlessingRules.CreativeSpeed;
                CreativeWork.Write(work, progress);
            }
            if (effects.Armed != null && (object)effects.Armed != work) Disarm(effects, record);
            effects.Work = work;
            effects.Progress = progress;
        }

        // Lends a nearly finished novel the game's own editorial-advice quality bonus, only while it is being finished.
        private static void Arm(Effects effects, WishRecord record, WrittenBookData book, float progress, float gained)
        {
            if (book == null || effects.Armed != null || book.HasReceivedAdviceFromBoss || !BlessingRules.NearlyFinished(progress, gained)) return;
            record.AdviceLent = true;
            book.HasReceivedAdviceFromBoss = true;
            effects.Armed = book;
        }

        private static void Disarm(Effects effects, WishRecord record)
        {
            if (effects.Armed != null) effects.Armed.HasReceivedAdviceFromBoss = false;
            effects.Armed = null;
            if (record != null) record.AdviceLent = false;
        }

        // A save made while a novel borrowed the advice bonus kept the flag; it is taken back the first time the Sim is updated after loading.
        private static void Reclaim(Sim sim, WishRecord record)
        {
            WrittenBookData book = CreativeWork.Novel(sim);
            if (book != null) book.HasReceivedAdviceFromBoss = false;
            record.AdviceLent = false;
        }

        private static void Scale(Motive motive, Effects effects, float factor)
        {
            if (motive == null || motive.Tuning == null) return;
            KeyValuePair<MotiveTuning, MotiveTuning> current;
            if (effects.Tunings.TryGetValue(motive, out current) && motive.Tuning == current.Value) return;
            MotiveTuning original = motive.Tuning;
            MotiveTuning scaled = MotiveDecay.Scaled(original, factor);
            if (scaled == null) return;
            motive.Tuning = scaled;
            effects.Tunings[motive] = new KeyValuePair<MotiveTuning, MotiveTuning>(original, scaled);
        }

        private static void RestoreTunings(Effects effects)
        {
            foreach (KeyValuePair<Motive, KeyValuePair<MotiveTuning, MotiveTuning>> pair in effects.Tunings)
                if (pair.Key.Tuning == pair.Value.Value) pair.Key.Tuning = pair.Value.Key;
            effects.Tunings.Clear();
        }

        internal static float ResearchFactor(Sim sim)
        {
            return Has(sim, Blessing.Inspiration) ? 1f + BlessingRules.ResearchBonus : 1f;
        }

        // Rolls once per hostile cast, before the native effect lands; Legendary magic is never resisted.
        internal static bool Resists(Sim caster, Sim target, SpellEntry spell)
        {
            if (caster == target || spell == null || spell.Tier >= CastingRules.Legendary || !Has(target, Blessing.Protection)) return false;
            if (spell.Category != SpellCategory.Curse && Array.IndexOf(HostileSpells, spell.Id) < 0) return false;
            if (!RandomUtil.RandomChance(BlessingRules.ResistChance)) return false;
            WishFeedback.Show(caster, target, "Arcana.Genie.Blessing.Protection.Resisted", target.SimDescription.FirstName);
            return true;
        }

        internal static bool ResistsAbility(Sim caster, Sim target, GenieAbility ability)
        {
            if (caster == target || target == null || ability == null || (ability.Id != "genie_ensorcel" && ability.Id != "genie_banish")) return false;
            if (!Has(target, Blessing.Protection) || !RandomUtil.RandomChance(BlessingRules.ResistChance)) return false;
            WishFeedback.Show(caster, target, "Arcana.Genie.Blessing.Protection.Resisted", target.SimDescription.FirstName);
            return true;
        }

        private static ListenerAction OnPaid(Event e)
        {
            try
            {
                GotPaidEvent pay = e as GotPaidEvent;
                Sim sim = e.Actor as Sim;
                if (pay == null || sim == null || sim.Household == null || !BlessingRules.EarnedIncome(pay.TypeOfPay) || !Has(sim, Blessing.Fortune))
                    return ListenerAction.Keep;
                int bonus = BlessingRules.Bonus((int)pay.Amount);
                if (bonus > 0) sim.Household.ModifyFamilyFunds(bonus);
            }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Applying Blessing of Fortune", error); }
            return ListenerAction.Keep;
        }

        private static ListenerAction OnPainting(Event e)
        {
            try
            {
                Sim sim = e.Actor as Sim;
                EaselCanvas canvas = e.TargetObject as EaselCanvas;
                if (canvas != null && Has(sim, Blessing.Inspiration)) PaintingBlessing.Improve(sim, canvas);
            }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Applying the Blessing of Inspiration to a painting", error); }
            return ListenerAction.Keep;
        }

        // Queues each new or refreshed moodlet, because the game sets its origin and refreshed timer only after this event.
        private static ListenerAction OnBuff(Event e)
        {
            try
            {
                Sim sim = e.Actor as Sim;
                Effects effects;
                if (sim == null || sim.SimDescription == null || !active.TryGetValue(sim.SimDescription.SimDescriptionId, out effects))
                    return ListenerAction.Keep;
                HasGuidEvent<BuffNames> added = e as HasGuidEvent<BuffNames>;
                BuffInstance incoming = added == null ? null : added.HasGuidObject as BuffInstance;
                if (incoming == null) return ListenerAction.Keep;
                PendingBuff pending = new PendingBuff();
                pending.Guid = incoming.Guid;
                pending.Prior = sim.BuffManager.GetElement(incoming.Guid);
                pending.Before = pending.Prior == null ? 0f : pending.Prior.TimeoutCount;
                pending.Stamp = SimClock.CurrentTicks;
                effects.Pending.Add(pending);
            }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Applying blessing moodlet durations", error); }
            return ListenerAction.Keep;
        }

        private static void SettleDurations(Sim sim, Effects effects)
        {
            List<BuffNames> settled = new List<BuffNames>();
            foreach (PendingBuff pending in effects.Pending)
            {
                BuffInstance live = sim.BuffManager.GetElement(pending.Guid);
                if (settled.Contains(pending.Guid) || live == null || live.TimeoutCount <= 0f) continue;
                settled.Add(pending.Guid);
                float factor = DurationFactor(sim, live);
                if (factor >= 1f) continue;
                float elapsed = SimClock.ConvertFromTicks(SimClock.CurrentTicks - pending.Stamp, TimeUnit.Minutes);
                float shortened = BlessingRules.Shortened(pending.Before, live.TimeoutCount + elapsed, pending.Prior != live, factor);
                live.TimeoutCount = Math.Max(1f, shortened - elapsed);
            }
            effects.Pending.Clear();
        }

        private static float DurationFactor(Sim sim, BuffInstance buff)
        {
            if (buff.Guid == BuffNames.Sore) return Has(sim, Blessing.Vitality) ? 0.5f : 1f;
            if (buff.EffectValue >= 0 || buff.Guid == WitchMagicRecovery.FizzleBuff) return 1f;
            Origin origin = buff.BuffOrigin;
            if (Array.IndexOf(SocialOrigins, origin) >= 0 && Has(sim, Blessing.Harmony)) return BlessingRules.SocialMoodletDuration;
            if (Array.IndexOf(NativeMagicOrigins, origin) >= 0 && Has(sim, Blessing.Protection)) return BlessingRules.MagicMoodletDuration;
            return 1f;
        }

        // Snapshots the target's moodlets before an ordinary hostile spell lands, so only what that spell adds or refreshes is shortened.
        internal static object ShieldSpell(Sim caster, Sim target, SpellEntry spell)
        {
            if (spell == null || !BlessingRules.ShieldsSpell(spell.Tier, caster == target) || !Has(target, Blessing.Protection)) return null;
            Effects effects;
            if (active.TryGetValue(target.SimDescription.SimDescriptionId, out effects)) SettleDurations(target, effects);
            SpellShield shield = new SpellShield();
            foreach (BuffInstance buff in target.BuffManager.Buffs)
                shield.Before[buff.Guid] = new KeyValuePair<BuffInstance, float>(buff, buff.TimeoutCount);
            return shield;
        }

        // Shortens what the spell added and withdraws those moodlets from the deferred queue, so no moodlet is shortened twice.
        internal static void ShortenSpell(object token, Sim target)
        {
            SpellShield shield = token as SpellShield;
            if (shield == null) return;
            List<BuffNames> handled = new List<BuffNames>();
            foreach (BuffInstance buff in new List<BuffInstance>(target.BuffManager.Buffs))
            {
                if (buff.EffectValue >= 0 || buff.TimeoutCount <= 0f || buff.Guid == WitchMagicRecovery.FizzleBuff) continue;
                KeyValuePair<BuffInstance, float> before;
                bool known = shield.Before.TryGetValue(buff.Guid, out before);
                bool fresh = !known || before.Key != buff;
                if (!fresh && buff.TimeoutCount <= before.Value) continue;
                buff.TimeoutCount = BlessingRules.Shortened(known ? before.Value : 0f, buff.TimeoutCount, fresh, BlessingRules.MagicMoodletDuration);
                handled.Add(buff.Guid);
            }
            Effects effects;
            if (handled.Count > 0 && active.TryGetValue(target.SimDescription.SimDescriptionId, out effects))
                effects.Pending.RemoveAll(delegate(PendingBuff pending) { return handled.Contains(pending.Guid); });
        }
    }

    internal static class DesireScores
    {
        private static readonly TraitNames[][] Traits =
        {
            new TraitNames[] { TraitNames.Frugal, TraitNames.Kleptomaniac, TraitNames.Mooch, TraitNames.Ambitious, TraitNames.Snob, TraitNames.Workaholic },
            new TraitNames[] { TraitNames.Athletic, TraitNames.CouchPotato, TraitNames.Daredevil, TraitNames.Excitable, TraitNames.PartyAnimal },
            new TraitNames[] { TraitNames.Artistic, TraitNames.Virtuoso, TraitNames.Genius, TraitNames.Perfectionist, TraitNames.NaturalCook,
                TraitNames.Handy, TraitNames.ComputerWhiz, TraitNames.Disciplined },
            new TraitNames[] { TraitNames.Friendly, TraitNames.Flirty, TraitNames.HopelessRomantic, TraitNames.Loner, TraitNames.Shy,
                TraitNames.SociallyAwkward, TraitNames.Charismatic, TraitNames.GoodSenseOfHumor },
            new TraitNames[] { TraitNames.Coward, TraitNames.Unlucky, TraitNames.Clumsy, TraitNames.Neurotic, TraitNames.Hydrophobic }
        };
        private const float LowNeed = -40f;
        private const int LowFunds = 2000;

        internal static Blessing Pick(Sim sim)
        {
            float[] scores = Score(sim);
            float total = 0f;
            foreach (float score in scores) total += score;
            float roll = RandomUtil.GetFloat(total);
            for (int index = 0; index < scores.Length; index++)
            {
                if (roll < scores[index]) return (Blessing)index;
                roll -= scores[index];
            }
            return Blessing.Harmony;
        }

        internal static float[] Score(Sim sim)
        {
            float[] scores = { 1f, 1f, 1f, 1f, 1f };
            if (sim.TraitManager != null)
            {
                for (int index = 0; index < Traits.Length; index++)
                    foreach (TraitNames trait in Traits[index])
                        if (sim.TraitManager.HasElement(trait)) scores[index] += 2f;
            }
            if (sim.Motives != null)
            {
                if (Low(sim, CommodityKind.Energy)) scores[(int)Blessing.Vitality] += 2f;
                if (Low(sim, CommodityKind.Social)) scores[(int)Blessing.Harmony] += 2f;
                if (Low(sim, CommodityKind.Fun)) scores[(int)Blessing.Inspiration] += 1f;
            }
            Occupation career = sim.SimDescription.CareerManager == null ? null : sim.SimDescription.CareerManager.Occupation;
            if (career == null && sim.SimDescription.YoungAdultOrAbove) scores[(int)Blessing.Fortune] += 2f;
            else if (career != null && career.Level <= 3) scores[(int)Blessing.Inspiration] += 1f;
            if (sim.Household != null && sim.Household.FamilyFunds < LowFunds) scores[(int)Blessing.Fortune] += 2f;
            if (sim.BuffManager != null && (sim.BuffManager.HasElement(BuffNames.FeelingUnlucky) || sim.BuffManager.HasElement(WishBuffs.GoneWrong)))
                scores[(int)Blessing.Protection] += 2f;
            if (HasPromisedWish(sim)) scores[(int)Blessing.Inspiration] += 1f;
            return scores;
        }

        private static bool Low(Sim sim, CommodityKind need)
        {
            return sim.Motives.HasMotive(need) && sim.Motives.GetValue(need) < LowNeed;
        }

        private static bool HasPromisedWish(Sim sim)
        {
            DreamsAndPromisesManager manager = sim.DreamsAndPromisesManager;
            if (manager == null) return false;
            foreach (ActiveNodeBase node in manager.ActiveNodes)
            {
                ActiveDreamNode dream = node as ActiveDreamNode;
                if (dream != null && dream.IsPromised && !dream.IsComplete) return true;
            }
            return false;
        }
    }

    internal static class HeartsDesireLedger
    {
#pragma warning disable 169, 649
        [Persistable]
        private sealed class Request
        {
            internal SimDescription Genie;
            internal ActiveDreamNode Desire;
            internal long Expires;
        }
#pragma warning restore 169, 649

        [PersistableStatic] private static Dictionary<ulong, Request> requests;

        // Carries the cooldowns of desires revealed before 0.9.4 into the per-Sim record, then retires the old ledger.
        internal static void Migrate()
        {
            if (requests == null) return;
            long cooldown = SimClock.ConvertToTicks(BlessingRules.DesireCooldownDays * 24f, TimeUnit.Hours);
            foreach (KeyValuePair<ulong, Request> pair in requests)
            {
                SimDescription sim = pair.Value == null ? null : SimDescription.Find(pair.Key);
                WishRecord record = sim == null || pair.Value.Expires <= SimClock.CurrentTicks ? null : WishState.Read(sim, true);
                if (record != null) record.DesireAsked = Math.Max(record.DesireAsked, pair.Value.Expires - cooldown);
            }
            requests = null;
        }

        internal static void Clear()
        {
            requests = null;
        }
    }

    [Persistable]
    public abstract class GenieSocial : SocialInteraction
    {
        [Persistable(false)] protected StateMachineClient Exchange;

        protected abstract bool Allowed();
        protected abstract bool Perform();
        protected abstract string ReplyName { get; }

        public override void Cleanup()
        {
            try
            {
                if (Exchange != null) Exchange.Dispose();
                Exchange = null;
            }
            finally { base.Cleanup(); }
        }

        protected override bool Run()
        {
            if (!Allowed() || !SafeToSync()) return false;
            if (!BeginSocialInteraction(new SocialInteractionB.Definition(null, ReplyName, false), true, false)) return false;
            bool done = false;
            StandardEntry();
            BeginCommodityUpdates();
            try
            {
                if (Allowed() && !Actor.HasExitReason(ExitReason.Default)) done = Perform();
                return done;
            }
            finally
            {
                EndCommodityUpdates(done);
                FinishLinkedInteraction(true);
                StandardExit();
                WaitForSyncComplete();
            }
        }

        protected bool Interrupted
        {
            get { return Actor.HasExitReason(ExitReason.Default) || Target.HasExitReason(ExitReason.Default); }
        }
    }

    [Persistable]
    public sealed class GenieBlessing : GenieSocial
    {
        public static readonly InteractionDefinition[] Singletons =
        {
            new Definition(Blessing.Fortune), new Definition(Blessing.Vitality), new Definition(Blessing.Inspiration),
            new Definition(Blessing.Harmony), new Definition(Blessing.Protection)
        };

        private Blessing Kind
        {
            get { Definition definition = InteractionDefinition as Definition; return definition == null ? Blessing.Fortune : definition.Kind; }
        }

        protected override string ReplyName
        {
            get { return GenieBlessings.Name(Kind); }
        }

        protected override bool Allowed()
        {
            return InteractionDefinition is Definition && GenieBlessings.Unlocked(Actor) && GenieBlessings.CanReceive(Actor, Target) &&
                GenieBlessings.CanBless(Target) && GenieBlessings.Affordable(Actor);
        }

        protected override bool Perform()
        {
            Exchange = BlessingPresentation.Play(Actor, Target);
            if (Exchange == null) Actor.PlaySoloAnimation("a_genie_clap_x", true, ProductVersion.EP6);
            return !Interrupted && GenieBlessings.Grant(Actor, Target, Kind);
        }

        [Persistable, DoesntRequireTuning]
        public sealed class Definition : InteractionDefinition<Sim, Sim, GenieBlessing>
        {
            internal Blessing Kind;

            public Definition() { }

            internal Definition(Blessing kind)
            {
                Kind = kind;
            }

            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            {
                return GenieBlessings.Name(Kind);
            }

            public override string[] GetPath(bool female)
            {
                return new[] { GenieMenu.Root(female), JournalText.Get("Arcana.Genie.Blessing.Menu") };
            }

            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                if (autonomous || !GenieBlessings.Unlocked(actor) || !GenieBlessings.CanReceive(actor, target)) return false;
                if (!GenieBlessings.CanBless(target))
                {
                    tooltip = delegate { return JournalText.Get("Arcana.Genie.Blessing.Cooldown"); };
                    return false;
                }
                if (GenieBlessings.Affordable(actor)) return true;
                tooltip = GenieMenu.SilentUnavailable;
                return false;
            }
        }
    }

    [Persistable]
    public sealed class HeartsDesire : GenieSocial
    {
        public static readonly InteractionDefinition Singleton = new Definition();

        protected override string ReplyName
        {
            get { return JournalText.Get("Arcana.Genie.HeartsDesire.Ask"); }
        }

        protected override bool Allowed()
        {
            return GenieBlessings.Unlocked(Actor) && GenieBlessings.CanReceive(Actor, Target) && GenieBlessings.CanReveal(Target);
        }

        protected override bool Perform()
        {
            Exchange = QuestionSocial.Play(Actor, Target);
            return Exchange != null && !Interrupted && GenieBlessings.Reveal(Actor, Target);
        }

        [DoesntRequireTuning]
        public sealed class Definition : InteractionDefinition<Sim, Sim, HeartsDesire>
        {
            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            {
                return JournalText.Get("Arcana.Genie.HeartsDesire.Ask");
            }

            public override string[] GetPath(bool female)
            {
                return new[] { GenieMenu.Root(female) };
            }

            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                if (autonomous || !GenieBlessings.Unlocked(actor) || !GenieBlessings.CanReceive(actor, target)) return false;
                if (GenieBlessings.CanReveal(target)) return true;
                tooltip = delegate { return JournalText.Get("Arcana.Genie.HeartsDesire.Waiting"); };
                return false;
            }
        }
    }

    internal static class BlessingPresentation
    {
        // Plays the native lamp wish graph with the recipient as the wisher, so the Genie claps and the recipient rises and glows.
        internal static StateMachineClient Play(Sim genie, Sim recipient)
        {
            StateMachineClient wish = StateMachineClient.Acquire(genie, "MakeWish", AnimationPriority.kAPDefault);
            if (wish == null) return null;
            wish.SetActor("x", recipient);
            wish.SetActor("y", genie);
            wish.EnterState("x", "Enter");
            wish.EnterState("y", "Enter");
            wish.SetParameter("WishType", GenieWishTypes.Happiness);
            wish.RequestState("x", "Exit");
            return wish;
        }
    }

    internal static class QuestionSocial
    {
        private static readonly string[] Actions = { "Get To Know", "Ask About Day", "Chat" };

        // Plays the accepted outcome of a native question social on that social's own graph, the way SocialInteractionA does.
        internal static StateMachineClient Play(Sim actor, Sim target)
        {
            ActionData action;
            SocialRuleRHS rule;
            if (!Find(out action, out rule)) return null;
            string graph = action.GetJazzGraph(actor, target);
            if (string.IsNullOrEmpty(graph)) return null;
            bool settle = actor.SocialComponent.GoToStance(target.SocialComponent.Stance != action.StartStanceY, action.StartStanceX);
            target.SocialComponent.GoToStance(false, action.StartStanceY);
            if (settle) Simulator.Sleep(6);
            StateMachineClient talk = StateMachineClient.Acquire(Simulator.CurrentTask, graph);
            if (talk == null) return null;
            talk.SetProductVersion(action.ProductVersion);
            talk.SetActor("x", actor);
            talk.SetActor("y", target);
            talk.SetParameter("isXMermaid", actor.SimDescription.IsMatureMermaid);
            talk.SetParameter("isYMermaid", target.SimDescription.IsMatureMermaid);
            talk.SetParameter("StartStanceX", action.StartStanceX);
            talk.SetParameter("StartStanceY", action.StartStanceY);
            talk.EnterState("x", "Enter");
            talk.EnterState("y", "Enter");
            talk.SetParameter("AnimX", rule.AnimX);
            talk.SetParameter("AnimY", rule.AnimY);
            talk.RequestState(false, "x", rule.JazzState);
            talk.RequestState(true, "y", rule.JazzState);
            actor.SocialComponent.SetStance(rule.EndStanceX);
            target.SocialComponent.SetStance(rule.EndStanceY);
            return talk;
        }

        private static bool Find(out ActionData action, out SocialRuleRHS rule)
        {
            foreach (string key in Actions)
            {
                action = ActionData.Get(key);
                rule = action == null ? null : SocialRuleRHS.Get(key, CommodityTypes.Friendly);
                if (rule != null && !rule.UseGenericPetReject && !string.IsNullOrEmpty(rule.JazzState) &&
                    !string.IsNullOrEmpty(rule.AnimX) && !string.IsNullOrEmpty(rule.AnimY)) return true;
            }
            action = null;
            rule = null;
            return false;
        }
    }
}
