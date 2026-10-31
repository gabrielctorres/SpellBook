using SpellBook.Core.Editor;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using UnityEditor;

namespace SpellBook.GAS.Editor
{
    /// <summary>Inspectors de uma linha dos tipos do GAS (Spec, seção 4.10, "Um editor por tipo").</summary>
    [CustomEditor(typeof(AbilitySystemComponent))]
    [CanEditMultipleObjects]
    internal sealed class AbilitySystemComponentEditor : SpellBookEditor { }

    [CustomEditor(typeof(AttributeSet))]
    [CanEditMultipleObjects]
    internal sealed class AttributeSetEditor : SpellBookEditor { }

    [CustomEditor(typeof(GameplayAbility))]
    [CanEditMultipleObjects]
    internal sealed class GameplayAbilityEditor : SpellBookEditor { }

    [CustomEditor(typeof(GameplayEffect))]
    [CanEditMultipleObjects]
    internal sealed class GameplayEffectEditor : SpellBookEditor { }

    [CustomEditor(typeof(AttributeDefinition))]
    [CanEditMultipleObjects]
    internal sealed class AttributeDefinitionEditor : SpellBookEditor { }

    [CustomEditor(typeof(Formula), true)]
    [CanEditMultipleObjects]
    internal sealed class FormulaEditor : SpellBookEditor { }

    [CustomEditor(typeof(TargetingStrategy), true)]
    [CanEditMultipleObjects]
    internal sealed class TargetingStrategyEditor : SpellBookEditor { }

    [CustomEditor(typeof(Projectile))]
    [CanEditMultipleObjects]
    internal sealed class ProjectileEditor : SpellBookEditor { }
}
