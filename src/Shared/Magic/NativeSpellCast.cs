using System;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    public sealed class NativeSpellCast : InteractionGameObjectHit<Sim, GameObject>
    {
        internal InteractionInstance Running;

        protected override bool Run()
        {
            Definition definition = InteractionDefinition as Definition;
            if (definition == null || definition.Native == null) return Fail("it has no native spell");
            if (MagicCasters.NeedsHands(Actor) && !MagicCasters.EnsureHands(Actor) && !MagicWand.HasWand(Actor))
                return Fail("Magic Hands could not be placed in the caster's inventory");
            InteractionInstanceParameters parameters = GetInteractionParameters().CloneForNewInteraction(definition.Native);
            parameters.SetInteractionObjectPair(new KnowledgePair(definition.Native, Target, InteractionObjectPair.Tuning));
            Running = definition.Native.CreateInstanceFromParameters(ref parameters);
            if (Running == null) return Fail("the game did not create " + definition.Native.GetType().FullName);
            try
            {
                bool completed = Running.RunInteraction();
                if (completed) WitchNativeCosts.Charge(Actor);
                return completed;
            }
            finally
            {
                Running = null;
            }
        }

        public override ThumbnailKey GetIconKey()
        {
            return new ThumbnailKey(ResourceKey.CreatePNGKey("w_cast_spell", ResourceUtils.ProductVersionToGroupId(ProductVersion.EP7)),
                ThumbnailSize.Large);
        }

        private bool Fail(string reason)
        {
            JournalBootstrap.ReportOnce("Native spell cast", new InvalidOperationException("The cast stopped because " + reason + "."));
            return false;
        }

        public sealed class Definition : InteractionDefinition<Sim, GameObject, NativeSpellCast>, IHasTraitIcon, IHasMenuPathIcon, IOverridesVisualType
        {
            internal readonly InteractionDefinition Native;

            internal Definition(InteractionDefinition native)
            {
                Native = native;
            }

            public override InteractionTestResult Test(ref InteractionInstanceParameters parameters, ref GreyedOutTooltipCallback tooltip)
            {
                return SpellKnowledgeGates.TestNative(Native, ref parameters, ref tooltip);
            }

            protected override bool Test(Sim actor, GameObject target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                return true;
            }

            public override string GetInteractionName(ref InteractionInstanceParameters parameters)
            {
                InteractionInstanceParameters original = parameters.CloneForNewInteraction(Native);
                return Native.GetInteractionName(ref original);
            }

            public override string[] GetPath(bool female)
            {
                return Native.GetPath(female);
            }

            public InteractionVisualTypes GetVisualType
            {
                get { IOverridesVisualType provider = Native as IOverridesVisualType; return provider == null ? InteractionVisualTypes.Standard : provider.GetVisualType; }
            }

            public ResourceKey GetTraitIcon(Sim sim, GameObject target)
            {
                IHasTraitIcon provider = Native as IHasTraitIcon; return provider == null ? new ResourceKey() : provider.GetTraitIcon(sim, target);
            }

            public ResourceKey GetPathIcon(Sim sim, GameObject target)
            {
                IHasMenuPathIcon provider = Native as IHasMenuPathIcon; return provider == null ? new ResourceKey() : provider.GetPathIcon(sim, target);
            }
        }
    }
}
