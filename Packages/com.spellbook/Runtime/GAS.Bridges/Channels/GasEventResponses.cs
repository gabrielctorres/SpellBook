using System;
using SpellBook.Core.Inspector;
using SpellBook.Events;

namespace SpellBook.GAS.Bridges
{
    /// <summary>Resposta ao <see cref="EffectEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas", Summary = "Roda reações quando um efeito é aplicado, removido, muda de pilhas ou é bloqueado.")]
    public sealed class EffectEventResponse : EventResponse<EffectEventData>
    {
    }

    /// <summary>Resposta ao <see cref="AbilityEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas", Summary = "Roda reações quando uma habilidade é ativada, termina, falha, é concedida ou removida.")]
    public sealed class AbilityEventResponse : EventResponse<AbilityEventData>
    {
    }

    /// <summary>Resposta ao <see cref="AttributeChangedEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas", Summary = "Roda reações quando o valor atual de um atributo muda.")]
    public sealed class AttributeChangedEventResponse : EventResponse<AttributeChangedData>
    {
    }

    /// <summary>Resposta ao <see cref="AttributeLimitEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas", Summary = "Roda reações quando um atributo chega ao mínimo ou ao máximo.")]
    public sealed class AttributeLimitEventResponse : EventResponse<AttributeLimitEventData>
    {
    }

    /// <summary>Resposta ao <see cref="TagChangedEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas", Summary = "Roda reações quando uma tag entra no ASC ou sai dele.")]
    public sealed class TagChangedEventResponse : EventResponse<TagChangedData>
    {
    }

    /// <summary>Resposta ao <see cref="BlockedEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas", Summary = "Roda reações quando um ASC informa um bloqueio, com a tag do motivo.")]
    public sealed class BlockedEventResponse : EventResponse<BlockedEventData>
    {
    }
}
