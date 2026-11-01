using System;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Conditions;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Regra de ativação de uma <see cref="GameplayAbility"/> (ADR 0032, item 4): uma condição e o motivo informado quando
    /// ela falha. A condição continua pura; o motivo (tag e chave de texto) fica aqui.
    /// </summary>
    [Serializable]
    public sealed class ActivationRule
    {
        [SerializeReference]
        [TypePicker]
        private Condition<ConditionContext> condition;

        [Tooltip("Tag do motivo da falha, para a UI e a lógica de jogo. Opcional.")]
        [SerializeField] private GameplayTag failureTag;

        [Tooltip("Chave de texto do motivo da falha. Vazia: a chave padrão da condição.")]
        [SerializeField] private string failureKey;

        public ActivationRule()
        {
        }

        public ActivationRule(Condition<ConditionContext> condition, GameplayTag failureTag = default, string failureKey = null)
        {
            this.condition = condition;
            this.failureTag = failureTag;
            this.failureKey = failureKey;
        }

        /// <summary>Condição avaliada; nula, a regra é pulada.</summary>
        public Condition<ConditionContext> Condition
        {
            get => condition;
            set => condition = value;
        }

        /// <summary>Tag do motivo da falha; inválida se a regra não define uma.</summary>
        public GameplayTag FailureTag
        {
            get => failureTag;
            set => failureTag = value;
        }

        /// <summary>Chave de texto configurada na regra; pode ser vazia.</summary>
        public string FailureKey
        {
            get => failureKey;
            set => failureKey = value;
        }

        /// <summary>Chave efetiva do motivo: a da regra ou, vazia, a <see cref="Condition{TContext}.DefaultFailureKey"/>.</summary>
        public string EffectiveFailureKey => string.IsNullOrEmpty(failureKey) ? condition?.DefaultFailureKey : failureKey;
    }
}
