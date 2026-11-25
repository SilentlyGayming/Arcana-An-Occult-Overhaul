using System;
using System.Collections.Generic;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.SimIFace;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal sealed class GrimoireEntry
    {
        internal readonly string Id;
        internal readonly string Name;
        internal readonly string TierName;
        internal readonly int TierOrder;
        internal readonly float EnergyCost;
        internal readonly string Description;
        internal readonly bool? Available;

        internal GrimoireEntry(string id, string name, string tierName, int tierOrder, float energyCost)
            : this(id, name, tierName, tierOrder, energyCost, null)
        {
        }

        internal GrimoireEntry(string id, string name, string tierName, int tierOrder, float energyCost, string description)
            : this(id, name, tierName, tierOrder, energyCost, description, null)
        {
        }

        internal GrimoireEntry(string id, string name, string tierName, int tierOrder, float energyCost, string description, bool? available)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentException("An entry needs a stable ID", "id");
            Id = id;
            Name = name ?? String.Empty;
            TierName = tierName ?? String.Empty;
            TierOrder = tierOrder;
            EnergyCost = energyCost;
            Description = description ?? String.Empty;
            Available = available;
        }
    }

    internal sealed class GrimoirePage
    {
        internal const int CategoryCount = (int)SpellCategory.Ability + 1;
        internal const int VisibleRows = 12;
        private const float IconSize = 26f;
        private const float IconGap = 8f;
        private const float HeaderHeight = 29f;
        private const float RowHeight = 30f;
        private const float ScrollWidth = 15f;
        private const float ScrollArrowHeight = 15f;
        private const float ScrollGap = 6f;
        private const float OutlineInset = 3f;
        private const float OutlineBottomExtra = 2f;
        private const float ArrowGutter = 21f;
        private const float TabTop = WindowGeometry.ContentTop + 14f;
        private const float PaneTop = TabTop + 54f;
        private const float PaneHeight = HeaderHeight + VisibleRows * RowHeight + 3f;
        private const float DescriptionGap = 8f;
        private const float BottomPad = 12f;
        private const float DescriptionHeight = WindowGeometry.ContentBottom - BottomPad - (PaneTop + PaneHeight + DescriptionGap);
        private const int MaxDescriptionLength = 240;
        private const int WheelRows = 1;
        private static readonly string[] Categories = {
            JournalText.Get("Arcana.Journal.Grimoire.Category.Spells"), JournalText.Get("Arcana.Journal.Grimoire.Category.Charms"),
            JournalText.Get("Arcana.Journal.Grimoire.Category.Curses"), JournalText.Get("Arcana.Journal.Grimoire.Category.Rituals"),
            JournalText.Get("Arcana.Journal.Grimoire.Category.Abilities")
        };
        private static readonly string[] EmptyMessages = {
            JournalText.Get("Arcana.Journal.Grimoire.EmptyList.Spells"), JournalText.Get("Arcana.Journal.Grimoire.EmptyList.Charms"),
            JournalText.Get("Arcana.Journal.Grimoire.EmptyList.Curses"), JournalText.Get("Arcana.Journal.Grimoire.EmptyList.Rituals"),
            JournalText.Get("Arcana.Journal.Grimoire.EmptyList.Abilities")
        };
        private static readonly string[] SelectPrompts = {
            JournalText.Get("Arcana.Journal.Grimoire.Select.Spells"), JournalText.Get("Arcana.Journal.Grimoire.Select.Charms"),
            JournalText.Get("Arcana.Journal.Grimoire.Select.Curses"), JournalText.Get("Arcana.Journal.Grimoire.Select.Rituals"),
            JournalText.Get("Arcana.Journal.Grimoire.Select.Abilities")
        };
        private static readonly string[] EmptyPrompts = {
            JournalText.Get("Arcana.Journal.Grimoire.EmptyDetails.Spells"), JournalText.Get("Arcana.Journal.Grimoire.EmptyDetails.Charms"),
            JournalText.Get("Arcana.Journal.Grimoire.EmptyDetails.Curses"), JournalText.Get("Arcana.Journal.Grimoire.EmptyDetails.Rituals"),
            JournalText.Get("Arcana.Journal.Grimoire.EmptyDetails.Abilities")
        };
        private static readonly float[] CaptionWidths = { 49f, 60f, 53f, 56f, 65f };
        private static readonly Color ActiveTabColor = new Color(0xFFBBC6E2);
        private static readonly Color InactiveTabColor = new Color(0xFFA5C6E6);
        private static readonly Color TextColor = new Color(0xFF0B246E);
        private static readonly Color MutedColor = new Color(0xFF5F78A3);
        private static readonly Color AffordableColor = new Color(0xFF28763C);
        private static readonly Color UnaffordableColor = new Color(0xFFB32C35);
        private const uint EvenRowFill = 0x00000000;
        private const uint OddRowFill = 0x70D2EBF7;
        private const uint HoverRowFill = 0xFFD2EBF7;
        private const uint SelectedRowFill = 0xFFB5D9FA;
        private readonly JournalWindow journal;
        private readonly Button[] tabs = new Button[CategoryCount];
        private readonly Window[] icons = new Window[CategoryCount];
        private readonly Text[] captions = new Text[CategoryCount];
        private readonly Window tableHeader;
        private readonly Window listPanel;
        private readonly Window listBody;
        private readonly Window descriptionPanel;
        private readonly Window descriptionBody;
        private readonly Text descriptionName;
        private readonly Text descriptionMeta;
        private readonly Text descriptionCost;
        private readonly Text descriptionPrompt;
        private readonly Text descriptionText;
        private readonly Button[] sortButtons = new Button[3];
        private readonly Text[] headers = new Text[3];
        private readonly Window[] sortArrows = new Window[3];
        private readonly Window[] sortArrowsDown = new Window[3];
        private readonly Window[] rowBackgrounds = new Window[VisibleRows];
        private readonly Button[] rowButtons = new Button[VisibleRows];
        private readonly Text[] rowNames = new Text[VisibleRows];
        private readonly Text[] rowTiers = new Text[VisibleRows];
        private readonly Text[] rowCosts = new Text[VisibleRows];
        private readonly Window scrollTrack;
        private readonly Button scrollThumb;
        private readonly Button upArrow;
        private readonly Button downArrow;
        private readonly Text emptyMessage;
        private readonly List<GrimoireEntry>[] entries = new List<GrimoireEntry>[CategoryCount];
        private readonly List<GrimoireEntry>[] sorted = new List<GrimoireEntry>[CategoryCount];
        private readonly int[] sortColumn = new int[CategoryCount];
        private readonly bool[] descending = new bool[CategoryCount];
        private readonly int[] scrollRow = new int[CategoryCount];
        private readonly string[] selectedIds = new string[CategoryCount];
        private int category;
        private TabFaces tabFaces;
        private bool visible;
        private float scrollbarTop;
        private float scrollbarTravel;
        private float scrollbarThumbSize;
        private bool dragging;
        private float dragOffset;
        private float scrollbarLeft;
        private float rowsLeft;
        private float rowsTop;
        private float rowsWidth;
        private int hoverRow = -1;
        private bool hasAvailablePower;
        private float availablePower;

        internal event EventHandler EntriesChanged;

        internal Button[] Tabs { get { return tabs; } }

        internal int GetEntryCount(int index)
        {
            return index < 0 || index >= CategoryCount ? 0 : entries[index].Count;
        }

        internal UIImage GetCategoryIcon(int index)
        {
            if (index < 0 || index >= CategoryCount)
            {
                return null;
            }
            ImageDrawable drawable = icons[index].Drawable as ImageDrawable;
            return drawable == null ? null : drawable.Image;
        }

        internal void UseFaces(TabFaces faces)
        {
            tabFaces = faces;
            tabFaces.Show(category);
        }

        internal static GrimoirePage Create(JournalWindow journal)
        {
            return new GrimoirePage(journal);
        }

        private GrimoirePage(JournalWindow journal)
        {
            this.journal = journal;
            for (int i = 0; i < CategoryCount; i++)
            {
                int index = i;
                entries[i] = new List<GrimoireEntry>();
                sorted[i] = new List<GrimoireEntry>();
                tabs[i] = journal.Button(NativeLayouts.GrimoireFirstTab + (uint)i, "",
                    0f, 0f, 100f, 39f, delegate { Select(index); });
                icons[i] = journal.Control<Window>(NativeLayouts.GrimoireFirstIcon + (uint)i);
                icons[i].IgnoreMouse = true;
                captions[i] = journal.Label(NativeLayouts.GrimoireFirstCaption + (uint)i,
                    Categories[i], 0f, 0f, CaptionWidths[i], 39f);
                captions[i].IgnoreMouse = true;
                captions[i].TextColor = TextColor;
            }

            tableHeader = journal.Control<Window>(NativeLayouts.GrimoireTableHeader);
            tableHeader.IgnoreMouse = true;
            listPanel = journal.Control<Window>(NativeLayouts.GrimoireListPanel);
            listBody = journal.Control<Window>(NativeLayouts.GrimoireListBody);
            listBody.IgnoreMouse = true;
            descriptionPanel = journal.Control<Window>(NativeLayouts.GrimoireDescriptionPanel);
            descriptionPanel.IgnoreMouse = true;
            descriptionBody = journal.Control<Window>(NativeLayouts.GrimoireDescriptionBody);
            descriptionBody.IgnoreMouse = true;
            descriptionName = journal.Label(NativeLayouts.GrimoireDescriptionName, "", 0, 0, 100, 22);
            descriptionName.IgnoreMouse = true;
            descriptionName.TextColor = TextColor;
            descriptionMeta = journal.Label(NativeLayouts.GrimoireDescriptionMeta, "", 0, 0, 100, 18);
            descriptionMeta.IgnoreMouse = true;
            descriptionMeta.TextColor = MutedColor;
            descriptionCost = journal.Label(NativeLayouts.GrimoireDescriptionCost, "", 0, 0, 100, 18);
            descriptionCost.IgnoreMouse = true;
            descriptionCost.TextColor = MutedColor;
            descriptionPrompt = journal.Label(NativeLayouts.GrimoireDescriptionPrompt, "", 0, 0, 100, 22);
            descriptionPrompt.IgnoreMouse = true;
            descriptionPrompt.TextColor = MutedColor;
            descriptionText = journal.Label(NativeLayouts.GrimoireDescriptionText, "", 0, 0, 100, 56);
            descriptionText.IgnoreMouse = true;
            descriptionText.TextColor = TextColor;
            uint[] headerIds = { NativeLayouts.GrimoireListTitle,
                NativeLayouts.GrimoireTierHeader, NativeLayouts.GrimoireCostHeader };
            string[] titles = { JournalText.Get("Arcana.Journal.Grimoire.Column.Name"), JournalText.Get("Arcana.Journal.Grimoire.Column.Tier"), JournalText.Get("Arcana.Journal.Grimoire.Column.EnergyCost") };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                headers[i] = journal.Label(headerIds[i], titles[i], 0, 0, 100, HeaderHeight);
                headers[i].IgnoreMouse = true;
                headers[i].TextColor = TextColor;
                sortArrows[i] = journal.Control<Window>(NativeLayouts.GrimoireFirstSortArrow + (uint)i);
                sortArrows[i].IgnoreMouse = true;
                sortArrowsDown[i] = journal.Control<Window>(NativeLayouts.GrimoireFirstSortDescending + (uint)i);
                sortArrowsDown[i].IgnoreMouse = true;
                sortButtons[i] = journal.Button(NativeLayouts.GrimoireFirstColumnButton + (uint)i,
                    "", 0, 0, 100, HeaderHeight, delegate { SortBy(index); });
            }

            for (int i = 0; i < VisibleRows; i++)
            {
                int row = i;
                uint id = NativeLayouts.GrimoireFirstRow + (uint)(i * 5);
                rowBackgrounds[i] = journal.Control<Window>(id);
                rowBackgrounds[i].IgnoreMouse = true;
                rowButtons[i] = journal.Button(id + 1, "", 0, 0, 100, RowHeight,
                    delegate { SelectRow(row); });
                rowButtons[i].MouseMove += delegate(WindowBase sender, UIMouseEventArgs args)
                {
                    SetHover(row);
                    args.Handled = true;
                };
                rowNames[i] = journal.Label(id + 2, "", 0, 0, 100, RowHeight);
                rowTiers[i] = journal.Label(id + 3, "", 0, 0, 100, RowHeight);
                rowCosts[i] = journal.Label(id + 4, "", 0, 0, 100, RowHeight);
                rowNames[i].TextColor = TextColor;
                rowTiers[i].TextColor = TextColor;
                rowCosts[i].TextColor = TextColor;
                rowNames[i].IgnoreMouse = true;
                rowTiers[i].IgnoreMouse = true;
                rowCosts[i].IgnoreMouse = true;
            }
            scrollTrack = journal.Control<Window>(NativeLayouts.GrimoireScrollTrack);
            scrollTrack.IgnoreMouse = false;
            scrollThumb = journal.Button(NativeLayouts.GrimoireScrollThumb, "", 0, 0, ScrollWidth, 28,
                delegate {  });
            upArrow = journal.Button(NativeLayouts.GrimoireScrollUp, "", 0, 0, ScrollWidth, ScrollArrowHeight,
                delegate { ScrollBy(-1); });
            downArrow = journal.Button(NativeLayouts.GrimoireScrollDown, "", 0, 0, ScrollWidth, ScrollArrowHeight,
                delegate { ScrollBy(1); });
            emptyMessage = journal.Label(NativeLayouts.GrimoireEmptyList,
                EmptyMessages[0], 0, 0, 200, 40);
            emptyMessage.TextColor = TextColor;
            emptyMessage.IgnoreMouse = true;

            listPanel.MouseWheel += OnWheel;
            scrollTrack.MouseWheel += OnWheel;
            scrollThumb.MouseWheel += OnWheel;
            upArrow.MouseWheel += OnWheel;
            downArrow.MouseWheel += OnWheel;
            for (int i = 0; i < VisibleRows; i++)
                rowButtons[i].MouseWheel += OnWheel;
            scrollTrack.MouseDown += OnTrackDown;
            scrollThumb.MouseDown += OnThumbDown;
            scrollThumb.MouseMove += OnThumbMove;
            scrollThumb.MouseUp += OnThumbUp;
            journal.RootWindow.MouseUp += OnThumbUp;
            journal.RootWindow.MouseMove += OnRootMove;
            Select(0);
            Show(false);
        }

        internal void Reset()
        {
            for (int i = 0; i < CategoryCount; i++)
            {
                sortColumn[i] = 0;
                descending[i] = false;
                scrollRow[i] = 0;
                selectedIds[i] = null;
                entries[i].Clear();
            }
            for (int i = 0; i < CategoryCount; i++) SortEntries(i);
            EndDrag();
            Select(0);
        }

        internal void SetAllEntries(List<GrimoireEntry>[] incoming)
        {
            bool changed = false;
            for (int i = 0; i < CategoryCount && i < incoming.Length; i++) changed |= Replace(i, incoming[i]);
            if (changed && EntriesChanged != null) EntriesChanged(this, EventArgs.Empty);
        }

        private bool Replace(int index, IEnumerable<GrimoireEntry> incoming)
        {
            if (index < 0 || index >= CategoryCount) return false;
            List<GrimoireEntry> snapshot = incoming == null ? null : new List<GrimoireEntry>(incoming);
            List<GrimoireEntry> target = entries[index];
            if (SameEntries(target, snapshot)) return false;
            target.Clear();
            if (snapshot != null)
            {
                Dictionary<string, bool> ids = new Dictionary<string, bool>(StringComparer.Ordinal);
                foreach (GrimoireEntry item in snapshot)
                {
                    if (item == null || ids.ContainsKey(item.Id)) continue;
                    ids.Add(item.Id, true);
                    target.Add(item);
                }
            }
            SortEntries(index);
            if (selectedIds[index] != null && !HasEntryWithId(sorted[index], selectedIds[index]))
                selectedIds[index] = null;
            if (index == category) RefreshRows();
            return true;
        }

        private static bool SameEntries(List<GrimoireEntry> current, List<GrimoireEntry> incoming)
        {
            int count = incoming == null ? 0 : incoming.Count;
            if (current.Count != count) return false;
            for (int i = 0; i < count; i++)
            {
                GrimoireEntry a = current[i], b = incoming[i];
                if (b == null || a.Id != b.Id || a.Name != b.Name || a.TierName != b.TierName || a.TierOrder != b.TierOrder
                    || !SameCost(a.EnergyCost, b.EnergyCost) || a.Available != b.Available || a.Description != b.Description) return false;
            }
            return true;
        }

        private void Select(int index)
        {
            if (index < 0 || index >= CategoryCount) return;
            category = index;
            hoverRow = -1;
            EndDrag();
            for (int i = 0; i < CategoryCount; i++)
            {
                tabs[i].Selected = i == index;
                tabs[i].ShadeColor = i == index ? ActiveTabColor : InactiveTabColor;
                tabs[i].Invalidate();
            }
            if (tabFaces != null) tabFaces.Show(index);
            RefreshHeader();
            RefreshRows();
        }

        private void SortBy(int column)
        {
            if (!visible || column < 0 || column > 2) return;
            if (sortColumn[category] == column) descending[category] = !descending[category];
            else { sortColumn[category] = column; descending[category] = false; }
            SortEntries(category);
            int selectedPosition = FindId(sorted[category], selectedIds[category]);
            scrollRow[category] = selectedPosition < 0 ? 0 :
                Math.Min(selectedPosition, Math.Max(0, sorted[category].Count - VisibleRows));
            RefreshHeader();
            RefreshRows();
        }

        private static int FindId(List<GrimoireEntry> list, string id)
        {
            if (id == null) return -1;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == id) return i;
            return -1;
        }

        private static bool HasEntryWithId(List<GrimoireEntry> list, string id)
        {
            return FindId(list, id) >= 0;
        }

        private void SortEntries(int index)
        {
            List<GrimoireEntry> original = entries[index];
            List<GrimoireEntry> result = sorted[index];
            result.Clear();
            result.AddRange(original);
            int column = sortColumn[index];
            bool reverse = descending[index];
            Dictionary<string, int> order = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < original.Count; i++) order[original[i].Id] = i;
            result.Sort(delegate(GrimoireEntry a, GrimoireEntry b)
            {
                int comparison;
                if (column == 1) comparison = a.TierOrder.CompareTo(b.TierOrder);
                else if (column == 2) comparison = a.EnergyCost.CompareTo(b.EnergyCost);
                else comparison = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
                if (reverse) comparison = -comparison;
                return comparison != 0 ? comparison : order[a.Id].CompareTo(order[b.Id]);
            });
            int maxScroll = Math.Max(0, result.Count - VisibleRows);
            scrollRow[index] = Math.Max(0, Math.Min(maxScroll, scrollRow[index]));
        }

        private void RefreshHeader()
        {
            for (int i = 0; i < 3; i++)
            {
                bool active = visible && i == sortColumn[category];
                sortArrows[i].Visible = active && !descending[category];
                sortArrowsDown[i].Visible = active && descending[category];
            }
        }

        private void RefreshRows()
        {
            ReadAvailablePower();
            List<GrimoireEntry> items = sorted[category];
            int first = Math.Max(0, Math.Min(scrollRow[category], Math.Max(0, items.Count - VisibleRows)));
            scrollRow[category] = first;
            bool hasEntries = items.Count > 0;
            emptyMessage.Caption = EmptyMessages[category];
            emptyMessage.Visible = visible && !hasEntries;
            if (!visible) hoverRow = -1;
            for (int i = 0; i < VisibleRows; i++)
            {
                int index = first + i;
                bool show = visible && index < items.Count;
                rowBackgrounds[i].Visible = show;
                rowButtons[i].Visible = show;
                rowNames[i].Visible = show;
                rowTiers[i].Visible = show;
                rowCosts[i].Visible = show;
                if (!show) continue;
                GrimoireEntry entry = items[index];
                rowNames[i].Caption = entry.Name;
                rowTiers[i].Caption = entry.TierName;
                rowCosts[i].Caption = CostText(entry);
                rowCosts[i].TextColor = ColorForCost(entry);
                rowButtons[i].Selected = entry.Id == selectedIds[category];
                PaintRow(i);
            }
            RefreshDescription();
            RefreshScrollbar();
        }

        private void PaintRow(int slot)
        {
            List<GrimoireEntry> items = sorted[category];
            int index = scrollRow[category] + slot;
            if (slot < 0 || slot >= VisibleRows || index >= items.Count) return;
            uint fill = items[index].Id == selectedIds[category] ? SelectedRowFill
                : slot == hoverRow ? HoverRowFill
                : index % 2 == 0 ? EvenRowFill : OddRowFill;
            if (rowBackgrounds[slot].FillColor.ARGB != fill)
            {
                rowBackgrounds[slot].FillColor = new Color(fill);
                rowBackgrounds[slot].Invalidate();
            }
        }

        private void SetHover(int slot)
        {
            if (slot == hoverRow) return;
            int previous = hoverRow;
            hoverRow = slot;
            PaintRow(previous);
            PaintRow(slot);
        }

        private void RefreshDescription()
        {
            int position = visible ? FindId(sorted[category], selectedIds[category]) : -1;
            GrimoireEntry entry = position < 0 ? null : sorted[category][position];
            descriptionName.Caption = entry == null ? String.Empty : entry.Name;
            descriptionMeta.Caption = entry == null || String.IsNullOrEmpty(entry.TierName) ? String.Empty
                : JournalText.Format("Arcana.Journal.Grimoire.Details.Tier", entry.TierName);
            descriptionCost.Caption = entry == null ? String.Empty : JournalText.Format("Arcana.Journal.Grimoire.Details.Cost", CostText(entry));
            descriptionCost.TextColor = entry == null ? MutedColor : ColorForCost(entry);
            descriptionText.Caption = entry == null ? String.Empty : ShortenDescription(entry.Description);
            bool hint = visible && entry == null;
            descriptionPrompt.Caption = sorted[category].Count == 0 ? EmptyPrompts[category] : SelectPrompts[category];
            descriptionPrompt.Visible = hint;
            descriptionName.Visible = visible && entry != null;
            descriptionMeta.Visible = visible && entry != null && !String.IsNullOrEmpty(entry.TierName);
            descriptionCost.Visible = visible && entry != null;
            descriptionText.Visible = visible && entry != null;
        }

        internal void RefreshAffordability()
        {
            if (!visible) return;
            ReadAvailablePower();
            List<GrimoireEntry> items = sorted[category];
            for (int i = 0; i < VisibleRows; i++)
            {
                int index = scrollRow[category] + i;
                if (index >= items.Count) break;
                rowCosts[i].TextColor = ColorForCost(items[index]);
            }
            RefreshDescription();
        }

        private void ReadAvailablePower()
        {
            hasAvailablePower = false;
            Sim actor = journal.CurrentActor;
            if (!MagicCasters.IsCaster(actor) || actor.Motives == null) return;
            availablePower = MagicCasters.AvailablePower(actor, MagicCasters.ReadRules(actor));
            hasAvailablePower = true;
        }

        private Color ColorForCost(GrimoireEntry entry)
        {
            if (entry.Available.HasValue) return entry.Available.Value ? AffordableColor : UnaffordableColor;
            if (!hasAvailablePower || float.IsNaN(entry.EnergyCost)) return TextColor;
            return entry.EnergyCost <= availablePower + 0.005f ? AffordableColor : UnaffordableColor;
        }

        private static string CostText(GrimoireEntry entry)
        {
            return float.IsNaN(entry.EnergyCost) ? JournalText.Get("Arcana.Common.UnavailableValue")
                : entry.EnergyCost.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool SameCost(float a, float b)
        {
            return a == b || (float.IsNaN(a) && float.IsNaN(b));
        }

        private static string ShortenDescription(string text)
        {
            if (String.IsNullOrEmpty(text) || text.Length <= MaxDescriptionLength) return text ?? String.Empty;
            string ellipsis = JournalText.Get("Arcana.Common.Ellipsis");
            return text.Substring(0, Math.Max(0, MaxDescriptionLength - ellipsis.Length)) + ellipsis;
        }

        private void OnRootMove(WindowBase sender, UIMouseEventArgs args)
        {
            if (hoverRow < 0) return;
            float x, y;
            if (journal.TryGetCursor(out x, out y))
            {
                float top = rowsTop + hoverRow * RowHeight;
                if (x >= rowsLeft && x < rowsLeft + rowsWidth && y >= top && y < top + RowHeight) return;
            }
            SetHover(-1);
        }

        private void SelectRow(int row)
        {
            int index = scrollRow[category] + row;
            if (index < 0 || index >= sorted[category].Count) return;
            selectedIds[category] = sorted[category][index].Id;
            RefreshRows();
        }

        private void ScrollBy(int delta)
        {
            int maxScroll = Math.Max(0, sorted[category].Count - VisibleRows);
            scrollRow[category] = Math.Max(0, Math.Min(maxScroll, scrollRow[category] + delta));
            RefreshRows();
        }

        private void RefreshScrollbar()
        {
            int max = GetMaxScroll();
            bool scrollable = visible && max > 0;
            scrollTrack.Visible = scrollable;
            upArrow.Visible = scrollable;
            downArrow.Visible = scrollable;
            scrollThumb.Visible = scrollable;
            upArrow.Enabled = scrollable && scrollRow[category] > 0;
            downArrow.Enabled = scrollable && scrollRow[category] < max;
            JournalTheme.TintArrow(upArrow, upArrow.Enabled);
            JournalTheme.TintArrow(downArrow, downArrow.Enabled);
            if (!scrollable)
            {
                EndDrag();
                return;
            }
            float trackHeight = VisibleRows * RowHeight - 2f * ScrollArrowHeight;
            scrollbarThumbSize = Math.Max(24f, trackHeight * VisibleRows / (float)sorted[category].Count);
            scrollbarTravel = Math.Max(1f, trackHeight - scrollbarThumbSize);
            journal.Place(scrollThumb, scrollbarLeft, GetThumbTop(max), ScrollWidth, scrollbarThumbSize);
        }

        private void OnWheel(WindowBase sender, UIMouseEventArgs args)
        {
            int delta = args.MouseWheelDelta;
            if (!visible || dragging || delta == 0) return;
            ScrollBy(delta > 0 ? -WheelRows : WheelRows);
            args.Handled = true;
        }

        private void OnThumbDown(WindowBase sender, UIMouseEventArgs args)
        {
            float y;
            if (!visible || !journal.TryGetCursorY(out y)) return;
            dragging = true;
            dragOffset = y - GetThumbTop(GetMaxScroll());
            UIManager.SetCaptureTarget(InputContext.kICMouse, scrollThumb);
            args.Handled = true;
        }

        private void OnTrackDown(WindowBase sender, UIMouseEventArgs args)
        {
            float y;
            if (!visible || dragging || GetMaxScroll() == 0 || !journal.TryGetCursorY(out y)) return;
            ScrollBy(y < GetThumbTop(GetMaxScroll()) ? -VisibleRows : VisibleRows);
            args.Handled = true;
        }

        private void OnThumbMove(WindowBase sender, UIMouseEventArgs args)
        {
            float y;
            if (!dragging) return;
            if (!visible || UIManager.GetCaptureTarget(InputContext.kICMouse) != scrollThumb)
            {
                dragging = false;
                return;
            }
            if (!journal.TryGetCursorY(out y)) return;
            int max = GetMaxScroll();
            float fraction = (y - dragOffset - scrollbarTop - ScrollArrowHeight) / scrollbarTravel;
            int target = Math.Max(0, Math.Min(max, (int)Math.Round(fraction * max)));
            if (target != scrollRow[category])
            {
                scrollRow[category] = target;
                RefreshRows();
            }
            args.Handled = true;
        }

        private void OnThumbUp(WindowBase sender, UIMouseEventArgs args)
        {
            EndDrag();
        }

        private void EndDrag()
        {
            if (!dragging) return;
            dragging = false;
            if (scrollThumb != null && !scrollThumb.Disposed)
                UIManager.ReleaseCapture(InputContext.kICMouse, scrollThumb);
        }

        internal void Release()
        {
            EndDrag();
        }

        private int GetMaxScroll()
        {
            return Math.Max(0, sorted[category].Count - VisibleRows);
        }

        private float GetThumbTop(int max)
        {
            return scrollbarTop + ScrollArrowHeight +
                scrollbarTravel * scrollRow[category] / (float)Math.Max(1, max);
        }

        internal void Show(bool show)
        {
            visible = show;
            for (int i = 0; i < CategoryCount; i++)
            {
                tabs[i].Visible = show;
                icons[i].Visible = show;
                captions[i].Visible = show;
            }
            tableHeader.Visible = show;
            listPanel.Visible = show;
            listBody.Visible = show;
            descriptionPanel.Visible = show;
            descriptionBody.Visible = show;
            descriptionName.Visible = show;
            descriptionText.Visible = show;
            descriptionMeta.Visible = false;
            descriptionCost.Visible = false;
            descriptionPrompt.Visible = false;
            for (int i = 0; i < 3; i++)
            {
                headers[i].Visible = show;
                sortButtons[i].Visible = show;
            }
            RefreshHeader();
            RefreshRows();
        }

        internal void Layout()
        {
            const float margin = WindowGeometry.ContentLeft + WindowGeometry.ContentInset;
            const float gap = 8f;
            float paneWidth = WindowGeometry.ContentWidth - 2f * WindowGeometry.ContentInset;
            float tabWidth = (paneWidth - (CategoryCount - 1) * gap) / CategoryCount;
            for (int i = 0; i < CategoryCount; i++)
            {
                float x = margin + i * (tabWidth + gap);
                journal.Place(tabs[i], x, TabTop, tabWidth, 39f);
                float combined = IconSize + IconGap + CaptionWidths[i];
                float groupX = x + (tabWidth - combined) / 2f;
                journal.Place(icons[i], groupX, TabTop + 6.5f, IconSize, IconSize);
                journal.Place(captions[i], groupX + IconSize + IconGap,
                    TabTop, CaptionWidths[i], 39f);
            }
            float headY = PaneTop;
            float listTop = headY + HeaderHeight - 1f;
            float tableWidth = paneWidth - ScrollWidth - ScrollGap;
            float outlineLeft = margin - OutlineInset;
            float outlineWidth = tableWidth + 2f * OutlineInset;
            float outlineHeight = PaneTop + PaneHeight + OutlineBottomExtra - listTop;
            journal.Place(listPanel, outlineLeft, listTop, outlineWidth, outlineHeight);
            journal.Place(listBody, outlineLeft + 1f, listTop, outlineWidth - 2f, outlineHeight - 1f);

            float headerX = margin;
            journal.Place(tableHeader, headerX, headY, tableWidth, HeaderHeight);
            scrollbarLeft = margin + paneWidth - ScrollWidth;
            float usableWidth = tableWidth;
            float nameWidth = usableWidth * 0.57f;
            float tierWidth = usableWidth * 0.20f;
            float costWidth = usableWidth - nameWidth - tierWidth;
            float[] starts = { headerX, headerX + nameWidth, headerX + nameWidth + tierWidth };
            float[] widths = { nameWidth, tierWidth, costWidth };
            for (int i = 0; i < 3; i++)
            {
                float cellWidth = widths[i];
                journal.Place(sortButtons[i], starts[i], headY, cellWidth, HeaderHeight);
                journal.Place(headers[i], starts[i], headY, cellWidth, HeaderHeight);
                journal.Place(sortArrows[i], starts[i], headY, cellWidth - 3f, HeaderHeight);
                journal.Place(sortArrowsDown[i], starts[i], headY, cellWidth - 3f, HeaderHeight);
            }
            float rowsY = headY + HeaderHeight;
            rowsLeft = headerX + 1f;
            rowsTop = rowsY;
            rowsWidth = usableWidth - 2f;
            for (int i = 0; i < VisibleRows; i++)
            {
                float y = rowsY + i * RowHeight;
                journal.Place(rowBackgrounds[i], rowsLeft, y, rowsWidth, RowHeight);
                journal.Place(rowButtons[i], rowsLeft, y, rowsWidth, RowHeight);
                journal.Place(rowNames[i], headerX + 11f, y, nameWidth - 15f, RowHeight);
                journal.Place(rowTiers[i], starts[1], y, tierWidth, RowHeight);
                journal.Place(rowCosts[i], starts[2], y, costWidth - ArrowGutter, RowHeight);
            }
            scrollbarTop = rowsY;
            float downY = rowsY + VisibleRows * RowHeight - ScrollArrowHeight;
            journal.Place(upArrow, scrollbarLeft, rowsY, ScrollWidth, ScrollArrowHeight);
            journal.Place(scrollTrack, scrollbarLeft, rowsY + ScrollArrowHeight,
                ScrollWidth, VisibleRows * RowHeight - 2f * ScrollArrowHeight);
            journal.Place(downArrow, scrollbarLeft, downY, ScrollWidth, ScrollArrowHeight);
            journal.Place(emptyMessage, headerX + 5f,
                rowsY + (VisibleRows * RowHeight - 40f) / 2f,
                nameWidth - 10f, 40f);

            float descriptionTop = PaneTop + PaneHeight + DescriptionGap;
            journal.Place(descriptionPanel, outlineLeft, descriptionTop, outlineWidth, DescriptionHeight);
            journal.Place(descriptionBody, outlineLeft + 1f, descriptionTop + 1f, outlineWidth - 2f, DescriptionHeight - 2f);
            journal.Place(descriptionName, margin + 14f, descriptionTop + 8f, tableWidth - 28f, 22f);
            journal.Place(descriptionMeta, margin + 14f, descriptionTop + 32f, 84f, 18f);
            journal.Place(descriptionCost, margin + 98f, descriptionTop + 32f, tableWidth - 112f, 18f);
            journal.Place(descriptionText, margin + 14f, descriptionTop + 56f, tableWidth - 28f, DescriptionHeight - 64f);
            journal.Place(descriptionPrompt, margin + 14f, descriptionTop, tableWidth - 28f, DescriptionHeight);
            RefreshRows();
        }
    }
}
