namespace SupernaturalExpanded.Journal
{
    internal enum CasterKind
    {
        Witch,
        Fairy,
        Genie
    }

    internal abstract class CasterProfile
    {
        internal const int InnateHighestTier = 2;

        internal static readonly CasterProfile Witch = new WitchProfile();
        internal static readonly CasterProfile Fairy = new FairyProfile();
        internal static readonly CasterProfile Genie = new GenieProfile();

        internal static CasterProfile For(CasterKind kind)
        {
            switch (kind)
            {
                case CasterKind.Fairy: return Fairy;
                case CasterKind.Genie: return Genie;
                default: return Witch;
            }
        }

        internal abstract float Capacity(Rank rank);

        internal virtual bool Innate { get { return true; } }
        internal virtual float BaseAccuracy { get { return 60f; } }
        internal virtual float InnateAccuracy { get { return 0f; } }
        internal virtual float InnateSpellCost { get { return 1f; } }
        internal virtual int HighestLearnableTier { get { return InnateHighestTier; } }
        internal virtual float RankRegeneration(Rank rank) { return 1f; }
        internal virtual float RankAccuracy(Rank rank) { return 5f * (int)rank; }
        internal virtual float FizzleDuration(Rank rank) { return 1f; }
        internal virtual float EarlyTierDiscount(Rank rank) { return 0f; }
        internal virtual float RankDiscount(Rank rank) { return 0f; }
        internal virtual bool Backlash { get { return false; } }
        internal virtual float ReboundChance(int tier) { return 0f; }
        internal virtual float BacklashFloor(int tier) { return 0f; }
    }

    internal sealed class WitchProfile : CasterProfile
    {
        internal const float NoviceCapacity = 50f;
        internal const float MasterFizzleDuration = 0.80f;
        internal const float ApprenticeDiscount = 0.05f;
        internal const float HighWitchDiscount = 0.05f;

        internal override bool Innate { get { return false; } }
        internal override float BaseAccuracy { get { return 50f; } }
        internal override int HighestLearnableTier { get { return CastingRules.Legendary - 1; } }
        internal override float Capacity(Rank rank) { return NoviceCapacity + 25f * (int)rank; }
        internal override float RankRegeneration(Rank rank) { return rank >= Rank.HighWitch ? 1.15f : rank >= Rank.Adept ? 1.10f : 1f; }
        internal override float FizzleDuration(Rank rank) { return rank >= Rank.Master ? MasterFizzleDuration : 1f; }
        internal override float EarlyTierDiscount(Rank rank) { return rank >= Rank.Apprentice ? ApprenticeDiscount : 0f; }
        internal override float RankDiscount(Rank rank) { return rank == Rank.HighWitch ? HighWitchDiscount : 0f; }
    }

    internal sealed class FairyProfile : CasterProfile
    {
        internal const float InnateAccuracyBonus = 25f;
        internal const float FixedCapacity = 100f;

        internal override float InnateAccuracy { get { return InnateAccuracyBonus; } }
        internal override float Capacity(Rank rank) { return FixedCapacity; }
    }

    internal sealed class GenieProfile : CasterProfile
    {
        internal const float FirstRankCapacity = 100f;
        internal const float CapacityPerRank = 50f;
        internal const float BorrowedSpellCost = 1.25f;
        internal const float AccuracyPerRank = 2f;
        private static readonly float[] Rebound = { 0f, 5f, 10f, 15f, 20f };
        private static readonly float[] Floor = { 5f, 8f, 12f, 16f, 20f };

        internal override float InnateSpellCost { get { return BorrowedSpellCost; } }
        internal override bool Backlash { get { return true; } }
        internal override float Capacity(Rank rank) { return FirstRankCapacity + CapacityPerRank * (int)rank; }
        internal override float RankAccuracy(Rank rank) { return AccuracyPerRank * (int)rank; }

        internal override float ReboundChance(int tier)
        {
            return tier >= 0 && tier < Rebound.Length ? Rebound[tier] : 0f;
        }

        internal override float BacklashFloor(int tier)
        {
            return tier >= 0 && tier < Floor.Length ? Floor[tier] : 0f;
        }
    }
}
