using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;

[assembly: Tunable]
[assembly: PersistableStatic]
[assembly: AssemblyVersion("0.9.0.0")]
[assembly: AssemblyFileVersion("0.9.4.5")]

namespace SupernaturalExpanded.Journal
{
    public static class JournalBootstrap
    {
        [Tunable]
        public static bool kInstantiator = false;

        [Tunable] public static float kStudyExperienceFraction = 0.40f;
        [Tunable] public static float kStudyResearchIntervalMinutes = 30f;
        [Tunable] public static float kStudyDiscoveryChancePercent = 25f;
        [Tunable] public static float kStudyWitchSpeed = 1f;
        [Tunable] public static float kStudyFairySpeed = 0.5f;
        [Tunable] public static float kStudyGenieSpeed = 0.5f;
        [Tunable] public static float kStudyWitchMasterBonusPercent = 10f;
        [Tunable] public static float kStudyGenieAscendantBonusPercent = 25f;
        [Tunable] public static float kStudyWitchingHourBonusPercent = 20f;

        private static SimologyEntry entry;
        private static OccultMeter meter;
        private static JournalWindow window;
        private static bool worldReady;
        private static int generation;
        private static readonly Dictionary<string, bool> reportedOperations = new Dictionary<string, bool>();

        static JournalBootstrap()
        {
            AccuracySources.Register(OccultTypes.Witch, CasterAttributes.ReadAccuracy);
            AttributeSources.Register(OccultTypes.Witch, JournalText.Get("Arcana.Journal.Overview.Attributes.PowerRegeneration"),
                CasterAttributes.ReadRegeneration);
            AttributeSources.Register(OccultTypes.Witch, JournalText.Get("Arcana.Journal.Overview.Attributes.SpellCost"),
                CasterAttributes.ReadSpellCost);
            foreach (OccultTypes innate in new OccultTypes[] { OccultTypes.Fairy, OccultTypes.Genie })
            {
                AccuracySources.Register(innate, CasterAttributes.ReadAccuracy);
                AttributeSources.Register(innate, JournalText.Get("Arcana.Journal.Overview.Attributes.PowerRegeneration"),
                    CasterAttributes.ReadInnateRegeneration);
                AttributeSources.Register(innate, JournalText.Get("Arcana.Journal.Overview.Attributes.SpellCost"), CasterAttributes.ReadSpellCost);
            }
            AttributeSources.Register(OccultTypes.Genie, JournalText.Get("Arcana.Journal.Overview.Attributes.BacklashChance"),
                CasterAttributes.ReadBacklash);
            World.OnWorldLoadFinishedEventHandler += OnWorldLoaded;
            World.OnWorldLoadStartedEventHandler += OnWorldStarting;
            World.OnWorldQuitEventHandler += OnWorldQuit;
        }

        private static void OnWorldStarting(object sender, EventArgs args)
        {
            OnWorldExiting(sender, args);
            reportedOperations.Clear();
            foreach (PerkProgressSource source in PerkProgressSource.All) source.ResetThresholds();
            try
            {
                WishState.Prepare();
                if (GameUtils.IsInstalled(ProductVersion.EP7)) SpellKnowledgeGates.Prepare();
                WitchCastingLifecycle.Prepare();
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                ReportOnce("Preparing Spell Knowledge and Witch recovery", error);
            }
        }

        private static void OnWorldLoaded(object sender, EventArgs args)
        {
            OnWorldExiting(sender, args);
            worldReady = true;
            ArcanaCheats.Start();
            try { WishState.Start(); }
            catch (ResetException) { throw; }
            catch (Exception error) { Report("Starting Genie wishes", error); }
            entry = new SimologyEntry();
            entry.Refresh();
            meter = new OccultMeter();
            meter.Refresh();
            try
            {
                WitchCastingLifecycle.Start();
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                WitchCastingLifecycle.Stop();
                Report("Starting Witch gameplay", error);
            }
            try
            {
                SpellStudyStations.Start();
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                SpellStudyStations.Stop();
                Report("Adding Study Spells to stations", error);
            }
        }

        private static void OnWorldQuit(object sender, EventArgs args)
        {
            try { OnWorldExiting(sender, args); }
            finally
            {
                StatusReaders.ClearIcons();
                MagicCasters.GeneratedHands = null;
                HeartsDesireLedger.Clear();
                RisingTide.Clear();
                GeniePerks.Clear();
                WitchProgressStore.sRecords = null;
                GenieProgressStore.sRecords = null;
                GenieRain.ResetSaved();
                WishLamp.ResetSaved();
                WishRepossession.ResetSaved();
                SpellKnowledgeGates.Restore();
                SpellKnowledgeRegistration.Restore();
                WishDeath.Restore();
                GenieWishMenu.Restore();
            }
        }

        private static void OnWorldExiting(object sender, EventArgs args)
        {
            worldReady = false;
            generation++;
            ArcanaCheats.Stop();
            LampRetreat.ReleaseAll();
            WishAging.Clear();
            WishState.Stop();
            WitchCastingLifecycle.Stop();
            SpellStudyStations.Stop();
            UiAnimationClock.Shutdown();
            Close(window);
            if (meter != null)
            {
                OccultMeter oldMeter = meter;
                meter = null;
                oldMeter.Dispose();
            }
            if (entry != null)
            {
                SimologyEntry old = entry;
                entry = null;
                try
                {
                    old.Dispose();
                }
                catch (Exception error)
                {
                    Report("Removing the Simology button", error);
                }
            }
        }

        internal static void RefreshMeter()
        {
            if (meter != null)
            {
                meter.Refresh();
            }
        }

        internal static void RefreshPerkPoints()
        {
            if (entry != null)
            {
                entry.RefreshPoints();
            }
        }

        internal static int ReservedPerkPoints(ulong simDescriptionId)
        {
            return window == null ? 0 : window.ReservedPerkPoints(simDescriptionId);
        }

        internal static bool TryButtonCenter(out Vector2 center)
        {
            center = new Vector2(0f, 0f);
            return entry != null && entry.TryButtonCenter(out center);
        }

        internal static void RunLater(Sims3.UI.Function action)
        {
            if (!worldReady)
            {
                return;
            }
            int token = generation;
            Simulator.AddObject(new Sims3.UI.OneShotFunctionTask(delegate
            {
                if (worldReady && token == generation)
                {
                    action();
                }
            }));
        }

        internal static void OpenJournal()
        {
            RunLater(Open);
        }

        private static void Open()
        {
            if (!worldReady)
            {
                return;
            }

            JournalWindow candidate = null;
            try
            {
                IResponder responder = Responder.Instance;
                IHudModel model = responder == null ? null : responder.HudModel;
                SimIdentity selected = SimIdentity.Read(model);
                if (selected == null)
                {
                    return;
                }

                if (window != null && window.IsOpen)
                {
                    window.Focus();
                    return;
                }
                if (UIManager.IsModal())
                {
                    return;
                }

                Close(window);
                candidate = new JournalWindow(model);
                window = candidate;
                candidate.Open(selected);
                RefreshPerkPoints();
            }
            catch (ResetException)
            {
                Close(candidate);
                throw;
            }
            catch (Exception error)
            {
                Report("Opening Supernatural Journal", error);
                Close(candidate);
            }
        }

        internal static void Close(JournalWindow closing)
        {
            if (closing == null || window != closing)
            {
                return;
            }

            window = null;
            try
            {
                closing.Dispose();
                RefreshPerkPoints();
            }
            catch (Exception error)
            {
                Report("Closing Supernatural Journal", error);
            }
        }

        internal static void ReportOnce(string operation, Exception error)
        {
            if (reportedOperations.ContainsKey(operation))
            {
                return;
            }
            reportedOperations[operation] = true;
            Report(operation, error);
        }

        internal static void Report(string operation, Exception error)
        {
            WriteLog("<Operation>" + Escape(operation) + "</Operation><Exception>"
                + Escape(error.ToString()) + "</Exception>");
        }

        private static bool WriteLog(string payload)
        {
            try
            {
                uint handle = 0;
                Simulator.CreateScriptErrorFile(ref handle);
                if (handle == 0)
                {
                    return false;
                }
                try
                {
                    string xml = "<SupernaturalJournal>" + payload + "</SupernaturalJournal>";
                    char[] buffer = new char[xml.Length + 1];
                    xml.CopyTo(0, buffer, 0, xml.Length);
                    return Simulator.AppendToScriptErrorFile(handle, buffer);
                }
                finally
                {
                    Simulator.CloseScriptErrorFile(handle);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string Escape(string value)
        {
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
