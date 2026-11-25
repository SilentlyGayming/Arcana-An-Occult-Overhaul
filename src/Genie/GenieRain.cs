using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Interfaces;
using Sims3.Gameplay.Objects.Environment;
using Sims3.Gameplay.Seasons;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace.Enums;
using Sims3.SimIFace;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    [Persistable]
    internal sealed class FortuneRainState
    {
        internal ulong StoneId;
        internal long RequestedAt;
        internal long ExpiresAt;
        internal long StartedAt = -1L;

        internal bool Pending { get { return StartedAt < 0L; } }

        internal bool TryStart(bool raining, bool rainQueued, long weatherStart, long now)
        {
            if (!Pending || now >= ExpiresAt || !raining || rainQueued || weatherStart < RequestedAt) return false;
            StartedAt = weatherStart;
            return true;
        }

        internal bool Active(bool raining, long weatherStart, long now)
        {
            return !Pending && now < ExpiresAt && raining && weatherStart == StartedAt;
        }

        internal bool Expired(bool raining, long weatherStart, long now)
        {
            return now >= ExpiresAt || (!Pending && !Active(raining, weatherStart, now));
        }
    }

    internal static class GenieRain
    {
        private const float WindowHours = 12f;
        [PersistableStatic] internal static FortuneRainState sRain;
        [Persistable(false)] private static WeatherStone installed;
        [Persistable(false)] private static EventListener started;
        [Persistable(false)] private static EventListener stopped;
        [Persistable(false)] private static EventListener instantiated;
        [Persistable(false)] private static EventListener enteredLot;
        [Persistable(false)] private static EventListener roomChanged;
        [Persistable(false)] private static AlarmHandle exposure = AlarmHandle.kInvalidHandle;

        internal static bool Active
        {
            get
            {
                WeatherStone stone = WeatherStone.Instance;
                return sRain != null && stone != null && !stone.HasBeenDestroyed && stone.ObjectId.Value == sRain.StoneId && SeasonsManager.Enabled &&
                    sRain.Active(SeasonsManager.CurrentWeather == Weather.Rain,
                        SeasonsManager.CurrentWeatherStartTime.Ticks, SimClock.CurrentTicks);
            }
        }

        internal static void Install(WeatherStone stone)
        {
            if (stone == null || stone.HasBeenDestroyed || stone == installed) return;
            StoneAccess.BindTuning(stone, SummonStoneRain.Singleton, "SummonWeather");
            StoneAccess.BindTuning(stone, SummonStoneRainWith.Singleton, "SummonWeatherWith");
            stone.AddInteraction(SummonStoneRain.Singleton, true);
            stone.AddInteraction(SummonStoneRain.JoinSingleton, true);
            stone.AddInteraction(SummonStoneRainWith.Singleton, true);
            foreach (InteractionObjectPair pair in stone.Interactions)
                if (pair.InteractionDefinition is SummonStoneRain.Definition || pair.InteractionDefinition is SummonStoneRainWith.Definition)
                    pair.AddTuning();
            installed = stone;
        }

        internal static void Begin(WeatherStone stone, Sim genie)
        {
            End(false);
            sRain = new FortuneRainState();
            sRain.StoneId = stone.ObjectId.Value;
            sRain.RequestedAt = SimClock.CurrentTicks;
            sRain.ExpiresAt = SimClock.CurrentTicks + SimClock.ConvertToTicks(WindowHours, TimeUnit.Hours);
            Listen();
            float[] duration = StoneAccess.Tuning("kWeatherStoneSummonedWeatherDuration");
            SeasonsManager.ForceRequestWeather(Weather.Rain, RandomUtil.GetFloat(duration[0], duration[1]));
            StoneAccess.Notify(stone, genie, "Arcana.Genie.Rain.Summoned", Sims3.UI.StyledNotification.NotificationStyle.kGameMessagePositive);
        }

        internal static void Restore()
        {
            if (sRain != null) Listen();
            Tick();
        }

        private static void Listen()
        {
            if (started == null) started = EventTracker.AddListener(EventTypeId.kWeatherStarted, OnStarted);
            if (stopped == null) stopped = EventTracker.AddListener(EventTypeId.kWeatherStopped, OnStopped);
            if (instantiated == null) instantiated = EventTracker.AddListener(EventTypeId.kSimInstantiated, OnExposureChanged);
            if (enteredLot == null) enteredLot = EventTracker.AddListener(EventTypeId.kEventSimMovedToLot, OnExposureChanged);
            if (roomChanged == null) roomChanged = EventTracker.AddListener(EventTypeId.kRoomChanged, OnExposureChanged);
            if (exposure == AlarmHandle.kInvalidHandle)
                exposure = AlarmManager.Global.AddAlarmRepeating(1f, TimeUnit.Minutes, RefreshExposure, 1f, TimeUnit.Minutes,
                    "Arcana Fortune's Rain exposure", AlarmType.NeverPersisted, null);
        }

        private static ListenerAction OnExposureChanged(Event e)
        {
            Sim sim = e.Actor as Sim ?? e.TargetObject as Sim;
            if (sim != null && Active) JournalBootstrap.RunLater(delegate { if (Active) RainLuck.Apply(sim); });
            return ListenerAction.Keep;
        }

        private static void RefreshExposure()
        {
            try { Tick(); }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Refreshing Fortune's Rain exposure", error); }
        }

        private static ListenerAction OnStarted(Event e)
        {
            WeatherEvent weather = e as WeatherEvent;
            if (sRain == null || weather == null) return ListenerAction.Keep;
            if (sRain.Pending && weather.Weather == Weather.Rain)
                sRain.TryStart(true, false, SeasonsManager.CurrentWeatherStartTime.Ticks, SimClock.CurrentTicks);
            else if (sRain.Pending)
            {
                End(true);
                return ListenerAction.Keep;
            }
            Tick();
            return ListenerAction.Keep;
        }

        private static ListenerAction OnStopped(Event e)
        {
            WeatherEvent weather = e as WeatherEvent;
            if (sRain != null && !sRain.Pending && weather != null && weather.Weather == Weather.Rain) End(true);
            return ListenerAction.Keep;
        }

        internal static void Tick()
        {
            WeatherStone stone = WeatherStone.Instance;
            Install(stone);
            if (sRain == null) return;
            if (!SeasonsManager.Enabled || stone == null || stone.HasBeenDestroyed || stone.ObjectId.Value != sRain.StoneId)
            {
                End(false);
                return;
            }
            bool raining = SeasonsManager.CurrentWeather == Weather.Rain;
            long weatherStart = SeasonsManager.CurrentWeatherStartTime.Ticks;
            sRain.TryStart(raining, SeasonsManager.ForcedWeather == Weather.Rain, weatherStart, SimClock.CurrentTicks);
            if (sRain.Expired(raining, weatherStart, SimClock.CurrentTicks))
            {
                End(true);
                return;
            }
            if (Active) RainLuck.Refresh();
        }

        private static void End(bool restStone)
        {
            WeatherStone stone = WeatherStone.Instance;
            bool ownsStone = sRain != null && stone != null && !stone.HasBeenDestroyed && stone.ObjectId.Value == sRain.StoneId;
            sRain = null;
            Clear();
            if (restStone && ownsStone && StoneAccess.IsFortune(stone) &&
                stone.CurrentWeatherStoneState == WeatherStone.WeatherStoneState.AfterSuccessfulSummon) StoneAccess.Rest(stone);
        }

        internal static void Clear()
        {
            WitchCastingLifecycle.RemoveListener(ref started);
            WitchCastingLifecycle.RemoveListener(ref stopped);
            WitchCastingLifecycle.RemoveListener(ref instantiated);
            WitchCastingLifecycle.RemoveListener(ref enteredLot);
            WitchCastingLifecycle.RemoveListener(ref roomChanged);
            WitchCastingLifecycle.RemoveAlarm(ref exposure);
            installed = null;
        }

        internal static void ResetSaved()
        {
            sRain = null;
        }

        internal static void EndForWeatherReplacement() { End(false); }
    }

    internal static class StoneAccess
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private delegate bool StartSummoning(Sim actor, WeatherStone.SummonWeatherType weather, bool join);
        private delegate bool DecideOutcome();
        private delegate void StoneAction();
        private delegate void ChangeStoneState(bool yield, WeatherStone.WeatherStoneState state);
        private delegate string Localize(bool female, string key, object[] arguments);
        internal const WeatherStone.SummonWeatherType Fortune = (WeatherStone.SummonWeatherType)0x41524347;
        private static readonly Dictionary<string, MethodInfo> methods = new Dictionary<string, MethodInfo>();
        private static readonly Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>();
        private static Localize localize;

        internal static void BindTuning(WeatherStone stone, InteractionDefinition definition, string nativeName)
        {
            Type nativeType = typeof(WeatherStone).GetNestedType(nativeName, Any);
            FieldInfo singleton = nativeType == null ? null : nativeType.GetField("Singleton", Any);
            InteractionDefinition native = singleton == null ? null : singleton.GetValue(null) as InteractionDefinition;
            if (native == null) throw new InvalidOperationException("Missing native Weather Stone interaction: " + nativeName);
            InteractionTuning tuning = new InteractionObjectPair(native, stone).Tuning;
            if (tuning == null) throw new InvalidOperationException("Missing native Weather Stone tuning: " + nativeName);
            InteractionTuning copy = WeatherStoneTuning.ForGenies(tuning);
            InteractionObjectPair.sTuningCache[new Pair<Type, Type>(definition.GetType(), stone.GetType())] = copy;
        }

        private static Delegate Bind(Type signature, WeatherStone stone, string name)
        {
            return NativeWeatherCalls.Bind(signature, stone, Method(name));
        }

        private static MethodInfo Method(string name)
        {
            MethodInfo method;
            if (methods.TryGetValue(name, out method)) return method;
            method = typeof(WeatherStone).GetMethod(name, Any);
            if (method == null) throw new InvalidOperationException("Missing Weather Stone method: " + name);
            methods[name] = method;
            return method;
        }

        private static FieldInfo Field(string name)
        {
            FieldInfo field;
            if (fields.TryGetValue(name, out field)) return field;
            field = typeof(WeatherStone).GetField(name, Any);
            if (field == null) throw new InvalidOperationException("Missing Weather Stone field: " + name);
            fields[name] = field;
            return field;
        }

        internal static float[] Tuning(string name)
        {
            return (float[])Field(name).GetValue(null);
        }

        internal static bool Start(WeatherStone stone, Sim sim, bool join)
        {
            if (!GenieAbilities.IsGenie(sim) || sim.SimDescription.ChildOrBelow || !SeasonsManager.Enabled ||
                stone == null || stone.HasBeenDestroyed || (!join && stone.CurrentWeatherStoneState != WeatherStone.WeatherStoneState.ReadyForSummoning)) return false;
            StartSummoning start = (StartSummoning)Bind(typeof(StartSummoning), stone, "TryStartSummoningWeather");
            return start(sim, Fortune, join);
        }

        internal static bool Roll(WeatherStone stone, Sim actor)
        {
            DecideOutcome decide = (DecideOutcome)Bind(typeof(DecideOutcome), stone, "RollForSuccessOrFailure");
            bool success = decide();
            if (success)
            {
                EventListener listener = Field("mWeatherChangedEventListener").GetValue(stone) as EventListener;
                if (listener != null) EventTracker.RemoveListener(listener);
                Field("mWeatherChangedEventListener").SetValue(stone, null);
                GenieRain.Begin(stone, actor);
            }
            else Notify(stone, actor, "Arcana.Genie.Rain.Failed", Sims3.UI.StyledNotification.NotificationStyle.kGameMessageNegative);
            return success;
        }

        internal static bool TryWeather(Sim actor, out WeatherStone.SummonWeatherType kind, out string weather)
        {
            kind = default(WeatherStone.SummonWeatherType);
            weather = "";
            if (actor == null || actor.HasBeenDestroyed || actor.SimDescription == null) return false;
            OccultTypes types = actor.OccultManager == null ? OccultTypes.None : actor.OccultManager.CurrentOccultTypes;
            if (GenieAbilities.IsGenie(actor)) { kind = Fortune; weather = "Fortune's Rain"; }
            else if ((types & OccultTypes.Witch) != 0) { kind = WeatherStone.SummonWeatherType.BewitchingRain; weather = "Bewitching Rain"; }
            else if ((types & OccultTypes.Fairy) != 0) { kind = WeatherStone.SummonWeatherType.RevivingSprinkle; weather = "Reviving Sprinkle"; }
            else if ((types & OccultTypes.Vampire) != 0) { kind = WeatherStone.SummonWeatherType.EclipsingFog; weather = "Eclipsing Fog"; }
            else if ((types & OccultTypes.Werewolf) != 0) { kind = WeatherStone.SummonWeatherType.HuntersStorm; weather = "Hunter's Storm"; }
            else return false;
            return true;
        }

        internal static bool ActivateSelectedWeather(WeatherStone stone, Sim actor, out string weather, out string reason)
        {
            weather = "";
            reason = "";
            WeatherStone.SummonWeatherType kind;
            if (!TryWeather(actor, out kind, out weather)) return false;
            if (actor.SimDescription.ChildOrBelow) { reason = "Select a Teen-or-older supernatural Sim."; return false; }
            if (!SeasonsManager.Enabled) { reason = "Seasons weather is unavailable in this world."; return false; }
            if (stone == null || stone.HasBeenDestroyed) { reason = "This world has no valid Weather Stone."; return false; }
            WeatherStone.WeatherStoneState state = stone.CurrentWeatherStoneState;
            if (state != WeatherStone.WeatherStoneState.ReadyForSummoning && state != WeatherStone.WeatherStoneState.Resting &&
                state != WeatherStone.WeatherStoneState.AfterSuccessfulSummon)
            { reason = "The Weather Stone is busy. Finish its current interaction first."; return false; }
            foreach (Sim user in stone.ActorsUsingMe)
                if (user != null) { reason = "The Weather Stone is in use; the cheat will not interrupt it."; return false; }
            if ((GenieRain.sRain != null && kind == Fortune) ||
                (state == WeatherStone.WeatherStoneState.AfterSuccessfulSummon &&
                (WeatherStone.SummonWeatherType)Field("mSummonWeatherType").GetValue(stone) == kind))
            { reason = weather + " is already pending or active. Use ActivateWeatherStone status."; return false; }
            EventListener listener = Field("mWeatherChangedEventListener").GetValue(stone) as EventListener;
            if (listener != null) EventTracker.RemoveListener(listener);
            Field("mWeatherChangedEventListener").SetValue(stone, null);
            if (GenieRain.sRain != null) GenieRain.EndForWeatherReplacement();
            if (state == WeatherStone.WeatherStoneState.AfterSuccessfulSummon) Rest(stone);
            Field("mSummonWeatherType").SetValue(stone, kind);
            Field("mSummoningSimDescriptionId").SetValue(stone, actor.SimDescription.SimDescriptionId);
            Field("mUseExtremeFailure").SetValue(stone, false);
            ChangeStoneState change = (ChangeStoneState)Bind(typeof(ChangeStoneState), stone, "AnimateChangeWeatherStoneState");
            change(false, WeatherStone.WeatherStoneState.AfterSuccessfulSummon);
            if (kind == Fortune)
            {
                GenieRain.Begin(stone, actor);
                StopElectrifiedEffect(stone);
                GenieRain.Tick();
            }
            return true;
        }

        internal static void Notify(WeatherStone stone, Sim actor, string key, Sims3.UI.StyledNotification.NotificationStyle style)
        {
            if (actor == null || !actor.IsSelectable) return;
            Sims3.UI.StyledNotification.Show(new Sims3.UI.StyledNotification.Format(JournalText.Format(key, actor.FullName), stone.ObjectId, actor.ObjectId, style));
        }

        internal static void StoppedEarly(WeatherStone stone)
        {
            StoneAction stop = (StoneAction)Bind(typeof(StoneAction), stone, "OnSummoningStoppedEarly");
            stop();
            foreach (Sim sim in stone.ActorsUsingMe)
            {
                if (sim != Summoner(stone) && sim.CurrentInteraction is SummonStoneRain) sim.AddExitReason(ExitReason.CanceledByScript);
            }
        }

        internal static void StopElectrifiedEffect(WeatherStone stone)
        {
            StoneAction stop = (StoneAction)Bind(typeof(StoneAction), stone, "StopElectrifiedEffect");
            stop();
        }

        internal static void Rest(WeatherStone stone)
        {
            ChangeStoneState change = (ChangeStoneState)Bind(typeof(ChangeStoneState), stone, "AnimateChangeWeatherStoneState");
            change(false, WeatherStone.WeatherStoneState.Resting);
        }

        internal static bool IsFortune(WeatherStone stone)
        {
            return (WeatherStone.SummonWeatherType)Field("mSummonWeatherType").GetValue(stone) == Fortune;
        }

        internal static bool Summoning(WeatherStone stone)
        {
            return stone.IsSummoning && IsFortune(stone);
        }

        internal static Sim Summoner(WeatherStone stone)
        {
            ulong id = (ulong)Field("mSummoningSimDescriptionId").GetValue(stone);
            return id == 0UL ? null : SimDescription.GetCreatedSim(id);
        }

        internal static string NativeText(bool female, string key, params object[] args)
        {
            if (localize == null) localize = (Localize)NativeWeatherCalls.Bind(typeof(Localize), null, Method("LocalizeString"));
            return localize(female, key, args);
        }
    }

    internal static class NativeWeatherCalls
    {
        internal static Delegate Bind(Type signature, object target, MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException("method");
            return method.IsStatic ? Delegate.CreateDelegate(signature, method) : Delegate.CreateDelegate(signature, target, method);
        }
    }

    internal static class WeatherStoneTuning
    {
        private static readonly MethodInfo Clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static InteractionTuning ForGenies(InteractionTuning source)
        {
            if (source == null) throw new ArgumentNullException("source");
            InteractionTuning copy = (InteractionTuning)Clone.Invoke(source, null);
            if (source.Availability != null)
            {
                copy.Availability = (Availability)Clone.Invoke(source.Availability, null);
                copy.Availability.OccultRestrictionType = OccultRestrictionType.Inclusive;
                copy.Availability.OccultRestrictions = Sims3.UI.Hud.OccultTypes.Genie;
            }
            return copy;
        }
    }

    [Persistable]
    internal sealed class StoneSummonProgress
    {
        internal const ExitReason Interruptions = ExitReason.Default &
            ~(ExitReason.HigherPriorityNext | ExitReason.Finished | ExitReason.StageComplete);
        private bool joining;
        private long decisionAt;
        private long finishAt;
        private bool decided;

        public StoneSummonProgress() { }
        internal StoneSummonProgress(bool join, long decision)
        {
            joining = join;
            decisionAt = decision;
        }
        internal bool Decided { get { return decided; } }
        internal bool ShouldRoll(long now) { return !joining && !decided && now >= decisionAt; }
        internal bool Finished(long now) { return decided && now >= finishAt; }
        internal void Outcome(long finish)
        {
            if (decided) return;
            decided = true;
            finishAt = finish;
        }
        internal static bool Interrupted(ExitReason reasons) { return (reasons & Interruptions) != ExitReason.None; }
    }

    public sealed class SummonStoneRain : Interaction<Sim, WeatherStone>
    {
        public static readonly InteractionDefinition Singleton = new Definition(false);
        public static readonly InteractionDefinition JoinSingleton = new Definition(true);

        private bool join;
        private StoneSummonProgress progress;
        private VisualEffect[] arcs;
        private bool animationExited;

        public override string GetInteractionName()
        {
            Definition definition = InteractionDefinition as Definition;
            return Name(Actor.IsFemale, definition != null && definition.Join, false);
        }

        internal static string Name(bool female, bool joining, bool withOthers)
        {
            return StoneAccess.NativeText(female, withOthers ? "SummonWeatherWithInteractionName" :
                joining ? "JoinSummonWeatherInteractionName" : "SummonWeatherInteractionName", JournalText.Get("Arcana.Genie.Rain.Name"));
        }

        internal static bool Available(Sim actor, WeatherStone target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
        {
            if (autonomous || !GenieAbilities.IsGenie(actor) || target == null || target.HasBeenDestroyed ||
                actor.SimDescription.ChildOrBelow || !SeasonsManager.Enabled) return false;
            WeatherStone.WeatherStoneState state = target.CurrentWeatherStoneState;
            if (state == WeatherStone.WeatherStoneState.ReadyForSummoning) return true;
            string key = state == WeatherStone.WeatherStoneState.Resting || state == WeatherStone.WeatherStoneState.AfterFailureSummon
                ? "StoneRestingTooltip" : state == WeatherStone.WeatherStoneState.AfterSuccessfulSummon
                ? "StoneCurrentSummoningWeatherTooltip" : state == WeatherStone.WeatherStoneState.SummoningWeather
                ? "TryingToSummonTooltip" : null;
            if (key != null) tooltip = delegate { return StoneAccess.NativeText(actor.IsFemale, key); };
            return false;
        }

        protected override bool Run()
        {
            bool resetting = false;
            try { return Summon(); }
            catch (ResetException) { resetting = true; throw; }
            catch (Exception error)
            {
                JournalBootstrap.Report("Summoning Fortune's Rain", error);
                return false;
            }
            finally
            {
                if (!resetting) ExitAnimation();
            }
        }

        private bool Summon()
        {
            Definition definition = InteractionDefinition as Definition;
            join = definition != null && definition.Join;
            float[] distance = StoneAccess.Tuning("kSummonWeatherRouteDistance");
            if (!Actor.RoutingComponent.RouteToObjectRadialRange(Target, distance[0], distance[1],
                new Route.RouteOption[] { Route.RouteOption.DoNotEmitDegenerateRoutesForRadialRangeGoals })) return false;
            if (!StoneAccess.Start(Target, Actor, join))
            {
                if (join || !StoneAccess.Start(Target, Actor, true))
                {
                    Actor.PlayRouteFailure((Sims3.Gameplay.Interfaces.IGameObject)StoneAccess.Summoner(Target));
                    return false;
                }
                join = true;
            }
            StandardEntry();
            EnterStateMachine("SummonWeather", "Enter", "x");
            StartArcs();
            AnimateSim("BewitchingRain");
            BeginCommodityUpdates();
            float[] outcome = StoneAccess.Tuning("kSummonWeatherDetermineOutcomeTime");
            float minutes = join ? outcome[1] + 10f : RandomUtil.GetFloat(outcome[0], outcome[1]);
            progress = new StoneSummonProgress(join, SimClock.CurrentTicks + SimClock.ConvertToTicks(minutes, TimeUnit.Minutes));
            bool finished = SummonLoop();
            EndCommodityUpdates(finished);
            StandardExit();
            switch (Target.CurrentWeatherStoneState)
            {
                case WeatherStone.WeatherStoneState.AfterFailureSummon:
                    AnimateSim("FailureNormal");
                    if (!OthersSummoning()) StoneAccess.Rest(Target);
                    break;
                case WeatherStone.WeatherStoneState.AfterSuccessfulSummon:
                    AnimateSim("Success");
                    if (!join) StoneAccess.StopElectrifiedEffect(Target);
                    break;
                default:
                    if (!join) StoneAccess.StoppedEarly(Target);
                    AnimateSim("FailureNormal");
                    break;
            }
            return finished;
        }

        private void ExitAnimation()
        {
            if (mCurrentStateMachine == null || !mCurrentStateMachine.IsValid) return;
            try
            {
                AnimateSim("Exit");
                animationExited = true;
            }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.Report("Ending the Fortune's Rain animation", error); }
        }

        public override void Cleanup()
        {
            try
            {
                if (!join && StandardEntryCalled && Target != null && !Target.HasBeenDestroyed &&
                    Target.CurrentWeatherStoneState == WeatherStone.WeatherStoneState.SummoningWeather) StoneAccess.StoppedEarly(Target);
            }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.Report("Releasing the Weather Stone", error); }
            finally
            {
                try { StopArcs(); }
                finally
                {
                    try
                    {
                        if (animationExited && mCurrentStateMachine != null)
                        {
                            mCurrentStateMachine.Dispose();
                            mCurrentStateMachine = null;
                        }
                    }
                    finally { base.Cleanup(); }
                }
            }
        }

        private bool SummonLoop()
        {
            while (!StoneSummonProgress.Interrupted(Actor.ExitReason))
            {
                if (Target.HasBeenDestroyed || !StoneAccess.IsFortune(Target)) return false;
                WeatherStone.WeatherStoneState state = Target.CurrentWeatherStoneState;
                if (state == WeatherStone.WeatherStoneState.SummoningWeather)
                {
                    if (progress.ShouldRoll(SimClock.CurrentTicks))
                    {
                        bool success = StoneAccess.Roll(Target, Actor);
                        Decided(success);
                        foreach (Sim sim in Target.ActorsUsingMe)
                        {
                            SummonStoneRain other = sim.CurrentInteraction as SummonStoneRain;
                            if (other != null) other.Decided(success);
                        }
                    }
                }
                else if (state == WeatherStone.WeatherStoneState.AfterSuccessfulSummon || state == WeatherStone.WeatherStoneState.AfterFailureSummon)
                {
                    if (!progress.Decided) Decided(state == WeatherStone.WeatherStoneState.AfterSuccessfulSummon);
                    if (progress.Finished(SimClock.CurrentTicks)) return true;
                }
                else return false;
                if (Actor.WaitForExitReason(1f, StoneSummonProgress.Interruptions)) return false;
                UpdateLoop();
            }
            return false;
        }

        private void Decided(bool success)
        {
            if (progress == null || progress.Decided) return;
            float[] time = StoneAccess.Tuning(success ? "kSummonWeatherSuccessContinueSummoningTime" : "kSummonWeatherFailureContinueSummoningTime");
            progress.Outcome(SimClock.CurrentTicks + SimClock.ConvertToTicks(RandomUtil.GetFloat(time[0], time[1]), TimeUnit.Minutes));
        }

        private bool OthersSummoning()
        {
            foreach (Sim sim in Target.ActorsUsingMe)
            {
                InteractionInstance other = sim.CurrentInteraction;
                if (sim != Actor && other is SummonStoneRain && !other.StandardExitCalled) return true;
            }
            return false;
        }

        private void StartArcs()
        {
            if (arcs != null) return;
            Vector3 stone = Target.GetPositionOfSlot(Slot.FXJoint_0);
            arcs = new VisualEffect[2];
            arcs[0] = Arc("ep8WeatherStoneArcHandRight", Sim.FXJoints.RightHand, stone, "weatherStoneWitchRightFx");
            arcs[1] = Arc("ep8WeatherStoneArcHandLeft", Sim.FXJoints.LeftHand, stone, "weatherStoneWitchLeftFx");
        }

        private VisualEffect Arc(string effect, Slot hand, Vector3 stone, string actor)
        {
            VisualEffect arc = VisualEffect.Create(effect);
            arc.ParentTo(Actor, hand);
            arc.SetAttractorLocation(stone, 1f);
            SetEffectActor(actor, arc);
            return arc;
        }

        private void StopArcs()
        {
            if (arcs == null) return;
            foreach (VisualEffect arc in arcs)
            {
                if (arc == null) continue;
                arc.Stop(VisualEffect.TransitionType.HardTransition);
                arc.Dispose();
            }
            arcs = null;
        }

        [Persistable]
        public sealed class Definition : InteractionDefinition<Sim, WeatherStone, SummonStoneRain>,
            Sim.IAskToJoinCustomTest, Sim.IAskToJoinCustomIOP, Sim.IAskToJoinCustomName
        {
            internal bool Join;

            public Definition() { }

            internal Definition(bool join)
            {
                Join = join;
            }

            protected override string GetInteractionName(Sim actor, WeatherStone target, InteractionObjectPair pair)
            {
                return Name(actor.IsFemale, Join, false);
            }

            protected override bool Test(Sim actor, WeatherStone target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                if (!Join) return Available(actor, target, autonomous, ref tooltip);
                return !autonomous && GenieAbilities.IsGenie(actor) && !actor.SimDescription.ChildOrBelow && target != null &&
                    !target.HasBeenDestroyed && StoneAccess.Summoning(target) && StoneAccess.Summoner(target) != actor;
            }

            public bool AskToJoinTest(Sim actor, Sim target, InteractionObjectPair interaction, bool isAskToJoin)
            {
                return !isAskToJoin && GenieAbilities.IsGenie(actor) && !actor.SimDescription.ChildOrBelow;
            }

            public InteractionObjectPair GetInteractionForJoin(IActor actor, Sim target, InteractionObjectPair interaction, bool isAskToJoin)
            {
                return new InteractionObjectPair(JoinSingleton, interaction.Target);
            }

            public string GetInteractionNameForJoin(IActor actor, IGameObject target, InteractionObjectPair interaction)
            {
                return Name(actor.IsFemale, true, false);
            }
        }
    }

    public sealed class SummonStoneRainWith : Interaction<Sim, WeatherStone>
    {
        public static readonly InteractionDefinition Singleton = new Definition();

        private static bool Candidate(Sim sim, Sim actor)
        {
            return sim != null && sim != actor && !sim.HasBeenDestroyed && GenieAbilities.IsGenie(sim) &&
                !sim.SimDescription.ChildOrBelow;
        }

        protected override bool Run()
        {
            List<Sim> followers = new List<Sim>();
            if (SelectedObjects != null)
                foreach (object selected in SelectedObjects)
                {
                    Sim sim = selected as Sim;
                    if (Candidate(sim, Actor)) followers.Add(sim);
                }
            if (Target.LotCurrent != Actor.LotCurrent)
            {
                Route route = Actor.CreateRoute();
                Target.LotCurrent.PlanToLot(route);
                if (!Actor.DoRouteWithFollowers(route, followers)) return false;
                Lot.ValidateFollowers(followers);
            }
            foreach (Sim sim in followers)
            {
                if (sim.LotCurrent != Target.LotCurrent) continue;
                InteractionPriority priority = sim.IsSelectable ? GetPriority() : new InteractionPriority(InteractionPriorityLevel.NonCriticalNPCBehavior);
                sim.InteractionQueue.AddNext(SummonStoneRain.Singleton.CreateInstance(Target, sim, priority, Autonomous, CancellableByPlayer));
            }
            Actor.InteractionQueue.PushAsContinuation(SummonStoneRain.Singleton, Target, true);
            return true;
        }

        [Persistable]
        public sealed class Definition : InteractionDefinition<Sim, WeatherStone, SummonStoneRainWith>
        {
            protected override bool Test(Sim actor, WeatherStone target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                return SummonStoneRain.Available(actor, target, autonomous, ref tooltip) && actor.LotCurrent != null &&
                    actor.LotCurrent.DoesSimExist(delegate(Sim sim) { return Candidate(sim, actor); });
            }

            protected override string GetInteractionName(Sim actor, WeatherStone target, InteractionObjectPair pair)
            {
                return SummonStoneRain.Name(actor.IsFemale, false, true);
            }

            public override void PopulatePieMenuPicker(ref InteractionInstanceParameters parameters,
                out List<Sims3.UI.ObjectPicker.TabInfo> listObjs, out List<Sims3.UI.ObjectPicker.HeaderInfo> headers, out int NumSelectableRows)
            {
                NumSelectableRows = -1;
                Sim actor = parameters.Actor as Sim;
                List<Sim> candidates = actor.LotCurrent.GetSims(delegate(Sim sim) { return Candidate(sim, actor); });
                PopulateSimPicker(ref parameters, out listObjs, out headers, candidates, false);
            }
        }
    }

    internal static class RainLuck
    {
        private const float Minutes = 180f;

        internal static void Refresh()
        {
            foreach (Sim sim in Sims3.Gameplay.Queries.GetObjects<Sim>()) Apply(sim);
        }

        internal static void Apply(Sim sim)
        {
            if (sim == null || sim.HasBeenDestroyed || sim.SimDescription == null || sim.BuffManager == null) return;
            try
            {
                if (GenieAbilities.IsGenie(sim)) Give(sim, BuffNames.FeelingLucky);
                else if (RainExposure.ReceivesUnlucky(sim.SimDescription.IsHuman, false, sim.IsOutside,
                    SimTemperature.OutsideSimIsConsideredInside(sim))) Give(sim, BuffNames.FeelingUnlucky);
            }
            catch (Sims3.SimIFace.ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Applying Fortune's Rain luck", error); }
        }

        private static void Give(Sim sim, BuffNames guid)
        {
            BuffInstance existing = sim.BuffManager.GetElement(guid);
            if (existing == null) LuckMoodlet.Add(sim, guid, Minutes, Origin.FromWeatherStone);
            else if (existing.BuffOrigin == Origin.FromWeatherStone && existing.TimeoutCount < Minutes) existing.TimeoutCount = Minutes;
        }
    }

    internal static class RainExposure
    {
        internal static bool ReceivesUnlucky(bool human, bool genie, bool outside, bool consideredInside)
        {
            return human && !genie && outside && !consideredInside;
        }
    }
}
