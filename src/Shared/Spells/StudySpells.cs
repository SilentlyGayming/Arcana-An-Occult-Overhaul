using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.Gameplay.Skills;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal sealed class SpellStudyTrack
    {
        internal readonly OccultTypes Occult;
        internal readonly SkillNames Skill;

        internal SpellStudyTrack(OccultTypes occult, SkillNames skill)
        {
            Occult = occult;
            Skill = skill;
        }
    }

    internal static class SpellStudy
    {
        internal static SpellResearchSettings Settings
        {
            get { return new SpellResearchSettings(JournalBootstrap.kStudyResearchIntervalMinutes,
                JournalBootstrap.kStudyDiscoveryChancePercent, JournalBootstrap.kStudyExperienceFraction,
                JournalBootstrap.kStudyWitchSpeed, JournalBootstrap.kStudyFairySpeed, JournalBootstrap.kStudyGenieSpeed,
                JournalBootstrap.kStudyWitchMasterBonusPercent, JournalBootstrap.kStudyGenieAscendantBonusPercent,
                JournalBootstrap.kStudyWitchingHourBonusPercent); }
        }

        internal static float Speed(Sim actor, SpellResearchSettings settings)
        {
            return settings.Speed(WitchCastingContext.ReadCapacityRules(actor), SpellResearchSettings.IsWitchingHour(SimClock.Hours24)) *
                GenieBlessings.ResearchFactor(actor);
        }

        internal static string RankUnlockText(string key)
        {
            if (key == "Arcana.Witch.Rank.Unlock.4")
                return JournalText.Format("Arcana.Witch.Rank.Unlock.4", PercentText.Format(Settings.WitchMasterBonusPercent, false));
            if (key == "Arcana.Genie.Rank.Unlock.3")
                return JournalText.Format("Arcana.Genie.Rank.Unlock.3", PercentText.Format(Settings.GenieAscendantBonusPercent, false));
            return JournalText.Get(key);
        }

        internal static float SkillPointsPerSimMinute(Sim actor)
        {
            InteractionObjectPair pair = new InteractionObjectPair((Sims3.Gameplay.Interactions.InteractionDefinition)MagicWand.PracticeSpellCasting.Singleton, actor);
            if (pair.Tuning == null || pair.Tuning.mTradeoff == null)
                throw new InvalidOperationException("Practice Magic XP tuning is unavailable.");
            foreach (CommodityChange output in pair.Tuning.mTradeoff.mOutputs)
            {
                if (output.Commodity == CommodityKind.SkillSpellcasting)
                    return output.mActualValue / 60f * CastingRules.PracticeExperience
                        * Settings.ExperienceFraction;
            }
            throw new InvalidOperationException("Practice Magic has no Spellcasting XP output.");
        }

        private static readonly SpellStudyTrack[] Tracks =
        {
            new SpellStudyTrack(OccultTypes.Witch, SkillNames.Spellcasting),
            new SpellStudyTrack(OccultTypes.Fairy, SkillNames.FairyMagic),
            new SpellStudyTrack(OccultTypes.Genie, GenieMagicSkill.GenieGuid)
        };

        internal static SpellStudyTrack TrackFor(Sim sim)
        {
            if (sim == null || sim.OccultManager == null)
            {
                return null;
            }

            for (int i = 0; i < Tracks.Length; i++)
            {
                if (sim.OccultManager.HasOccultType(Tracks[i].Occult))
                {
                    return Tracks[i];
                }
            }
            return null;
        }
    }

    internal static class SpellStudyStations
    {
        private static bool active;
        private static PropertyInfo placedObject;

        internal static void Start()
        {
            if (active)
            {
                return;
            }

            active = true;
            World.OnObjectPlacedInLotEventHandler += OnPlaced;
            foreach (AlchemyStation station in Sims3.Gameplay.Queries.GetObjects<AlchemyStation>())
            {
                Install(station);
            }
        }

        internal static void Stop()
        {
            if (!active)
            {
                return;
            }

            active = false;
            World.OnObjectPlacedInLotEventHandler -= OnPlaced;
        }

        private static void OnPlaced(object sender, EventArgs args)
        {
            if (args == null || args.GetType().Name != "OnObjectPlacedInLotEventArgs")
            {
                return;
            }

            if (placedObject == null)
            {
                placedObject = args.GetType().GetProperty("ObjectId");
            }
            if (placedObject != null)
            {
                ObjectGuid id = (ObjectGuid)placedObject.GetValue(args, null);
                Install(Sims3.Gameplay.Abstracts.GameObject.GetObject(id) as AlchemyStation);
            }
        }

        private static void Install(AlchemyStation station)
        {
            if (station == null || station.HasBeenDestroyed)
            {
                return;
            }

            try
            {
                station.AddInteraction(StudySpells.Singleton, true);
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce("Adding Study Spells to a station", error);
            }
        }
    }

    public sealed class StudySpells : Interaction<Sim, AlchemyStation>
    {
        private const uint BenchSlot = 824351309U;
        private const uint PageOffEvent = 101;
        private const uint PageOnEvent = 102;

        private const BindingFlags Methods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly MethodInfo StartCandles = typeof(AlchemyStation).GetMethod("StartLitCandlesVFX", Methods);
        private static readonly MethodInfo StopCandles = typeof(AlchemyStation).GetMethod("StopLitCandlesVFX", Methods);
        private static readonly MethodInfo Extinguish = typeof(AlchemyStation).GetMethod("FireOneShotCandleExtinguishVFX", Methods);

        public static readonly InteractionDefinition Singleton = new Definition();

        [Persistable(false)]
        private Skill studied;
        private static readonly MethodInfo ShowMeter = typeof(SkillManager).GetMethod("ShowSkillMeter",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Skill), typeof(bool) }, null);
        private static readonly MethodInfo HideMeter = typeof(SkillManager).GetMethod("HideSkillMeter",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        private SkillNames studiedName;
        private float appliedRate;
        private bool forcedMeter;
        private SpellResearchClock research;
        private bool discovered;

        protected override bool Run()
        {
            SpellStudyTrack track = SpellStudy.TrackFor(Actor);
            if (track == null)
            {
                return false;
            }

            if (SpellKnowledge.Eligible(Actor).Count == 0)
            {
                SpellKnowledge.Notify(Actor, "Arcana.Study.NoEligible");
                return false;
            }
            studiedName = track.Skill;
            if (track.Occult == OccultTypes.Genie) GenieMagicRegistration.Register();
            Actor.SkillManager.AddElement(track.Skill);
            studied = Actor.SkillManager.GetElement(track.Skill);
            if (studied == null || !Actor.RouteToSlot(Target, (Slot)BenchSlot))
            {
                return false;
            }

            StandardEntry();
            try
            {
                AcquireStateMachine("AlchemyStation");
                SetActor("x", Actor);
                SetActor("alchemyStation", Target);
                SetParameter("ProductVersion", ProductVersion.EP7);
                EnterState("x", "EnterAlchemyBench");
                AddPersistentScriptEventHandler(PageOffEvent, HidePage);
                AddPersistentScriptEventHandler(PageOnEvent, ShowPage);
                Candles(StartCandles);
                AnimateSim("ResearchingAlchemy");
                BeginStudy();
                SpellResearchSettings settings = SpellStudy.Settings;
                research.Start(SpellStudy.Speed(Actor, settings), settings.IntervalMinutes);
                bool finished = DoLoop(ExitReason.Default & ~ExitReason.MaxSkillPointsReached, new InsideLoopFunction(StudyLoop), mCurrentStateMachine);
                EndStudy();
                AnimateSim(discovered ? "DiscoveryNewRecipe" : "ExitResearch");
                Candles(StopCandles);
                Candles(Extinguish);
                return finished;
            }
            finally
            {
                EndStudy();
                StandardExit();
            }
        }

        public override void Cleanup()
        {
            try
            {
                EndStudy();
                if (Target != null)
                {
                    Candles(StopCandles);
                }
            }
            finally
            {
                base.Cleanup();
            }
        }

        private void Candles(MethodInfo effect)
        {
            if (effect != null)
            {
                effect.Invoke(Target, null);
            }
        }

        private void BeginStudy()
        {
            appliedRate = SpellStudy.SkillPointsPerSimMinute(Actor);
            Actor.SkillManager.StartSkillGain(studiedName, appliedRate);
            Skill skill = Actor.SkillManager.GetElement(studiedName);
            if (studiedName == SkillNames.FairyMagic && skill != null && ShowMeter != null && HideMeter != null)
            {
                ShowMeter.Invoke(Actor.SkillManager, new object[] { skill, false });
                forcedMeter = true;
            }
        }

        private void EndStudy()
        {
            if (appliedRate <= 0f)
            {
                return;
            }

            float rate = appliedRate;
            appliedRate = 0f;
            bool hide = forcedMeter;
            forcedMeter = false;
            if (Actor != null && Actor.SkillManager != null)
            {
                Actor.SkillManager.StopSkillGain(studiedName, rate);
                if (hide) HideMeter.Invoke(Actor.SkillManager, null);
            }
        }

        private void StudyLoop(StateMachineClient client, LoopData loop)
        {
            SpellResearchSettings settings = SpellStudy.Settings;
            if (discovered || !research.Advance(loop.mLifeTime, SpellStudy.Speed(Actor, settings), settings.IntervalMinutes)) return;
            List<SpellEntry> candidates = SpellKnowledge.Eligible(Actor);
            if (candidates.Count == 0)
            {
                SpellKnowledge.Notify(Actor, "Arcana.Study.NoEligible");
                Actor.AddExitReason(ExitReason.Finished);
                return;
            }
            if (!RandomUtil.RandomChance(settings.DiscoveryChancePercent)) return;
            SpellEntry learned = candidates[RandomUtil.GetInt(0, candidates.Count - 1)];
            if (!SpellKnowledge.Learn(Actor, learned))
            {
                SpellKnowledge.Notify(Actor, "Arcana.Study.CannotSave");
                Actor.AddExitReason(ExitReason.Finished);
                return;
            }
            discovered = true;
            SpellKnowledge.Notify(Actor, "Arcana.Study.Discovered", Actor.FullName, SpellKnowledge.Name(learned));
            Actor.AddExitReason(ExitReason.Finished);
        }

        private void HidePage(StateMachineClient client, IEvent evt)
        {
            Target.SetGeometryState("pageOff");
        }

        private void ShowPage(StateMachineClient client, IEvent evt)
        {
            Target.SetGeometryState("pageOn");
        }

        public sealed class Definition : InteractionDefinition<Sim, AlchemyStation, StudySpells>
        {
            protected override string GetInteractionName(Sim actor, AlchemyStation target, InteractionObjectPair pair)
            {
                return JournalText.Get("Arcana.Station.StudySpells");
            }

            protected override bool Test(Sim actor, AlchemyStation target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                if (autonomous || SpellStudy.TrackFor(actor) == null) return false;
                if (SpellKnowledge.Eligible(actor).Count == 0)
                {
                    tooltip = delegate { return JournalText.Get("Arcana.Study.NoEligible"); };
                    return false;
                }
                return true;
            }
        }
    }
}
