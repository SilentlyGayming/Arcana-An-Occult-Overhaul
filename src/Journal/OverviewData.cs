using System;
using System.Collections.Generic;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal sealed class OverviewTip
    {
        internal readonly string Title;
        internal readonly string Tagline;
        internal readonly string Body;
        internal readonly TooltipModifier[] Modifiers;

        internal OverviewTip(string title, string tagline, string body)
            : this(title, tagline, body, null)
        {
        }

        internal OverviewTip(string title, string tagline, string body, TooltipModifier[] modifiers)
        {
            Title = title ?? "";
            Tagline = tagline ?? "";
            Body = body ?? "";
            Modifiers = modifiers;
        }

        internal static bool Same(OverviewTip a, OverviewTip b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }
            if (a.Title != b.Title || a.Tagline != b.Tagline || a.Body != b.Body)
            {
                return false;
            }
            if (a.Modifiers == null || b.Modifiers == null)
            {
                return a.Modifiers == b.Modifiers;
            }
            if (a.Modifiers.Length != b.Modifiers.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Modifiers.Length; i++)
            {
                if (a.Modifiers[i].Text != b.Modifiers[i].Text || a.Modifiers[i].Positive != b.Modifiers[i].Positive ||
                    a.Modifiers[i].Neutral != b.Modifiers[i].Neutral || a.Modifiers[i].Nested != b.Modifiers[i].Nested ||
                    a.Modifiers[i].Section != b.Modifiers[i].Section)
                {
                    return false;
                }
            }
            return true;
        }
    }

    internal sealed class OverviewRow
    {
        internal readonly UIImage Icon;
        internal readonly string Name;
        internal readonly string Detail;
        internal readonly string Value;
        internal readonly OverviewTip Tip;

        internal OverviewRow(UIImage icon, string name, string detail, string value)
            : this(icon, name, detail, value, null)
        {
        }

        internal OverviewRow(UIImage icon, string name, string detail, string value, OverviewTip tip)
        {
            Icon = icon;
            Name = name ?? "";
            Detail = detail ?? "";
            Value = value ?? "";
            Tip = tip;
        }
    }

    internal sealed class OverviewData
    {
        internal string RankTitle = "";
        internal string RankDescription = "";
        internal int RankLevel;
        internal UIImage RankArtwork = null;
        internal int PerkCount = -1;
        internal readonly List<OverviewRow> Statistics = new List<OverviewRow>();
        internal readonly List<OverviewRow> Perks = new List<OverviewRow>();
        internal readonly List<OverviewRow> Knowledge = new List<OverviewRow>();
        internal string LifeStateTitle = "";
        internal string LifeStateText = "";
        internal UIImage LifeStateIcon;
        internal UIImage LifeStateArt = null;
    }

    internal sealed class OverviewContext
    {
        internal readonly SimIdentity Identity;
        internal readonly OccultTypes Primary;
        internal readonly PowerSelection Selection;
        internal readonly StatusField Power;
        private readonly PowerPage perkPage;
        private readonly GrimoirePage grimoire;

        internal OverviewContext(SimIdentity identity, PowerSelection selection, StatusField power, PowerPage perkPage, GrimoirePage grimoire)
        {
            Identity = identity;
            Primary = OccultAssetData.Primary(identity.Occults);
            Selection = selection;
            Power = power;
            this.perkPage = perkPage;
            this.grimoire = grimoire;
        }

        internal UIImage KnowledgeIcon(int category)
        {
            return grimoire == null ? null : grimoire.GetCategoryIcon(category);
        }

        internal int KnowledgeCount(int category)
        {
            return grimoire == null ? 0 : grimoire.GetEntryCount(category);
        }

        internal UIImage PerkIcon(Perk perk)
        {
            return perkPage == null ? null : perkPage.PerkIcon(perk);
        }
    }

    internal interface IOverviewProvider
    {
        void Fill(OverviewContext context, OverviewData data);
    }

    internal static class OverviewProviders
    {
        private static readonly IOverviewProvider common = new CommonOverview();
        private static readonly IOverviewProvider witch = new WitchOverview();
        private static readonly IOverviewProvider magical = new MagicalOverview();
        private static readonly IOverviewProvider genie = new GenieOverview();

        private static IOverviewProvider For(OccultTypes primary)
        {
            switch (primary)
            {
                case OccultTypes.Witch:
                    return witch;
                case OccultTypes.Fairy:
                    return magical;
                case OccultTypes.Genie:
                    return genie;
                default:
                    return null;
            }
        }

        internal static OverviewData Build(OverviewContext context)
        {
            OverviewData data = new OverviewData();
            common.Fill(context, data);
            IOverviewProvider species = For(context.Primary);
            if (species != null)
            {
                species.Fill(context, data);
            }
            return data;
        }

        private static readonly string[] Numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

        internal static string GetRomanNumeral(int value)
        {
            return value >= 1 && value <= Numerals.Length ? Numerals[value - 1] : value.ToString();
        }

        private sealed class CommonOverview : IOverviewProvider
        {
            public void Fill(OverviewContext context, OverviewData data)
            {
                SimIdentity identity = context.Identity;
                data.LifeStateTitle = JournalText.Format("Arcana.LifeState.Header", identity.LifeState);
                data.LifeStateText = LifeStateDescriptions.For(identity);
                data.LifeStateIcon = OverviewIcons.Species(context.Primary);
                data.LifeStateArt = OverviewIcons.Art(context.Primary);

                string rank = OccultRankLabel.Rank(identity, context.Selection);
                if (rank.Length != 0)
                {
                    data.RankTitle = JournalText.Format("Arcana.Journal.Overview.Rank.Header", rank);
                    data.RankLevel = 1;
                }
                else if (OccultRankLabel.FirstRank(context.Primary).Length != 0)
                {
                    data.RankTitle = JournalText.Format("Arcana.Journal.Overview.Rank.Header", OccultRankLabel.FirstRank(context.Primary));
                    data.RankDescription = OccultRankLabel.FirstRankDescription(context.Primary, identity.FirstName);
                }
                else
                {
                    data.RankTitle = JournalText.Get("Arcana.Journal.Overview.Rank.Unranked");
                    data.RankDescription = JournalText.Get("Arcana.Journal.Overview.Rank.Unimplemented");
                }

                StatusField power = context.Power;
                if (power != null && power.Available)
                {
                    data.Statistics.Add(new OverviewRow(power.Icon, power.Label, "", power.Caption, power.Tip));
                }
                try
                {
                    AttributeSources.AddRows(context, data);
                }
                catch (ResetException)
                {
                    throw;
                }
                catch (Exception error)
                {
                    JournalBootstrap.ReportOnce("Reading Overview attributes", error);
                }
            }
        }

        private static void AddKnowledge(OverviewContext context, OverviewData data)
        {
            for (int category = 0; category < KnowledgeNames.Length; category++)
            {
                data.Knowledge.Add(new OverviewRow(context.KnowledgeIcon(category), KnowledgeNames[category], "",
                    context.KnowledgeCount(category).ToString()));
            }
        }

        private static readonly string[] KnowledgeNames = {
            JournalText.Get("Arcana.Journal.Overview.Knowledge.Spells"), JournalText.Get("Arcana.Journal.Overview.Knowledge.Charms"),
            JournalText.Get("Arcana.Journal.Overview.Knowledge.Curses"), JournalText.Get("Arcana.Journal.Overview.Knowledge.Rituals"),
            JournalText.Get("Arcana.Journal.Overview.Knowledge.Abilities")
        };

        private sealed class MagicalOverview : IOverviewProvider
        {
            public void Fill(OverviewContext context, OverviewData data)
            {
                AddKnowledge(context, data);
            }
        }

        private sealed class GenieOverview : IOverviewProvider
        {
            public void Fill(OverviewContext context, OverviewData data)
            {
                AddKnowledge(context, data);
                Rank rank;
                if (!GenieRanks.TryRead(context.Identity.Description, out rank)) return;
                data.RankDescription = GenieRanks.Description(rank, context.Identity.FirstName);
                data.RankLevel = (int)rank + 1;
                AddPerks(context, data);
            }
        }

        private sealed class WitchOverview : IOverviewProvider
        {
            public void Fill(OverviewContext context, OverviewData data)
            {
                AddKnowledge(context, data);

                PowerSelection selection = context.Selection;
                if (selection == null || !selection.Available)
                {
                    return;
                }

                int rank = (int)selection.Rank;
                data.RankDescription = WitchRankDescriptions.For(rank, context.Identity.FirstName);
                data.RankLevel = rank + 1;
                AddPerks(context, data);
            }
        }

        private static void AddPerks(OverviewContext context, OverviewData data)
        {
            PowerSelection selection = context.Selection;
            if (selection == null || !selection.Available)
            {
                return;
            }

            OccultTree tree = selection.Tree;
            for (int unlock = PerkProgression.RankCount - 1; unlock >= 0; unlock--)
            {
                foreach (Perk perk in tree.Perks)
                {
                    int bought = selection.Acquired(perk);
                    if ((int)perk.Rank != unlock || bought <= 0)
                    {
                        continue;
                    }
                    data.Perks.Add(new OverviewRow(context.PerkIcon(perk), perk.Name,
                        perk.Tiers > 1 ? JournalText.Format("Arcana.Common.CompactRatio", bought, perk.Tiers) : "",
                        JournalText.Format("Arcana.Journal.Overview.Perks.UnlockRank", GetRomanNumeral(unlock + 1)), PerkTip(tree, perk, bought, selection.SimName)));
                }
            }
            data.PerkCount = data.Perks.Count;
        }

        private static OverviewTip PerkTip(OccultTree tree, Perk perk, int bought, string simName)
        {
            string description = perk.Describe(bought - 1, simName);
            string body = perk.Tiers > 1 ? JournalText.Format("Arcana.Journal.Overview.Perks.OwnedRank", bought, perk.Tiers, description) : description;
            return new OverviewTip(perk.Name, perk.Tagline, body, tree.BenefitsFor(perk, bought - 1));
        }
    }

    internal static class WitchRankDescriptions
    {
        private const string Placeholder = "{Name}";
        private static readonly string[] Descriptions =
        {
            "Arcana.Witch.Rank.Description.Novice",
            "Arcana.Witch.Rank.Description.Apprentice",
            "Arcana.Witch.Rank.Description.Adept",
            "Arcana.Witch.Rank.Description.Master",
            "Arcana.Witch.Rank.Description.HighWitch"
        };

        internal static string For(int rank, string firstName)
        {
            int index = Math.Max(0, Math.Min(Descriptions.Length - 1, rank));
            string name = string.IsNullOrEmpty(firstName) ? JournalText.Get("Arcana.Common.ThisSim") : firstName;
            return JournalText.Get(Descriptions[index]).Replace(Placeholder, name);
        }
    }

    internal static class OverviewIcons
    {
        internal static UIImage Art(OccultTypes primary)
        {
            ulong instance = OccultAssetData.LifeStateArt(primary);
            return instance == 0ul ? null : StatusReaders.StatusIcon(instance);
        }

        internal static UIImage Species(OccultTypes primary)
        {
            if (primary == OccultTypes.None)
            {
                return null;
            }

            ulong instance = OccultAssetData.StatusIcon(primary, false);
            UIImage skill = instance == 0ul ? null : StatusReaders.StatusIcon(instance);
            if (primary == OccultTypes.Witch && skill != null)
            {
                return skill;
            }
            return OccultMeter.MeterIcon(primary) ?? skill;
        }
    }
}
