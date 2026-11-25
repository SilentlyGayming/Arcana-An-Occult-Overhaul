using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Interfaces;
using Sims3.Gameplay.Pools;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal sealed class GenieAbility
    {
        internal readonly string Id;
        internal readonly string NativeClass;
        internal readonly Rank Rank;
        internal readonly float Cost;
        internal readonly int Tier;
        internal readonly string NameKey;
        internal readonly string DescriptionKey;

        internal GenieAbility(string id, string nativeClass, Rank rank, float cost, int tier, string nameKey, string descriptionKey)
        {
            Id = id;
            NativeClass = nativeClass;
            Rank = rank;
            Cost = cost;
            Tier = tier;
            NameKey = nameKey;
            DescriptionKey = descriptionKey;
        }

        internal bool CanFail { get { return Tier >= 0; } }

        internal float CostFor(CastingRules rules)
        {
            float cost = Cost * rules.AbilityCostFactor;
            if (rules.DomesticDjinn && Rank <= Rank.Apprentice) cost *= 1f - CastingRules.DomesticDjinnDiscount;
            if (rules.SilkenCommand && Id == "genie_ensorcel") cost *= 1f - CastingRules.SilkenEnsorcelDiscount;
            if (rules.FinePrint && CanFail) cost *= CastingRules.FinePrintCost;
            return cost;
        }
    }

    internal static class GenieAbilities
    {
        private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal static readonly GenieAbility[] All =
        {
            new GenieAbility("genie_clean_sim", "CleanSim", Rank.Novice, 10f, 0, "Arcana.Ability.genie_clean_sim.Name", "Arcana.Ability.genie_clean_sim.Description"),
            new GenieAbility("genie_meal", "SummonFood", Rank.Apprentice, 20f, 1, "Arcana.Ability.genie_meal.Name", "Arcana.Ability.genie_meal.Description"),
            new GenieAbility("genie_enchant_home", "EnchantHouse", Rank.Adept, 30f, 1, "Arcana.Ability.genie_enchant_home.Name", "Arcana.Ability.genie_enchant_home.Description"),
            new GenieAbility("genie_banish", "RemoveSim", Rank.Adept, 40f, 2, "Arcana.Ability.genie_banish.Name", "Arcana.Ability.genie_banish.Description"),
            new GenieAbility("genie_ensorcel", "EnsorcelInteraction", Rank.Master, 80f, 3, "Arcana.Ability.genie_ensorcel.Name", "Arcana.Ability.genie_ensorcel.Description"),
            new GenieAbility("genie_release", "ReleaseSim", Rank.Master, 0f, -1, "Arcana.Ability.genie_release.Name", "Arcana.Ability.genie_release.Description")
        };

        internal static List<KeyValuePair<FieldInfo, InteractionDefinition>> Singletons()
        {
            List<KeyValuePair<FieldInfo, InteractionDefinition>> result = new List<KeyValuePair<FieldInfo, InteractionDefinition>>();
            foreach (GenieAbility ability in All)
            {
                Type type = typeof(OccultGenie).GetNestedType(ability.NativeClass, BindingFlags.Public | BindingFlags.NonPublic);
                FieldInfo singleton = type == null ? null : type.GetField("Singleton", StaticFields);
                if (singleton == null) throw new InvalidOperationException("Missing native Genie ability singleton: " + ability.NativeClass);
                result.Add(new KeyValuePair<FieldInfo, InteractionDefinition>(singleton, (InteractionDefinition)singleton.GetValue(null)));
            }
            return result;
        }

        internal static GenieAbility For(InteractionDefinition native)
        {
            Type owner = native == null ? null : native.GetType().DeclaringType;
            if (owner == null || owner.DeclaringType != typeof(OccultGenie)) return null;
            foreach (GenieAbility ability in All)
            {
                if (ability.NativeClass == owner.Name) return ability;
            }
            return null;
        }

        internal static InteractionDefinition Wrap(InteractionDefinition native)
        {
            if (native is ISoloInteractionDefinition) return new SoloGenieAbilityDefinition(native);
            return new GenieAbilityDefinition(native);
        }

        internal static bool IsGenie(Sim actor)
        {
            CasterKind kind;
            return WitchCastingLifecycle.CastingInstalled && MagicCasters.TryKind(actor, out kind) && kind == CasterKind.Genie;
        }

        internal static InteractionTestResult Test(InteractionDefinition native, GenieAbility ability, ref InteractionInstanceParameters parameters,
            ref GreyedOutTooltipCallback tooltip)
        {
            Sim actor = parameters.Actor as Sim;
            InteractionInstanceParameters original = parameters.CloneForNewInteraction(native);
            if (!IsGenie(actor)) return native.Test(ref original, ref tooltip);
            CastingRules rules = MagicCasters.ReadRules(actor);
            if (rules.Rank < ability.Rank) return InteractionTestResult.Def_TestFailed;
            if (MagicCasters.AvailablePower(actor, rules) + 0.005f < ability.CostFor(rules))
            {
                tooltip = GenieMenu.SilentUnavailable;
                return InteractionTestResult.Def_TestFailed;
            }
            GeniePower.RefillNativePoints(actor);
            return native.Test(ref original, ref tooltip);
        }

        internal static InteractionInstance Create(InteractionDefinition native, ref InteractionInstanceParameters parameters)
        {
            InteractionDefinition implementation = IsGenie(parameters.Actor as Sim) ? new GenieAbilityCast.Definition(native) : native;
            InteractionInstanceParameters copy = parameters.CloneForNewInteraction(implementation);
            copy.SetInteractionObjectPair(new KnowledgePair(implementation, parameters.Target, new InteractionObjectPair(native, parameters.Target).Tuning));
            return implementation.CreateInstanceFromParameters(ref copy);
        }
    }

    [Persistable]
    internal class GenieAbilityDefinition : InteractionDefinition
    {
        internal InteractionDefinition Native;
        internal GenieAbilityDefinition(InteractionDefinition native) { Native = native; }
        internal GenieAbility Ability { get { return GenieAbilities.For(Native); } }
        public string[] GetPath(bool female) { return Native.GetPath(female); }
        public string GetInteractionName(IActor actor, IGameObject target, InteractionObjectPair pair)
        { return Native.GetInteractionName(actor, target, new InteractionObjectPair(Native, target)); }
        public string GetInteractionName(ref InteractionInstanceParameters parameters)
        { InteractionInstanceParameters original = parameters.CloneForNewInteraction(Native); return Native.GetInteractionName(ref original); }
        public InteractionTestResult Test(ref InteractionInstanceParameters parameters, ref GreyedOutTooltipCallback tooltip)
        {
            GenieAbility ability = Ability;
            if (ability == null) return InteractionTestResult.Def_TestFailed;
            return GenieAbilities.Test(Native, ability, ref parameters, ref tooltip);
        }
        public void AddInteractions(InteractionObjectPair pair, IActor actor, List<InteractionObjectPair> results)
        {
            List<InteractionObjectPair> expanded = new List<InteractionObjectPair>();
            Native.AddInteractions(new InteractionObjectPair(Native, pair.Target), actor, expanded);
            foreach (InteractionObjectPair item in expanded)
                results.Add(new KnowledgePair(this, item.Target, item.Tuning));
        }
        public InteractionInstance CreateInstanceFromParameters(ref InteractionInstanceParameters parameters)
        { return GenieAbilities.Create(Native, ref parameters); }
        public InteractionInstance CreateInstance(IGameObject target, IActor actor, InteractionPriority priority, bool autonomous, bool cancellable)
        {
            InteractionInstanceParameters parameters = new InteractionInstanceParameters(new InteractionObjectPair(Native, target), actor, priority, autonomous, cancellable);
            return CreateInstanceFromParameters(ref parameters);
        }
        public InteractionInstance CreateInstanceWithCallbacks(IGameObject target, IActor actor, InteractionPriority priority, bool autonomous,
            bool cancellable, Callback started, Callback completed, Callback failed)
        {
            if (!GenieAbilities.IsGenie(actor as Sim))
                return Native.CreateInstanceWithCallbacks(target, actor, priority, autonomous, cancellable, started, completed, failed);
            GenieAbilityCast.Definition cast = new GenieAbilityCast.Definition(Native);
            InteractionObjectPair.sTuningCache[new Pair<Type, Type>(cast.GetType(), target.GetType())] = new InteractionObjectPair(Native, target).Tuning;
            return cast.CreateInstanceWithCallbacks(target, actor, priority, autonomous, cancellable, started, completed, failed);
        }
        public void PopulatePieMenuPicker(ref InteractionInstanceParameters parameters, out List<ObjectPicker.TabInfo> tabs,
            out List<ObjectPicker.HeaderInfo> headers, out int selectable)
        { InteractionInstanceParameters original = parameters.CloneForNewInteraction(Native); Native.PopulatePieMenuPicker(ref original, out tabs, out headers, out selectable); }
        public MenuItem.ObjectPickerTitleDelegate GetPickerTitleDelegate() { return Native.GetPickerTitleDelegate(); }
    }

    [Persistable]
    internal sealed class SoloGenieAbilityDefinition : GenieAbilityDefinition, ISoloInteractionDefinition
    {
        internal SoloGenieAbilityDefinition(InteractionDefinition native) : base(native) { }
    }

    public sealed class GenieAbilityCast : InteractionGameObjectHit<Sim, GameObject>, ICannotStartInPool
    {
        internal InteractionInstance Running;
        private ThumbnailKey icon;
        private bool hasIcon;

        protected override bool Run()
        {
            Definition definition = InteractionDefinition as Definition;
            GenieAbility ability = definition == null ? null : GenieAbilities.For(definition.Native);
            if (ability == null) return false;
            CastingRules rules = MagicCasters.ReadRules(Actor);
            if (rules.Rank < ability.Rank) return false;
            float cost = ability.CostFor(rules);
            if (MagicCasters.AvailablePower(Actor, rules) + 0.005f < cost) return false;
            bool forced = ArcanaPresentationCheats.TakeCast(Actor);
            bool succeeded = true;
            bool penalty = false;
            if (ability.CanFail && !forced)
            {
                MagicSpell spell = new MagicSpell(ability.Tier, MagicSpellKind.Spell);
                float needPenalty;
                int empty;
                WitchCastingContext.CountNeeds(Actor, out needPenalty, out empty, null);
                float accuracy = rules.Accuracy(spell, rules.InnateAccuracy, WitchCastingContext.EmotionPenalty(Actor), needPenalty, empty);
                succeeded = RandomUtil.RandomChance(accuracy);
                penalty = MagicCasters.ResolveBacklash(Actor, rules, spell, accuracy, true, ref succeeded);
            }
            if (!succeeded)
            {
                MagicCasters.SpendInnate(Actor, rules, cost);
                return Fizzle(rules, penalty);
            }
            GeniePower.RefillNativePoints(Actor);
            InteractionInstanceParameters parameters = GetInteractionParameters().CloneForNewInteraction(definition.Native);
            parameters.SetInteractionObjectPair(new KnowledgePair(definition.Native, Target, InteractionObjectPair.Tuning));
            if (!forced && GenieBlessings.ResistsAbility(Actor, Target as Sim, ability))
            {
                MagicCasters.SpendInnate(Actor, rules, cost);
                return Fizzle(rules, false);
            }
            Running = definition.Native.CreateInstanceFromParameters(ref parameters);
            if (Running == null) return false;
            Running.SelectedObjects = SelectedObjects;
            Sim patient = Target as Sim;
            float hygiene = patient == null ? float.NaN : patient.Motives.GetValue(CommodityKind.Hygiene);
            float power = MagicCasters.AvailablePower(Actor, rules);
            try
            {
                bool ran = Running.RunInteraction();
                if (ran)
                {
                    MagicCasters.SpendInnate(Actor, rules, cost);
                    GeniePerks.AfterAbility(ability, Actor, patient, rules);
                }
                if (forced) ArcanaPresentationCheats.ReportCast(ability, Actor, patient, ran, hygiene, power, MagicCasters.AvailablePower(Actor, rules));
                return ran;
            }
            finally
            {
                Running = null;
                GeniePower.RefillNativePoints(Actor);
            }
        }

        public override ThumbnailKey GetIconKey()
        {
            if (hasIcon) return icon;
            Definition definition = InteractionDefinition as Definition;
            if (definition == null) return base.GetIconKey();
            InteractionInstanceParameters parameters = GetInteractionParameters().CloneForNewInteraction(definition.Native);
            InteractionInstance native = definition.Native.CreateInstanceFromParameters(ref parameters);
            icon = native == null ? base.GetIconKey() : native.GetIconKey();
            hasIcon = true;
            return icon;
        }

        // A failed cast keeps the clap but has no whirlwind and ends in a shrug, so it never reads as a successful one.
        private bool Fizzle(CastingRules rules, bool penalty)
        {
            StandardEntry();
            Actor.PlaySoloAnimation("a_genie_clap_x", true, ProductVersion.EP6);
            StandardExit();
            if (penalty) WitchMagicRecovery.Fizzle(Actor, rules);
            Actor.PlayReaction(ReactionTypes.Shrug, ReactionSpeed.AfterInteraction);
            return false;
        }

        public sealed class Definition : InteractionDefinition<Sim, GameObject, GenieAbilityCast>
        {
            internal readonly InteractionDefinition Native;

            internal Definition(InteractionDefinition native)
            {
                Native = native;
            }

            public override InteractionTestResult Test(ref InteractionInstanceParameters parameters, ref GreyedOutTooltipCallback tooltip)
            {
                GenieAbility ability = GenieAbilities.For(Native);
                if (ability == null) return InteractionTestResult.Def_TestFailed;
                return GenieAbilities.Test(Native, ability, ref parameters, ref tooltip);
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
        }
    }
}
