using System;
using System.Collections.Generic;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal sealed class SimIdentity
    {
        internal readonly ObjectGuid Id;
        internal readonly string Name;
        internal readonly string LifeState;
        internal readonly OccultTypes Occults;
        internal readonly int Mood;
        internal readonly SimDescription Description;
        internal readonly ulong SimDescriptionId;

        private SimIdentity(ObjectGuid id, string name, string lifeState, OccultTypes occults, int mood,
            SimDescription description)
        {
            Id = id;
            Name = name;
            LifeState = lifeState;
            Occults = occults;
            Mood = mood;
            Description = description;
            SimDescriptionId = description == null ? 0UL : description.SimDescriptionId;
        }

        internal string FirstName
        {
            get
            {
                int space = Name.IndexOf(' ');
                return space > 0 ? Name.Substring(0, space) : Name;
            }
        }

        internal static SimIdentity Read(IHudModel model)
        {
            SimInfo info = model == null ? null : model.GetCurrentSimInfo();
            if (info == null || !info.mGuid.IsValid)
            {
                return null;
            }

            Sim sim = GlobalFunctions.ConvertGuidToObject<Sim>(info.mGuid);
            OccultTypes types = OccultsOf(info, sim);
            if (types == OccultTypes.None)
            {
                return null;
            }

            SimDescription description = ReadDescription(model, info, sim);
            string name = info.mName;
            if (string.IsNullOrEmpty(name) && sim != null)
            {
                name = sim.FullName;
            }
            return new SimIdentity(info.mGuid, name ?? JournalText.Get("Arcana.Common.SelectedSim"),
                LifeStateName(types), types, Math.Max(0, (int)info.mMood), description);
        }

        private static SimDescription ReadDescription(IHudModel model, SimInfo info, Sim sim)
        {
            if (sim != null && sim.SimDescription != null)
            {
                return sim.SimDescription;
            }
            if (info.UninstantiatedButInHousehold && info.UninstantiatedSimDescriptionId != 0UL)
            {
                return SimDescription.Find(info.UninstantiatedSimDescriptionId);
            }

            SimDescription selected = model.GetSimDescription() as SimDescription;
            Sim actor = selected == null ? null : selected.CreatedSim;
            return actor != null && actor.ObjectId == info.mGuid ? selected : null;
        }

        internal static OccultTypes ReadOccults(IHudModel model, out ObjectGuid id)
        {
            SimInfo info = model == null ? null : model.GetCurrentSimInfo();
            if (info == null || !info.mGuid.IsValid)
            {
                id = default(ObjectGuid);
                return OccultTypes.None;
            }

            id = info.mGuid;
            return OccultsOf(info, GlobalFunctions.ConvertGuidToObject<Sim>(info.mGuid));
        }

        private static OccultTypes OccultsOf(SimInfo info, Sim sim)
        {
            OccultTypes types = info.OccultInfo == null ? OccultTypes.None : info.OccultInfo.OccultType;
            if (sim != null)
            {
                if (sim.OccultManager != null)
                {
                    types |= sim.OccultManager.CurrentOccultTypes;
                }
                if (sim.SimDescription != null && sim.SimDescription.IsGhost)
                {
                    types |= OccultTypes.Ghost;
                }
            }
            return types & OccultAssetData.SupportedTypes;
        }

        private static string LifeStateName(OccultTypes types)
        {
            List<string> names = new List<string>();
            OccultTypes remaining = types;
            foreach (OccultTypes type in Enum.GetValues(typeof(OccultTypes)))
            {
                if (type == OccultTypes.None || (remaining & type) != type)
                {
                    continue;
                }
                remaining &= ~type;
                names.Add(LifeStateNames.For(type));
            }

            return string.Join(JournalText.Get("Arcana.LifeState.Separator"), names.ToArray());
        }

        internal UIImage Portrait(IHudModel model)
        {
            ThumbnailKey key = model.GetThumbnailForGameObject(Id, ThumbnailSize.Large, Mood);
            return UIManager.GetThumbnailImage(key);
        }
    }

    internal static class OccultRankLabel
    {
        internal static string FirstRankKey(OccultTypes primary)
        {
            switch (primary)
            {
                case OccultTypes.Witch: return "Uninitiated";
                case OccultTypes.Genie: return "Dormant";
                case OccultTypes.Fairy: return "Budding";
                default: return null;
            }
        }

        internal static string FirstRank(OccultTypes primary)
        {
            string key = FirstRankKey(primary);
            return key == null ? "" : JournalText.Get("Arcana." + LifeStateKey(primary) + ".Rank.Full." + key);
        }

        internal static string FirstRankDescription(OccultTypes primary, string firstName)
        {
            string key = FirstRankKey(primary);
            if (key == null) return "";
            string name = string.IsNullOrEmpty(firstName) ? JournalText.Get("Arcana.Common.ThisSim") : firstName;
            return JournalText.Get("Arcana." + LifeStateKey(primary) + ".Rank.Description." + key).Replace("{Name}", name);
        }

        private static string LifeStateKey(OccultTypes primary)
        {
            return primary == OccultTypes.Witch ? "Witch" : primary == OccultTypes.Genie ? "Genie" : "Fairy";
        }

        internal static string Rank(SimIdentity identity, PowerSelection witchProgress)
        {
            OccultTypes primary = OccultAssetData.Primary(identity.Occults);
            SupernaturalExpanded.Journal.Rank genieRank;
            if (primary == OccultTypes.Genie && GenieRanks.TryRead(identity.Description, out genieRank))
            {
                return GenieRanks.FullName(genieRank);
            }
            if (primary == OccultTypes.Witch && witchProgress != null && witchProgress.Available)
            {
                return WitchTree.Tree.RankLabel((int)witchProgress.Rank);
            }
            return "";
        }

        internal static string For(SimIdentity identity, PowerSelection witchProgress)
        {
            OccultTypes primary = OccultAssetData.Primary(identity.Occults);
            string rank = Rank(identity, witchProgress);
            if (rank.Length == 0) rank = FirstRank(primary);
            string lifeState = LifeStateNames.For(primary);
            return rank.Length == 0 ? lifeState : JournalText.Format("Arcana.Common.WithQualifier", lifeState, rank);
        }
    }

    internal static class LifeStateNames
    {
        internal static string For(OccultTypes state)
        {
            switch (state)
            {
                case OccultTypes.Vampire: return JournalText.Get("Arcana.LifeState.Name.Vampire");
                case OccultTypes.Genie: return JournalText.Get("Arcana.LifeState.Name.Genie");
                case OccultTypes.Werewolf: return JournalText.Get("Arcana.LifeState.Name.Werewolf");
                case OccultTypes.Ghost: return JournalText.Get("Arcana.LifeState.Name.Ghost");
                case OccultTypes.Fairy: return JournalText.Get("Arcana.LifeState.Name.Fairy");
                case OccultTypes.Witch: return JournalText.Get("Arcana.LifeState.Name.Witch");
                case OccultTypes.Mermaid: return JournalText.Get("Arcana.LifeState.Name.Mermaid");
                default: return "";
            }
        }
    }

    internal static class LifeStateDescriptions
    {
        private static readonly OccultTypes[] States =
        {
            OccultTypes.Vampire, OccultTypes.Witch, OccultTypes.Werewolf, OccultTypes.Fairy,
            OccultTypes.Genie, OccultTypes.Ghost, OccultTypes.Mermaid
        };

        private static readonly string[] Descriptions =
        {
            "Arcana.LifeState.Description.Vampire",
            "Arcana.LifeState.Description.Witch",
            "Arcana.LifeState.Description.Werewolf",
            "Arcana.LifeState.Description.Fairy",
            "Arcana.LifeState.Description.Genie",
            "Arcana.LifeState.Description.Ghost",
            "Arcana.LifeState.Description.Mermaid"
        };

        internal static string For(SimIdentity identity)
        {
            for (int i = 0; i < States.Length; i++)
            {
                if ((identity.Occults & States[i]) != OccultTypes.None)
                {
                    return JournalText.Get(Descriptions[i]);
                }
            }

            return "";
        }
    }
}
