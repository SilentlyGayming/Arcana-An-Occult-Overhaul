using System;
using System.Collections.Generic;
using Sims3.Gameplay;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace.Enums;
using Sims3.SimIFace;
using Sims3.UI;
using Gameflow = Sims3.Gameplay.Gameflow;

namespace SupernaturalExpanded.Journal
{
    internal static class ArcanaPresentationCheats
    {
        private static readonly string[] Names =
        {
            "arcanaTestMoodlets", "arcanaTestNotifications", "arcanaForceWish", "arcanaForceFate", "arcanaForceDesire", "arcanaClearForcedOutcomes", "arcanaForceCast"
        };
        private static readonly CommandHandler[] Handlers =
        {
            delegate { return ArcanaMoodletPreview.Toggle(); }, delegate { return Notifications(); },
            ForceWish, ForceFate, ForceDesire, delegate { ClearForced(); return 1; }, ForceCast
        };
        [Persistable(false)] private static readonly bool[] Registered = new bool[Names.Length];
        [Persistable(false)] private static readonly Queue<string> NotificationKeys = new Queue<string>();
        [Persistable(false)] private static Sim previewSubject;
        [Persistable(false)] private static Sim secondarySubject;
        [Persistable(false)] private static AlarmHandle notificationAlarm = AlarmHandle.kInvalidHandle;
        [Persistable(false)] private static ulong forcedWishActor, forcedFateActor, forcedCastActor;
        [Persistable(false)] private static WishKind forcedWish;
        [Persistable(false)] private static bool forcedWishSuccess;
        [Persistable(false)] private static FateOutcome forcedFate;

        internal static void Register()
        {
            if (!CommandSystem.Exists()) return;
            for (int i = 0; i < Names.Length; i++)
            {
                if (Registered[i] && !string.IsNullOrEmpty(CommandSystem.GetCommandHelp(Names[i]))) continue;
                Registered[i] = CommandSystem.RegisterCommand(Names[i], "Arcana developer preview or outcome control", Handlers[i]);
            }
        }

        internal static void Stop()
        {
            ClearNotifications();
            ArcanaMoodletPreview.Clear();
            ClearForced();
            for (int i = 0; i < Names.Length; i++)
            {
                if (Registered[i] && CommandSystem.Exists()) CommandSystem.UnregisterCommand(Names[i]);
                Registered[i] = false;
            }
        }

        private static void ClearNotifications()
        {
            WitchCastingLifecycle.RemoveAlarm(ref notificationAlarm);
            NotificationKeys.Clear();
            previewSubject = null;
            secondarySubject = null;
        }

        private static void ClearForced()
        { forcedWishActor = 0; forcedFateActor = 0; forcedCastActor = 0; GenieBlessings.ClearForced(); }

        private static int Notifications()
        {
            ClearNotifications();
            Sim sim = Sim.ActiveActor;
            if (sim == null || sim.HasBeenDestroyed) return -1;
            previewSubject = sim;
            foreach (Sim candidate in Sims3.Gameplay.Queries.GetObjects<Sim>())
                if (candidate != sim && !candidate.HasBeenDestroyed && candidate.SimDescription != null && candidate.SimDescription.IsHuman)
                { secondarySubject = candidate; break; }
            foreach (string key in ArcanaEnglish.NotificationKeys) NotificationKeys.Enqueue(key);
            try
            {
                Next();
                if (NotificationKeys.Count > 0)
                    notificationAlarm = AlarmManager.Global.AddAlarmRepeating(5f, TimeUnit.Minutes, Next, 5f, TimeUnit.Minutes,
                        "Arcana notification previews", AlarmType.NeverPersisted, null);
                return 1;
            }
            catch { ClearNotifications(); throw; }
        }

        private static void Next()
        {
            if (NotificationKeys.Count == 0 || previewSubject == null || previewSubject.HasBeenDestroyed)
            { ClearNotifications(); return; }
            string key = NotificationKeys.Dequeue();
            Sim subject = previewSubject;
            Sim other = secondarySubject != null && !secondarySubject.HasBeenDestroyed ? secondarySubject : subject;
            try
            {
                string text = PreviewText(key, subject.FullName, other.FullName);
                StyledNotification.Show(new StyledNotification.Format(text, other.ObjectId, subject.ObjectId,
                    StyledNotification.NotificationStyle.kGameMessagePositive));
            }
            catch (ResetException) { ClearNotifications(); throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Previewing " + key, error); }
            if (NotificationKeys.Count == 0) ClearNotifications();
        }

        private static string PreviewText(string key, string primary, string secondary)
        {
            if (key == "Arcana.Genie.Wish.Success.More") return JournalText.Format(key, primary, 2);
            if (key == "Arcana.Genie.Wish.Success.Fortune") return JournalText.Format(key, primary, 100000);
            if (key == "Arcana.Cheat.UnlockAll.Success") return JournalText.Format(key, primary, 20);
            if (key == "Arcana.Genie.HeartsDesire.Response") return JournalText.Format(key, primary, JournalText.Get("Arcana.Genie.HeartsDesire.Fortune.1"));
            if (key == "Arcana.Study.Discovered") return JournalText.Format(key, primary, JournalText.Get("Arcana.Spell.conversion.Name"));
            return JournalText.Format(key, primary, secondary, 1);
        }

        private static int ForceWish(object[] arguments)
        {
            Sim actor = Sim.ActiveActor;
            if (actor == null || arguments == null || arguments.Length != 2) return Usage("arcanaForceWish success|failure WishKind");
            string result = Convert.ToString(arguments[0]).ToLowerInvariant();
            WishKind kind;
            try { kind = (WishKind)Enum.Parse(typeof(WishKind), Convert.ToString(arguments[1]), true); }
            catch (ArgumentException) { return Usage("Unknown wish."); }
            if (WishCatalog.Find(kind) == null || (result != "success" && result != "failure"))
                return Usage("arcanaForceWish success|failure WishKind");
            forcedWishActor = actor.SimDescription.SimDescriptionId;
            forcedWish = kind;
            forcedWishSuccess = result == "success";
            return Usage("Functional test armed for the next matching ordinary wish. It applies real gameplay changes.");
        }

        private static int ForceFate(object[] arguments)
        {
            Sim actor = Sim.ActiveActor;
            if (actor == null || arguments == null || arguments.Length != 1) return Usage("arcanaForceFate FateOutcome");
            FateOutcome outcome;
            try { outcome = (FateOutcome)Enum.Parse(typeof(FateOutcome), Convert.ToString(arguments[0]), true); }
            catch (ArgumentException) { return Usage("Unknown Fate outcome."); }
            if (!Enum.IsDefined(typeof(FateOutcome), outcome)) return Usage("Unknown Fate outcome.");
            forcedFateActor = actor.SimDescription.SimDescriptionId;
            forcedFate = outcome;
            return Usage("Functional test armed for the next committed self-wish. It applies real Fate consequences.");
        }

        private static int ForceDesire(object[] arguments)
        {
            Sim actor = Sim.ActiveActor;
            if (actor == null || arguments == null || arguments.Length != 1) return Usage("arcanaForceDesire Fortune|Vitality|Inspiration|Harmony|Protection|Reset");
            string choice = Convert.ToString(arguments[0]);
            if (string.Equals(choice, "Reset", StringComparison.OrdinalIgnoreCase))
            {
                GenieBlessings.ResetDesires();
                return Usage("Heart's Desire cooldowns and revealed desires were cleared for every Sim in the world.");
            }
            Blessing kind;
            try { kind = (Blessing)Enum.Parse(typeof(Blessing), choice, true); }
            catch (ArgumentException) { return Usage("Unknown blessing."); }
            if (!Enum.IsDefined(typeof(Blessing), kind)) return Usage("Unknown blessing.");
            GenieBlessings.ForceDesire(actor, kind);
            return Usage("The next Heart's Desire this Genie reveals will ask for the Blessing of " + kind + ".");
        }

        private static int ForceCast(object[] arguments)
        {
            Sim actor = Sim.ActiveActor;
            if (actor == null || !GenieAbilities.IsGenie(actor)) return Usage("Select a Genie in Live Mode, then enter arcanaForceCast.");
            forcedCastActor = actor.SimDescription.SimDescriptionId;
            return Usage("The next Genie ability this Genie casts will succeed without an accuracy roll, Backlash or Protection. It still costs Magical Power.");
        }

        internal static bool TakeCast(Sim actor)
        {
            if (forcedCastActor == 0 || forcedCastActor != actor.SimDescription.SimDescriptionId) return false;
            forcedCastActor = 0;
            return true;
        }

        // Reports what a forced Genie ability actually did, so a missing effect can be told apart from a cast that never ran.
        internal static void ReportCast(GenieAbility ability, Sim actor, Sim target, bool ran, float hygieneBefore, float powerBefore, float powerAfter)
        {
            string text = JournalText.Get(ability.NameKey) + ": the native interaction " + (ran ? "completed" : "did not complete") + ".\n";
            if (target != null && ability.Id == "genie_clean_sim")
            {
                float hygiene = target.Motives.GetValue(CommodityKind.Hygiene);
                text += "Hygiene of " + target.FullName + ": " + hygieneBefore.ToString("0") + " to " + hygiene.ToString("0") +
                    ". The native whirlwind is submitted on " + target.FullName + " just before Hygiene is filled, so a full Hygiene bar means it was submitted.\n";
            }
            text += "Magical Power: " + powerBefore.ToString("0.#") + " to " + powerAfter.ToString("0.#") + ".";
            Usage(text);
        }

        internal static bool TryWish(Sim actor, WishKind kind, WishMode mode, out bool succeeds)
        {
            succeeds = forcedWishSuccess;
            return (mode == WishMode.Other || mode == WishMode.Lamp) && forcedWishActor == actor.SimDescription.SimDescriptionId && kind == forcedWish;
        }

        internal static void ConsumeWish(Sim actor, WishKind kind, WishMode mode)
        {
            bool unused;
            if (TryWish(actor, kind, mode, out unused)) forcedWishActor = 0;
        }

        internal static FateOutcome TakeFate(Sim actor, float draw)
        {
            if (forcedFateActor != actor.SimDescription.SimDescriptionId) return WishFate.Select(draw);
            forcedFateActor = 0;
            return forcedFate;
        }

        private static int Usage(string text)
        { SimpleMessageDialog.Show("Arcana developer tools", text); return -1; }
    }
}
