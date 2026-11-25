using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Sims3.UI.Hud;

internal static class GenieWishMenuTests
{
    private const string Prefix = "SupernaturalExpanded.Journal.";
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static int checks;
    private static readonly Dictionary<short, OpCode> Codes = new Dictionary<short, OpCode>();
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static void Main(string[] args)
    {
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.FieldType == typeof(OpCode)) { OpCode code = (OpCode)field.GetValue(null); Codes[code.Value] = code; }
        Assembly mod = Assembly.LoadFrom(args[0]);
        Type grant = mod.GetType(Prefix + "GrantWish", true);
        Type definition = grant.GetNestedType("Definition", BindingFlags.Public);
        FieldInfo mode = definition.GetField("Mode", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo picker = definition.GetField("UsePicker", BindingFlags.NonPublic | BindingFlags.Instance);
        Array actions = (Array)grant.GetField("Singletons", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Check(actions.Length == 2, "ordinary and self wishes each need one aggregate action");
        HashSet<int> modes = new HashSet<int>();
        foreach (object action in actions)
        {
            Check((bool)picker.GetValue(action), "registered action bypasses native picker");
            Check(modes.Add(Convert.ToInt32(mode.GetValue(action))), "duplicate registered wish mode");
        }
        Check(modes.Contains(0) && modes.Contains(2) && !modes.Contains(1) && !modes.Contains(3), "unexpected standalone guarantee or lamp action");
        object lamp = grant.GetField("LampSingleton", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Check(Convert.ToInt32(mode.GetValue(lamp)) == 3 && (bool)picker.GetValue(lamp), "lamp root does not use expanded picker");
        string[] path = (string[])definition.GetMethod("GetPath").Invoke(lamp, new object[] { false });
        Check(path.Length == 0, "lamp Make a Wish acquired an extra Genie submenu");
        Type menus = mod.GetType(Prefix + "GenieWishMenu", true);
        MethodInfo isLamp = menus.GetMethod("IsLampWish", PrivateStatic);
        Check((bool)isLamp.Invoke(null, new[] { lamp }), "current lamp action is not recognized during cleanup");
        foreach (object action in actions) Check(!(bool)isLamp.Invoke(null, new[] { action }), "playable action mistaken for native lamp action");
        Type oldNative = typeof(Sims3.Gameplay.ActorSystems.OccultGenie.MakeWish).GetNestedType("Definition", BindingFlags.NonPublic);
        object oldAction = Activator.CreateInstance(oldNative, true);
        Check((bool)isLamp.Invoke(null, new[] { oldAction }), "old vanilla ten-wish definition is not removed during reconciliation");

        Type menu = mod.GetType(Prefix + "GenieMenu", true);
        Delegate silent = (Delegate)menu.GetField("SilentUnavailable", PrivateStatic).GetValue(null);
        Check((string)silent.DynamicInvoke() == "", "greyed out actions show a power explanation");
        MethodInfo selectRoot = menu.GetMethod("SelectRoot", PrivateStatic);
        foreach (string native in new[] { "Genie\u2026", "Génie\u2026", "Genie...", "Genie\u00a0\u2026" })
        {
            string root = (string)selectRoot.Invoke(null, new object[] { new[] { native }, "Genie..." });
            Check(object.ReferenceEquals(native, root), "native localized root or ellipsis was normalized into a duplicate branch");
        }
        Check((string)selectRoot.Invoke(null, new object[] { null, "Genie..." }) == "Genie...", "missing native root has no fallback");
        Check((string)selectRoot.Invoke(null, new object[] { new string[0], "Genie..." }) == "Genie...", "empty native path has no fallback");

        Type kind = mod.GetType(Prefix + "WishKind", true);
        Type choice = mod.GetType(Prefix + "GenieWishChoice", true);
        Type dialog = mod.GetType(Prefix + "GenieWishDialog", true);
        MethodInfo nativeName = dialog.GetMethod("NativeName", PrivateStatic);
        MethodInfo icon = dialog.GetMethod("Icon", PrivateStatic);
        int[] catalog = { 3, 4, 5, 6, 7, 8, 9, 0, 10, 11, 12, 13, 15 };
        int originals = 0, additions = 0;
        foreach (int id in catalog)
        {
            object value = Enum.ToObject(kind, id);
            string original = (string)nativeName.Invoke(null, new[] { value, (object)false });
            if (original == null) additions++; else originals++;
            string image = (string)icon.Invoke(null, new[] { value, (object)false });
            Check(image.StartsWith("w_genieWish_") && image.Length > "w_genieWish_".Length, "wish has no native thumbnail key");
            IGenieWish card = (IGenieWish)Activator.CreateInstance(choice, BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { value, "title " + id, "details " + id, image, (object)false }, null);
            Check(card.GetWishName() == "title " + id && card.GetWishDescription() == "details " + id, "native dialog row lost localized text");
            Check(!card.IsDisabled(), "validated native dialog row unexpectedly disabled");
        }
        Check(originals == 10 && additions == 3, "native and added wishes do not share one IGenieWish row model");
        MethodInfo descriptionKey = dialog.GetMethod("NativeDescriptionName", PrivateStatic);
        MethodInfo description = dialog.GetMethod("Description", PrivateStatic);
        foreach (int id in new[] { 12, 13, 15 })
        {
            object value = Enum.ToObject(kind, id);
            Check(descriptionKey.Invoke(null, new[] { value, (object)false }) == null, "new wish retained a description key");
            Check((string)description.Invoke(null, new[] { value, (object)false, (object)false }) == string.Empty, "new wish retained a tooltip description");
        }
        Check((string)descriptionKey.Invoke(null, new[] { Enum.ToObject(kind, 6), (object)false }) == "MakeSimLoveMeDescription", "Love no longer uses its vanilla description");
        Check((string)descriptionKey.Invoke(null, new[] { Enum.ToObject(kind, 4), (object)false }) == "RaiseDeadDescription", "resurrection no longer uses its vanilla description");
        Check(Calls(dialog.GetMethod("Description", PrivateStatic)).Contains("Sims3.Gameplay.ActorSystems.OccultGenie+GenieWishInstance.GetWishDescription"), "old wish descriptions are custom rewrites");
        IGenieWish disabled = (IGenieWish)Activator.CreateInstance(choice, BindingFlags.NonPublic | BindingFlags.Instance, null,
            new[] { Enum.ToObject(kind, 4), "unavailable", "", "w_genieWish_Resurrect", (object)true }, null);
        Check(disabled.IsDisabled(), "unavailable NPC row can be selected");
        MethodInfo run = grant.GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo reserved = grant.GetMethod("RunReserved", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(Calls(run).Contains(Prefix + "WishReservation.TryAcquire"), "wish routing runs without a reservation");
        Check(Calls(reserved).Contains("Sims3.Gameplay.Interactions.InteractionInstance.StartSync"), "wish cast does not reserve its recipient");
        Check(Calls(reserved).Contains(Prefix + "WishRendezvous.Approach"), "lamp wisher is not routed to the waiting Genie");
        Type participant = mod.GetType(Prefix + "WishParticipant", true);
        Check(Calls(participant.GetMethod("Run", BindingFlags.Instance | BindingFlags.NonPublic)).Contains(Prefix + "WishRendezvous.Approach"), "playable Genie still chases its recipient");
        Type wrongBuff = mod.GetType(Prefix + "WishGoneWrongInstance", true);
        HashSet<string> refreshCalls = Calls(wrongBuff.GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance));
        Check(refreshCalls.Contains("Sims3.Gameplay.Socializing.SocialComponent.AddReducedEffectiveness") && refreshCalls.Contains("Sims3.Gameplay.Socializing.SocialComponent.RemoveReducedEffectiveness"), "Beauty failure edits only tooltip data, not native social modifiers");
        Type buffs = mod.GetType(Prefix + "WishBuffs", true);
        Check(Calls(buffs.GetMethod("Refresh", PrivateStatic)).Contains("Sims3.Gameplay.ActorSystems.BuffManager.ChangeMoodValue"), "one-sided attraction never updates its native mood contribution");
        Check(Calls(mod.GetType(Prefix + "WishState", true).GetMethod("Update", PrivateStatic)).Contains(Prefix + "WishEffectSkill.RestoreTravelBuffs"), "custom-buff travel snapshots are never restored");
        object peace = Enum.ToObject(kind, 3);
        Check((string)nativeName.Invoke(null, new[] { peace, (object)false }) == "WorldPeaceName" &&
            (string)nativeName.Invoke(null, new[] { peace, (object)true }) == "WorldMiseryName", "personality variant uses wrong vanilla title");

        Check(Calls(dialog.GetMethod("Show", PrivateStatic)).Contains("Sims3.UI.GenieWishSelectionDialog.Show"), "actual DLL does not open vanilla wish dialog");
        Check(Calls(reserved).Contains(Prefix + "GenieWishDialog.Show"), "wish execution still uses old per-wish leaf path");
        Check(Calls(menu.GetMethod("Root", PrivateStatic)).Contains("Sims3.Gameplay.Interactions.InteractionDefinition.GetPath"), "root is not taken from the native Genie action");
        foreach (string type in new[] { "GrantWish+Definition", "HeartsDesire+Definition", "GenieBlessing+Definition" })
            Check(Calls(mod.GetType(Prefix + type, true).GetMethod("GetPath")).Contains(Prefix + "GenieMenu.Root"), type + " still constructs its own mismatched Genie path");
        Check(Calls(mod.GetType(Prefix + "SpellKnowledgeGates", true).GetMethod("AddGenieVerbs", PrivateStatic)).Contains(Prefix + "GenieWishMenu.Install"), "existing Sims never reconcile old lamp actions");
        Check(Calls(mod.GetType(Prefix + "WishState", true).GetMethod("Prepare", PrivateStatic)).Contains(Prefix + "GenieWishMenu.Prepare"), "newly created lamps keep EA's old catalog singleton");
        Type retreat = mod.GetType(Prefix + "LampRetreat", true);
        HashSet<string> retreatRun = Calls(retreat.GetMethod("Run", BindingFlags.Instance | BindingFlags.NonPublic));
        Check(retreatRun.Contains("Sims3.Gameplay.Abstracts.GameObject.FadeOut") &&
            retreatRun.Contains("Sims3.Gameplay.Interactions.InteractionInstance.AddMotiveDelta") &&
            !retreatRun.Contains("Sims3.Gameplay.Gameflow.DisableSave"), "lamp retreat omitted native fade or continuous motives, or still locks saving");
        HashSet<string> retreatRelease = Calls(retreat.GetMethod("Release", BindingFlags.Instance | BindingFlags.NonPublic));
        Check(retreatRelease.Contains(Prefix + "LampRetreat.Show") &&
            Calls(retreat.GetMethod("Show", PrivateStatic)).Contains("Sims3.Gameplay.Abstracts.GameObject.FadeIn") &&
            retreatRelease.Contains("Sims3.Gameplay.ActorSystems.BuffManager.RemoveElement"), "lamp exit lacks visibility or moodlet cleanup");
        Check(Calls(retreat.GetMethod("Check", BindingFlags.Static | BindingFlags.NonPublic)).Contains("Sims3.Gameplay.ActorSystems.InteractionQueue.AddNext"),
            "a Genie saved inside the lamp never resumes the rest");
        Check(Calls(retreat.GetMethod("Cleanup")).Contains(Prefix + "LampRetreat.Release"), "reset cleanup skips restoration");
        HashSet<string> smoke = Calls(retreat.GetMethod("PlayDeparture", PrivateStatic));
        Check(smoke.Contains("Sims3.SimIFace.VisualEffect.SubmitOneShotEffect") &&
            smoke.Contains("Sims3.SimIFace.VisualEffect.ParentTo"), "lamp smoke omitted native one-shot bindings");
        HashSet<string> emerge = Calls(retreat.GetMethod("PlayEmergence", PrivateStatic));
        Check(emerge.Contains("Sims3.SimIFace.VisualEffect.SetPosAndOrient") &&
            emerge.Contains("Sims3.SimIFace.VisualEffect.SubmitOneShotEffect") &&
            !emerge.Contains("Sims3.SimIFace.VisualEffect.ParentTo"), "emergence lost native world anchor");
        Check(retreatRun.Contains(Prefix + "JournalBootstrap.RunLater"), "emergence must sample the actor after StandardExit");
        Type handler = mod.GetType(Prefix + "GenieWishes", true);
        HashSet<string> grantCalls = Calls(handler.GetMethod("Grant", PrivateStatic));
        Check(grantCalls.Contains(Prefix + "GenieWishes.ResolveMode") && grantCalls.Contains(Prefix + "WishRules.OrdinarySucceeds") &&
            grantCalls.Contains(Prefix + "WishRules.NpcAccuracy"), "compiled grant path dropped an outcome policy");
        HashSet<string> feedback = Calls(handler.GetMethod("AfterCommit", PrivateStatic));
        Check(feedback.Contains(Prefix + "WishFeedback.Result") && feedback.Contains(Prefix + "WishFate.Apply") &&
            !feedback.Contains(Prefix + "WitchMagicRecovery.Fizzle") && !feedback.Contains(Prefix + "GeniePerks.AfterBacklash"),
            "ordinary wish leaked into spell Backlash or lost feedback");
        Type fate = mod.GetType(Prefix + "WishFate", true);
        MethodInfo select = fate.GetMethod("Select", PrivateStatic);
        int[] counts = new int[7];
        for (int i = 0; i < 10000; i++)
        {
            int result = Convert.ToInt32(select.Invoke(null, new object[] { (i + .5f) / 100f }));
            counts[result]++;
        }
        int[] odds = { 1, 2, 7, 15, 20, 25, 30 };
        for (int i = 0; i < odds.Length; i++)
            Check(counts[i] == odds[i] * 100, "incorrect committed Fate weight " + i);
        Type resultText = mod.GetType(Prefix + "WishFeedback", true);
        MethodInfo success = resultText.GetMethod("SuccessKey", PrivateStatic);
        foreach (int id in catalog)
            Check((string)success.Invoke(null, new[] { Enum.ToObject(kind, id), (object)false }) != null, "wish lacks success/concealment text");
        Type cheats = mod.GetType(Prefix + "ArcanaPresentationCheats", true);
        HashSet<string> preview = Calls(cheats.GetMethod("Notifications", PrivateStatic));
        Check(preview.Contains("Sims3.Gameplay.Utilities.AlarmManager.AddAlarmRepeating") && !preview.Contains("Sims3.UI.ObjectPickerDialog.Show"),
            "notification preview should automatically sequence without a picker");
        Type hud = mod.GetType(Prefix + "ArcanaMoodletPreview", true);
        HashSet<string> advanceCalls = Calls(hud.GetMethod("Advance", PrivateStatic));
        Check(advanceCalls.Contains("Sims3.Gameplay.ActorSystems.BuffManager.AddElementIgnoreDelay") &&
            advanceCalls.Contains("Sims3.Gameplay.ActorSystems.Buff..ctor"), "HUD previews must use plain Buff instances");
        Type payment = mod.GetType(Prefix + "WishYouthPayment", true);
        Check(Calls(payment.GetMethod("Complete", BindingFlags.Instance | BindingFlags.NonPublic)).Contains(Prefix + "WishCommit.EffectApplied"),
            "youth does not share the once-only marker");
        Check(grantCalls.Contains(Prefix + "WishAging.QueueYouth"), "successful youth is not deferred");
        Type language = mod.GetType(Prefix + "ArcanaEnglish", true);
        string[] notices = (string[])language.GetField("NotificationKeys", PrivateStatic).GetValue(null);
        Check(Array.IndexOf(notices, "Arcana.Genie.Wish.Success.Resurrect") >= 0 &&
            Array.IndexOf(notices, "Arcana.Genie.Wish.Failure.HappinessReveal") >= 0 &&
            Array.IndexOf(notices, "Arcana.Genie.Fate.DeathsDue") >= 0, "compiled preview registry omitted representative notices");
        Console.WriteLine("PASS: " + checks + " actual compiled wish action, old-definition cleanup, native picker, localized path, 13 IGenieWish row and call-binding checks");
        Console.WriteLine("Host tests verify compiled behavior without opening a live game menu or rendering the dialog.");
    }

    private static HashSet<string> Calls(MethodInfo method)
    {
        HashSet<string> result = new HashSet<string>();
        byte[] il = method.GetMethodBody().GetILAsByteArray();
        int index = 0;
        while (index < il.Length)
        {
            int first = il[index++];
            short value = unchecked((short)(first == 0xfe ? 0xfe00 | il[index++] : first));
            OpCode code = Codes[value];
            int size;
            switch (code.OperandType)
            {
                case OperandType.InlineNone: size = 0; break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: size = 1; break;
                case OperandType.InlineVar: size = 2; break;
                case OperandType.InlineI8: case OperandType.InlineR: size = 8; break;
                case OperandType.InlineSwitch: size = 4 + 4 * BitConverter.ToInt32(il, index); break;
                default: size = 4; break;
            }
            if (code.OperandType == OperandType.InlineMethod)
            {
                MethodBase called = method.Module.ResolveMethod(BitConverter.ToInt32(il, index));
                result.Add(called.DeclaringType.FullName + "." + called.Name);
            }
            index += size;
        }
        return result;
    }
}
