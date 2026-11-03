using System;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Targeting;
using UnityEngine;

namespace SpellBook.GAS.Abilities.Triggers
{
    /// <summary>
    /// Tenta ativar a habilidade a cada <c>Tick</c> do ASC enquanto ela está inativa, em modo silencioso: a falha não
    /// dispara <c>OnAbilityFailed</c>. Marcado "só com autocast", tenta só com o autocast ligado.
    /// </summary>
    [Serializable, TypeCategory("Autocast", Summary = "Ativa a habilidade sozinha quando ela fica pronta.")]
    public sealed class WhenReadyTrigger : AbilityTrigger
    {
        [Tooltip("Escolhe o alvo da tentativa a partir de quem lança. Vazio: sem alvo. Sem resultado: a tentativa é pulada.")]
        [SerializeReference, TypePicker] private TargetSelector selector;

        /// <summary>
        /// Seletor do alvo da tentativa, avaliado a partir de quem lança; a ativação usa a primeira unidade. Nulo tenta sem
        /// alvo; sem resultado, a tentativa é pulada, sem custo e sem evento.
        /// </summary>
        public TargetSelector Selector
        {
            get => selector;
            set => selector = value;
        }
    }
}
