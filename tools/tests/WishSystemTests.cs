using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.SimIFace;
using SupernaturalExpanded.Journal;

internal static class WishSystemTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void TestBlessingRules()
    {
        Check(BlessingRules.Count == 5 && BlessingRules.Names.Length == 5, "five blessings");
        Check(BlessingRules.Cost == 40f && BlessingRules.Hours == 12f && BlessingRules.DesiredHours == 24f, "approved blessing cost and durations");
        long hour = Sims3.Gameplay.Utilities.SimClock.ConvertToTicks(1f, Sims3.Gameplay.Utilities.TimeUnit.Hours);
        long idle = 0;
        long[] none = new long[5];
        long cooled = BlessingRules.CooldownEnds(100);
        Check(BlessingRules.CooldownHours == 48f && cooled == 100 + 48 * hour, "the recipient cooldown is 48 hours from the blessing");
        Check(BlessingRules.CanBless(idle, none, 100), "a Sim never blessed can be blessed");
        Check(!BlessingRules.CanBless(cooled, none, 100 + 47 * hour), "no blessing of any kind during the cooldown, even after the moodlet ended");
        Check(BlessingRules.CanBless(cooled, none, 100 + 48 * hour), "blessings return when the cooldown ends");
        Check(!BlessingRules.CanBless(idle, new long[] { 0, 0, 100 + 5 * hour, 0, 0 }, 100),
            "an older save without a cooldown still waits for its active blessing to end");
        Check(BlessingRules.CanBless(idle, new long[] { 0, 0, 50, 0, 0 }, 100), "an expired blessing from an older save does not block");
        Check(BlessingRules.Survivor(new long[] { 0, 0, 0, 0, 0 }, 100) == -1 && BlessingRules.Survivor(new long[] { 50, 0, 0, 0, 0 }, 100) == -1,
            "no active blessing");
        Check(BlessingRules.Survivor(new long[] { 300, 0, 900, 500, 0 }, 100) == 2, "an older save keeps the blessing that ends last");
        Check(BlessingRules.Survivor(new long[] { 0, 700, 0, 700, 0 }, 100) == 1, "ties keep the first blessing in menu order");
        Check(BlessingRules.OwnLuck(true, 300f, 301f) && !BlessingRules.OwnLuck(false, 300f, 300f) && !BlessingRules.OwnLuck(true, 30f, 300f) &&
            !BlessingRules.OwnLuck(true, -1f, 300f), "only Fortune's own lucky moodlet is taken back");
        Check(BlessingRules.CanReveal(0, 5) && !BlessingRules.CanReveal(10, 10 + 71 * hour) && BlessingRules.CanReveal(10, 10 + 72 * hour),
            "Heart's Desire cooldown is not three days");
        Check(BlessingRules.Bonus(1000) == 100 && BlessingRules.Bonus(5) == 1 && BlessingRules.Bonus(4) == 0 && BlessingRules.Bonus(-50) == 0, "income bonus rounding");
        Check(BlessingRules.EarnedIncome(Sims3.Gameplay.Careers.GotPaidEvent.PayType.kCareerNormalPay) &&
            BlessingRules.EarnedIncome(Sims3.Gameplay.Careers.GotPaidEvent.PayType.kSoldPainting) &&
            !BlessingRules.EarnedIncome(Sims3.Gameplay.Careers.GotPaidEvent.PayType.kCareerRetirementPay) &&
            !BlessingRules.EarnedIncome(Sims3.Gameplay.Careers.GotPaidEvent.PayType.kCareerVacationPay), "income eligibility");
        CastingRules lampbound = new CastingRules(Rank.Master, CasterKind.Genie);
        lampbound.Lampbound = true;
        Check(Math.Abs(BlessingRules.CostFor(lampbound) - 36f) < 0.001f, "Lampbound's ability discount does not reach blessings");
        Check(BlessingRules.DesireLiking == 15f && BlessingRules.WrongDesireLiking == -10f && BlessingRules.FulfilledHours == 24f &&
            BlessingRules.LostInTranslationHours == 6f, "approved Heart's Desire outcomes");
        Check(BlessingRules.FightBonus == 0.20f && BlessingRules.FightCap == 0.95f && BlessingRules.CreativeSpeed == 0.20f,
            "approved fight and creative blessing bonuses");
        Check(BlessingRules.ShieldsSpell(4, false) && !BlessingRules.ShieldsSpell(CastingRules.Legendary, false) && !BlessingRules.ShieldsSpell(4, true),
            "Protection shields ordinary hostile spells but never Legendary magic or a Sim's own casting");
        Check(BlessingRules.Shortened(0f, 60f, true, 0.5f) == 30f && BlessingRules.Shortened(20f, 60f, false, 0.5f) == 40f &&
            BlessingRules.Shortened(30f, 30f, false, 0.5f) == 30f, "only the added part of a moodlet is shortened");
        Check(BlessingRules.CreativeQualityChance == 50f && BlessingRules.HarmonyGain == 1.25f && BlessingRules.HarmonyLoss == 0.75f  &&
            BlessingRules.ResistChance == 40f, "approved quality, relationship and resistance magnitudes");
        Check(BlessingRules.NearlyFinished(95f, 2f) && !BlessingRules.NearlyFinished(90f, 2f) && !BlessingRules.NearlyFinished(99f, 0f) &&
            !BlessingRules.NearlyFinished(100f, 2f), "novels are armed only just before they finish");
        Check(BlessingRules.Attributed(10f, 5f, 15f) && !BlessingRules.Attributed(10f, 5f, 20f) && BlessingRules.Attributed(98f, 5f, 100f) &&
            !BlessingRules.Attributed(10f, 0f, 10f), "relationship changes are corrected only when attributable");
        Check(BlessingRules.Harmonized(10f, 8f) == 20f && BlessingRules.Harmonized(10f, -8f) == 4f && BlessingRules.Harmonized(95f, 8f) == 100f &&
            BlessingRules.Harmonized(-95f, -8f) == -100f, "Harmony scales gains and losses and respects the liking limits");
    }

    private static void Main(string[] args)
    {
        HashSet<WishKind> kinds = new HashSet<WishKind>();
        int lampOnly = 0;
        foreach (WishDefinition wish in WishCatalog.All)
        {
            Check(kinds.Add(wish.Kind), "duplicate wish");
            Check(WishCatalog.Find(wish.Kind) == wish, "lookup identity");
            Check(!WishRules.CanOffer(wish, false, true), "uninstalled expansion offered");
            Check(WishRules.CanOffer(wish, true, true), "lamp catalog lost an installed wish");
            Check(WishRules.CanOffer(wish, true, false) == !wish.LampOnly, "lamp-only wish leaked to playable menu");
            if (wish.LampOnly) lampOnly++;
            Check(!string.IsNullOrEmpty(wish.NameKey), "missing text");
        }
        Check(kinds.Count == 13 && lampOnly == 2, "11 playable wishes and two lamp-only wishes");
        Check(WishCatalog.Find(WishKind.Youth).Product == ProductVersion.EP4, "Youth requires Generations");
        Check(WishCatalog.Find(WishKind.Fame).Product == ProductVersion.EP3, "Fame requires Late Night");
        Check(WishRules.RepossessionCredit(0, 0, 900) == 0, "Repossessing from a debt-free family created debt");
        Check(WishRules.RepossessionCredit(50, 0, 900) == 50, "Credit exceeded the outstanding debt");
        Check(WishRules.RepossessionCredit(1000, 100, 900) == 900, "Native debt reduction was lost");
        Check(WishRules.RepossessionCredit(1000, 400, 900) == 0, "Concurrent debt changes were overwritten");
        Check((int)WishKind.Fortune == 0 && (int)WishKind.Vitality == 1 && (int)WishKind.Heart == 2, "saved queue IDs changed");
        Check((int)WishMode.Other == 0 && (int)WishMode.Guaranteed == 1 && (int)WishMode.Own == 2, "saved mode IDs changed");
        Check(WishRules.CanReceive(false, false, false), "living wisher blocked");
        Check(WishRules.CanReceive(true, true, false), "playable ghost wisher blocked");
        Check(!WishRules.CanReceive(true, false, false), "visiting non-playable ghost accepted");
        Check(!WishRules.CanReceive(true, true, true) && !WishRules.CanReceive(false, false, true), "active death scene can receive wishes");
        Check(WishRules.Fortune == 100000, "Fortune must award exactly 100000");
        for (int escapes = 0; escapes < 1000; escapes++)
        {
            float chance = WishRules.DeathChance(escapes);
            Check(chance >= 0 && chance <= 90, "death protection out of bounds");
            if (escapes > 0 && escapes <= 9) Check(WishRules.DeathChance(escapes - 1) - chance == 10f, "escape reduction not 10 points");
            if (escapes >= 9) Check(chance == 0f, "wish became immortality");
        }
        Check(WishRules.MoreWishesDelta(false, false) == -1 && WishRules.MoreWishesDelta(false, true) == -1, "failed extra wishes refunded");
        Check(WishRules.MoreWishesDelta(true, false) == 0 && WishRules.MoreWishesDelta(true, true) == 1, "native lamp net allowance changed");
        for (uint level = 0; level < 12; level++)
        {
            Check(WishRules.CanGainFame(level, true, true) == (level < 4), "Fame cap");
            Check(!WishRules.CanGainFame(level, false, true) && !WishRules.CanGainFame(level, true, false), "disabled celebrity system");
        }
        Check((int)WishKind.Success == 14 && !kinds.Contains(WishKind.Success) && !kinds.Contains(WishKind.Vitality) &&
            !kinds.Contains(WishKind.Heart), "retired wishes lost saved IDs or entered the catalog");
        Check((int)WishMode.Lamp == 3, "saved lamp mode ID changed");
        TestPower();
        TestAccuracy();
        TestTransaction();
        Check(WishRules.NormalizedPerformance(0.5f, true) == 50f && WishRules.NormalizedPerformance(50f, false) == 50f, "profession and career progress scales differ");
        WishRecord original = new WishRecord();
        Check(!original.CheatDeath && original.Escapes == 0 && original.JoyEnds == 0 && !original.CareerPending, "old-save defaults unsafe");
        Assembly built = Assembly.LoadFrom(args[0]);
        foreach (string name in new[] { "WishRecord", "WishEffectSkill", "LampHandoff", "WishRepoRequest", "WishRepoArrival", "UnrequitedWishInstance", "WishGoneWrongInstance", "WishParticipant", "WishReapSoul", "WishReapSoulDiving", "GrantWish", "GenieBlessing", "HeartsDesire", "GenieBlessing+Definition" })
        {
            Type type = built.GetType("SupernaturalExpanded.Journal." + name, true);
            bool persistable = false;
            foreach (CustomAttributeData attribute in type.GetCustomAttributesData())
                if (attribute.AttributeType.Name == "PersistableAttribute") persistable = true;
            Check(persistable, "nonpersistable long-lived record " + name);
        }
        foreach (string path in new[] { "WishLamp.sHandoffs", "WishRepossession.sRequests", "HeartsDesireLedger.requests" })
        {
            string[] parts = path.Split('.');
            FieldInfo field = built.GetType("SupernaturalExpanded.Journal." + parts[0], true).GetField(parts[1], BindingFlags.Static | BindingFlags.NonPublic);
            bool persistent = false;
            foreach (CustomAttributeData attribute in field.GetCustomAttributesData())
                if (attribute.AttributeType.Name == "PersistableStaticAttribute") persistent = true;
            Check(persistent, "state stored only in transient static: " + path);
        }
        Type wishSkill = built.GetType("SupernaturalExpanded.Journal.WishEffectSkill", true);
        Check(wishSkill.GetMethod("ExportContent", new Type[] { typeof(IPropertyStreamWriter) }).DeclaringType == wishSkill && wishSkill.GetMethod("ImportContent", new Type[] { typeof(IPropertyStreamReader) }).DeclaringType == wishSkill,
            "travel records missing native property-stream support");
        Check(wishSkill.GetMethod("Clone").DeclaringType == wishSkill, "travel clone shares record");
        WishRecord copy = original.Clone(); copy.Escapes = 4;
        Check(original.Escapes == 0, "cloning a Sim shares death protection history");
        WishEffectSkill first = new WishEffectSkill();
        first.Record.CheatDeath = true; first.Record.CheatDeathAttempted = true; first.Record.Escapes = 4;
        first.Record.JoyEnds = 12345; first.Record.SadEnds = 23456;
        first.Record.ScheduleLoveReveal(32222); first.Record.ScheduleLoveReveal(38888); first.Record.ConcealedJoy = true;
        first.Record.CareerPending = true; first.Record.CareerGuid = (Sims3.Gameplay.Careers.OccupationNames)123;
        first.Record.CareerExpires = 34567; first.Record.WorkStarted = true; first.Record.WaitForCurrentShift = true;
        first.Record.PenaltyRemaining = 7.5f; first.Record.LastPerformance = 41.25f; first.Record.LastCareerLevel = 3;
        first.Record.TemporaryLoveTarget = 222UL; first.Record.TemporaryLoveHadNativeFlag = true;
        first.Record.WorldEnds = 32123L; first.Record.WorldKind = 4; first.Record.WorldOwnsTrait = true;
        first.Record.FateSealEnds = 1234L; first.Record.FateVictim = 439UL;
        first.Record.YouthPending = true; first.Record.YouthViewer = 123UL;
        first.Record.FateDeathIssuer = 742UL; first.Record.FateDeathUntil = 674L; first.Record.FateDeathVisible = true;
        first.Record.Blessings()[0] = 1111L; first.Record.Blessings()[4] = 5555L;
        first.Record.Desire = 3; first.Record.DesireGenie = 77UL; first.Record.DesireAsked = 9999L; first.Record.DesireOpen = true;
        first.Record.AthleticBonus = 0.2f; first.Record.InLamp = true; first.Record.BlessingCooldownEnds = 4848L;
        MemoryStreamData stream = new MemoryStreamData();
        Check(first.ExportContent(stream), "native wish property stream export failed");
        WishEffectSkill restored = new WishEffectSkill();
        Check(restored.ImportContent(stream), "native wish property stream import failed");
        Check(restored.Record.CheatDeath && restored.Record.CheatDeathAttempted && restored.Record.Escapes == 4, "travel lost decreasing death protection");
        Check(restored.Record.JoyEnds == 12345 && restored.Record.SadEnds == 23456, "travel reset delayed happiness deadlines");
        Check(restored.Record.CareerPending && restored.Record.CareerGuid == first.Record.CareerGuid && restored.Record.CareerExpires == 34567,
            "travel lost eligible career or penalty deadline");
        Check(restored.Record.WorkStarted && restored.Record.WaitForCurrentShift && restored.Record.PenaltyRemaining == 7.5f
            && restored.Record.LastPerformance == 41.25f && restored.Record.LastCareerLevel == 3, "travel reset workday penalty progress");
        Check(restored.Record.TemporaryLoveTarget == 222UL && restored.Record.TemporaryLoveHadNativeFlag &&
            restored.Record.WorldEnds == 32123L && restored.Record.WorldKind == 4 && restored.Record.WorldOwnsTrait,
            "temporary outcome fields lost across export/import");
        Check(restored.Record.FateSealEnds == 1234L && restored.Record.FateVictim == 439UL &&
            restored.Record.YouthPending && restored.Record.YouthViewer == 123UL, "new long-lived or deferred fields lost across export/import");
        Check(restored.Record.FateDeathIssuer == 742UL && restored.Record.FateDeathUntil == 674L &&
            restored.Record.FateDeathVisible, "Fate death notice lost its request marker across export/import");
        Check(restored.Record.LoveRevealAt == 32222 && restored.Record.ConcealedJoy, "travel lost concealment fields");
        Check(restored.Record.Blessings()[0] == 1111L && restored.Record.Blessings()[4] == 5555L,
            "travel lost active blessings");
        Check(restored.Record.Desire == 3 && restored.Record.DesireGenie == 77UL && restored.Record.DesireAsked == 9999L && restored.Record.DesireOpen,
            "travel lost the revealed desire or its cooldown");
        Check(restored.Record.AthleticBonus == 0.2f && restored.Record.InLamp, "travel lost the saved Athletic modifier or the lamp rest");
        Check(restored.Record.BlessingCooldownEnds == 4848L, "travel lost the blessing cooldown");
        WishRecord blessed = first.Record.Clone(); blessed.Blessings()[0] = 1L;
        Check(first.Record.Blessings()[0] == 1111L, "cloned Sims share blessing timers");
        WishRecord legacy = new WishRecord(); legacy.BlessingEnds = null;
        Check(legacy.Blessings().Length == BlessingRules.Count && legacy.Clone().Blessings().Length == BlessingRules.Count, "older records lack blessing slots");
        TestBlessingRules();
        Check(!restored.Record.TakeLoveReveal(32221) && restored.Record.TakeLoveReveal(32222) &&
            !restored.Record.TakeLoveReveal(32222), "Love disclosure advanced or ran twice");
        Check(!restored.Record.TakeJoyEnd(12344) && restored.Record.TakeJoyEnd(12345) &&
            !restored.Record.TakeJoyEnd(12345), "Happiness transition advanced or ran twice");
        stream.Data.Remove(0xAC520026); stream.Data.Remove(0xAC520027); stream.Data.Remove(0xAC520028);
        Check(restored.ImportContent(stream) && restored.Record.JoyEnds == 12345 && restored.Record.CareerPending &&
            restored.Record.LoveRevealAt == 0 && !restored.Record.ConcealedJoy, "pre-Step-2 wish streams were reset");
        WishEffectSkill travelClone = (WishEffectSkill)first.Clone(null); travelClone.Record.Escapes++;
        Check(first.Record.Escapes == 4 && travelClone.Record.Escapes == 5, "cloned wish histories alias");
        restored.ImportContent(new MemoryStreamData());
        Check(!restored.Record.CheatDeath && restored.Record.Escapes == 0 && !restored.Record.CareerPending && restored.Record.JoyEnds == 0,
            "missing records inherited effects from another Sim or save");
        foreach (string instanceName in new[] { "WishGoneWrongInstance", "UnrequitedWishInstance" })
        {
            Type instanceType = built.GetType("SupernaturalExpanded.Journal." + instanceName, true);
            string fieldName = instanceName == "WishGoneWrongInstance" ? "RomanceEnds" : "belovedId";
            FieldInfo savedField = instanceType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            object savedValue = instanceName == "WishGoneWrongInstance" ? (object)123456L : (object)987654UL;
            Sims3.Gameplay.ActorSystems.BuffInstance buff = (Sims3.Gameplay.ActorSystems.BuffInstance)Activator.CreateInstance(instanceType);
            savedField.SetValue(buff, savedValue);
            Sims3.Gameplay.ActorSystems.BuffInstance buffClone = buff.Clone();
            Check(buffClone.GetType() == instanceType && !object.ReferenceEquals(buff, buffClone), "custom moodlet clone lost its derived type: " + instanceName);
            Check(savedField.GetValue(buffClone).Equals(savedValue), "custom moodlet clone lost wish data: " + instanceName);
            savedField.SetValue(buffClone, instanceName == "WishGoneWrongInstance" ? (object)1L : (object)2UL);
            Check(savedField.GetValue(buff).Equals(savedValue), "custom moodlet clone shares instance state: " + instanceName);
            MemoryStreamData buffStream = new MemoryStreamData();
            Check(buff.ExportContent(null, null, buffStream), "custom moodlet export failed");
            Sims3.Gameplay.ActorSystems.BuffInstance imported = (Sims3.Gameplay.ActorSystems.BuffInstance)Activator.CreateInstance(instanceType);
            imported.ImportContentSpecial(null, null, buffStream);
            Check(savedField.GetValue(imported).Equals(savedValue), "custom moodlet property stream lost wish data: " + instanceName);
            imported.ImportContentSpecial(null, null, new MemoryStreamData());
            Check(savedField.GetValue(imported).Equals(instanceName == "WishGoneWrongInstance" ? (object)0L : (object)0UL), "missing moodlet fields retained another instance's effect");
        }
        object[] snapshots = { 900f, -20, 600f, 1200f, 123456UL, 800f };
        string[] snapshotFields = { "wrongMinutes", "wrongMood", "beautyMinutes", "loveMinutes", "loveTargetId", "joyMinutes" };
        object magicWishSource = Activator.CreateInstance(wishSkill);
        for (int i = 0; i < snapshotFields.Length; i++)
            wishSkill.GetField(snapshotFields[i], BindingFlags.Instance | BindingFlags.NonPublic).SetValue(magicWishSource, snapshots[i]);
        MemoryStreamData snapshotStream = new MemoryStreamData();
        Check((bool)wishSkill.GetMethod("ExportContent", new Type[] { typeof(IPropertyStreamWriter) }).Invoke(magicWishSource, new object[] { snapshotStream }), "travel moodlet snapshot export failed");
        object snapshotRestored = Activator.CreateInstance(wishSkill);
        MethodInfo importSnapshot = wishSkill.GetMethod("ImportContent", new Type[] { typeof(IPropertyStreamReader) });
        Check((bool)importSnapshot.Invoke(snapshotRestored, new object[] { snapshotStream }), "travel moodlet snapshot import failed");
        for (int i = 0; i < snapshotFields.Length; i++)
            Check(wishSkill.GetField(snapshotFields[i], BindingFlags.Instance | BindingFlags.NonPublic).GetValue(snapshotRestored).Equals(snapshots[i]), "travel lost moodlet snapshot " + snapshotFields[i]);
        Check((bool)wishSkill.GetField("restoreTravelBuffs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(snapshotRestored), "travel never restores buffs rejected by EA's custom-enum filter");
        importSnapshot.Invoke(snapshotRestored, new object[] { new MemoryStreamData() });
        Check(!(bool)wishSkill.GetField("restoreTravelBuffs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(snapshotRestored), "empty travel stream inherited custom buffs");
        Type magicType = built.GetType("SupernaturalExpanded.Journal.GenieMagicSkill", true);
        object magic = Activator.CreateInstance(magicType);
        object magicImported = Activator.CreateInstance(magicType);
        string[] magicFields = { "power", "delicateCasting", "exhaustEnds", "vesselEnds", "chainsEnds", "redirectReady", "guaranteeReady" };
        object[] magicValues = { 137.5f, true, 10L, 20L, 30L, 40L, 50L };
        for (int i = 0; i < magicFields.Length; i++)
            magicType.GetField(magicFields[i], BindingFlags.NonPublic | BindingFlags.Instance).SetValue(magic, magicValues[i]);
        MethodInfo exportMagic = magicType.GetMethod("ExportContent", new Type[] { typeof(IPropertyStreamWriter) });
        MethodInfo importMagic = magicType.GetMethod("ImportContent", new Type[] { typeof(IPropertyStreamReader) });
        MemoryStreamData magicStream = new MemoryStreamData();
        Check((bool)exportMagic.Invoke(magic, new object[] { magicStream }), "Genie power/cooldown export failed");
        Check((bool)importMagic.Invoke(magicImported, new object[] { magicStream }), "Genie power/cooldown import failed");
        for (int i = 0; i < magicFields.Length; i++)
            Check(magicType.GetField(magicFields[i], BindingFlags.NonPublic | BindingFlags.Instance).GetValue(magicImported).Equals(magicValues[i]), "Genie travel lost " + magicFields[i]);
        importMagic.Invoke(magicImported, new object[] { new MemoryStreamData() });
        Check(float.IsNaN((float)magicType.GetField("power", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(magicImported)), "missing Genie power inherited a different Sim's value");
        for (int i = 1; i < magicFields.Length; i++)
            Check(magicType.GetField(magicFields[i], BindingFlags.NonPublic | BindingFlags.Instance).GetValue(magicImported).Equals(i == 1 ? (object)false : (object)0L), "missing Genie cooldown retained " + magicFields[i]);
        Type grim = typeof(Sims3.Gameplay.Services.GrimReaperSituation);
        Check(grim.GetField("mIsFirstSim", BindingFlags.NonPublic | BindingFlags.Instance) != null, "native reaping sequence flag missing");
        Check(grim.GetField("mScubaDeathJig", BindingFlags.NonPublic | BindingFlags.Instance) != null, "native diving jig binding missing");
        Type linked = grim.GetNestedType("BeReapedDiving", BindingFlags.NonPublic);
        Check(linked != null && linked.GetField("mHadDeathFlower", BindingFlags.Public | BindingFlags.Instance) != null,
            "native diving second-chance continuation missing");
        Console.WriteLine("PASS: " + checks + " wish policy, transaction, catalog, eligibility, and compiled persistence checks");
        Console.WriteLine("Host checks do not execute native gameplay, animations or a save/load cycle.");
    }

    private static void TestPower()
    {
        Check(WishRules.GuaranteeHours == 168f, "guarantee duration");
        Check(!WishRules.GuaranteeAvailable(1f, true, 101, 100), "guarantee fired early");
        Check(WishRules.GuaranteeAvailable(0.001f, true, 100, 100), "positive power and exact boundary did not qualify");
        Check(!WishRules.GuaranteeAvailable(0f, true, 99, 100), "zero-power guarantee");
        Check(!WishRules.GuaranteeAvailable(10f, false, 0, 100), "unowned guarantee");
        Check(WishRules.IsMenuMode(WishMode.Other) && WishRules.IsMenuMode(WishMode.Own) &&
            WishRules.IsMenuMode(WishMode.Lamp) && !WishRules.IsMenuMode(WishMode.Guaranteed), "obsolete guarantee action is selectable");
        Check(WishRules.ResolveMode(WishMode.Other, .001f, true, 200, 200) == WishMode.Guaranteed,
            "ready guarantee not selected on other-Sim wish");
        Check(WishRules.ResolveMode(WishMode.Other, 300f, true, 201, 200) == WishMode.Other &&
            WishRules.ResolveMode(WishMode.Other, 0f, true, 200, 200) == WishMode.Other &&
            WishRules.ResolveMode(WishMode.Other, 300f, false, 0, 200) == WishMode.Other,
            "guarantee bypassed cooldown, positive power or perk gate");
        Check(WishRules.ResolveMode(WishMode.Own, 300f, true, 0, 200) == WishMode.Own &&
            WishRules.ResolveMode(WishMode.Lamp, 300f, true, 0, 200) == WishMode.Lamp, "guarantee leaked across modes");
        foreach (float capacity in new[] { 50f, 200f, 362.5f })
        {
            Check(WishRules.Cost(WishMode.Other, 10, capacity) == capacity, "ordinary cost diverges from maximum");
            Check(WishRules.Cost(WishMode.Own, 10, capacity) == capacity, "self wish did not share full cost");
            Check(WishRules.Cost(WishMode.Guaranteed, 12.5f, capacity) == 12.5f, "guaranteed wish paid wrong balance");
            Check(WishRules.Cost(WishMode.Lamp, 10, capacity) == 0f, "lamp charged playable power");
            Check(WishRules.OrdinaryAvailable(capacity, capacity, false), "full-power wish blocked");
            Check(!WishRules.OrdinaryAvailable(capacity - 0.001f, capacity, false), "partial power allowed");
            Check(!WishRules.OrdinaryAvailable(capacity + 1, capacity, true), "external refill bypassed exhaustion");
            Check(GeniePowerPolicy.Change(0f, capacity * 2, capacity, true) == capacity * .75f, "large gain bypassed ceiling");
            Check(GeniePowerPolicy.Change(capacity * .75f, 1f, capacity, true) == capacity * .75f, "small gain bypassed ceiling");
            Check(GeniePowerPolicy.Change(capacity * .9f, 1f, capacity, true) == capacity * .9f, "recovery erased power above ceiling");
            Check(GeniePowerPolicy.Change(capacity * .75f, capacity, capacity, false) == capacity, "expiry failed to release ceiling");
            Check(GeniePowerPolicy.Change(5f, -30f, capacity, true) == 0f, "debits produced negative power");
        }
        Check(WishRules.RequiredSuccess(WishMode.Own) && WishRules.RequiredSuccess(WishMode.Guaranteed) &&
            !WishRules.RequiredSuccess(WishMode.Other) && !WishRules.RequiredSuccess(WishMode.Lamp), "wish outcome policy crossed paths");
        foreach (WishDefinition wish in WishCatalog.All)
            Check(WishRules.Concealed(wish.Kind) == (wish.Kind == WishKind.Love || wish.Kind == WishKind.Happiness ||
                wish.Kind == WishKind.CheatDeath), "failure concealment assigned to the wrong wish");
    }

    private static void TestAccuracy()
    {
        int[] mood = { -51, -50, -11, -10, 9, 10, 24, 25, 74, 75 };
        int[] odds = { 60, 65, 65, 70, 70, 75, 75, 75, 75, 80 };
        for (int i = 0; i < mood.Length; i++) Check(WishRules.NpcAccuracy(0f, mood[i]) == odds[i], "native boundary " + mood[i]);
        Check(WishRules.NpcAccuracy(-100f, -100) == 40f && WishRules.NpcAccuracy(100f, 100) == 95f, "NPC accuracy caps");
        Check(WishRules.NpcAccuracy(-25f, 0) == 65f && WishRules.NpcAccuracy(37.5f, 0) == 77.5f, "NPC relationship interpolation");
        Check(WishRules.NpcAccuracy(110f, 0) == 90f && WishRules.NpcAccuracy(-110f, 0) == 50f, "NPC relationship input range");
        Check(WishRules.PlayerAccuracy(63f, true) == 83f && WishRules.PlayerAccuracy(90f, true) == 95f &&
            WishRules.PlayerAccuracy(97f, false) == 95f, "Genie Law bonus or ordinary cap");
        foreach (float accuracy in new[] { 0f, 40f, 70f, 95f })
        {
            int successes = 0;
            const int samples = 20000;
            for (int i = 0; i < samples; i++)
                if (WishRules.OrdinarySucceeds(accuracy, (i + 0.5f) * 100f / samples)) successes++;
            Check(successes * 100f / samples == accuracy, "ordinary wish did not use its calculated accuracy");
            Check(!WishRules.OrdinarySucceeds(accuracy, accuracy), "strict success threshold");
        }
        foreach (WishDefinition wish in WishCatalog.All)
            Check((WishRules.FailureKey(wish.Kind, false) == null) == WishRules.Concealed(wish.Kind),
                "failed wish feedback leaks or goes missing for " + wish.Kind);
        Check(WishRules.FailureKey(WishKind.Fame, true) != WishRules.FailureKey(WishKind.Fame, false),
            "zero-star failure incorrectly reports a scandal");
    }

    private static void TestTransaction()
    {
        WishReservation first = WishReservation.TryAcquire(100, 200);
        Check(first != null && WishReservation.TryAcquire(100, 300) == null &&
            WishReservation.TryAcquire(200, 100) == null && WishReservation.TryAcquire(300, 200) == null,
            "one participant supplied two concurrent wish transactions");
        WishReservation other = WishReservation.TryAcquire(300, 400);
        Check(other != null, "independent wish was blocked");
        other.Dispose();
        first.Dispose();
        WishReservation self = WishReservation.TryAcquire(100, 100);
        first.Dispose();
        Check(self != null && WishReservation.TryAcquire(100, 200) == null, "obsolete scope released a newer reservation");
        self.Dispose();
        Check(WishReservation.TryAcquire(0, 100) == null, "unresolved participant reserved a wish");
        int payments = 0;
        WishCommit commit = new WishCommit(delegate { payments++; });
        Check(!commit.Applied && payments == 0, "tentative wish paid before its effect");
        commit.EffectApplied(); commit.EffectApplied();
        Check(commit.Applied && payments == 1, "duplicate effect signal charged twice");
        WishCommit feedbackError = new WishCommit(delegate { payments++; throw new InvalidOperationException(); });
        try { feedbackError.EffectApplied(); } catch (InvalidOperationException) { }
        feedbackError.EffectApplied();
        Check(feedbackError.Applied && payments == 2, "exception after effect allowed a second payment");
    }

}
