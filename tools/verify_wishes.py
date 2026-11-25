"""Source contracts for wish execution. These checks do not run The Sims 3."""
import json
import re
from pathlib import Path
import cs_source

ROOT = Path(__file__).resolve().parents[1]


def check(root=ROOT):
    catalog = cs_source.file_code(root, 'WishCatalog')
    effects = cs_source.class_code(root, 'WishEffects')
    grants = cs_source.class_code(root, 'GenieWishes')
    interaction = cs_source.class_code(root, 'GrantWish')
    names = re.findall(r'new WishDefinition\(WishKind\.(\w+),', catalog)
    assert len(names) == len(set(names)) == 13
    eligible = effects.split('internal static bool Apply(')[0]
    success = effects.split('internal static bool Apply(')[1].split('private static bool Fail(')[0]
    failure = effects.split('private static bool Fail(')[1].split('private static bool ResolveGhost(')[0]
    for name in names:
        for label, source in [('eligibility', eligible), ('success', success), ('failure', failure)]:
            assert 'case WishKind.' + name + ':' in source, name + ' missing ' + label
    assert 'WishRules.Fortune' in success and 'ModifyFamilyFunds' not in failure
    assert 'WishRepossession.Request(wisher)' in failure and 'WishLamp.Escape(genie, wisher, commit)' in failure
    aging = cs_source.file_code(root, 'WishAging')
    consequence = cs_source.class_code(root, 'WishConsequences')
    assert 'WishAging.QueueYouth' in grants and '.DoDrinkBehavior(' not in effects
    assert 'GetMethod("RunNonPersistable"' in aging and 'native.Cleanup()' in aging
    assert 'if (Wisher.SimDescription.YoungAdult) commit.EffectApplied();' in aging
    assert 'GenieWishes.Pay(Actor, Genie, Wisher, WishKind.Youth' in aging
    assert 'RemoveNativeFallback' in aging and 'WishAging.IsReserved(actor, target)' in grants
    assert grants.index('WishAging.QueueYouth') < grants.index('WishCommit commit =')
    assert 'WishAging.LoseDays(wisher, 2f, false)' in failure and 'WishAging.LoseDays(wisher, 5f, true)' in failure
    assert 'case WishKind.Success:' not in effects and 'Success = 14' in catalog
    assert 'ForceSetLevel(level + 1)' in success and 'DisgracefulActionType.FalselyAccused' in consequence
    assert 'WishConsequences.Love(wisher, selected.Lover, true, commit)' in success
    assert 'WishConsequences.Love(wisher, selected.Lover, false, commit)' in failure
    assert 'record.TemporaryLoveTarget' in consequence and 'RemoveRomance(wisher.SimDescription, other, relationship, -100f)' in consequence
    assert 'ScheduleLoveReveal(WishState.After(24f))' in consequence
    assert 'WishConsequences.World(wisher, false, commit)' in failure and 'state.WorldEnds = WishState.After(72f);' in consequence
    assert 'ResolveGhost(wisher, selected.Grave, true, commit)' in success and 'ResolveGhost(wisher, selected.Grave, false, commit)' in failure
    for buff in ['WishHappiness', 'WishForLargeFamily']:
        assert buff in success
    assert grants.index('WishEffects.Eligible') < grants.index('RandomUtil.GetFloat')
    assert grants.index('markCommitted();') < grants.index('WishLamp.Commit')
    assert 'skill.Power = 0f;' in grants and 'MagicCasters.SpendInnate' not in grants
    assert 'if (mode == WishMode.Guaranteed) skill.GuaranteeReady = WishState.After(WishRules.GuaranteeHours);' in grants
    assert 'else skill.ExhaustEnds = WishState.After(CastingRules.ExhaustionDays * 24f);' in grants
    assert 'if (mode == WishMode.Other)' in grants and 'else if (mode == WishMode.Lamp)' in grants
    assert 'WishRules.OrdinarySucceeds' in grants and 'WishRules.NpcAccuracy' in grants
    assert 'if (mode == WishMode.Own) WishFate.Apply(' in grants
    assert 'TryRedirect' not in grants and 'WitchMagicRecovery' not in grants and 'AfterBacklash' not in grants
    assert 'committed = true;' in interaction and '!definition.UsePicker || !WishRules.IsMenuMode(definition.Mode) || committed' in interaction
    assert 'WishReservation.TryAcquire' in interaction and 'using (WishReservation' in interaction
    assert 'DisableSave(this,' in interaction and 'finally' in interaction and 'EnableSave(this)' in interaction
    assert 'RouteToObjectRadiusAndCheckInUse' not in interaction and 'a_genie_clap_x' in interaction
    assert 'StartSync(true, false' in interaction and 'WishRendezvous.Approach' in interaction
    assert 'EnterStateMachine("MakeWish", "Enter", "x", "y")' in interaction
    assert 'new Definition(WishMode.Guaranteed)' not in interaction and 'WishRules.IsMenuMode' in interaction
    dialog = cs_source.file_code(root, 'GenieWishDialog')
    assert 'GenieWishSelectionDialog.Show(choices)' in dialog
    assert 'WishEffects.Eligible(genie, wisher, entry.Kind, mode)' in dialog
    assert 'OccultGenie.CleanSim.Singleton.GetPath(female)' in dialog
    assert 'field.SetValue(null, GrantWish.LampSingleton);' in dialog
    assert 'sim.Interactions.RemoveAll' in dialog and 'IsLampWish(pair.InteractionDefinition)' in dialog
    assert 'GenieWishDialog.Show(genie, wisher, mode)' in interaction
    assert 'if (choice == null || choice.IsDisabled()) return false;' in interaction
    assert interaction.index('GenieWishDialog.Show') < interaction.index('committed = true;')
    assert 'new Definition(WishMode.Other), new Definition(WishMode.Own)' in interaction
    assert 'if (Mode == WishMode.Lamp) return new string[0];' in interaction
    assert 'mode = ResolveMode(mode, rules, skill);' in grants
    assert 'GenieMenu.SilentUnavailable' in grants and 'WishFeedback.Result(' in grants
    retreat = cs_source.class_code(root, 'LampRetreat')
    for invariant in ('FadeOut(true, false)', 'HiddenFlags.Model', 'FadeIn(false,', 'Sim.FXJoints.Spine0', 'SubmitOneShotEffect',
                      'Cleanup()', 'Release(false)', 'record.InLamp = true;', 'record.InLamp = false;', 'HasInteractionOfType(Singleton)', 'AddMotiveDelta(Needs[index], Gain[index])',
                      'CommodityKind.Energy, CommodityKind.Hygiene, CommodityKind.Hunger, CommodityKind.Bladder, CommodityKind.Fun, CommodityKind.Social', 'GetPath(bool female)', 'rules.Rank >= Rank.Adept'):
        assert invariant in retreat, invariant
    assert 'ChangeValue(' not in retreat and 'FreezeDecay(' not in retreat and 'SetIsSleeping(' not in retreat
    assert not re.search(r'\.(?:Destroy|RemoveFromWorld|CancelAllInteractions)\(', retreat)
    assert 'AddElementIgnoreDelay(WishBuffs.HomeSweetLamp, 0, -1f, Origin.None)' in retreat
    assert 'RemoveElement(WishBuffs.HomeSweetLamp)' in retreat
    assert 'DisableSave' not in retreat and 'EnableSave' not in retreat, 'Saving inside the lamp is allowed; the retreat resumes after loading'
    assert 'LampRetreat.Check(sim, state);' in cs_source.file_code(root, 'WishState')
    emergence = retreat.split('private static void PlayEmergence')[1].split('private static bool CanRest')[0]
    assert 'ParentTo' not in emergence and 'SetPosAndOrient(sim.Position, Vector3.UnitX, Vector3.UnitZ)' in emergence
    assert 'JournalBootstrap.RunLater(delegate { PlayEmergence(actor); });' in retreat
    fate = cs_source.class_code(root, 'WishFate')
    assert 'SimDescriptionEvent death = e as SimDescriptionEvent;' in fate
    assert 'marker.FateDeathUntil = 0;' in fate and 'EventTypeId.kSimDied, WishFate.OnDeath' in cs_source.class_code(root, 'WishState')
    repo = cs_source.file_code(root, 'WishRepossession')
    for native in ['Repoman.Instance', 'MakeServiceRequest', 'ServiceSituation.FindServiceSituationInvolving',
                   'RepomanSituation.Repossess', 'base.Run()', 'RouteRepomanToObject', 'TeleportRepomanToObject']:
        assert native in repo
    assert 'int value = Target.Value;' in repo and 'household.DelinquentFunds += WishRules.RepossessionCredit(debt, household.DelinquentFunds, value);' in repo
    assert 'DisableSave(this,' in repo and 'EnableSave(this)' in repo
    lamp = cs_source.file_code(root, 'WishLamp')
    for native in ['PlayReturnToLamp()', 'TryToRemove(lamp)', 'TryToAdd(lamp)', 'FreedomSteps', 'sHandoffs', 'Household.ActiveHousehold']:
        assert native in lamp
    assert not re.search(r'(?:genie|lamp)\.(?:Destroy|Kill)\(', lamp, re.I)
    state = cs_source.file_code(root, 'WishState')
    assert 'WishState.JoyExpired(manager.Actor)' in cs_source.file_code(root, 'WishBuffs')
    assert 'career.Guid != state.CareerGuid' in state and 'CareerExpires' in state and 'WaitForCurrentShift' in state
    assert 'career.UpdatePerformanceOrExperience(-withheld * units)' in state
    assert 'career.Level != state.LastCareerLevel' in state
    death = cs_source.file_code(root, 'WishDeath')
    assert 'ReapSoulDiving' in death and 'ReapSoul' in death and 'return base.Run()' in death
    assert 'DeathType.OldAge' in death and 'record.Escapes++' in death and 'CountEscape' in death
    assert 'GhostToSim(Target, false, false)' in death
    bootstrap = cs_source.class_code(root, 'JournalBootstrap')
    for owner in ['WishLamp', 'WishRepossession']:
        assert owner + '.ResetSaved();' in bootstrap
    assert 'sim.SkillManager.GetElement(WishEffectSkill.WishGuid)' in state
    assert 'WishState.Start();' in bootstrap and 'WishState.Stop();' in bootstrap
    strings = json.loads((root / 'src/Localization/en-US/strings.json').read_text())
    assert not any(key.endswith(".Description") and key.startswith("Arcana.Genie.Wishes.") for key in strings)
    assert 'GenieWishInstance(name, NativeDescriptionName(kind, evil), null, false, female).GetWishDescription()' in dialog
    assert 'CostText(' not in dialog
    assert 'BuffNames.Disappointed' not in effects and 'BuffNames.RejectedProposal' not in effects
    assert 'GenieOutcomeBuffs.Give(wisher, "BeautyTurned", 48f)' in failure and 'WishBuffs.GiveWrong' not in failure
    assert 'WishRules.CanReceive' in effects and 'WishEffects.CanReceive(wisher)' in lamp
    preview = cs_source.class_code(root, 'ArcanaPresentationCheats').split('private static int ForceWish')[0]
    assert 'ObjectPickerDialog.Show(' not in preview and 'StyledNotification.Show(' in preview
    assert 'AddAlarmRepeating' in preview and 'arcanaTestNext' not in preview
    moodlets = cs_source.class_code(root, 'ArcanaMoodletPreview')
    assert 'new Buff(data).CreateBuffInstance()' in moodlets and 'ArcanaPreview:' in moodlets
    assert 'AddElementIgnoreDelay(alias' in moodlets and 'RemoveElement(id)' in moodlets
    assert 'AlarmType.NeverPersisted' in moodlets and 'EnableSave(owner)' in moodlets
    assert 'MoodletPreviewInteraction SaveOwner' in moodlets and 'AddNext(job)' in moodlets
    assert 'subject.CurrentInteraction != job' in moodlets and 'DisableSave(job,' in moodlets
    preview_job = cs_source.class_code(root, 'MoodletPreviewInteraction')
    assert 'Interaction<Sim, Sim>' in preview_job and 'finally { ArcanaMoodletPreview.Finish(this); }' in preview_job
    assert 'override void Cleanup()' in preview_job and 'base.Cleanup()' in preview_job
    assert 'TryGive(wisher, "EmptyNest", 48f)' in failure
    assert 'family.LargeFamilyBlessed = true' not in success and 'UpdateFamily(' not in state
    assert 'RetreatMinutes' not in retreat and 'endAt' not in retreat
    assert 'FateSealEnds > SimClock.CurrentTicks' in cs_source.class_code(root, 'GenieMagicSkill')
    assert not re.search(r'AddElement|RemoveElement|\.Kill\(|ModifyFamilyFunds|SpendInnate|WishEffects.Apply', preview)
    print('PASS: all 13 wish execution branches; native effect/service bindings; single outcome/cost/guarantee order; persistence lifecycle and submenu contracts')
    print('Not tested here: native effects, routing, animation, service arrival, death recovery, save/load or travel inside The Sims 3.')


def fight_odds(odds, bonus=0.20, cap=0.95):
    raised = min(odds + bonus, cap)
    flip = (raised - odds) / (1 - odds) if raised > odds else 0.0
    return odds + (1 - odds) * flip


def check_blessings(root=ROOT):
    blessings = cs_source.file_code(root, 'GenieBlessings')
    hooks = cs_source.file_code(root, 'FightBlessing')
    grant = blessings.split('internal static bool Grant(')[1].split('private static void Revert(')[0]
    assert grant.index('SyncMoodlet(target') < grant.index('MagicCasters.SpendInnate') < grant.index('ShowOutcome(')
    assert grant.index('if (!applied) return false;') < grant.index('record.DesireOpen = false')
    assert grant.index('if (!BlessingRules.CanBless(record.BlessingCooldownEnds, record.Blessings(), now)) return false;') < grant.index('long[] previous = (long[])ends.Clone();')
    assert grant.index('long[] previous = (long[])ends.Clone();') < grant.index('Array.Clear(ends, 0, ends.Length);') < grant.index('SyncMoodlet(target')
    assert grant.index('if (!applied) return false;') < grant.index('record.BlessingCooldownEnds = BlessingRules.CooldownEnds(now);') < grant.index('MagicCasters.SpendInnate')
    assert 'Replace(' not in blessings and 'Refresh(' not in blessings.split('internal static class BlessingRules')[1].split('internal static bool CanReveal(')[0], 'blessings no longer refresh or replace'
    definition = blessings.split('public sealed class GenieBlessing : GenieSocial')[1].split('public sealed class HeartsDesire')[0]
    assert 'GenieBlessings.CanBless(Target)' in definition and 'GenieBlessings.CanBless(target)' in definition and 'Arcana.Genie.Blessing.Cooldown' in definition
    hearts = blessings.split('public sealed class HeartsDesire')[1].split('internal static class BlessingPresentation')[0]
    assert 'CanBless' not in hearts, 'asking about a Heart\'s Desire never involves the blessing cooldown'
    record_io = cs_source.class_code(root, 'WishEffectSkill')
    assert 'StreamVersion = 5' in record_io and 'WriteInt64(0xAC520042, value.BlessingCooldownEnds)' in record_io
    assert 'if (version >= 5) reader.ReadInt64(0xAC520042, out record.BlessingCooldownEnds, 0);' in record_io
    revert = blessings.split('private static void Revert(')[1].split('private static DesireOutcome Outcome(')[0]
    assert 'Array.Copy(previous, record.Blessings(), previous.Length);' in revert and 'Update(target, record)' in revert, 'a failed replacement restores the previous blessing'
    has = blessings.split('internal static bool Has(')[1].split('internal static bool CanReceive(')[0]
    assert 'BlessingRules.Survivor(record.Blessings(), SimClock.CurrentTicks) == (int)kind' in has, 'gameplay hooks see one blessing even before an old save is reconciled'
    update = blessings.split('internal static void Update(Sim sim, WishRecord record)')[1].split('private static void SettleDurations(')[0]
    assert update.index('int keep = BlessingRules.Survivor(ends, now);') < update.index('if (index != keep) ends[index] = 0;') < update.index('SyncMoodlet(sim, Moodlet((Blessing)index)')
    assert 'ApplyLuck(sim, effects, ends[(int)Blessing.Fortune] - now)' in update and 'ReleaseLuck(sim, effects);' in blessings
    assert 'GenieOutcomeBuffs' not in update and 'HeartsDesireFulfilled' not in update, 'Heart\'s Desire moodlets stay independent of blessing replacement'
    assert 'Revert(target, record, previous)' in grant and 'record.DesireOpen = false;' not in blessings.split('private static DesireOutcome Outcome(')[1].split('private static void ShowOutcome(')[0]
    assert 'AddElementIgnoreDelay(id, template.EffectValue, minutes, Origin.FromGenie) && sim.BuffManager.HasElement(id)' in blessings
    question = blessings.split('internal static class QuestionSocial')[1]
    assert 'social_generic' not in blessings and 'AnimateJoinSims' not in blessings
    for call in ('action.GetJazzGraph(actor, target)', 'StateMachineClient.Acquire(Simulator.CurrentTask, graph)',
                 'SetProductVersion(action.ProductVersion)', 'SocialRuleRHS.Get(key, CommodityTypes.Friendly)',
                 'RequestState(false, "x", rule.JazzState)', 'RequestState(true, "y", rule.JazzState)'):
        assert call in question, call
    desire = blessings.split('public sealed class HeartsDesire')[1]
    assert 'return Exchange != null && !Interrupted && GenieBlessings.Reveal(Actor, Target);' in desire
    assert 'Exchange.Dispose()' in blessings and 'public override void Cleanup()' in blessings
    assert 'FightBlessing.Install()' in blessings and 'FightBlessing.Remove()' in blessings
    assert 'SocialCallback.DecideWinnerOfFight(actor, target, interaction, topic, i);' in hooks
    assert 'current.Name != NativeName || current.DeclaringType != typeof(SocialCallback)' in hooks
    for name in ('IsBonehilda', 'MartialArts', 'TraitNames.Loser', 'BodyBuilder', 'AthleticSkillFightModifier',
                 'TraitSimFightModifier', 'OddlyPowerful', 'CanApprehendBurglar', 'PoliceFightModifier'):
        assert name in hooks, name
    assert 'ApplyCreativeSpeed(sim, effects, record, inspiration)' in blessings and 'current is Easel.Paint' in hooks
    assert 'current is Computer.WriteNovel' in hooks and 'Math.Min(progress, 100f)' in hooks
    painting = cs_source.file_code(root, 'PaintingBlessing')
    shield = blessings.split('internal static object ShieldSpell(')[1].split('internal static void ShortenSpell(')[0]
    shorten = blessings.split('internal static void ShortenSpell(')[1].split('internal static class DesireScores')[0]
    assert shield.index('SettleDurations(target, effects)') < shield.index('shield.Before[buff.Guid]'), 'earlier moodlets settle before the spell snapshot'
    assert 'effects.Pending.RemoveAll(' in shorten and 'handled.Contains(pending.Guid)' in shorten, 'a spell moodlet is never shortened again by the deferred path'
    assert 'EventTypeId.kStartedPainting, OnPainting' in blessings and 'PaintingBlessing.Improve(sim, canvas)' in blessings
    assert 'canvas.PainterSimId != sim.SimDescription.SimDescriptionId' in painting and '(uint)Stage.GetValue(canvas) != 0u' in painting
    assert 'PaintingSkill.PickPaintingQuality(sim, SkillNames.Painting' in painting and 'if (reroll <= current) return;' in painting
    assert 'GetNumPaintingsPaintedPerSize(canvas.Size)) + 1' in painting and 'World.SetIsPaintingTextureInUse(key, true, false)' in painting
    novel = blessings.split('private static void Arm(')[1].split('private static ListenerAction OnPaid(')[0]
    assert 'book.HasReceivedAdviceFromBoss ||' in novel and 'effects.Armed.HasReceivedAdviceFromBoss = false' in novel
    assert 'Disarm(effects, WishState.Read(effects.Owner, false));' in blessings.split('internal static void Stop()')[1].split('internal static bool Has(')[0]
    assert 'record.AdviceLent = true;' in novel and 'if (record.AdviceLent && (effects == null || effects.Armed == null)) Reclaim(sim, record);' in blessings
    assert blessings.index('Reclaim(sim, record);') < blessings.index('if (!any && effects == null && record.AthleticBonus == 0f) return;'), 'lent advice is reclaimed even after every blessing ends'
    assert '!process.BornToCookEnhanced && process.PreparerId == cook' in painting
    harmony = cs_source.file_code(root, 'HarmonyBlessing')
    assert 'Baselines.Remove(ltr);' in harmony and 'effect.LHS.LTROverride != 0' in harmony
    assert harmony.index('Correct(actor, target, effect)') < harmony.index('call(actor, target, interaction, topic, i)')
    assert 'BlessingRules.Attributed(baseline.Liking, change, ltr.Liking)' in harmony and 'change < 0f && ltr.CurrentLTR != baseline.State' in harmony
    assert 'HarmonyBlessing.Install()' not in blessings and 'HarmonyBlessing.Remove()' in blessings
    assert 'HarmonyBlessing.Sync(sims)' in cs_source.file_code(root, 'WishState'), 'Harmony hooks exist only while a Sim holds the blessing'
    assert 'Originals.Remove(rule)' in harmony and 'Originals.Clear()' not in harmony, 'callbacks wrapped by another mod keep forwarding'
    assert 'RemoveInteractionBit(LongTermRelationship.InteractionBits.MakeEnemy)' in harmony and 'Haunting.Invoke(ltr' in harmony
    assert 'AccidentWard' not in blessings + painting and 'mTuning' not in painting, 'Protection no longer alters repair tuning'
    assert 'ChanceOfRandomFireAfterFailure' not in blessings + painting
    for odds in (0.0, 0.3, 0.5, 0.74, 0.75, 0.8, 0.95, 0.99):
        lifted = fight_odds(odds)
        assert abs(lifted - min(odds + 0.20, max(odds, 0.95))) < 1e-9, odds
    abilities = cs_source.class_code(root, 'GenieAbilityCast')
    fizzle = abilities.split('private bool Fizzle(')[1]
    assert 'a_genie_clap_x' in fizzle and 'ReactionTypes.Shrug, ReactionSpeed.AfterInteraction' in fizzle
    assert 'ep6GenieTargetSimwhirlwind_main' not in abilities, 'The native CleanSim already submits the whirlwind'
    skill = cs_source.class_code(root, 'WishEffectSkill')
    assert 'IconKey' not in skill and 'OccultAssetData' not in skill, 'the hidden storage skill shows a blank icon'
    magic = cs_source.class_code(root, 'GenieMagicSkill')
    assert 'source.IconKey' not in magic and 'source.SkillUIIconKey' not in magic and 'source.DreamsAndPromisesIcon' not in magic, 'Genie Magic never inherits the Spellcasting icon'
    for field in ('data.IconKey = icon;', 'data.SkillUIIconKey = icon;', 'data.DreamsAndPromisesIconKey = icon;', 'data.DreamsAndPromisesIcon = ArtName;'):
        assert field in magic, field
    assert 'new ResourceKey(OccultAssetData.GenieLifeStateArt, 0x2F7D0004, 0)' in magic and 'ArtName = "SupernaturalExpanded.LifeStateArt.Genie"' in magic
    assert 'GenieGuid' in magic and 'casting.NonPersistableData' in magic
    import sys as _sys
    _sys.path.insert(0, str(root / 'tools'))
    from dbpf import fnv64
    assert fnv64('SupernaturalExpanded.LifeStateArt.Genie') == 0x1F9FE1E94FE3CEFA, 'the level-up image name hashes to the packaged art'
    blessing = cs_source.class_code(root, 'GenieBlessing')
    presentation = cs_source.class_code(root, 'BlessingPresentation')
    assert 'BlessingPresentation.Play(Actor, Target)' in blessing and blessing.index('BlessingPresentation.Play') < blessing.index('GenieBlessings.Grant(')
    assert '"MakeWish"' in presentation and 'SetActor("x", recipient)' in presentation and 'SetActor("y", genie)' in presentation
    assert 'RequestState("x", "Exit")' in presentation and 'GenieWishTypes.Happiness' in presentation
    assert 'AdjustRemainingWishes' not in presentation + blessings and 'ep6GenieTargetSimwhirlwind_main' not in blessings, 'blessings never touch lamp wishes or add particles'
    assert 'SkillCategory.Hidden' in skill and 'WishGuid = (SkillNames)0xA9C4B6E3' in skill
    print('PASS: blessing transaction order, native question social, fight, creative, quality, and Harmony hooks, fight odds, failed-cast shrug and hidden skill icon')


if __name__ == '__main__':
    check()
    check_blessings()
