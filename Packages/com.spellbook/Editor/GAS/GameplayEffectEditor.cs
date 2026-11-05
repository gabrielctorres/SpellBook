using SpellBook.Core.Editor;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Effects;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Inspector do <see cref="GameplayEffect"/> (DS do Inspector da habilidade, seção 4). Abaixo dos avisos e da
    /// validação, quatro abas com os campos do Inspector padrão em grupos: Configuração (Identidade, Duração e
    /// Empilhamento), Modificadores (Persistentes e Do tique), Ações (as abas de ações por evento, com as reações
    /// agrupadas, e abaixo os atributos de dano e de morte) e Tags (as listas compactas, <see cref="CompactTagList"/>,
    /// com "N de 4 com itens" no título do grupo). Um campo que o editor não conhece vai para "Outros", no fim de
    /// Configuração. Campo que não vale na configuração atual some e guarda o valor (CA7): o grupo "Do tique" e a aba
    /// "A cada tick" só com <see cref="GameplayEffect.Period"/> maior que 0, os campos de pilha só com
    /// <see cref="GameplayEffect.CanStack"/> e a duração só com <see cref="EffectDurationPolicy.HasDuration"/>, pelo
    /// <c>[ShowIf]</c> do runtime. O aviso de ações em "A cada tick" que não vão rodar fica no grupo Duração.
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

        private static readonly string[] StackFields =
        {
            nameof(GameplayEffect.MaxStacks), nameof(GameplayEffect.RefreshDurationOnStack), "stacksMultiplyMagnitude",
        };

        private VisualElement _tickGroup;
        private VisualElement _stacking;

        protected override void BuildAfterDefault(VisualElement root)
        {
            var tabs = new InspectorTabsView(serializedObject.targetObject, ConfigurationTab, ModifiersTab, ActionsTab, TagsTab);

            VisualElement configuration = tabs.Content(ConfigurationTab);
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(configuration, "Identidade"),
                "localizedName", "icon", "isBuff", "isDebuff", "isHidden", "isPurgable");
            VisualElement duration = InspectorTabsView.AddGroup(configuration, "Duração");
            InspectorTabsView.MoveFields(root, duration,
                nameof(GameplayEffect.DurationPolicy), "duration", "defaultLevel", nameof(GameplayEffect.Period));
            _stacking = InspectorTabsView.AddGroup(configuration, "Empilhamento");
            InspectorTabsView.MoveFields(root, _stacking, nameof(GameplayEffect.CanStack));
            InspectorTabsView.MoveFields(root, _stacking, StackFields);

            VisualElement modifiers = tabs.Content(ModifiersTab);
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(modifiers, "Persistentes"),
                nameof(GameplayEffect.Modifiers));
            _tickGroup = InspectorTabsView.AddGroup(modifiers, "Do tique");
            InspectorTabsView.MoveFields(root, _tickGroup, nameof(GameplayEffect.PeriodicModifiers));

            VisualElement actions = tabs.Content(ActionsTab);
            ActionEventsView.HideDefaultFields(root, typeof(GameplayEffect));
            var events = new ActionEventsView(serializedObject,
                "Este efeito ainda não faz nada. Adicione uma ação em \"Ao ser aplicado\".",
                field => field != nameof(GameplayEffect.OnTick) || IsPeriodic(target),
                Reactions, "o período atual");
            if (events.HiddenWarning != null) duration.Add(events.HiddenWarning);
            actions.Add(events);
            InspectorTabsView.MoveFields(root, InspectorTabsView.AddGroup(actions, "Reações"),
                "damageAttributes", "deathAttribute");

            VisualElement tags = InspectorTabsView.AddGroup(tabs.Content(TagsTab), "Tags");
            InspectorTabsView.MoveFields(root, tags, TagLists);
            CompactTagList.WrapGroup(tags, serializedObject, TagLists);

            InspectorTabsView.MoveRemaining(root, configuration, "m_Script");
            root.Add(tabs);
            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                RefreshConfigFields();
                CompactTagList.RefreshAll(tags);
            });
            RefreshConfigFields();
        }

        /// <summary>
        /// Mostra só o que vale na configuração (CA7): o grupo "Do tique" com período maior que 0 e os campos de pilha
        /// com <see cref="GameplayEffect.CanStack"/>. O campo escondido guarda o valor. Com vários assets, o campo
        /// aparece se algum deles o usa.
        /// </summary>
        internal void RefreshConfigFields()
        {
            bool periodic = false;
            bool stacks = false;
            foreach (Object selected in targets)
            {
                periodic |= IsPeriodic(selected);
                stacks |= selected is GameplayEffect effect && effect.CanStack;
            }

            Show(_tickGroup, periodic);
            foreach (string field in StackFields) Show(_stacking?.Q<PropertyField>("PropertyField:" + field), stacks);
        }

        private static bool IsPeriodic(Object selected)
        {
            return selected is GameplayEffect effect && effect.Period > 0f;
        }

        private static void Show(VisualElement element, bool visible)
        {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
