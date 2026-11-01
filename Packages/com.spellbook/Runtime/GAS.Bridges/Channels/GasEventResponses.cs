using System;
using SpellBook.Core.Inspector;
using SpellBook.Events;

namespace SpellBook.GAS.Bridges
{
    /// <summary>Resposta ao <see cref="EffectEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas")]
    public sealed class EffectEventResponse : EventResponse<EffectEventData>
    {
    }

    /// <summary>Resposta ao <see cref="AbilityEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas")]
    public sealed class AbilityEventResponse : EventResponse<AbilityEventData>
    {
    }

    /// <summary>Resposta ao <see cref="AttributeChangedEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas")]
    public sealed class AttributeChangedEventResponse : EventResponse<AttributeChangedData>
    {
    }

    /// <summary>Resposta ao <see cref="TagChangedEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas")]
    public sealed class TagChangedEventResponse : EventResponse<TagChangedData>
    {
    }

    /// <summary>Resposta ao <see cref="BlockedEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas")]
    public sealed class BlockedEventResponse : EventResponse<BlockedEventData>
    {
    }
}
