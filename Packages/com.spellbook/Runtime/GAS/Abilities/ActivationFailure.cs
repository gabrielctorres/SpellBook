using SpellBook.Tags;

namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Resultado detalhado de uma checagem de ativação: o <see cref="AbilityActivationResult"/> e o motivo. Nas falhas de
    /// tag, <see cref="ReasonTag"/> é a tag que decidiu; nas de regra, a tag e a chave da <see cref="ActivationRule"/>. Nas
    /// demais (recarga, custo, cargas), não há tag nem chave: a UI mapeia o <see cref="Result"/>.
    /// </summary>
    public readonly struct ActivationFailure
    {
        // Guarda o índice + 1 para que o default da struct tenha ConditionIndex -1
        private readonly int _conditionSlot;

        internal ActivationFailure(AbilityActivationResult result, GameplayTag reasonTag = default, string reasonKey = null,
            int conditionIndex = -1, ActivationRule rule = null)
        {
            Result = result;
            ReasonTag = reasonTag;
            ReasonKey = reasonKey;
            _conditionSlot = conditionIndex + 1;
            Rule = rule;
        }

        /// <summary>Resultado da checagem.</summary>
        public AbilityActivationResult Result { get; }

        /// <summary>
        /// Tag do motivo: a bloqueadora que o ASC tem, a exigida que falta, a <c>AbilityTag</c> bloqueada por uma habilidade
        /// ativa, ou a tag da regra que falhou. Inválida quando não há.
        /// </summary>
        public GameplayTag ReasonTag { get; }

        /// <summary>Chave de texto do motivo (só nas falhas de regra); nula quando não há.</summary>
        public string ReasonKey { get; }

        /// <summary>Índice da regra que falhou em <see cref="GameplayAbility.ActivationConditions"/>; -1 fora de regra.</summary>
        public int ConditionIndex => _conditionSlot - 1;

        /// <summary>Regra que falhou; nula fora de regra.</summary>
        public ActivationRule Rule { get; }

        /// <summary>A checagem passou.</summary>
        public bool IsSuccess => Result == AbilityActivationResult.Success;
    }
}
