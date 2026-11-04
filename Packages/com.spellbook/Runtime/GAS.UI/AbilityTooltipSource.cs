using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Description;
using SpellBook.GAS.Effects;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Fonte do tooltip de uma habilidade (Spec UI-3, Design 4; R3 do PRD): título, ícone, subtítulo, as linhas da
    /// descrição e as de custo, recarga e cargas, no nível do <see cref="AbilitySpec"/> do ASC dono. Sem ASC (dono
    /// ausente ou de outro tipo), mostra só o que o asset sabe, no <see cref="LevelWithoutOwner"/> (P2 do PRD). Atualiza
    /// por evento do nível, da recarga, das cargas, da redução de recarga e dos atributos do custo; não pede tick e não
    /// altera o ASC.
    /// </summary>
    [Serializable, TypeCategory("Tooltip", Summary = "Tooltip de uma habilidade: descrição, custo, recarga e cargas no nível do dono, ou no nível configurado sem dono.",
        Line = "Habilidade {ability}")]
    public sealed class AbilityTooltipSource : UISource<TooltipModel>
    {
        private static readonly CostElement CostLines = new CostElement();
        private static readonly CooldownElement CooldownLine = new CooldownElement();
        private static readonly ChargesElement ChargesLine = new ChargesElement();

        [Tooltip("Habilidade mostrada. Vazio: a do item (UIItemContext.Subject).")]
        [SerializeField] private GameplayAbility ability;

        [Tooltip("Subtítulo abaixo do título, como a categoria da habilidade. Vazio: sem subtítulo.")]
        [SerializeField] private LocalizedText subtitle;

        [Tooltip("Nível dos valores quando não há ASC dono ou ele não tem a habilidade.")]
        [SerializeField, Min(1)] private int levelWithoutOwner = 1;

        [Tooltip("Formato do texto dos valores. Vazio: o formato do atributo.")]
        [SerializeReference, TypePicker] private ModifierTextFormatter formatter;

        [Tooltip("Acrescenta as linhas de custo, se a descrição não as tiver.")]
        [SerializeField] private bool showCost = true;

        [Tooltip("Acrescenta a linha de recarga, se a descrição não a tiver.")]
        [SerializeField] private bool showCooldown = true;

        [Tooltip("Acrescenta a linha de cargas, se a descrição não a tiver.")]
        [SerializeField] private bool showCharges = true;

        [NonSerialized] private TooltipLineWriter _preview;

        /// <summary>A habilidade mostrada; nula usa o assunto do item.</summary>
        public GameplayAbility Ability { get => ability; set => ability = value; }

        /// <summary>O subtítulo; vazio não mostra.</summary>
        public LocalizedText Subtitle { get => subtitle; set => subtitle = value; }

        /// <summary>Nível sem ASC dono; menor que 1 vale 1.</summary>
        public int LevelWithoutOwner { get => levelWithoutOwner; set => levelWithoutOwner = Mathf.Max(1, value); }

        /// <summary>Formato do texto dos valores; nulo usa o do atributo.</summary>
        public ModifierTextFormatter Formatter { get => formatter; set => formatter = value; }

        /// <summary>Se acrescenta as linhas de custo.</summary>
        public bool ShowCost { get => showCost; set => showCost = value; }

        /// <summary>Se acrescenta a linha de recarga.</summary>
        public bool ShowCooldown { get => showCooldown; set => showCooldown = value; }

        /// <summary>Se acrescenta a linha de cargas.</summary>
        public bool ShowCharges { get => showCharges; set => showCharges = value; }

        /// <inheritdoc />
        public override Type OwnerType => typeof(AbilitySystemComponent);

        /// <inheritdoc />
        public override UIBinding<TooltipModel> CreateBinding() => new Binding(this);

        /// <summary>Sem habilidade e sem <see cref="UIItemContext"/> no objeto: erro no campo (R14).</summary>
        public override void Validate(ValidationResult result, string propertyPath) =>
            TooltipSubjectRule.Validate(result, ability, $"{propertyPath}.{nameof(ability)}", "a", "habilidade");

        /// <summary>O tooltip da habilidade configurada no nível do contexto, sem dono; vazio sem habilidade.</summary>
        public override TooltipModel CreatePreview(in UIPreviewContext context)
        {
            return Build(_preview ??= new TooltipLineWriter(), ability, context.Level, null, context.Text);
        }

        // Monta o modelo no writer; sem habilidade (nula ou destruída), o modelo vazio
        private TooltipModel Build(TooltipLineWriter writer, GameplayAbility shown, int level, AbilitySystemComponent asc,
            ITextProvider text)
        {
            if (shown == null) return default;

            writer.Begin(shown, level, asc, text, formatter);
            DescriptionContext context = writer.Context;
            shown.Describe(context);
            if (showCost && !writer.Has(DescriptionLineKind.Cost)) CostLines.Describe(context);
            if (showCooldown && !writer.Has(DescriptionLineKind.Cooldown)) CooldownLine.Describe(context);
            if (showCharges && !writer.Has(DescriptionLineKind.Charges)) ChargesLine.Describe(context);
            writer.Convert();

            string sub = subtitle.IsEmpty ? null : subtitle.Resolve(text);
            return new TooltipModel(shown.DisplayName.Resolve(text), shown.Icon, writer.Lines, sub);
        }

        // Estado de uma ligação: o writer, o ASC e os atributos assinados; os delegates nascem uma vez
        private sealed class Binding : UIBinding<TooltipModel>
        {
            private readonly AbilityTooltipSource _source;
            private readonly TooltipLineWriter _writer = new TooltipLineWriter();
            private readonly List<AttributeState> _states = new List<AttributeState>(4);
            private readonly Action<AbilitySpec, int, int> _onSpecChanged;
            private readonly Action<AbilitySpec, CooldownChange> _onCooldownChanged;
            private readonly Action<float, float> _onValueChanged;

            private AbilitySystemComponent _asc;
            private GameplayAbility _ability;

            public Binding(AbilityTooltipSource source)
            {
                _source = source;
                _onSpecChanged = (_, _, _) => Refresh();
                _onCooldownChanged = (_, _) => Refresh();
                _onValueChanged = (_, _) => Refresh();
            }

            // Dono que não é ASC (o binder sem dono liga a si mesmo): só o asset, sem assinar nada
            protected override void OnAttach(Object owner)
            {
                _ability = _source.ability != null ? _source.ability : Context.Subject as GameplayAbility;
                if (owner is AbilitySystemComponent asc && _ability != null)
                {
                    _asc = asc;
                    _asc.OnAbilityLevelChanged += _onSpecChanged;
                    _asc.OnCooldownChanged += _onCooldownChanged;
                    _asc.OnChargesChanged += _onSpecChanged;
                    Listen(_ability.CooldownReductionAttribute);
                    ListenCost(_ability.CostEffect);
                }

                Refresh();
            }

            // Solta pelo ReferenceEquals: um ASC destruído ainda guarda os delegates
            protected override void OnDetach()
            {
                if (!ReferenceEquals(_asc, null))
                {
                    _asc.OnAbilityLevelChanged -= _onSpecChanged;
                    _asc.OnCooldownChanged -= _onCooldownChanged;
                    _asc.OnChargesChanged -= _onSpecChanged;
                }

                for (int i = 0; i < _states.Count; i++)
                {
                    _states[i].OnValueChanged -= _onValueChanged;
                }

                _states.Clear();
                _asc = null;
                _ability = null;
            }

            private void Refresh()
            {
                AbilitySystemComponent asc = _asc != null ? _asc : null;
                AbilitySpec spec = asc != null && _ability != null ? asc.FindSpec(_ability) : null;
                int level = spec != null ? spec.Level : _source.levelWithoutOwner;
                Publish(_source.Build(_writer, _ability, level, asc, Context.Text));
            }

            // Atributos de que o valor do custo depende, assinados uma vez por ligação
            private void ListenCost(GameplayEffect cost)
            {
                if (cost == null || cost.Modifiers == null) return;

                foreach (AttributeEffectModifier modifier in cost.Modifiers)
                {
                    ModifierValueSource value = modifier?.ValueSource;
                    if (value == null) continue;

                    if (value.Type == ModifierValueSource.SourceType.Attribute)
                    {
                        Listen(value.Attribute);
                    }
                    else if (value.Type == ModifierValueSource.SourceType.Formula && value.Formula != null)
                    {
                        foreach (AttributeDefinition dependency in value.Formula.GetSourceDependencies())
                        {
                            Listen(dependency);
                        }
                    }
                }
            }

            private void Listen(AttributeDefinition definition)
            {
                if (definition == null || _asc.AttributeSet == null
                    || !_asc.AttributeSet.TryGetState(definition, out AttributeState state) || _states.Contains(state)) return;

                _states.Add(state);
                state.OnValueChanged += _onValueChanged;
            }
        }
    }
}
