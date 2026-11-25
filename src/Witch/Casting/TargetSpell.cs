using System;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.SimIFace;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal sealed class TargetSpell
    {
        private const BindingFlags Methods = BindingFlags.Instance | BindingFlags.NonPublic;

        internal readonly InteractionDefinition Stock;
        internal readonly SpellEntry CatalogEntry;
        internal readonly MagicSpell Category;
        internal readonly MagicWand.SpellType Id;
        internal readonly BuffNames Backfire;
        internal readonly BuffNames TimedCurse;
        internal readonly float BaseCost;
        internal readonly MethodInfo NativeSuccess;
        internal readonly MethodInfo NativeFailure;
        internal readonly MethodInfo NativeEpicAllowed;
        internal readonly MethodInfo NativePreparation;
        internal readonly MethodInfo NativeSuccessVisual;
        internal readonly string JazzState;
        internal readonly MethodInfo SuccessVfx;
        internal readonly MethodInfo EpicVfx;
        internal readonly MethodInfo HitVfx;

        internal TargetSpell(string id, InteractionDefinition stock, MagicWand.CastSpell template, BuffNames backfire)
            : this(id, stock, template, backfire, BuffNames.Undefined)
        {
        }

        internal TargetSpell(string id, InteractionDefinition stock, MagicWand.CastSpell template,
            BuffNames backfire, BuffNames timedCurse)
        {
            SpellEntry entry = SpellCatalog.Find(id);
            if (entry == null) throw new InvalidOperationException("Missing spell catalog entry: " + id);
            Stock = stock;
            CatalogEntry = entry;
            Category = MagicSpell.For(entry);
            BaseCost = entry.Cost;
            Type type = template.GetType();
            Backfire = backfire;
            TimedCurse = timedCurse;
            NativeSuccessVisual = type.GetMethod("ShowSuccessVfx", Methods);
            NativeSuccess = type.GetMethod("OnSpellSuccess", Methods);
            NativeFailure = type.GetMethod("OnSpellEpicFailure", Methods);
            NativeEpicAllowed = type.GetMethod("EpicFailureAllowed", Methods);
            NativePreparation = type.GetMethod("AdditionalInteractionPreparation", Methods);
            Id = (MagicWand.SpellType)Read(type, "TypeOfSpell", template);
            JazzState = (string)Read(type, "JazzStateName", template);
            SuccessVfx = type.GetProperty("SuccessVfxName", Methods).GetGetMethod(true);
            EpicVfx = type.GetProperty("EpicFailVfxName", Methods).GetGetMethod(true);
            HitVfx = type.GetProperty("HitVfxName", Methods).GetGetMethod(true);
        }

        private static object Read(Type type, string property, MagicWand.CastSpell template)
        {
            return WitchSimCast.Invoke(type.GetProperty(property, Methods).GetGetMethod(true), template, null);
        }

        public abstract class Definition : InteractionDefinition<Sim, Sim, WitchSimCast>, IHasTraitIcon, IHasMenuPathIcon, IOverridesVisualType
        {
            internal abstract TargetSpell Spell { get; }

            protected override InteractionInstance CreateInstance(ref InteractionInstanceParameters parameters)
            {
                if (!MagicCasters.IsCaster(parameters.Actor as Sim))
                {
                    InteractionInstanceParameters original = parameters.CloneForNewInteraction(Spell.Stock);
                    return Spell.Stock.CreateInstanceFromParameters(ref original);
                }
                return base.CreateInstance(ref parameters);
            }

            public override string GetInteractionName(ref InteractionInstanceParameters parameters)
            {
                InteractionInstanceParameters original = parameters.CloneForNewInteraction(Spell.Stock);
                return Spell.Stock.GetInteractionName(ref original);
            }

            public override string[] GetPath(bool female)
            {
                return Spell.Stock.GetPath(female);
            }

            public InteractionVisualTypes GetVisualType
            {
                get { IOverridesVisualType provider = Spell.Stock as IOverridesVisualType; return provider == null ? InteractionVisualTypes.Standard : provider.GetVisualType; }
            }

            public ResourceKey GetTraitIcon(Sim sim, Sims3.Gameplay.Abstracts.GameObject target)
            {
                IHasTraitIcon provider = Spell.Stock as IHasTraitIcon; return provider == null ? new ResourceKey() : provider.GetTraitIcon(sim, target);
            }

            public ResourceKey GetPathIcon(Sim sim, Sims3.Gameplay.Abstracts.GameObject target)
            {
                IHasMenuPathIcon provider = Spell.Stock as IHasMenuPathIcon; return provider == null ? new ResourceKey() : provider.GetPathIcon(sim, target);
            }

            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                if (MagicCasters.IsInnateCaster(actor))
                {
                    return InnateSpellTests.Allows(Spell, actor, target, ref tooltip);
                }
                InteractionInstanceParameters parameters = new InteractionInstanceParameters(new InteractionObjectPair(Spell.Stock, target),
                    actor, new InteractionPriority(InteractionPriorityLevel.UserDirected), autonomous, true);
                if (actor.OccultManager == null || MagicWand.HasWand(actor) || !WitchCastingContext.HasPracticallyMagic(actor))
                {
                    return SpellKnowledgeGates.TestNative(Spell.Stock, ref parameters, ref tooltip) == InteractionTestResult.Pass;
                }

                OccultWitch witch = actor.OccultManager.GetOccultType(OccultTypes.Witch) as OccultWitch;
                bool casting = witch.IsCastingSpell;
                try
                {
                    witch.IsCastingSpell = true;
                    return SpellKnowledgeGates.TestNative(Spell.Stock, ref parameters, ref tooltip) == InteractionTestResult.Pass;
                }
                finally
                {
                    witch.IsCastingSpell = casting;
                }
            }
        }

        public sealed class GoodLuckCharm : Definition
        {
            private static readonly TargetSpell value = new TargetSpell("good_luck", MagicWand.CastGoodLuckCharm.Singleton,
                new MagicWand.CastGoodLuckCharm(), BuffNames.FeelingUnlucky);

            internal override TargetSpell Spell { get { return value; } }
        }

        public sealed class LoveCharm : Definition
        {
            private static readonly TargetSpell value = new TargetSpell("love", MagicWand.CastLoveCharm.Singleton,
                new MagicWand.CastLoveCharm(), BuffNames.Undefined);

            internal override TargetSpell Spell { get { return value; } }
        }

        public sealed class SunlightCharm : Definition
        {
            private static readonly TargetSpell value = new TargetSpell("sunlight", MagicWand.CastSunlightCharm.Singleton,
                new MagicWand.CastSunlightCharm(), BuffNames.Dazed);

            internal override TargetSpell Spell { get { return value; } }
        }

        public sealed class PestilenceCurse : Definition
        {
            private static readonly TargetSpell value = new TargetSpell("pestilence", MagicWand.CastPestilenceCurse.Singleton,
                new MagicWand.CastPestilenceCurse(), BuffNames.SickAndTired, BuffNames.SickAndTired);

            internal override TargetSpell Spell { get { return value; } }
        }

        public sealed class IceBlast : Definition
        {
            private static readonly TargetSpell value = new TargetSpell("ice_blast", MagicWand.CastIceBlast.Singleton,
                new MagicWand.CastIceBlast(), BuffNames.SoCold);

            internal override TargetSpell Spell { get { return value; } }
        }

        public sealed class HauntingCurse : Definition
        {
            private static readonly TargetSpell value = new TargetSpell("haunting", MagicWand.CastHauntingCurse.Singleton,
                new MagicWand.CastHauntingCurse(), BuffNames.Undefined, BuffNames.Haunted);

            internal override TargetSpell Spell { get { return value; } }
        }

        public sealed class FireBlast : Definition
        {
            private static readonly TargetSpell value = new TargetSpell("fire_blast", MagicWand.CastFireBlast.Singleton,
                new MagicWand.CastFireBlast(), BuffNames.Undefined);

            internal override TargetSpell Spell { get { return value; } }
        }

        public sealed class Toadify : Definition
        {
            private static readonly TargetSpell value = new TargetSpell("toadification", MagicWand.CastToadify.Singleton,
                new MagicWand.CastToadify(), BuffNames.Undefined);

            internal override TargetSpell Spell { get { return value; } }
        }

        public sealed class MotiveSpell : Definition
        {
            private readonly TargetSpell value;

            internal MotiveSpell(TargetSpell spell)
            {
                value = spell;
            }

            internal override TargetSpell Spell { get { return value; } }

            internal static MotiveSpell Create(SpellEntry entry, CommodityKind motive, int level)
            {
                bool charm = entry.Category == SpellCategory.Charm;
                Type definition = charm ? typeof(MagicWand.CastMotiveCharm.Definition) : typeof(MagicWand.CastMotiveCurse.Definition);
                InteractionDefinition stock = (InteractionDefinition)Activator.CreateInstance(definition,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { motive, level }, null);
                MagicWand.CastSpell template = charm ? (MagicWand.CastSpell)new MagicWand.CastMotiveCharm() : new MagicWand.CastMotiveCurse();
                return new MotiveSpell(new TargetSpell(entry.Id, stock, template, BuffNames.Undefined));
            }
        }
    }
}
