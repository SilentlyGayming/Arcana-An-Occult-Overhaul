using System;
using System.Collections.Generic;
using Sims3.Gameplay;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.CelebritySystem;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects;
using Sims3.Gameplay.Objects.HobbiesSkills;
using Sims3.Gameplay.Opportunities;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal sealed class WishSelection
    {
        internal Sim Lover;
        internal Urnstone Grave;
    }

    internal static class WishEffects
    {
        internal static bool CanReceive(Sim target)
        {
            if (target == null || target.HasBeenDestroyed || target.SimDescription == null || !target.SimDescription.IsHuman ||
                target.BuffManager == null || target.TraitManager == null) return false;
            InteractionInstance current = target.CurrentInteraction;
            bool reaping = target.DeathReactionBroadcast != null || current is Urnstone.KillSim || current != null && current.InteractionDefinition is SocialInteractionB.DefinitionDeathInteraction;
            return WishRules.CanReceive(target.SimDescription.IsDead, (target.SimDescription.IsPlayableGhost || target.SimDescription.IsGhost && target.IsSelectable), reaping);
        }

        internal static bool Eligible(Sim genie, Sim target, WishKind kind, WishMode mode)
        {
            if (!CanReceive(target)) return false;
            WishDefinition definition = WishCatalog.Find(kind);
            if (definition == null || !WishRules.CanOffer(definition, GameUtils.IsInstalled(definition.Product), mode == WishMode.Lamp)) return false;
            SimDescription sim = target.SimDescription;
            switch (kind)
            {
                case WishKind.WorldPeace:
                    WishRecord world = WishState.Read(sim, false);
                    return (world == null || world.WorldEnds == 0) &&
                        !target.HasTrait(target.HasTrait(TraitNames.Evil) ? TraitNames.WishedWorldMisery : TraitNames.WishedWorldPeace);
                case WishKind.Resurrect: return Graves(target).Count > 0;
                case WishKind.MoreWishes: return WishLamp.Lamp(genie) != null;
                case WishKind.Love: return Lovers(target).Count > 0 && (!sim.IsEP11Bot || target.HasTrait(TraitNames.CapacityToLoveChip));
                case WishKind.LongLife: return !sim.IsEP11Bot && !sim.IsFrankenstein && !target.HasTrait(TraitNames.WishedForLongLife) && sim.AgingState != null;
                case WishKind.LargeFamily: return !sim.IsMummy && !target.IsSimBot && !sim.IsEP11Bot &&
                    !target.HasTrait(TraitNames.WishedForLargeFamily);
                case WishKind.Happiness:
                    WishRecord joy = WishState.Read(sim, false);
                    return (joy == null || joy.JoyEnds == 0) && !target.BuffManager.HasElement(WishBuffs.ConcealedHappiness) &&
                        !target.HasTrait(TraitNames.WishedForHappiness) && !target.BuffManager.HasElement(WishBuffs.BorrowedJoy) && (!sim.IsEP11Bot || target.HasTrait(TraitNames.HumanEmotionChip));
                case WishKind.Fortune: return sim.Household != null;
                case WishKind.Beauty: return sim.TeenOrAbove && !target.HasTrait(TraitNames.WishedForBeauty) && !WishBuffs.HasBeautyPenalty(target);
                case WishKind.FreeGenie: return WishLamp.CanFree(genie, target);
                case WishKind.CheatDeath:
                    WishRecord protection = WishState.Read(sim, false);
                    return !sim.IsDead && sim.TeenOrAbove && !sim.IsEP11Bot && (protection == null || !protection.CheatDeath && !protection.CheatDeathAttempted);
                case WishKind.Youth: return CanUseYouth(target);
                case WishKind.Fame: return sim.TeenOrAbove && target.CelebrityManager != null && WishRules.CanGainFame(target.CelebrityManager.Level,
                    GameUtils.IsInstalled(ProductVersion.EP3), sim.CanBeCelebrity);
            }
            return false;
        }

        private static bool CanUseYouth(Sim target)
        {
            if (!WishAgeDown.CanRun || !GameUtils.IsInstalled(ProductVersion.EP4) || !target.SimDescription.Adult && !target.SimDescription.Elder) return false;
            GreyedOutTooltipCallback tooltip = null;
            return target.SimDescription.AgingState != null && new YoungAgainPotion { IsMixedDrink = true }.UsePotionTest(target, ref tooltip);
        }

        private static bool Romantic(Sim wisher, Sim other)
        {
            if (other == null || other == wisher || other.HasBeenDestroyed || other.SimDescription == null || !CanReceive(other) ||
                !wisher.SimDescription.TeenOrAbove || !other.SimDescription.TeenOrAbove || wisher.SimDescription.Teen != other.SimDescription.Teen ||
                GenieAbilities.IsGenie(other) || (other.OccultManager != null && other.OccultManager.HasOccultType(OccultTypes.TimeTraveler)) ||
                other.SimDescription.IsEP11Bot != wisher.SimDescription.IsEP11Bot) return false;
            if (wisher.Genealogy == null || other.Genealogy == null || wisher.Genealogy.IsSufficientlyRelatedToRuleOutRomance(other.Genealogy)) return false;
            return OccultImaginaryFriend.CanSimGetRomanticWithSim(wisher, other) && wisher.CanGetRomantic(other, false) &&
                WishConsequences.CanStartLove(wisher, other);
        }

        internal static List<Sim> Lovers(Sim target)
        {
            List<Sim> result = new List<Sim>();
            if (target.SocialComponent != null)
                foreach (Sim other in target.SocialComponent.GetRelationshipsWithLikingAbove(WishRules.RomanceMinLiking)) if (Romantic(target, other)) result.Add(other);
            return result;
        }

        internal static List<Urnstone> Graves(Sim wisher)
        {
            List<Urnstone> result = new List<Urnstone>();
            foreach (Urnstone grave in Sims3.Gameplay.Queries.GetObjects<Urnstone>())
                if (CanUseGrave(grave) && (grave.LotCurrent == LotManager.ActiveLot || grave.LotCurrent == wisher.LotCurrent ||
                    wisher.Inventory != null && wisher.Inventory.Contains(grave) ||
                    wisher.Household != null && grave.LotCurrent == wisher.LotHome))
                    result.Add(grave);
            if (wisher.SocialComponent != null)
            {
                foreach (Sim known in wisher.SocialComponent.GetRelationshipsWithLikingAbove(WishRules.RomanceMinLiking))
                {
                    if (known == null || known.SimDescription == null || !known.SimDescription.IsPlayableGhost) continue;
                    Urnstone grave = Urnstone.FindGhostsGrave(known);
                    if (CanUseGrave(grave) && !result.Contains(grave)) result.Add(grave);
                }
            }
            return result;
        }

        private static bool CanUseGrave(Urnstone grave)
        {
            if (grave == null || grave.HasBeenDestroyed || grave.InUse) return false;
            SimDescription dead = grave.DeadSimsDescription;
            if (dead == null || !dead.IsGhost || !dead.IsDead) return false;
            Sim ghost = dead.CreatedSim;
            return ghost == null || ghost.DeathReactionBroadcast == null && !(ghost.CurrentInteraction is Urnstone.KillSim) &&
                (ghost.CurrentInteraction == null || !(ghost.CurrentInteraction.InteractionDefinition is SocialInteractionB.DefinitionDeathInteraction));
        }

        internal static WishSelection Select(Sim wisher, WishKind kind)
        {
            WishSelection result = new WishSelection();
            if (kind == WishKind.Love) result.Lover = Pick(Lovers(wisher), kind) as Sim;
            if (kind == WishKind.Resurrect) result.Grave = Pick(Graves(wisher), kind) as Urnstone;
            return kind == WishKind.Love && result.Lover == null || kind == WishKind.Resurrect && result.Grave == null ? null : result;
        }

        private static object Pick<T>(List<T> choices, WishKind kind) where T : class
        {
            List<ObjectPicker.RowInfo> rows = new List<ObjectPicker.RowInfo>();
            foreach (T choice in choices)
            {
                Sim sim = choice as Sim;
                Urnstone grave = choice as Urnstone;
                SimDescription desc = sim != null ? sim.SimDescription : grave.DeadSimsDescription;
                rows.Add(new ObjectPicker.RowInfo(choice, new List<ObjectPicker.ColumnInfo> {
                    new ObjectPicker.ThumbAndTextColumn(desc.GetThumbnailKey(ThumbnailSize.Large, 0), desc.FullName) }));
            }
            string tabIcon = kind == WishKind.Resurrect ? "DeadSimImage" : "RelationshipSimImage";
            List<ObjectPicker.RowInfo> selected = ObjectPickerDialog.Show(true, ModalDialog.PauseMode.PauseSimulator,
                GenieWishDialog.DisplayName(WishCatalog.Find(kind), false, false), Localization.LocalizeString("Ui/Caption/ObjectPicker:OK"),
                Localization.LocalizeString("Ui/Caption/ObjectPicker:Cancel"),
                new List<ObjectPicker.TabInfo> { new ObjectPicker.TabInfo(tabIcon, Localization.LocalizeString("Ui/Tooltip/CAS/LoadSim:Header"), rows) },
                new List<ObjectPicker.HeaderInfo> { new ObjectPicker.HeaderInfo("Ui/Caption/ObjectPicker:Name", "Ui/Tooltip/ObjectPicker:Name", 500) }, 1);
            return selected == null || selected.Count == 0 ? null : selected[0].Item;
        }

        internal static bool ValidateSelection(Sim wisher, WishKind kind, WishSelection selection)
        {
            return selection != null && (kind != WishKind.Love || Lovers(wisher).Contains(selection.Lover)) &&
                (kind != WishKind.Resurrect || Graves(wisher).Contains(selection.Grave));
        }

        internal static bool Apply(Sim genie, Sim wisher, WishKind kind, bool success, WishSelection selected, WishCommit commit)
        {
            if (!success) return Fail(genie, wisher, kind, selected, commit);
            SimDescription sim = wisher.SimDescription;
            switch (kind)
            {
                case WishKind.WorldPeace:
                    return WishConsequences.World(wisher, true, commit);
                case WishKind.Resurrect:
                    return ResolveGhost(wisher, selected.Grave, true, commit);
                case WishKind.MoreWishes:
                    commit.EffectApplied();
                    EventTracker.SendEvent(EventTypeId.kWishForMoreWishes, wisher);
                    return true;
                case WishKind.Love:
                    return Romantic(wisher, selected.Lover) && WishConsequences.Love(wisher, selected.Lover, true, commit);
                case WishKind.LongLife:
                    if (!GrantTrait(wisher, TraitNames.WishedForLongLife, commit)) return false;
                    EventTracker.SendEvent(EventTypeId.kWishForLongLife, wisher);
                    return true;
                case WishKind.LargeFamily:
                    if (!GrantTrait(wisher, TraitNames.WishedForLargeFamily, commit)) return false;
                    wisher.BuffManager.AddElement(BuffNames.WishForLargeFamily, Origin.FromGenie);
                    EventTracker.SendEvent(EventTypeId.kWishForLargeFamily, wisher);
                    return true;
                case WishKind.Happiness:
                    if (!GrantTrait(wisher, TraitNames.WishedForHappiness, commit)) return false;
                    wisher.BuffManager.AddElement(BuffNames.WishHappiness, Origin.FromGenie);
                    EventTracker.SendEvent(EventTypeId.kWishForHappiness, wisher);
                    return true;
                case WishKind.Fortune:
                    Household household = wisher.Household;
                    if (household == null) return false;
                    int oldFunds = household.FamilyFunds;
                    try { household.ModifyFamilyFunds(WishRules.Fortune); }
                    finally { if (household.FamilyFunds != oldFunds) commit.EffectApplied(); }
                    if (!commit.Applied) return false;
                    EventTracker.SendEvent(EventTypeId.kWishForFortune, wisher);
                    return true;
                case WishKind.Beauty:
                    if (!GrantTrait(wisher, TraitNames.WishedForBeauty, commit)) return false;
                    EventTracker.SendEvent(EventTypeId.kWishForBeauty, wisher);
                    return true;
                case WishKind.FreeGenie:
                    if (!wisher.OpportunityManager.AddOpportunityNow(OpportunityNames.EP6_ReleaseGenie_FreezeLamp, true, false)) return false;
                    commit.EffectApplied();
                    return true;
                case WishKind.CheatDeath:
                    WishRecord protection = WishState.Read(sim, true);
                    if (protection == null) return false;
                    protection.CheatDeath = true;
                    protection.CheatDeathAttempted = true;
                    commit.EffectApplied();
                    return true;
                case WishKind.Youth:
                    return false;
                case WishKind.Fame:
                    uint level = wisher.CelebrityManager.Level;
                    try { wisher.CelebrityManager.ForceSetLevel(level + 1); }
                    finally { if (wisher.CelebrityManager.Level != level) commit.EffectApplied(); }
                    return commit.Applied;
                default:
                    return false;
            }
        }

        private static bool GrantTrait(Sim wisher, TraitNames trait, WishCommit commit)
        {
            try { wisher.TraitManager.AddHiddenElement(trait); }
            finally { if (wisher.HasTrait(trait)) commit.EffectApplied(); }
            return commit.Applied;
        }

        private static bool Fail(Sim genie, Sim wisher, WishKind kind, WishSelection selected, WishCommit commit)
        {
            SimDescription sim = wisher.SimDescription;
            float hours = 24f;
            string moodlet;
            switch (kind)
            {
                case WishKind.WorldPeace:
                    return WishConsequences.World(wisher, false, commit);
                case WishKind.Resurrect:
                    if (!ResolveGhost(wisher, selected.Grave, false, commit)) return false;
                    moodlet = "UnfinishedBusiness";
                    break;
                case WishKind.MoreWishes:
                    commit.EffectApplied();
                    moodlet = "EmptyLamp";
                    break;
                case WishKind.Love:
                    return Romantic(wisher, selected.Lover) && WishConsequences.Love(wisher, selected.Lover, false, commit);
                case WishKind.LongLife:
                    if (!WishAging.LoseDays(wisher, 2f, false)) return false;
                    commit.EffectApplied();
                    moodlet = "BorrowedTime";
                    break;
                case WishKind.LargeFamily:
                    if (!GenieOutcomeBuffs.TryGive(wisher, "EmptyNest", 48f)) return false;
                    commit.EffectApplied();
                    return true;
                case WishKind.Happiness:
                    WishRecord joy = WishState.Read(sim, true);
                    if (joy == null) return false;
                    joy.JoyEnds = WishState.After(24f);
                    joy.SadEnds = WishState.After(48f);
                    joy.ConcealedJoy = true;
                    commit.EffectApplied();
                    WishBuffs.ShowConcealedJoy(wisher);
                    return true;
                case WishKind.Fortune:
                    if (!WishRepossession.Request(wisher)) return false;
                    commit.EffectApplied();
                    moodlet = "RepossessedWish";
                    break;
                case WishKind.Beauty:
                    GenieOutcomeBuffs.Give(wisher, "BeautyTurned", 48f);
                    commit.EffectApplied();
                    return true;
                case WishKind.FreeGenie:
                    if (!WishLamp.Escape(genie, wisher, commit)) return false;
                    moodlet = "GenieEscaped";
                    break;
                case WishKind.CheatDeath:
                    WishRecord protection = WishState.Read(sim, true);
                    if (protection == null) return false;
                    protection.CheatDeathAttempted = true;
                    commit.EffectApplied();
                    return true;
                case WishKind.Youth:
                    if (!WishAging.LoseDays(wisher, 5f, true)) return false;
                    commit.EffectApplied();
                    moodlet = "FleetingYouth";
                    hours = 48f;
                    break;
                case WishKind.Fame:
                    if (wisher.CelebrityManager.Level > 0)
                    {
                        if (!WishConsequences.Accuse(wisher)) return false;
                        commit.EffectApplied();
                        return true;
                    }
                    moodlet = "BadName";
                    break;
                default:
                    return false;
            }
            GenieOutcomeBuffs.Give(wisher, moodlet, hours);
            commit.EffectApplied();
            return true;
        }

        private static bool ResolveGhost(Sim wisher, Urnstone grave, bool resurrect, WishCommit commit)
        {
            if (!CanUseGrave(grave)) return false;
            SimDescription dead = grave.DeadSimsDescription;
            Sim original = dead.CreatedSim;
            try
            {
                if (!SpawnGhost(wisher, grave)) return false;
                Sim ghost = dead.CreatedSim;
                if (ghost == null || ghost.HasBeenDestroyed) return false;
                if (resurrect)
                {
                    grave.GhostToSim(ghost, true, !dead.IsPlayableGhost);
                    if (dead.IsDead) return false;
                    commit.EffectApplied();
                    ghost.UpdateWalkStyle();
                    EventTracker.SendEvent(EventTypeId.kResurrectedSim, wisher, ghost);
                }
                else commit.EffectApplied();
                return true;
            }
            finally
            {
                if (resurrect ? !dead.IsDead : original == null && dead.CreatedSim != null && !dead.CreatedSim.HasBeenDestroyed)
                    commit.EffectApplied();
            }
        }

        private static bool SpawnGhost(Sim wisher, Urnstone grave)
        {
            if (grave == null || grave.HasBeenDestroyed || grave.DeadSimsDescription == null) return false;
            Sim ghost = grave.DeadSimsDescription.CreatedSim;
            if (ghost == null) return grave.GhostSpawn(false, wisher.Position, wisher.LotCurrent, false, 5f);
            if ((ghost.Position - wisher.Position).Length() <= 4f && ghost.LotCurrent == wisher.LotCurrent) return true;
            return GlobalFunctions.PlaceAtGoodLocation(ghost, new World.FindGoodLocationParams(wisher.Position), false)
                && ghost.LotCurrent == wisher.LotCurrent;
        }
    }
}
