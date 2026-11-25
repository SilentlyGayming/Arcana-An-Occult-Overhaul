using System;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal enum WishKind
    {
        // Vitality, Heart and Success are reserved for old saves.
        Fortune = 0, Vitality = 1, Heart = 2, WorldPeace = 3, Resurrect = 4, MoreWishes = 5,
        Love = 6, LongLife = 7, LargeFamily = 8, Happiness = 9, Beauty = 10, FreeGenie = 11,
        CheatDeath = 12, Youth = 13, Success = 14, Fame = 15
    }

    internal enum WishMode
    {
        Other,
        Guaranteed,
        Own,
        Lamp
    }

    internal sealed class WishDefinition
    {
        internal readonly WishKind Kind;
        internal readonly string NameKey;
        internal readonly ProductVersion Product;
        internal readonly bool LampOnly;

        internal WishDefinition(WishKind kind, string name, ProductVersion product, bool lampOnly)
        {
            Kind = kind; NameKey = name; Product = product; LampOnly = lampOnly;
        }
    }

    internal static class WishCatalog
    {
        internal static readonly WishDefinition[] All =
        {
            new WishDefinition(WishKind.WorldPeace, "Arcana.Genie.Wishes.WorldPeace.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.Resurrect, "Arcana.Genie.Wishes.Resurrect.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.MoreWishes, "Arcana.Genie.Wishes.MoreWishes.Name", ProductVersion.EP6, true),
            new WishDefinition(WishKind.Love, "Arcana.Genie.Wishes.Love.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.LongLife, "Arcana.Genie.Wishes.LongLife.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.LargeFamily, "Arcana.Genie.Wishes.LargeFamily.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.Happiness, "Arcana.Genie.Wishes.Happiness.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.Fortune, "Arcana.Genie.Wishes.Fortune.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.Beauty, "Arcana.Genie.Wishes.Beauty.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.FreeGenie, "Arcana.Genie.Wishes.FreeGenie.Name", ProductVersion.EP6, true),
            new WishDefinition(WishKind.CheatDeath, "Arcana.Genie.Wishes.CheatDeath.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.Youth, "Arcana.Genie.Wishes.Youth.Name", ProductVersion.EP4, false),
            new WishDefinition(WishKind.Fame, "Arcana.Genie.Wishes.Fame.Name", ProductVersion.EP3, false)
        };

        internal static WishDefinition Find(WishKind kind)
        {
            foreach (WishDefinition entry in All) if (entry.Kind == kind) return entry;
            return null;
        }
    }

    internal static class WishRules
    {
        internal const int Fortune = CastingRules.WishFortune;
        internal const float CheatDeathInitialChance = 90f;
        internal const float CareerPenalty = 20f;

        internal static int RepossessionCredit(int debtBefore, int debtAfter, int value)
        {
            int deducted = Math.Min(debtBefore, Math.Max(0, value));
            return debtAfter == debtBefore - deducted ? deducted : 0;
        }

        internal static float DeathChance(int escapes)
        { return Math.Max(0f, CheatDeathInitialChance - 10f * Math.Max(0, escapes)); }

        internal static int MoreWishesDelta(bool success, bool extra)
        { return success ? (extra ? 1 : 0) : -1; }

        internal const float GuaranteeHours = 7f * 24f;
        internal const float MaximumAccuracy = 95f;
        internal const float RomanceMinLiking = -100f;

        internal static bool GuaranteeAvailable(float power, bool ownsPerk, long ready, long now)
        { return ownsPerk && power > 0f && ready <= now; }

        internal static bool OrdinaryAvailable(float power, float capacity, bool exhausted)
        { return !exhausted && capacity > 0f && power >= capacity; }

        internal static float Cost(WishMode mode, float power, float capacity)
        {
            if (mode == WishMode.Lamp) return 0f;
            return mode == WishMode.Guaranteed ? power : capacity;
        }

        internal static float PlayerAccuracy(float accuracy, bool genieLaw)
        { return Math.Max(0f, Math.Min(MaximumAccuracy, accuracy + (genieLaw ? CastingRules.GenieLawWishBonus : 0f))); }

        internal static float NpcAccuracy(float liking, int mood)
        {
            int moodBonus = mood < -50 ? -10 : mood < -10 ? -5 : mood < 10 ? 0 : mood < 75 ? 5 : 10;
            float relationship = 0.2f * Math.Max(-100f, Math.Min(100f, liking));
            return Math.Max(40f, Math.Min(MaximumAccuracy, 70f + relationship + moodBonus));
        }

        internal static bool Concealed(WishKind kind)
        { return kind == WishKind.Love || kind == WishKind.Happiness || kind == WishKind.CheatDeath; }

        internal static bool RequiredSuccess(WishMode mode)
        { return mode == WishMode.Own || mode == WishMode.Guaranteed; }

        internal static bool IsMenuMode(WishMode mode)
        { return mode == WishMode.Other || mode == WishMode.Own || mode == WishMode.Lamp; }

        internal static WishMode ResolveMode(WishMode mode, float power, bool ownsPerk, long ready, long now)
        { return mode == WishMode.Other && GuaranteeAvailable(power, ownsPerk, ready, now) ? WishMode.Guaranteed : mode; }

        internal static bool OrdinarySucceeds(float accuracy, float roll)
        { return roll < accuracy; }

        internal static string FailureKey(WishKind kind, bool scandal)
        {
            switch (kind)
            {
                case WishKind.WorldPeace: return "Arcana.Genie.Wish.Failure.World";
                case WishKind.Resurrect: return "Arcana.Genie.Wish.Failure.Resurrect";
                case WishKind.MoreWishes: return "Arcana.Genie.Wish.Failure.More";
                case WishKind.LongLife: return "Arcana.Genie.Wish.Failure.LongLife";
                case WishKind.LargeFamily: return "Arcana.Genie.Wish.Failure.Family";
                case WishKind.Fortune: return "Arcana.Genie.Wish.Failure.Fortune";
                case WishKind.Beauty: return "Arcana.Genie.Wish.Failure.Beauty";
                case WishKind.FreeGenie: return "Arcana.Genie.Wish.Failure.Freedom";
                case WishKind.Youth: return "Arcana.Genie.Wish.Failure.Youth";
                case WishKind.Fame: return scandal ? "Arcana.Genie.Wish.Failure.Scandal" : "Arcana.Genie.Wish.Failure.Fame";
                default: return null;
            }
        }

        internal static bool CanReceive(bool dead, bool playableGhost, bool beingReaped)
        { return (!dead || playableGhost) && !beingReaped; }

        internal static bool CanOffer(WishDefinition wish, bool installed, bool lamp)
        { return wish != null && installed && (!wish.LampOnly || lamp); }

        internal static float NormalizedPerformance(float performance, bool xpBased)
        { return xpBased ? performance * 100f : performance; }

        internal static bool CanGainFame(uint level, bool installed, bool enabled)
        { return installed && enabled && level < 4; }
    }
}
