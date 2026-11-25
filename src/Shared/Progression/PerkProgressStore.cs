using System;
using System.Collections.Generic;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal sealed class PerkRecord
    {
        private const int Version = 1;
        private const int HeaderSize = 5;

        internal int Claimed;
        internal int Earned;
        internal int Available;
        private readonly Dictionary<uint, int> tiers = new Dictionary<uint, int>();

        private static uint Hash(string key)
        {
            uint hash = 2166136261;
            for (int i = 0; i < key.Length; i++)
            {
                hash ^= key[i];
                hash *= 16777619;
            }
            return hash;
        }

        internal int TierOf(string perkKey)
        {
            int tier;
            return tiers.TryGetValue(Hash(perkKey), out tier) ? tier : 0;
        }

        internal void SetTier(string perkKey, int tier)
        {
            tiers[Hash(perkKey)] = tier;
        }

        internal int RemoveTier(string perkKey)
        {
            int tier = TierOf(perkKey);
            tiers.Remove(Hash(perkKey));
            return tier;
        }

        internal List<int> Encode()
        {
            List<int> data = new List<int>(HeaderSize + 2 * tiers.Count);
            data.Add(Version);
            data.Add(Claimed);
            data.Add(Earned);
            data.Add(Available);
            data.Add(tiers.Count);
            foreach (KeyValuePair<uint, int> pair in tiers)
            {
                data.Add((int)pair.Key);
                data.Add(pair.Value);
            }
            return data;
        }

        internal static PerkRecord Decode(List<int> data, out bool newer)
        {
            newer = false;
            if (data == null || data.Count < HeaderSize || data[0] < 1)
            {
                return null;
            }
            if (data[0] > Version)
            {
                newer = true;
                return null;
            }

            if (data[4] < 0 || data[4] > (data.Count - HeaderSize) / 2) return null;
            PerkRecord record = new PerkRecord();
            record.Claimed = data[1] & PerkProgression.AllMask;
            record.Earned = PerkProgression.PointsIn(record.Claimed);
            record.Available = Math.Max(0, Math.Min(data[3], record.Earned));
            int count = data[4];
            for (int i = 0; i < count && HeaderSize + 2 * i + 1 < data.Count; i++)
            {
                record.tiers[(uint)data[HeaderSize + 2 * i]] = Math.Max(0, data[HeaderSize + 2 * i + 1]);
            }
            return record;
        }
    }

    internal delegate Dictionary<ulong, List<int>> RecordTable();
    internal delegate bool RecordMigration(PerkRecord record);

    internal sealed class PerkLedger
    {
        private readonly RecordTable table;
        private readonly RecordMigration migrate;

        internal PerkLedger(RecordTable table) : this(table, null)
        {
        }

        internal PerkLedger(RecordTable table, RecordMigration migrate)
        {
            this.table = table;
            this.migrate = migrate;
        }

        private PerkRecord Load(ulong simId, out bool newer)
        {
            newer = false;
            List<int> data;
            PerkRecord record = table().TryGetValue(simId, out data) ? PerkRecord.Decode(data, out newer) : null;
            if (record != null && migrate != null && migrate(record)) Save(simId, record);
            return record;
        }

        private void Save(ulong simId, PerkRecord record)
        {
            table()[simId] = record.Encode();
        }

        internal PerkRecord ReadCommitted(ulong simId)
        {
            bool newer;
            return Load(simId, out newer);
        }

        internal PerkRecord ClaimMilestones(ulong simId, long xpMilli, long[] thresholds)
        {
            if (simId == 0UL)
            {
                return null;
            }

            bool newer;
            PerkRecord record = Load(simId, out newer);
            if (newer || (record == null && table().ContainsKey(simId)))
            {
                return null;
            }

            bool changed = false;
            if (record == null)
            {
                record = new PerkRecord();
            }
            int reached = PerkProgression.Reached(xpMilli, thresholds);
            for (int i = 0; i < reached; i++)
            {
                int bit = 1 << i;
                if ((record.Claimed & bit) == 0)
                {
                    int award = PerkProgression.PointsFor(i);
                    record.Claimed |= bit;
                    record.Earned += award;
                    record.Available += award;
                    changed = true;
                }
            }
            if (changed)
            {
                Save(simId, record);
            }
            return record;
        }

        internal PerkRecord Purchase(ulong simId, OccultTree tree, Dictionary<string, int> newTiers)
        {
            bool newer;
            PerkRecord record = Load(simId, out newer);
            if (record == null)
            {
                return null;
            }

            int cost = 0;
            foreach (KeyValuePair<string, int> pair in newTiers)
            {
                Perk perk = tree.Find(pair.Key);
                int owned = perk == null ? 0 : record.TierOf(perk.Key);
                if (perk == null || perk.PreviewOnly || pair.Value <= owned || pair.Value > perk.Tiers)
                {
                    return null;
                }
                if (Holds(record, newTiers, perk.Conflict) || (perk.Requires != null && !Holds(record, newTiers, perk.Requires)))
                {
                    return null;
                }
                if (!HoldsAny(record, newTiers, perk.RequiresAny)) return null;
                for (int tier = owned; tier < pair.Value; tier++)
                {
                    cost += perk.Costs[tier];
                }
            }
            if (cost > record.Available)
            {
                return null;
            }

            foreach (KeyValuePair<string, int> pair in newTiers)
            {
                record.SetTier(pair.Key, pair.Value);
            }
            record.Available -= cost;
            Save(simId, record);
            return record;
        }

        private static bool Holds(PerkRecord record, Dictionary<string, int> newTiers, Perk perk)
        {
            int together;
            return perk != null && (record.TierOf(perk.Key) > 0 || (newTiers.TryGetValue(perk.Key, out together) && together > 0));
        }

        private static bool HoldsAny(PerkRecord record, Dictionary<string, int> newTiers, Perk[] options)
        {
            if (options == null || options.Length == 0) return true;
            foreach (Perk option in options)
                if (Holds(record, newTiers, option)) return true;
            return false;
        }
    }

    internal static class WitchProgressStore
    {
        [PersistableStatic]
        internal static Dictionary<ulong, List<int>> sRecords = new Dictionary<ulong, List<int>>();

        internal static readonly PerkLedger Ledger = new PerkLedger(Records);

        private static Dictionary<ulong, List<int>> Records()
        {
            if (sRecords == null)
            {
                sRecords = new Dictionary<ulong, List<int>>();
            }
            return sRecords;
        }
    }

    internal static class GenieProgressStore
    {
        [PersistableStatic]
        internal static Dictionary<ulong, List<int>> sRecords = new Dictionary<ulong, List<int>>();

        internal static readonly PerkLedger Ledger = new PerkLedger(Records, Migrate);

        private const string Marker = "GenieTree15";

        private static bool Migrate(PerkRecord record)
        {
            if (record.TierOf(Marker) > 0) return false;
            int refund = Refund(record, "StolenSecrets", 3)
                + Refund(record, "RawPower", 4)
                + Refund(record, "HomeSweetLamp", 3)
                + Refund(record, "BottledUp", 3)
                + Refund(record, "CushionedFall", 2, 3, 4)
                + Refund(record, "EvenKeel", 4)
                + Refund(record, "Wellspring", 3, 4, 5)
                + Refund(record, "HardLessons", 2, 3, 3)
                + Refund(record, "SilverLining", 3, 3, 4)
                + Refund(record, "FairExchange", 3, 3, 4)
                + Refund(record, "Overdraw", 3, 3, 4)
                + Refund(record, "TemperedMagic", 3, 3, 4)
                + Refund(record, "Unshaken", 3)
                + Refund(record, "ForbiddenLessons", 4)
                + Refund(record, "BindingOath", 4)
                + Refund(record, "Unleashed", 6)
                + Refund(record, "GenieLaw", 6);
            if (record.TierOf("Lampbound") == 0) refund += Refund(record, "SilkenCommand", 4);
            if (record.TierOf("PocketPalace") == 0 && record.TierOf("SilkenCommand") == 0) refund += Refund(record, "HeartsDesire", 4);
            record.SetTier(Marker, 1);
            record.Available = Math.Min(record.Earned, record.Available + refund);
            return true;
        }

        private static int Refund(PerkRecord record, string key, params int[] costs)
        {
            int bought = Math.Min(record.RemoveTier(key), costs.Length);
            int refund = 0;
            for (int tier = 0; tier < bought; tier++) refund += costs[tier];
            return refund;
        }

        private static Dictionary<ulong, List<int>> Records()
        {
            if (sRecords == null)
            {
                sRecords = new Dictionary<ulong, List<int>>();
            }
            return sRecords;
        }
    }
}
