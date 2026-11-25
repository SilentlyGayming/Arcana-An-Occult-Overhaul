using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Objects.Environment;
using Sims3.Gameplay.EventSystem;

namespace SupernaturalExpanded.Journal
{
    internal static class WeatherStoneRainTests
    {
        private static int checks;
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new Exception(label);
        }

        public static void Main()
        {
            SummoningProgress();
            NativeTuningCopy();
            NativeDelegateCalls();
            OutdoorExposure();
            FortuneRainState pending = new FortuneRainState { StoneId = 7UL, RequestedAt = 100L, ExpiresAt = 1000L };
            Check(pending.Pending, "a successful summon first waits for its weather");
            Check(!pending.Active(true, 90L, 110L), "pre-existing ordinary rain gets no bonuses");
            Check(!pending.TryStart(true, false, 90L, 110L), "old rain cannot claim the new summon");
            Check(!pending.TryStart(true, true, 110L, 110L), "a queued replacement must start before granting benefits");
            Check(!pending.TryStart(false, false, 110L, 110L), "clear weather cannot start Fortune's Rain");
            Check(!pending.Expired(false, 90L, 110L), "fading old weather does not discard a pending summon");
            Check(pending.TryStart(true, false, 120L, 120L), "the requested rain starts the effect");
            Check(pending.Active(true, 120L, 500L), "the same storm retains bonuses");
            Check(!pending.TryStart(true, false, 600L, 600L), "another storm cannot extend the effect");
            Check(pending.Expired(true, 600L, 600L), "consecutive ordinary rain does not inherit bonuses");
            Check(pending.Expired(false, 120L, 500L), "changing to non-rain ends bonuses");
            Check(!pending.Active(true, 120L, 1000L), "the safety deadline excludes its exact boundary");
            Check(pending.Expired(true, 120L, 1000L), "a stuck storm expires");
            FortuneRainState restored = new FortuneRainState { StoneId = pending.StoneId, RequestedAt = pending.RequestedAt,
                ExpiresAt = pending.ExpiresAt, StartedAt = pending.StartedAt };
            Check(restored.Active(true, 120L, 500L), "saved storm identifiers can resume after loading");
            Check(!restored.Active(true, 121L, 500L), "a different restored storm is rejected");
            FortuneRainState stale = new FortuneRainState { RequestedAt = 100L, ExpiresAt = 200L };
            Check(!stale.TryStart(true, false, 200L, 200L) && stale.Expired(false, 0L, 200L), "an expired request cannot arm late");

            CastingRules rules = new CastingRules(Rank.Master, CasterKind.Genie);
            MagicSpell spell = new MagicSpell(0, MagicSpellKind.Spell);
            rules.AtHomeLamp = true;
            rules.FortuneRain = true;
            Check(Math.Abs(rules.GenieRegeneration - 1.75f) < 0.001f, "rain adds 50% regeneration alongside the home bonus");
            Check(Math.Abs(rules.BacklashChance(spell, 60f) - 30f) < 0.001f, "rain reduces a 40% Backlash chance to 30%");
            Check(Math.Abs(rules.BacklashChance(spell, 100f) - 5f) < 0.001f, "rain preserves the Tier I Backlash floor");
            rules.Exhausted = true;
            Check(Math.Abs(rules.GenieRegeneration - 0.875f) < 0.001f, "Wish Exhaustion still halves the combined regeneration");
            rules.Exhausted = false;
            rules.FortuneRain = false;
            Check(Math.Abs(rules.GenieRegeneration - 1.25f) < 0.001f, "ending rain restores the ordinary regeneration");
            Check(Math.Abs(rules.BacklashChance(spell, 60f) - 40f) < 0.001f, "ending rain restores the ordinary Backlash chance");
            rules.Lampbound = true;
            rules.FortuneRain = true;
            Check(Math.Abs(rules.BacklashChance(spell, 60f) - 25.5f) < 0.001f, "rain multiplies Lampbound protection without removing its floor");

            Type stone = typeof(WeatherStone);
            Check(!Enum.IsDefined(typeof(WeatherStone.SummonWeatherType), 0x41524347), "Genie summon ID does not alias a native weather type");
            MethodInfo start = stone.GetMethod("TryStartSummoningWeather", All, null,
                new Type[] { typeof(Sim), typeof(WeatherStone.SummonWeatherType), typeof(bool) }, null);
            Check(start != null && start.ReturnType == typeof(bool), "native summoning/join API signature");
            MethodInfo change = stone.GetMethod("AnimateChangeWeatherStoneState", All, null,
                new Type[] { typeof(bool), typeof(WeatherStone.WeatherStoneState) }, null);
            Check(change != null && change.ReturnType == typeof(void), "native state animation signature");
            foreach (string name in new[] { "OnSummoningStoppedEarly", "StopElectrifiedEffect" })
                Check(stone.GetMethod(name, All, null, Type.EmptyTypes, null) != null, name);
            foreach (string name in new[] { "kSummonWeatherRouteDistance", "kSummonWeatherDetermineOutcomeTime",
                "kSummonWeatherSuccessContinueSummoningTime", "kSummonWeatherFailureContinueSummoningTime", "kWeatherStoneSummonedWeatherDuration" })
            {
                FieldInfo field = stone.GetField(name, All);
                Check(field != null && field.IsStatic && field.FieldType == typeof(float[]), name);
            }
            Check(stone.GetField("mCurrentSummoningSuccessChance", All).FieldType == typeof(float), "success chance remains native tuning");
            Check(stone.GetField("mWeatherChangedEventListener", All).FieldType == typeof(EventListener), "native listener can be replaced locally");
            Check(stone.GetField("mSummoningSimDescriptionId", All).FieldType == typeof(ulong), "native summoner identity");
            Check(stone.GetField("mSummonWeatherType", All).FieldType == typeof(WeatherStone.SummonWeatherType), "native summon identity");
            Check(stone.GetField("mUseExtremeFailure", All).FieldType == typeof(bool), "cheat clears the native failure flag");
            Check(typeof(Buff).GetField("mInfo", All).FieldType == typeof(Buff.BuffData), "native luck data can be reused without global edits");
            MethodInfo add = typeof(BuffManager).GetMethod("AddBuff", All, null,
                new Type[] { typeof(BuffInstance), typeof(BuffNames), typeof(int), typeof(float), typeof(bool),
                    typeof(MoodAxis), typeof(Origin), typeof(bool), typeof(bool) }, null);
            Check(add != null && add.ReturnType == typeof(bool), "luck application retains native manager validation");
            Console.WriteLine("PASS: " + checks + " Weather Stone state and native API checks");
        }

        private static void SummoningProgress()
        {
            Check((ExitReason.Default & ExitReason.HigherPriorityNext) != 0, "old default loop exits when another queued action signals priority");
            Check((ExitReason.Default & ExitReason.Finished) != 0, "old default loop exits on animation completion");
            Check(!StoneSummonProgress.Interrupted(ExitReason.HigherPriorityNext), "another queued action must not abort summoning");
            Check(!StoneSummonProgress.Interrupted(ExitReason.Finished), "animation completion must not decide the weather");
            Check(!StoneSummonProgress.Interrupted(ExitReason.StageComplete), "an unrelated completed stage does not decide the weather");
            Check(!StoneSummonProgress.Interrupted(ExitReason.Finished | ExitReason.HigherPriorityNext), "combined transient signals are ignored");
            foreach (ExitReason reason in new[] { ExitReason.UserCanceled, ExitReason.CanceledByScript, ExitReason.RouteFailed,
                ExitReason.MoodFailure, ExitReason.CancelledByPosture, ExitReason.PriorityOverride, ExitReason.FailedToStart })
                Check(StoneSummonProgress.Interrupted(reason), "genuine interruption remains cancellable: " + reason);
            Check(StoneSummonProgress.Interrupted(ExitReason.Finished | ExitReason.UserCanceled), "transient flags cannot hide player cancellation");

            StoneSummonProgress ritual = new StoneSummonProgress(false, 100L);
            Check(!ritual.ShouldRoll(99L) && !ritual.Finished(99L), "the initial animation cannot finish an undecided ritual");
            Check(ritual.ShouldRoll(100L), "initiator rolls at its tuned absolute deadline");
            Check(ritual.ShouldRoll(200L), "large simulator time steps cannot skip the decision");
            ritual.Outcome(300L);
            Check(ritual.Decided && !ritual.ShouldRoll(200L), "the weather outcome is decided only once");
            Check(!ritual.Finished(299L) && ritual.Finished(300L), "the post-outcome animation keeps its tuned duration");
            ritual.Outcome(900L);
            Check(ritual.Finished(300L), "duplicate notifications do not restart the continuation timer");
            StoneSummonProgress joiner = new StoneSummonProgress(true, 100L);
            Check(!joiner.ShouldRoll(100L) && !joiner.ShouldRoll(long.MaxValue), "a joiner never rolls its own outcome");
            Check(!joiner.Finished(500L), "a joiner's initial timer cannot silently finish summoning");
            joiner.Outcome(600L);
            Check(!joiner.Finished(599L) && joiner.Finished(600L), "joiner follows the initiator's outcome and its own continuation");
        }

        private static void NativeTuningCopy()
        {
            InteractionTuning native = (InteractionTuning)Activator.CreateInstance(typeof(InteractionTuning), true);
            native.Availability = new Availability();
            native.Availability.OccultRestrictionType = OccultRestrictionType.Inclusive;
            native.Availability.OccultRestrictions = Sims3.UI.Hud.OccultTypes.Witch;
            native.mTradeoff = (Tradeoff)Activator.CreateInstance(typeof(Tradeoff), true);
            InteractionTuning genie = WeatherStoneTuning.ForGenies(native);
            Check(!ReferenceEquals(native, genie), "Genie tuning is an owned copy");
            Check(!ReferenceEquals(native.Availability, genie.Availability), "Genie eligibility is copied separately");
            Check(native.Availability.OccultRestrictions == Sims3.UI.Hud.OccultTypes.Witch, "native Witch eligibility is unchanged");
            Check(genie.Availability.OccultRestrictions == Sims3.UI.Hud.OccultTypes.Genie, "only Genies pass custom tuning");
            Check(genie.Availability.OccultRestrictionType == OccultRestrictionType.Inclusive, "Genie eligibility remains inclusive");
            Check(ReferenceEquals(native.mTradeoff, genie.mTradeoff), "native immutable execution tradeoff is carried over");
            Check(ReferenceEquals(native.mChecks, genie.mChecks), "native execution checks are retained");
            foreach (string name in new[] { "SummonWeather", "SummonWeatherWith" })
            {
                Type type = typeof(WeatherStone).GetNestedType(name, All);
                Check(type != null && type.GetField("Singleton", All) != null, "native tuning donor exists: " + name);
            }
        }

        private delegate bool RollCall();
        private delegate void StateCall(bool yield, WeatherStone.WeatherStoneState state);
        private delegate bool StartCall(Sim actor, WeatherStone.SummonWeatherType weather, bool join);
        private delegate string TextCall(bool female, string key, object[] arguments);
        private delegate void EmptyCall();
        private delegate bool BuffCall(BuffInstance buff, BuffNames guid, int mood, float timeout, bool paused,
            MoodAxis axis, Origin origin, bool delay, bool travel);

        private sealed class NativeFixture
        {
            internal bool Called;
            internal WeatherStone.WeatherStoneState State;
            private bool Roll() { Called = true; return true; }
            private void Change(bool yield, WeatherStone.WeatherStoneState state) { State = state; }
            private static string Text(bool female, string key, object[] arguments) { return key + arguments.Length; }
            private void Fail() { throw new InvalidOperationException("native failure"); }
        }

        private static void NativeDelegateCalls()
        {
            NativeFixture fixture = new NativeFixture();
            RollCall roll = (RollCall)NativeWeatherCalls.Bind(typeof(RollCall), fixture, typeof(NativeFixture).GetMethod("Roll", All));
            Check(roll() && fixture.Called, "typed native outcome invokes its bound target");
            StateCall change = (StateCall)NativeWeatherCalls.Bind(typeof(StateCall), fixture, typeof(NativeFixture).GetMethod("Change", All));
            change(false, WeatherStone.WeatherStoneState.AfterSuccessfulSummon);
            Check(fixture.State == WeatherStone.WeatherStoneState.AfterSuccessfulSummon, "enum state arguments use typed dispatch");
            TextCall text = (TextCall)NativeWeatherCalls.Bind(typeof(TextCall), null, typeof(NativeFixture).GetMethod("Text", All));
            Check(text(false, "rain", new object[] { 1 }) == "rain1", "static localization binds without an instance target");
            EmptyCall fail = (EmptyCall)NativeWeatherCalls.Bind(typeof(EmptyCall), fixture, typeof(NativeFixture).GetMethod("Fail", All));
            bool direct = false;
            try { fail(); }
            catch (InvalidOperationException error) { direct = error.Message == "native failure"; }
            Check(direct, "native exceptions propagate directly without reflection wrappers");

            WeatherStone stone = (WeatherStone)RuntimeHelpers.GetUninitializedObject(typeof(WeatherStone));
            RollCall nativeRoll = (RollCall)NativeWeatherCalls.Bind(typeof(RollCall), stone, typeof(WeatherStone).GetMethod("RollForSuccessOrFailure", All));
            Check(nativeRoll.Target == stone, "real native roll binds to the Weather Stone instance");
            StateCall nativeState = (StateCall)NativeWeatherCalls.Bind(typeof(StateCall), stone, typeof(WeatherStone).GetMethod("AnimateChangeWeatherStoneState", All));
            Check(nativeState.Target == stone, "real native state transition binds with enum parameters");
            StartCall nativeStart = (StartCall)NativeWeatherCalls.Bind(typeof(StartCall), stone, typeof(WeatherStone).GetMethod("TryStartSummoningWeather", All));
            Check(nativeStart.Target == stone, "real native summoning entry binds with actor/enum/bool parameters");
            foreach (string name in new[] { "OnSummoningStoppedEarly", "StopElectrifiedEffect" })
                Check(NativeWeatherCalls.Bind(typeof(EmptyCall), stone, typeof(WeatherStone).GetMethod(name, All)).Target == stone,
                    "real native cleanup binds: " + name);
            Check(NativeWeatherCalls.Bind(typeof(TextCall), null, typeof(WeatherStone).GetMethod("LocalizeString", All)).Target == null,
                "real native localization binds as static");
            BuffManager manager = (BuffManager)RuntimeHelpers.GetUninitializedObject(typeof(BuffManager));
            MethodInfo add = typeof(BuffManager).GetMethod("AddBuff", All, null,
                new Type[] { typeof(BuffInstance), typeof(BuffNames), typeof(int), typeof(float), typeof(bool),
                    typeof(MoodAxis), typeof(Origin), typeof(bool), typeof(bool) }, null);
            Check(NativeWeatherCalls.Bind(typeof(BuffCall), manager, add).Target == manager, "real native weather moodlet overload binds to its manager");
        }

        private static void OutdoorExposure()
        {
            Check(RainExposure.ReceivesUnlucky(true, false, true, false), "an exposed ordinary human receives bad luck regardless of household selection");
            Check(!RainExposure.ReceivesUnlucky(true, true, true, false), "an exposed Genie is excluded from bad luck");
            Check(!RainExposure.ReceivesUnlucky(false, false, true, false), "animals are excluded from human bad luck");
            Check(!RainExposure.ReceivesUnlucky(true, false, false, false), "indoor humans are excluded");
            Check(!RainExposure.ReceivesUnlucky(true, false, true, true), "cars and interactions considered indoors use vanilla rain protection");
            Check(typeof(SimTemperature).GetMethod("OutsideSimIsConsideredInside", All, null, new Type[] { typeof(Sim) }, null) != null,
                "native Bewitching Rain exposure helper is available");
            Check(Enum.IsDefined(typeof(EventTypeId), "kSimInstantiated") && Enum.IsDefined(typeof(EventTypeId), "kEventSimMovedToLot") &&
                Enum.IsDefined(typeof(EventTypeId), "kRoomChanged"), "native spawn, lot and room exposure events are available");
        }
    }
}
