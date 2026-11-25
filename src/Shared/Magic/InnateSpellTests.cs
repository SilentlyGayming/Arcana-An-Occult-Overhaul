using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Interfaces;
using Sims3.Gameplay.ObjectComponents;
using Sims3.Gameplay.Services;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using System.Reflection;
using System;

namespace SupernaturalExpanded.Journal
{
    internal static class InnateSpellTests
    {
        private const BuffNames PestilenceBlock = (BuffNames)0x804E0048B3B91820UL;
        private const BuffNames IceBlock = (BuffNames)0x804E0048B3B91830UL;
        private const BuffNames FireBlockA = (BuffNames)0xB82D0015B9294950UL;
        private const BuffNames FireBlockB = (BuffNames)0x04C800171BD46B80UL;
        private const BuffNames ToadBlockA = (BuffNames)0x804E0147B62F2560UL;
        private const BuffNames ToadBlockB = (BuffNames)0x04C8001714E6C330UL;
        private static readonly uint[] BusyRoles = { 140790011U, 140790015U, 209840865U, 246634757U, 230743240U };

        internal static bool Allows(TargetSpell spell, Sim actor, Sim target, ref GreyedOutTooltipCallback tooltip)
        {
            if (spell == null || actor == null || target == null || !SpellKnowledge.Knows(actor, spell.CatalogEntry)) return false;
            CastingRules rules = MagicCasters.ReadRules(actor);
            string id = spell.CatalogEntry.Id;
            bool selfAllowed = id == "good_luck" || id == "love" || id == "sunlight" || spell.CatalogEntry.Category == SpellCategory.Charm;
            if (!selfAllowed && actor == target) return false;
            if (!Social(actor, target)) return false;
            if (MagicCasters.AvailablePower(actor, rules) + 0.005f < rules.FinalCost(spell.BaseCost, spell.Category, 1f))
            {
                tooltip = rules.Caster == CasterKind.Genie ? GenieMenu.SilentUnavailable :
                    InteractionInstance.CreateTooltipCallback(JournalText.Get("Arcana.Cast.NotEnoughPower"));
                return false;
            }
            BuffManager buffs = target.BuffManager;
            switch (id)
            {
                case "pestilence":
                    return buffs == null || !buffs.HasElement(PestilenceBlock);
                case "ice_blast":
                    if (buffs != null && buffs.HasElement(IceBlock)) return false;
                    return NotPregnant(target, ref tooltip);
                case "fire_blast":
                    if (buffs != null && buffs.HasAnyElement(new BuffNames[] { FireBlockA, FireBlockB })) return false;
                    return NotPregnant(target, ref tooltip);
                case "toadification":
                    if (buffs != null && (buffs.HasElement(ToadBlockA) || buffs.HasElement(ToadBlockB))) return false;
                    if ((target.OccultManager != null && target.OccultManager.HasAnyOccultType()) || target.SimDescription.IsGhost) return false;
                    return !(target.Service is GrimReaper);
                default:
                    return true;
            }
        }

        private static bool NotPregnant(Sim target, ref GreyedOutTooltipCallback tooltip)
        {
            if (!target.SimDescription.IsPregnant) return true;
            string text = Localization.LocalizeString(target.IsFemale, "Gameplay/Actors/Sim:PregnantFailure");
            tooltip = delegate { return text; };
            return false;
        }

        private static bool Social(Sim actor, Sim target)
        {
            if (target.SimDescription.IsEP11Bot) return false;
            if (!actor.Posture.AllowsNormalSocials() || !target.Posture.AllowsNormalSocials()) return false;
            if (!actor.SimDescription.ShowSocialsOnSim) return false;
            if (!target.SimDescription.IsZombie)
            {
                if (!target.SimDescription.ShowSocialsOnSim) return false;
                if (!target.CanBeSocializedWith || !actor.CanBeSocializedWith) return false;
            }
            if (SocialComponent.IsInServicePreventingSocialization(target)) return false;
            if (target.SimDescription.AssignedRole != null)
            {
                uint role = (uint)target.SimDescription.AssignedRole.Type;
                if (Array.IndexOf(BusyRoles, role) >= 0) return false;
            }
            if (target.Service is GrimReaper) return false;
            IPreventSocialization blocker = target.CurrentInteraction as IPreventSocialization;
            if (blocker != null && !blocker.SocializationAllowed(actor, target)) return false;
            return !target.SimDescription.ChildOrBelow && !target.SimDescription.IsPet;
        }
    }

    internal static class InnateObjectTests
    {
        private const BuffNames StatueBlock = (BuffNames)0x804E0048B3B91840UL;
        private const Slot ConvertSlot = (Slot)0xA820F8A6U;

        internal static bool Allows(InteractionDefinition native, Sim actor, ref InteractionInstanceParameters parameters,
            ref GreyedOutTooltipCallback tooltip)
        {
            System.Type owner = native.GetType().DeclaringType;
            GameObject target = parameters.Target as GameObject;
            if (owner == null || actor == null || target == null || target.HasBeenDestroyed) return false;
            switch (owner.Name)
            {
                case "CastFireBlastTerrain":
                    return GroundTarget(actor, parameters.Hit);
                case "CastIceBlastTerrain":
                    return true;
                case "CastIceBlastPlumbing":
                    if (target.UpgradableComponent != null && target.UpgradableComponent.Unbreakable) return false;
                    return target.Repairable == null || !target.Repairable.Broken;
                case "CastFireBlastOnStatue":
                case "CastSunlightCharmOnStatue":
                    return Statue(actor, target as ISculptureFrozenSim);
                case "MagicallyUpgrade":
                    return Upgrade(native, target);
                case "CastConvert":
                    return target.GetContainedObject(ConvertSlot) == null;
                default:
                    return false;
            }
        }

        private static bool GroundTarget(Sim actor, GameObjectHit hit)
        {
            Lot lot = LotManager.GetLotAtPoint(hit.mPoint);
            if (lot == null || lot.LotId == ulong.MaxValue) return false;
            int type = (int)hit.mType;
            if (type >= 8 && type <= 11) return false;
            Route route = actor.CreateRoute();
            return route != null && route.IsPointRoutable(hit.mPoint);
        }

        private static bool Statue(Sim actor, ISculptureFrozenSim statue)
        {
            Sim frozen = statue == null ? null : statue.GetFrozenSim();
            if (frozen == null || frozen == actor || frozen.BuffManager == null || !frozen.BuffManager.HasElement(StatueBlock)) return false;
            return !frozen.SimDescription.ChildOrBelow && !frozen.SimDescription.IsPet;
        }

        private static bool Upgrade(InteractionDefinition definition, GameObject target)
        {
            FieldInfo selected = definition == null ? null : definition.GetType().GetField("SelectedUpgrade", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (selected == null || target.UpgradableComponent == null) return false;
            if (target.Repairable != null && target.Repairable.Broken) return false;
            int upgrade = Convert.ToInt32(selected.GetValue(definition));
            if (upgrade == 1) return target.Repairable != null && target.Repairable.HasChanceOfBreaking();
            ITelevision television = target as ITelevision;
            return upgrade != 10 || television == null || !television.HasAllChannels;
        }
    }
}
