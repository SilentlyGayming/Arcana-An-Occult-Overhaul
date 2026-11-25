using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.CAS;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal sealed class OccultMeter : IDisposable
    {
        private const string BarName = "mMagicMotiveBarWindow";
        private const string FillControlName = "mMagicMotiveSizeControlWin";
        private const string FillReferenceName = "mMagicMotiveSizeReferenceWin";
        private static readonly FieldInfo BarField = typeof(SimDisplay).GetField(BarName, NativeReflection.Instance);
        private static readonly FieldInfo FillControlField = typeof(SimDisplay).GetField(FillControlName, NativeReflection.Instance);
        private static readonly FieldInfo FillReferenceField = typeof(SimDisplay).GetField(FillReferenceName, NativeReflection.Instance);
        private static readonly Dictionary<OccultTypes, UIImage> icons = new Dictionary<OccultTypes, UIImage>();
        private static UIImage nativeIcon;
        private static bool hudReplaced;
        private Window glyph;
        private ImageDrawable glyphImage;
        private IconDrawable glyphIcon;
        private UIImage originalImage;
        private Color originalIconShade;
        private bool iconReported;
        private bool custom;
        private OccultTypes active;

        private IHudModel model;
        private WindowBase shown;
        private Window fill;
        private Color originalShade;
        private string originalTooltip;
        private string tooltip;
        private bool queued;
        private bool reported;
        private bool disposed;

        internal void Refresh()
        {
            if (queued || disposed)
            {
                return;
            }

            queued = true;
            JournalBootstrap.RunLater(delegate
            {
                queued = false;
                if (!disposed)
                {
                    UpdateSafely();
                }
            });
        }

        private void UpdateSafely()
        {
            try
            {
                Connect();
                Update();
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                if (!reported)
                {
                    reported = true;
                    JournalBootstrap.Report("Showing the occult meter", error);
                }
            }
        }

        private void Connect()
        {
            IResponder responder = Responder.Instance;
            IHudModel current = responder == null ? null : responder.HudModel;
            if (model == current)
            {
                return;
            }

            Disconnect();
            model = current;
            if (model != null)
            {
                model.SimChanged += OnSimChanged;
                model.OccultUpdated += OnOccultUpdated;
                model.SimMotivesChanged += OnMotivesChanged;
            }
        }

        private void Disconnect()
        {
            if (model != null)
            {
                model.SimChanged -= OnSimChanged;
                model.OccultUpdated -= OnOccultUpdated;
                model.SimMotivesChanged -= OnMotivesChanged;
                model = null;
            }
        }

        private void Update()
        {
            SimDisplay display = NativeUi.GetSimDisplay();
            if (display == null || model == null)
            {
                return;
            }

            WindowBase bar = ReadWindow(display, BarField, BarName);
            if (bar == null || bar.Disposed)
            {
                throw new InvalidOperationException("The HUD magic meter window was not found.");
            }

            ObjectGuid simId;
            OccultTypes type = OccultAssetData.Primary(SimIdentity.ReadOccults(model, out simId));
            if (type == OccultTypes.None)
            {
                bool hide = custom;
                Restore();
                if (hide) bar.Visible = false;
                return;
            }
            bool changed = shown != bar || active != type;
            if (changed)
            {
                Restore();
                Attach(bar);
                active = type;
            }
            custom = OccultEnergy.For(type) != null;
            uint accent = OccultAssetData.Accent(type);
            if (fill.ShadeColor.ARGB != accent)
            {
                fill.ShadeColor = new Color(accent);
            }
            if (changed)
            {
                tooltip = PowerTooltip(type);
            }
            if (tooltip != null)
            {
                bar.TooltipText = tooltip;
            }
            if (custom)
            {
                bar.Visible = true;
                Fill(display, simId);
            }
            if (changed) UpdateIcon(type);
        }

        private static string PowerTooltip(OccultTypes type)
        {
            switch (type)
            {
                case OccultTypes.Mermaid: return JournalText.Get("Arcana.Hud.Power.Tooltip.Mermaid");
                case OccultTypes.Vampire: return JournalText.Get("Arcana.Hud.Power.Tooltip.Vampire");
                case OccultTypes.Werewolf: return JournalText.Get("Arcana.Hud.Power.Tooltip.Werewolf");
                case OccultTypes.Ghost: return JournalText.Get("Arcana.Hud.Power.Tooltip.Ghost");
                case OccultTypes.Fairy: return JournalText.Get("Arcana.Hud.Power.Tooltip.Fairy");
                case OccultTypes.Genie: return JournalText.Get("Arcana.Hud.Power.Tooltip.Genie");
                default: return null;
            }
        }

        private void UpdateIcon(OccultTypes type)
        {
            ResourceKey key;
            if (!OccultAssetData.TryGetIcon(type, out key)) return;
            try
            {
                Window found = FindIcon(shown);
                if (found == null)
                {
                    throw new InvalidOperationException("The HUD meter has no unique icon control.");
                }
                UIImage replacement = UIManager.LoadUIImage(key);
                if (!NativeUi.HasImage(replacement))
                {
                    throw new InvalidOperationException("The embedded " + type + " HUD icon resource could not be loaded.");
                }
                glyph = found;
                glyphImage = glyph.Drawable as ImageDrawable;
                glyphIcon = glyph.Drawable as IconDrawable;
                originalImage = ReadIcon(glyph);
                if (nativeIcon == null && NativeUi.HasImage(originalImage))
                {
                    nativeIcon = originalImage;
                }
                originalIconShade = glyph.ShadeColor;
                SetIcon(replacement);
                hudReplaced = true;
                glyph.ShadeColor = new Color(0xFFFFFFFF);
                glyph.Invalidate();
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                if (!iconReported)
                {
                    iconReported = true;
                    JournalBootstrap.Report("Replacing the occult meter icon", error);
                }
            }
        }

        private static Window FindIcon(WindowBase parent)
        {
            Window icon = null;
            Window image = null;
            int icons = 0;
            int images = 0;
            for (uint index = 0; ; index++)
            {
                WindowBase child = parent.GetChildByIndex(index);
                if (child == null) break;
                Window window = child as Window;
                if (window == null || window.Disposed) continue;
                if (window.Drawable is IconDrawable)
                {
                    icon = window;
                    icons++;
                }
                else if (window.Drawable is ImageDrawable)
                {
                    Rect area = window.Area;
                    if (area.TopLeft.x < 0 && area.BottomRight.x > 0 &&
                        area.Width > 0 && area.Width < parent.Area.Width / 2)
                    {
                        image = window;
                        images++;
                    }
                }
            }
            if (icons == 1) return icon;
            return icons == 0 && images == 1 ? image : null;
        }

        internal static UIImage MeterIcon(OccultTypes type)
        {
            if (type == OccultTypes.None)
            {
                return null;
            }

            UIImage image;
            if (icons.TryGetValue(type, out image))
            {
                return image;
            }

            ResourceKey key;
            if (OccultAssetData.TryGetIcon(type, out key))
            {
                image = UIManager.LoadUIImage(key);
                if (!NativeUi.HasImage(image))
                {
                    image = null;
                }
                icons[type] = image;
                return image;
            }

            if (nativeIcon == null && !hudReplaced)
            {
                SimDisplay display = NativeUi.GetSimDisplay();
                WindowBase bar = display == null ? null : ReadWindow(display, BarField, BarName);
                Window found = bar == null || bar.Disposed ? null : FindIcon(bar);
                UIImage current = found == null ? null : ReadIcon(found);
                if (NativeUi.HasImage(current))
                {
                    nativeIcon = current;
                }
            }
            return nativeIcon;
        }

        private static UIImage ReadIcon(Window window)
        {
            IconDrawable icon = window.Drawable as IconDrawable;
            if (icon != null) return icon.Image;
            ImageDrawable image = window.Drawable as ImageDrawable;
            return image == null ? null : image.Image;
        }

        private void SetIcon(UIImage image)
        {
            if (glyphIcon != null) glyphIcon.Image = image;
            else if (glyphImage != null) glyphImage.Image = image;
        }

        private void Attach(WindowBase bar)
        {
            fill = NativeLayouts.Require<Window>(bar, NativeLayouts.MagicBar);
            originalShade = fill.ShadeColor;
            originalTooltip = bar.TooltipText;
            shown = bar;
        }

        private void Fill(SimDisplay display, ObjectGuid id)
        {
            WindowBase control = ReadWindow(display, FillControlField, FillControlName);
            WindowBase reference = ReadWindow(display, FillReferenceField, FillReferenceName);
            if (control == null || reference == null)
            {
                return;
            }

            Rect area = control.Area;
            OccultEnergy energy = OccultEnergy.For(active);
            if (energy == null)
            {
                return;
            }

            float right = area.TopLeft.x + reference.Area.Width * (energy.Get(id) / OccultEnergy.Max);
            control.Area = new Rect(area.TopLeft, new Vector2(right, area.BottomRight.y));
        }

        private void Restore()
        {
            if (shown == null)
            {
                return;
            }

            if (!shown.Disposed)
            {
                shown.TooltipText = originalTooltip;
                if (glyph != null && !glyph.Disposed)
                {
                    SetIcon(originalImage);
                    glyph.ShadeColor = originalIconShade;
                    glyph.Invalidate();
                }
                if (fill != null && !fill.Disposed)
                {
                    fill.ShadeColor = originalShade;
                }
            }
            glyph = null;
            glyphImage = null;
            glyphIcon = null;
            originalImage = null;
            hudReplaced = false;
            custom = false;
            active = OccultTypes.None;
            fill = null;
            shown = null;
        }

        private static WindowBase ReadWindow(SimDisplay display, FieldInfo field, string member)
        {
            return (WindowBase)NativeReflection.Field(field, display, "SimDisplay." + member);
        }

        private void OnSimChanged(ObjectGuid id)
        {
            Refresh();
        }

        private void OnOccultUpdated()
        {
            Refresh();
        }

        private void OnMotivesChanged(SimInfo info)
        {
            Refresh();
        }

        public void Dispose()
        {
            disposed = true;
            Disconnect();
            try
            {
                if (custom && shown != null && !shown.Disposed) shown.Visible = false;
                Restore();
                icons.Clear();
                nativeIcon = null;
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.Report("Removing the occult meter", error);
            }
        }
    }

    internal sealed class OccultEnergy
    {
        internal const float Max = 100f;

        internal static readonly OccultEnergy Vampire = new OccultEnergy(100f);
        internal static readonly OccultEnergy Werewolf = new OccultEnergy(100f);
        internal static readonly OccultEnergy Genie = new OccultEnergy(100f, true);
        internal static readonly OccultEnergy Mermaid = new OccultEnergy(100f);
        internal static readonly OccultEnergy Ghost = new OccultEnergy(100f);

        private readonly float start;
        private readonly bool genie;

        private OccultEnergy(float start) : this(start, false) { }

        private OccultEnergy(float start, bool genie)
        {
            this.start = start;
            this.genie = genie;
        }

        internal static OccultEnergy For(OccultTypes type)
        {
            switch (type)
            {
                case OccultTypes.Vampire: return Vampire;
                case OccultTypes.Werewolf: return Werewolf;
                case OccultTypes.Genie: return Genie;
                case OccultTypes.Mermaid: return Mermaid;
                case OccultTypes.Ghost: return Ghost;
                default: return null;
            }
        }


        internal float Get(ObjectGuid id)
        {
            if (genie)
            {
                IScriptProxy proxy = Simulator.GetProxy(id);
                Sim sim = proxy == null ? null : proxy.Target as Sim;
                if (sim != null) return GeniePower.Get(sim.SimDescription) / GeniePower.CapacityOf(sim.SimDescription) * Max;
            }
            return start;
        }

    }
}
