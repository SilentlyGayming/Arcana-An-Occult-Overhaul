using System;

namespace SupernaturalExpanded.Journal
{
    internal enum Rank
    {
        Novice,
        Apprentice,
        Adept,
        Master,
        HighWitch
    }

    internal sealed class Perk
    {
        internal readonly string Key;
        internal readonly Rank Rank;
        internal readonly int Column;
        internal readonly float Shift;
        internal readonly string Name;
        internal readonly int[] Costs;
        internal string Tagline;
        internal string[] Descriptions;
        internal Perk Conflict;
        internal Perk Requires;
        internal Perk[] RequiresAny;
        internal int Index;
        internal bool PreviewOnly;

        internal uint Face;

        internal Perk(string key, Rank rank, int column, float shift, string name, int[] costs)
        {
            Key = key;
            Rank = rank;
            Column = column;
            Shift = shift;
            Name = name;
            Costs = costs;
        }

        internal int Tiers { get { return Costs.Length; } }

        internal string Describe(int tier, string simName)
        {
            int shown = PreviewIndex(tier);
            return Descriptions[shown].Replace("{name}", simName);
        }

        internal int PreviewIndex(int tier)
        {
            return Math.Min(Math.Max(tier, 0), Descriptions.Length - 1);
        }
    }

    internal delegate TooltipModifier[] PerkBenefits(Perk perk, int descriptionIndex);

    internal sealed class PerkConnection
    {
        internal readonly Perk[] Lower;
        internal readonly Perk[] Upper;
        internal readonly uint Color;
        internal readonly bool Side;

        internal PerkConnection(Perk[] lower, Perk[] upper, uint color)
        {
            Lower = lower;
            Upper = upper;
            Color = color;
        }

        internal PerkConnection(Perk from, Perk to, uint color) : this(new[] { from }, new[] { to }, color)
        {
            Side = true;
        }
    }

    internal sealed class PerkPath
    {
        internal readonly string Name;
        internal readonly Perk Entry;

        internal PerkPath(string name, Perk entry)
        {
            Name = name;
            Entry = entry;
        }
    }

    internal sealed class OccultTree
    {
        internal readonly string Name;
        internal readonly string PointsName;
        internal readonly string PointsHelp;
        internal readonly string[] RankNames;
        private readonly string[] rankTitles;
        internal readonly Perk[] Perks;
        internal readonly uint IdBase;
        internal readonly PerkLedger Ledger;
        internal string[] RankUnlocks;
        internal PerkBenefits Benefits;
        internal PerkConnection[] Connections;
        internal PerkPath[] Paths;

        internal OccultTree(uint idBase, string textPrefix, PerkLedger ledger, string name, string pointsName, string pointsHelp,
            string[] rankNames, string[] rankTitles, Perk[] perks)
        {
            IdBase = idBase;
            Ledger = ledger;
            Name = name;
            PointsName = pointsName;
            PointsHelp = pointsHelp;
            RankNames = rankNames;
            this.rankTitles = rankTitles;
            Perks = perks;
            for (int i = 0; i < perks.Length; i++)
            {
                perks[i].Index = i;
                string[] entry = PerkText.Entry(textPrefix, perks[i].Key, perks[i].Tiers);
                perks[i].Tagline = entry[0];
                perks[i].Descriptions = new string[entry.Length - 1];
                Array.Copy(entry, 1, perks[i].Descriptions, 0, entry.Length - 1);
            }
        }

        internal TooltipModifier[] BenefitsFor(Perk perk, int descriptionIndex)
        {
            return Benefits == null ? new TooltipModifier[0] : Benefits(perk, descriptionIndex);
        }

        internal string RankTitle(int rank)
        {
            int index = ClampRank(rank);
            return rankTitles == null
                ? JournalText.Format("Arcana.Common.RankTitle", RankNames[index], Name) : rankTitles[index];
        }

        private int ClampRank(int rank)
        {
            return rank < 0 ? 0 : (rank >= RankNames.Length ? RankNames.Length - 1 : rank);
        }

        internal string RankLabel(int rank)
        {
            rank = ClampRank(rank);
            return rank == RankNames.Length - 1 ? RankTitle(rank) : RankNames[rank];
        }

        internal Perk Find(string key)
        {
            foreach (Perk perk in Perks)
            {
                if (perk.Key == key)
                {
                    return perk;
                }
            }
            return null;
        }

        internal uint Id(PageControl kind, int index)
        {
            return IdBase + (uint)kind * 0x100 + (uint)index;
        }
    }

    internal enum PageControl
    {
        Tile = 0,
        Counter = 1,
        CounterPlate = 2,
        Cost = 3,
        CostPlate = 4,
        RankMarker = 5,
        Page = 6,
        CostIcon = 7,
        Icon = 8,
        Edge = 9,
        Connection = 10,
        PathCaption = 11
    }
}
