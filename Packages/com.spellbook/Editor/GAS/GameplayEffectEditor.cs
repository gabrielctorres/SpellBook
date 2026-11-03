using SpellBook.Core.Editor;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Effects;
using UnityEditor;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Editor
{
    /// <summary>Inspector do <see cref="GameplayEffect"/>: o padrão e, no fim, as abas de ações, com as reações agrupadas.</summary>
    [CustomEditor(typeof(GameplayEffect))]
    [CanEditMultipleObjects]
    internal sealed class GameplayEffectEditor : SpellBookEditor
    {
        private static readonly string[] Reactions =
        {
            nameof(GameplayEffect.OnTakeDamage), nameof(GameplayEffect.OnDeath), nameof(GameplayEffect.OnAbilityExecuted),
        };

        protected override void BuildAfterDefault(VisualElement root)
        {
            ActionEventsView.HideDefaultFields(root, typeof(GameplayEffect));
            root.Add(new ActionEventsView(serializedObject,
                "Este efeito ainda não faz nada. Adicione uma ação em \"Ao ser aplicado\".", reactions: Reactions));
        }
    }
}
