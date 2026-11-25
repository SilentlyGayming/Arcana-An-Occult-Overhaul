using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SupernaturalExpanded.Journal;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Objects.Environment;
using Sims3.Gameplay.Seasons;
using Sims3.Gameplay.EventSystem;
using Sims3.SimIFace;
using Sims3.UI.Hud;

class WeatherStoneCheatTests
{
    static int checks;
    static void Check(bool result, string message) { checks++; if (!result) throw new Exception(message); }
    static int Run(params object[] args) { return WeatherStoneCheat.Execute(args); }
    static void Reset()
    {
        WeatherStoneCheat.Stop();
        JournalBootstrap.Queue.Clear(); JournalBootstrap.Errors.Clear();
        Sims3.UI.StyledNotification.Messages.Clear();
        CommandSystem.Commands.Clear(); CommandSystem.Registrations = 0; CommandSystem.Available = true;
        Sim.ActiveActor = new Sim(); Sims3.Gameplay.GameStates.IsLiveState = true;
        WeatherStone.Instance = new WeatherStone();
        SeasonsManager.Enabled = true; SeasonsManager.Requests = 0;
        GenieRain.sRain = null; GenieRain.Active = false; GenieRain.Ends = GenieRain.Listens = GenieRain.Ticks = 0;
        EventTracker.Removed = 0;
    }
    static void Main()
    {
        Reset(); WeatherStoneCheat.Register(); WeatherStoneCheat.Register();
        Check(CommandSystem.Commands.Count == 2 && CommandSystem.Registrations == 2, "canonical name and old alias register once");
        Check(CommandSystem.Commands.ContainsKey("ActivateWeatherStone") && CommandSystem.Commands.ContainsKey("ActiveWeatherStone"), "both spellings remain available");
        Check(!CommandSystem.Commands.ContainsKey("arcanaTestFortuneVfx") && !CommandSystem.Commands.ContainsKey("arcanaForceFortuneRain"), "experimental commands removed");
        CommandSystem.Commands.Remove(WeatherStoneCheat.Alias); WeatherStoneCheat.Register();
        Check(CommandSystem.Registrations == 3 && CommandSystem.Commands.Count == 2, "lost console registration can be restored");
        WeatherStoneCheat.Stop(); Check(CommandSystem.Commands.Count == 0, "world exit unregisters both names");
        Reset(); CommandSystem.Available = false; WeatherStoneCheat.Register();
        Check(CommandSystem.Registrations == 0, "unavailable console is tolerated");

        foreach (OccultTypes type in new[] { OccultTypes.None, OccultTypes.Mermaid, OccultTypes.Ghost })
        {
            Reset(); Sim.ActiveActor.Genie = false; Sim.ActiveActor.OccultManager.CurrentOccultTypes = type;
            Check(Run() == 1, "unsupported Sim is consumed as a quiet successful no-op");
            Check(JournalBootstrap.Queue.Count == 0 && Sims3.UI.StyledNotification.Messages.Count == 0 && JournalBootstrap.Errors.Count == 0,
                  "unsupported Sim receives no notification, queue or error");
            Check(SeasonsManager.Requests == 0 && WeatherStone.Instance.Changes == 0 && GenieRain.sRain == null,
                  "unsupported Sim changes no weather or ownership");
        }
        Reset(); Sim.ActiveActor = null;
        Check(Run() == 1 && JournalBootstrap.Queue.Count == 0 && Sims3.UI.StyledNotification.Messages.Count == 0, "no selection is silent");
        Reset(); Sims3.Gameplay.GameStates.IsLiveState = false;
        Check(Run() == -1 && JournalBootstrap.Queue.Count == 0, "supported activation requires Live Mode");
        Reset(); Check(Run("render", "gold") == -1 && JournalBootstrap.Queue.Count == 0, "render probe arguments are gone");
        Check(Run(42) == -1 && JournalBootstrap.Queue.Count == 0, "invalid arguments cannot activate weather");

        var types = new[] { OccultTypes.None, OccultTypes.Witch, OccultTypes.Fairy, OccultTypes.Vampire, OccultTypes.Werewolf };
        var kinds = new[] { WeatherStone.SummonWeatherType.Fortune, WeatherStone.SummonWeatherType.BewitchingRain,
            WeatherStone.SummonWeatherType.RevivingSprinkle, WeatherStone.SummonWeatherType.EclipsingFog, WeatherStone.SummonWeatherType.HuntersStorm };
        for (int i = 0; i < types.Length; i++)
        {
            Reset(); Sim.ActiveActor.Genie = i == 0; Sim.ActiveActor.OccultManager.CurrentOccultTypes = types[i];
            float chance = WeatherStone.Instance.mCurrentSummoningSuccessChance;
            Check(Run() == 1 && WeatherStone.Instance.Changes == 0, "activation runs on the deferred simulation path");
            JournalBootstrap.Flush();
            Check(WeatherStone.Instance.mSummonWeatherType == kinds[i] && WeatherStone.Instance.CurrentWeatherStoneState == WeatherStone.WeatherStoneState.AfterSuccessfulSummon,
                  "supported supernatural takes the matching success branch");
            Check(!WeatherStone.Instance.mUseExtremeFailure && WeatherStone.Instance.mCurrentSummoningSuccessChance == chance,
                  "guarantee clears the failure flag without changing chance tuning");
            Check(EventTracker.Removed == 1 && WeatherStone.Instance.mWeatherChangedEventListener == null,
                  "previous weather listener is released");
            Check(SeasonsManager.Requests == (i == 0 ? 1 : 0), "only Genie needs the existing ordinary-rain request");
            Check((GenieRain.sRain != null) == (i == 0), "native occult weather does not create Genie ownership");
            int changes = WeatherStone.Instance.Changes;
            Run(); JournalBootstrap.Flush();
            Check(WeatherStone.Instance.Changes == changes, "repeated activation cannot duplicate the current effect");
        }
        Reset(); Run(); JournalBootstrap.Flush();
        Check(GenieRain.sRain.Pending && GenieRain.sRain.StoneId == WeatherStone.Instance.ObjectId.Value,
              "Genie uses genuine pending Fortune ownership");
        Check(GenieRain.sRain.ExpiresAt > GenieRain.sRain.RequestedAt && GenieRain.Listens == 1 && GenieRain.Ticks == 1,
              "existing ownership lifetime and reconciliation remain in use");
        Check(WeatherStone.Instance.ElectrifiedStops == 1, "Genie ritual effect cleanup retained");
        Run("status"); Check(Sims3.UI.StyledNotification.Messages.Last().Contains("pending"), "status distinguishes pending from active weather");
        GenieRain.Active = true; Run("status"); Check(Sims3.UI.StyledNotification.Messages.Last().Contains("active"), "status reports active Fortune state");
        WeatherStoneCheat.Stop(); Check(GenieRain.sRain != null, "unregistering the cheat does not erase saved weather ownership");

        Reset(); Sim captured = Sim.ActiveActor; captured.SimDescription.SimDescriptionId = 99;
        Run(); Sim.ActiveActor = new Sim { Genie = false }; JournalBootstrap.Flush();
        Check(WeatherStone.Instance.mSummoningSimDescriptionId == 99 && SeasonsManager.Requests == 1, "selection is captured when the cheat is entered");
        Reset(); Run(); WeatherStoneCheat.Stop(); JournalBootstrap.Flush();
        Check(SeasonsManager.Requests == 0 && WeatherStone.Instance.Changes == 0, "world exit invalidates queued activation");
        Reset(); Run(); Sim.ActiveActor.HasBeenDestroyed = true; JournalBootstrap.Flush();
        Check(WeatherStone.Instance.Changes == 0 && JournalBootstrap.Errors.Count == 0, "destroyed queued selection cannot mutate the stone");

        Reset(); Sim.ActiveActor.SimDescription.ChildOrBelow = true; Run(); JournalBootstrap.Flush();
        Check(WeatherStone.Instance.Changes == 0, "native age requirement retained");
        Reset(); WeatherStone.Instance = null; Run(); JournalBootstrap.Flush(); Check(GenieRain.sRain == null, "missing stone cannot create ownership");
        Reset(); SeasonsManager.Enabled = false; Run(); JournalBootstrap.Flush(); Check(WeatherStone.Instance.Changes == 0, "unavailable Seasons weather cannot activate");
        Reset(); WeatherStone.Instance.mWeatherStoneState = WeatherStone.WeatherStoneState.SummoningWeather; Run(); JournalBootstrap.Flush();
        Check(WeatherStone.Instance.Changes == 0, "in-progress summoning is preserved");
        Reset(); WeatherStone.Instance.ActorsUsingMe.Add(Sim.ActiveActor); Run(); JournalBootstrap.Flush();
        Check(WeatherStone.Instance.Changes == 0, "occupied stone is preserved");
        Reset(); WeatherStone.Instance.mWeatherStoneState = WeatherStone.WeatherStoneState.Resting; Run(); JournalBootstrap.Flush();
        Check(SeasonsManager.Requests == 1, "cheat can activate a resting stone");
        Reset(); Run(); JournalBootstrap.Flush();
        Sim.ActiveActor.Genie = false; Sim.ActiveActor.OccultManager.CurrentOccultTypes = OccultTypes.Fairy; Run(); JournalBootstrap.Flush();
        Check(GenieRain.sRain == null && WeatherStone.Instance.mSummonWeatherType == WeatherStone.SummonWeatherType.RevivingSprinkle,
              "switching from Genie to Fairy releases Fortune ownership");
        Check(WeatherStone.Instance.States.Contains(WeatherStone.WeatherStoneState.Resting), "native rest branch runs before replacing successful weather");
        Reset(); Sim.ActiveActor.OccultManager.CurrentOccultTypes = OccultTypes.Witch | OccultTypes.Fairy; Run(); JournalBootstrap.Flush();
        Check(WeatherStone.Instance.mSummonWeatherType == WeatherStone.SummonWeatherType.Fortune, "hybrid Genie priority retained");
        Reset(); Sim.ActiveActor.Genie = false; Sim.ActiveActor.OccultManager.CurrentOccultTypes = OccultTypes.Witch | OccultTypes.Fairy; Run(); JournalBootstrap.Flush();
        Check(WeatherStone.Instance.mSummonWeatherType == WeatherStone.SummonWeatherType.BewitchingRain, "hybrid Witch priority retained");
        Console.WriteLine("PASS: " + checks + " Weather Stone cheat activation, quiet no-op, lifecycle and ownership checks; simulated engine, no live rendering claim");
    }
}

namespace Sims3.SimIFace
{
    [AttributeUsage(AttributeTargets.Field)] class PersistableAttribute : Attribute { public PersistableAttribute(bool value) { } }
    class ResetException : Exception { }
    struct ObjectGuid { public ulong Value; }
    delegate int CommandHandler(object[] args);
    static class CommandSystem
    {
        public static Dictionary<string, CommandHandler> Commands = new Dictionary<string, CommandHandler>();
        public static int Registrations; public static bool Available = true;
        public static bool Exists() { return Available; }
        public static string GetCommandHelp(string name) { return Commands.ContainsKey(name) ? "help" : ""; }
        public static bool RegisterCommand(string name, string help, CommandHandler handler) { Registrations++; Commands[name] = handler; return true; }
        public static void UnregisterCommand(string name) { Commands.Remove(name); }
    }
}
namespace Sims3.UI
{
    static class StyledNotification
    {
        public static List<string> Messages = new List<string>();
        public enum NotificationStyle { kGameMessagePositive }
        public class Format { public string Text; public Format(string text, NotificationStyle style) { Text = text; } }
        public static void Show(Format format) { Messages.Add(format.Text); }
    }
}
namespace Sims3.UI.Hud { [Flags] enum OccultTypes { None = 0, Witch = 1, Fairy = 2, Vampire = 4, Werewolf = 8, Mermaid = 16, Ghost = 32 } }
namespace Sims3.Gameplay { static class GameStates { public static bool IsLiveState = true; } }
namespace Sims3.Gameplay.Actors
{
    class OccultManager { public Sims3.UI.Hud.OccultTypes CurrentOccultTypes; }
    class SimDescription { public bool ChildOrBelow; public ulong SimDescriptionId = 9; }
    class Sim
    {
        public static Sim ActiveActor; public bool HasBeenDestroyed, Genie = true;
        public OccultManager OccultManager = new OccultManager(); public SimDescription SimDescription = new SimDescription();
    }
}
namespace Sims3.Gameplay.Objects.Environment
{
    class WeatherStone
    {
        public enum WeatherStoneState { ReadyForSummoning, SummoningWeather, AfterSuccessfulSummon, Resting }
        public enum SummonWeatherType { None, BewitchingRain, EclipsingFog, RevivingSprinkle, HuntersStorm, Fortune = 0x41524347 }
        public static WeatherStone Instance; public bool HasBeenDestroyed;
        public ObjectGuid ObjectId = new ObjectGuid { Value = 11 }; public List<Sim> ActorsUsingMe = new List<Sim>();
        public WeatherStoneState mWeatherStoneState = WeatherStoneState.ReadyForSummoning;
        public WeatherStoneState CurrentWeatherStoneState { get { return mWeatherStoneState; } }
        public SummonWeatherType mSummonWeatherType; public ulong mSummoningSimDescriptionId;
        public bool mUseExtremeFailure = true; public float mCurrentSummoningSuccessChance = 50;
        public EventListener mWeatherChangedEventListener = new EventListener();
        public int Changes, ElectrifiedStops; public List<WeatherStoneState> States = new List<WeatherStoneState>();
        public void Change(bool yield, WeatherStoneState value) { Changes++; States.Add(value); mWeatherStoneState = value; }
    }
}
namespace Sims3.Gameplay.EventSystem
{
    class EventListener { }
    static class EventTracker { public static int Removed; public static void RemoveListener(EventListener listener) { Removed++; } }
}
namespace Sims3.Gameplay.Seasons
{
    static class SeasonsManager
    {
        public static bool Enabled; public static string CurrentWeather = "Sunny", ForcedWeather = "None";
        public static int Requests; public static void ForceRequestWeather(string weather, float duration) { Requests++; ForcedWeather = weather; }
    }
}
namespace Sims3.Gameplay.Utilities
{
    static class SimClock { public static long CurrentTicks = 100; public static long ConvertToTicks(float value, TimeUnit unit) { return (long)(value * 100); } }
    enum TimeUnit { Hours }
}
namespace Sims3.SimIFace.Enums { static class Weather { public const string Rain = "Rain"; } }
namespace Sims3.Gameplay.Core { static class RandomUtil { public static float GetFloat(float low, float high) { return low; } } }
namespace SupernaturalExpanded.Journal
{
    static class GenieAbilities { public static bool IsGenie(Sim sim) { return sim.Genie; } }
    static class JournalBootstrap
    {
        public static List<Action> Queue = new List<Action>(); public static List<string> Errors = new List<string>();
        public static void RunLater(Action action) { Queue.Add(action); }
        public static void Flush() { var actions = Queue.ToArray(); Queue.Clear(); foreach (var action in actions) action(); }
        public static void Report(string operation, Exception error) { Errors.Add(operation + error); }
    }
    partial class GenieRain
    {
        public static FortuneRainState sRain; public static bool Active; public static int Ends, Listens, Ticks;
        static void End(bool rest) { Ends++; sRain = null; }
        public static void EndForWeatherReplacement() { End(false); }
        static void Listen() { Listens++; }
        public static void Tick() { Ticks++; }
    }
    partial class StoneAccess
    {
        internal const WeatherStone.SummonWeatherType Fortune = WeatherStone.SummonWeatherType.Fortune;
        delegate void ChangeStoneState(bool yield, WeatherStone.WeatherStoneState state);
        static FieldInfo Field(string name) { return typeof(WeatherStone).GetField(name); }
        static Delegate Bind(Type signature, WeatherStone stone, string name) { return Delegate.CreateDelegate(signature, stone, typeof(WeatherStone).GetMethod("Change")); }
        static void Rest(WeatherStone stone) { stone.Change(false, WeatherStone.WeatherStoneState.Resting); }
        static void StopElectrifiedEffect(WeatherStone stone) { stone.ElectrifiedStops++; }
        public static float[] Tuning(string name) { return new[] { 3f, 6f }; }
        public static void Notify(WeatherStone stone, Sim actor, string key, Sims3.UI.StyledNotification.NotificationStyle style) { }
    }
}
