using System;
using Sims3.Gameplay;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Objects.Environment;
using Sims3.Gameplay.Seasons;
using Sims3.SimIFace;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal static class WeatherStoneCheat
    {
        internal const string Name = "ActivateWeatherStone";
        internal const string Alias = "ActiveWeatherStone";
        private const string Help = "Activate the selected supernatural Sim's Weather Stone effect with guaranteed success. Also: status.";
        private static readonly CommandHandler Handler = new CommandHandler(Execute);
        [Persistable(false)] private static bool registered, aliasRegistered;
        [Persistable(false)] private static int generation;

        internal static void Register()
        {
            if (!CommandSystem.Exists()) return;
            RegisterOne(Name, ref registered);
            RegisterOne(Alias, ref aliasRegistered);
        }

        private static void RegisterOne(string name, ref bool ready)
        {
            if (ready && !string.IsNullOrEmpty(CommandSystem.GetCommandHelp(name))) return;
            ready = CommandSystem.RegisterCommand(name, Help, Handler);
            if (!ready || string.IsNullOrEmpty(CommandSystem.GetCommandHelp(name)))
                throw new InvalidOperationException("The console could not register " + name);
        }

        internal static int Execute(object[] arguments)
        {
            Sim selected = Sim.ActiveActor;
            WeatherStone.SummonWeatherType kind;
            string weather;
            if (!StoneAccess.TryWeather(selected, out kind, out weather)) return 1;
            if (!GameStates.IsLiveState)
            {
                Say("Use this cheat in Live Mode.");
                return -1;
            }
            if (arguments != null && arguments.Length != 0)
            {
                if (arguments.Length == 1 && string.Equals(arguments[0] as string, "status", StringComparison.OrdinalIgnoreCase))
                {
                    ShowStatus();
                    return 1;
                }
                Say(Name + " takes no arguments, or status.");
                return -1;
            }
            int requestedGeneration = generation;
            try
            {
                JournalBootstrap.RunLater(delegate
                {
                    if (requestedGeneration == generation && GameStates.IsLiveState) Activate(selected);
                });
                Say("Weather Stone activation queued. Close the console and unpause.");
                return 1;
            }
            catch (ResetException) { throw; }
            catch (Exception error)
            {
                JournalBootstrap.Report("Queueing Weather Stone activation", error);
                Say("The Weather Stone could not be activated.");
                return -1;
            }
        }

        private static void Activate(Sim selected)
        {
            try
            {
                string weather, reason;
                if (!StoneAccess.ActivateSelectedWeather(WeatherStone.Instance, selected, out weather, out reason))
                {
                    if (!string.IsNullOrEmpty(reason)) Say(reason);
                    return;
                }
                Say(weather + " summoned. Allow the weather transition to finish.");
            }
            catch (ResetException) { throw; }
            catch (Exception error)
            {
                JournalBootstrap.Report("Activating the selected supernatural's Weather Stone effect", error);
                Say("The Weather Stone could not be activated.");
            }
        }

        private static void ShowStatus()
        {
            WeatherStone stone = WeatherStone.Instance;
            if (!SeasonsManager.Enabled) { Say("Seasons weather is unavailable in this world."); return; }
            if (stone == null || stone.HasBeenDestroyed) { Say("This world has no valid Weather Stone."); return; }
            string fortune = GenieRain.sRain == null ? "" : GenieRain.Active ? "; Fortune's Rain active" : "; Fortune's Rain pending";
            Say("Weather: " + SeasonsManager.CurrentWeather + "; Weather Stone: " + stone.CurrentWeatherStoneState + fortune + ".");
        }

        private static void Say(string text)
        {
            StyledNotification.Show(new StyledNotification.Format(text, StyledNotification.NotificationStyle.kGameMessagePositive));
        }

        internal static void Stop()
        {
            generation++;
            try { if (registered) CommandSystem.UnregisterCommand(Name); }
            finally
            {
                registered = false;
                try { if (aliasRegistered) CommandSystem.UnregisterCommand(Alias); }
                finally { aliasRegistered = false; }
            }
        }
    }
}
