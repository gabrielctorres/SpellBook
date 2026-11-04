using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Validation;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.UI;
using UnityEngine;

namespace SpellBook.GAS.UI
{
    /// <summary>Sentido da variação que uma <see cref="AttributeRule"/> aceita.</summary>
    public enum ChangeSign
    {
        /// <summary>Só variação negativa, como dano.</summary>
        Negative,

        /// <summary>Só variação positiva, como cura.</summary>
        Positive,

        /// <summary>Qualquer variação diferente de zero.</summary>
        Any,
    }

    /// <summary>Regra de número flutuante (R13): atributo, sentido e tags de dano que escolhem um estilo.</summary>
    [Serializable]
    public sealed class AttributeRule
    {
        [Tooltip("Atributo cuja variação gera o número.")]
        [SerializeField] private AttributeDefinition attribute;

        [Tooltip("Sentido da variação aceito.")]
        [SerializeField] private ChangeSign sign;

        [Tooltip("Tags de dano do efeito, pela hierarquia. Vazio: qualquer efeito, e também a alteração sem efeito.")]
        [SerializeField] private GameplayTagContainer damageTags = new GameplayTagContainer();

        [Tooltip("Estilo do número.")]
        [SerializeField] private FloatingTextStyle style;

        /// <summary>Atributo cuja variação gera o número.</summary>
        public AttributeDefinition Attribute => attribute;

        /// <summary>Sentido da variação aceito.</summary>
        public ChangeSign Sign => sign;

        /// <summary>Tags de dano exigidas do efeito; vazio aceita qualquer efeito.</summary>
        public GameplayTagContainer DamageTags => damageTags ??= new GameplayTagContainer();

        /// <summary>Estilo do número.</summary>
        public FloatingTextStyle Style => style;

        /// <summary>
        /// A regra casa com a variação <paramref name="delta"/> de <paramref name="changed"/> vinda de
        /// <paramref name="effect"/> (nulo numa alteração por código). Zero nunca casa. Não aloca.
        /// </summary>
        public bool Matches(AttributeDefinition changed, float delta, GameplayEffect effect)
        {
            if (changed == null || changed != attribute || delta == 0f) return false;
            bool signMatches = sign == ChangeSign.Any || (sign == ChangeSign.Negative ? delta < 0f : delta > 0f);
            if (!signMatches) return false;
            return DamageTags.Count == 0 || (effect != null && effect.DamageTags.HasAny(damageTags));
        }
    }

    /// <summary>
    /// Regras dos números flutuantes do GAS (R13): a primeira <see cref="AttributeRule"/> que casa escolhe o estilo, e
    /// <see cref="Blocked"/> é o estilo do efeito bloqueado. A <see cref="FloatingTextSource"/> lê as regras a cada
    /// evento, então mudar o asset muda o número seguinte.
    /// </summary>
    [CreateAssetMenu(menuName = "SpellBook/GAS/UI/Floating Text Rules", fileName = "FloatingTextRules")]
    [HelpURL(DocUrls.UiFloatingText)]
    public sealed class FloatingTextRules : ScriptableObject, IValidatable
    {
        [Tooltip("Regras na ordem de busca; a primeira que casa vence.")]
        [SerializeField] private List<AttributeRule> rules = new List<AttributeRule>();

        [Tooltip("Estilo do efeito bloqueado (imunidade, variação anulada ou bloqueio informado). Vazio: sem número.")]
        [SerializeField] private FloatingTextStyle blocked;

        /// <summary>Regras na ordem de busca.</summary>
        public IReadOnlyList<AttributeRule> Rules => rules;

        /// <summary>Estilo do efeito bloqueado, ou nulo.</summary>
        public FloatingTextStyle Blocked => blocked;

        /// <summary>A primeira regra que casa, ou nulo. Não aloca.</summary>
        public AttributeRule Find(AttributeDefinition attribute, float delta, GameplayEffect effect)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] != null && rules[i].Matches(attribute, delta, effect)) return rules[i];
            }

            return null;
        }

        /// <summary>Regra sem estilo: erro no campo dela.</summary>
        public void Validate(ValidationResult result)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] != null && rules[i].Style == null)
                {
                    result.AddError($"Regra sem estilo em rules (posição {i}). Escolha o estilo do número.",
                        $"{nameof(rules)}.Array.data[{i}].style");
                }
            }
        }
    }
}
