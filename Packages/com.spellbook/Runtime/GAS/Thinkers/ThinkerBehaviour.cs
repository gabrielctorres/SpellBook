using System;
using SpellBook.Core.Validation;
using SpellBook.GAS.Actions;

namespace SpellBook.GAS.Thinkers
{
    /// <summary>
    /// Comportamento de um <see cref="GameplayThinker"/>, como a aura. É configuração compartilhada (ADR 0013): o estado
    /// de cada thinker fica no objeto de <see cref="CreateState"/>, que o thinker cria uma vez e passa a cada chamada.
    /// </summary>
    [Serializable]
    public abstract class ThinkerBehaviour
    {
        /// <summary>Estado de um thinker, criado uma vez no <c>Awake</c> dele e reutilizado a cada volta do pool. O padrão é nulo.</summary>
        public virtual object CreateState() => null;

        /// <summary>Chamado no começo da vida, antes da lista "Ao criar".</summary>
        public virtual void OnBegin(object state, GameplayThinker thinker, ActionContext context) { }

        /// <summary>Chamado a cada pulso, antes da lista "A cada pulso".</summary>
        public virtual void OnPulse(object state, GameplayThinker thinker, ActionContext context) { }

        /// <summary>Chamado no término, depois da lista "Ao terminar". Deve limpar o estado para a próxima vida.</summary>
        public virtual void OnEnd(object state, GameplayThinker thinker, ActionContext context) { }

        /// <summary>Acusa a configuração inválida. O padrão não acusa nada.</summary>
        public virtual void Validate(ValidationResult result) { }
    }
}
