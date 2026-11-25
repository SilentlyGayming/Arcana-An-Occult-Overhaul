using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.SimIFace.Enums;
using Sims3.UI;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal static class GenieMenu
    {
        // A callback keeps a failed item visible in the stock pie menu.
        internal static readonly GreyedOutTooltipCallback SilentUnavailable = delegate { return string.Empty; };

        internal static string Root(bool female)
        {
            return SelectRoot(OccultGenie.CleanSim.Singleton.GetPath(female), JournalText.Get("Arcana.Genie.Menu"));
        }

        internal static string SelectRoot(string[] nativePath, string fallback)
        { return nativePath != null && nativePath.Length > 0 && !string.IsNullOrEmpty(nativePath[0]) ? nativePath[0] : fallback; }
    }

    internal static class GenieWishMenu
    {
        [Persistable(false)] private static InteractionDefinition originalLampWish;

        internal static void Prepare()
        {
            FieldInfo field = typeof(OccultGenie.MakeWish).GetField("Singleton", BindingFlags.Public | BindingFlags.Static);
            if (field == null) throw new InvalidOperationException("Missing native MakeWish singleton.");
            InteractionDefinition current = field.GetValue(null) as InteractionDefinition;
            if (current == GrantWish.LampSingleton) return;
            if (current == null || current.GetType().DeclaringType != typeof(OccultGenie.MakeWish)) return;
            originalLampWish = current;
            field.SetValue(null, GrantWish.LampSingleton);
        }

        internal static void Restore()
        {
            FieldInfo field = typeof(OccultGenie.MakeWish).GetField("Singleton", BindingFlags.Public | BindingFlags.Static);
            if (originalLampWish != null && field != null && field.GetValue(null) == GrantWish.LampSingleton)
                field.SetValue(null, originalLampWish);
            originalLampWish = null;
        }

        internal static bool IsLampWish(InteractionDefinition definition)
        {
            GrantWish.Definition arcana = definition as GrantWish.Definition;
            return arcana != null ? arcana.Mode == WishMode.Lamp : definition != null &&
                (definition == originalLampWish || definition.GetType().DeclaringType == typeof(OccultGenie.MakeWish));
        }

        internal static void Install(Sim sim)
        {
            if (sim == null) return;
            sim.Interactions.RemoveAll(delegate(InteractionObjectPair pair)
            { return pair.InteractionDefinition is GrantWish.Definition || originalLampWish != null && IsLampWish(pair.InteractionDefinition); });
            foreach (InteractionDefinition definition in GrantWish.Singletons) sim.AddInteraction(definition, true);
            if (originalLampWish != null) sim.AddInteraction(GrantWish.LampSingleton, true);
        }
    }

    internal sealed class GenieWishChoice : IGenieWish
    {
        internal readonly WishKind Kind;
        private readonly string name;
        private readonly string description;
        internal readonly string NativeIcon;
        private readonly bool disabled;

        internal GenieWishChoice(WishKind kind, string title, string details, string nativeIcon, bool disabled)
        { Kind = kind; name = title; description = details; NativeIcon = nativeIcon; this.disabled = disabled; }

        public string GetWishName() { return name; }
        public string GetWishDescription() { return description; }
        public bool IsDisabled() { return disabled; }
        public ResourceKey PrimaryIconKey
        {
            get
            {
                ResourceKey key = ResourceKey.CreatePNGKey(NativeIcon, ResourceUtils.ProductVersionToGroupId(ProductVersion.EP6));
                if (!World.ResourceExists(key)) key.GroupId = 0u;
                return key;
            }
        }
    }

    internal static class GenieWishDialog
    {
        internal static GenieWishChoice Show(Sim genie, Sim wisher, WishMode mode)
        {
            List<IGenieWish> choices = Build(genie, wisher, mode);
            return choices.Count == 0 ? null : GenieWishSelectionDialog.Show(choices) as GenieWishChoice;
        }

        internal static List<IGenieWish> Build(Sim genie, Sim wisher, WishMode mode)
        {
            List<IGenieWish> choices = new List<IGenieWish>();
            foreach (WishDefinition entry in WishCatalog.All)
            {
                if (!WishRules.CanOffer(entry, GameUtils.IsInstalled(entry.Product), mode == WishMode.Lamp)) continue;
                bool evil = wisher.HasTrait(TraitNames.Evil);
                bool disabled = !WishEffects.Eligible(genie, wisher, entry.Kind, mode);
                if (disabled && (mode != WishMode.Lamp || NativeName(entry.Kind, evil) == null)) continue;
                string name = DisplayName(entry, evil, wisher.IsFemale);
                string description = disabled ? JournalText.Get("Arcana.Genie.Wishes.Unavailable")
                    : Description(entry.Kind, evil, wisher.IsFemale);
                choices.Add(new GenieWishChoice(entry.Kind, name, description, Icon(entry.Kind, evil), disabled));
            }
            return choices;
        }

        internal static string DisplayName(WishDefinition entry, bool evil, bool female)
        {
            string nativeName = NativeName(entry.Kind, evil);
            if (nativeName != null)
                return Localization.LocalizeString(female, "Gameplay/ActorSystems/OccultGenie/MakeWish:" + nativeName);
            switch (entry.Kind)
            {
                case WishKind.CheatDeath: return JournalText.Get("Arcana.Genie.Wish.Dialog.CheatDeath");
                case WishKind.Youth: return JournalText.Get("Arcana.Genie.Wish.Dialog.Youth");
                case WishKind.Fame: return JournalText.Get("Arcana.Genie.Wish.Dialog.Fame");
                default: return JournalText.Get(entry.NameKey);
            }
        }

        internal static string NativeDescriptionName(WishKind kind, bool evil)
        {
            string name = NativeName(kind, evil);
            return name == null ? null : name.Substring(0, name.Length - 4) + "Description";
        }

        internal static string Description(WishKind kind, bool evil, bool female)
        {
            string name = NativeName(kind, evil);
            if (name != null)
                return new OccultGenie.GenieWishInstance(name, NativeDescriptionName(kind, evil), null, false, female).GetWishDescription();
            switch (kind)
            {
                case WishKind.CheatDeath: return JournalText.Get("Arcana.Genie.Wish.Dialog.Description.CheatDeath");
                case WishKind.Youth: return JournalText.Get("Arcana.Genie.Wish.Dialog.Description.Youth");
                case WishKind.Fame: return JournalText.Get("Arcana.Genie.Wish.Dialog.Description.Fame");
                default: return string.Empty;
            }
        }

        internal static bool HasAvailable(Sim genie, Sim wisher, WishMode mode)
        {
            foreach (WishDefinition entry in WishCatalog.All)
                if (WishEffects.Eligible(genie, wisher, entry.Kind, mode)) return true;
            return false;
        }

        internal static string NativeName(WishKind kind, bool evil)
        {
            switch (kind)
            {
                case WishKind.Fortune: return "FortuneName";
                case WishKind.WorldPeace: return evil ? "WorldMiseryName" : "WorldPeaceName";
                case WishKind.Resurrect: return "RaiseDeadName";
                case WishKind.MoreWishes: return "MoreWishesName";
                case WishKind.Love: return "MakeSimLoveMeName";
                case WishKind.LongLife: return "LongLifeName";
                case WishKind.LargeFamily: return "LargeFamilyName";
                case WishKind.Happiness: return "HappinessName";
                case WishKind.Beauty: return "BeautyName";
                case WishKind.FreeGenie: return "FreeGenieName";
                default: return null;
            }
        }

        internal static bool TryNativeAnimation(WishKind kind, bool evil, out GenieWishTypes animation)
        {
            switch (kind)
            {
                case WishKind.Fortune: animation = GenieWishTypes.Fortune; return true;
                case WishKind.Beauty: animation = GenieWishTypes.Beauty; return true;
                case WishKind.LongLife: animation = GenieWishTypes.LongLife; return true;
                case WishKind.Happiness: animation = GenieWishTypes.Happiness; return true;
                case WishKind.LargeFamily: animation = GenieWishTypes.LargeFamily; return true;
                case WishKind.Love: animation = GenieWishTypes.Love; return true;
                case WishKind.WorldPeace: animation = evil ? GenieWishTypes.WorldMisery : GenieWishTypes.WorldPeace; return true;
                case WishKind.FreeGenie: animation = GenieWishTypes.FreeGenie; return true;
                case WishKind.MoreWishes: animation = GenieWishTypes.MoreWishes; return true;
                default:
                    animation = default(GenieWishTypes);
                    return false;
            }
        }

        internal static string Icon(WishKind kind, bool evil)
        {
            string nativeName = NativeName(kind, evil);
            if (nativeName == null)
                nativeName = kind == WishKind.CheatDeath ? "RaiseDeadName" : kind == WishKind.Youth ? "LongLifeName"
                    : kind == WishKind.Fame ? "BeautyName" : "HappinessName";
            return "w_genieWish_" + nativeName;
        }
    }
}
