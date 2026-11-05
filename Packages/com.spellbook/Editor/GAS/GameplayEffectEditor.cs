using SpellBook.Core.Editor;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Effects;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Inspector do <see cref="GameplayEffect"/> (DS do Inspector da habilidade, seção 4). Abaixo dos avisos e da
    /// validação, quatro abas com os campos do Inspector padrão em grupos: Configuração (Identidade, Duração e
    /// Empilhamento), Modificadores (Persistentes e Do tique), Ações (as abas de ações por evento, com as reações
    /// agrupadas, e abaixo os atributos de dano e de morte) e Tags (as listas compactas, <see cref="CompactTagList"/>,
    /// com "N de 4 com itens" no título do grupo). Um campo que o editor não conhece vai para "Outros", no fim de
    /// Configuração.
    /// </summary>
    [CustomEditor(typeof(GameplayEffect), true)]
    [CanEditMultipleObjects]
    internal sealed class GameplayEffectEditor : SpellBookEditor
    {
        internal const string ConfigurationTab = "Configuração";
        internal const string ModifiersTab = "Modificadores";
        internal const string ActionsTab = "Ações";
        internal const string TagsTab = "Tags";

        private static readonly string[] Reactions =
        {
            nameof(GameplayEffect.OnTakeDamage), nameof(GameplayEffect.OnDeath), nameof(GameplayEffect.OnAbilityExecuted),
        };

        private static readonly string[] TagLists =
        {
            "damageTags", nameof(GameplayEffect.GrantedTags), nameof(GameplayEffect.ApplicationRequirements),
            nameof(GameplayEffect.ApplicationImmunityTags),
        };

        protected override void BuildAfterDefault(VisualElement root)
        {
            var tabs = new InspectorTabsView(serializedObject.targetObject, ConfigurationTab, ModifiersTab, ActionsTab, TagsTab);

            VisualElement configuration = tabs.Content(ConfigurationTab);
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(configuration, "Identidade"),
                "localizedName", "icon", "isBuff", "isDebuff", "isHidden", "isPurgable");
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(configuration, "Duração"),
                nameof(GameplayEffect.DurationPolicy), "duration", "defaultLevel", nameof(GameplayEffect.Period));
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(configuration, "Empilhamento"),
                nameof(GameplayEffect.CanStack), nameof(GameplayEffect.MaxStacks),
                nameof(GameplayEffect.RefreshDurationOnStack), "stacksMultiplyMagnitude");

            VisualElement modifiers = tabs.Content(ModifiersTab);
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(modifiers, "Persistentes"),
                nameof(GameplayEffect.Modifiers));
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(modifiers, "Do tique"),
                nameof(GameplayEffect.PeriodicModifiers));

            VisualElement actions = tabs.Content(ActionsTab);
            ActionEventsView.HideDefaultFields(root, typeof(GameplayEffect));
            actions.Add(new ActionEventsView(serializedObject,
                "Este efeito ainda não faz nada. Adicione uma ação em \"Ao ser aplicado\".", reactions: Reactions));
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(actions, "Reações"),
                "damageAttributes", "deathAttribute");

            VisualElement tags = InspectorTabsView.AddGroup(tabs.Content(TagsTab), "Tags");
            InspectorTabsView.MoveFields(root, tags, TagLists);
            CompactTagList.WrapGroup(tags, serializedObject, TagLists);

            InspectorTabsView.MoveRemaining(root, configuration, "m_Script");
            root.Add(tabs);
            root.TrackSerializedObjectValue(serializedObject, _ => CompactTagList.RefreshAll(tags));
        }
    }
}
