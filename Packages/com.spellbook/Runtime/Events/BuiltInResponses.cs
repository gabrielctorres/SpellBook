using System;
using SpellBook.Core.Inspector;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>Resposta ao <see cref="VoidEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas")]
    public sealed class VoidEventResponse : EventResponse<SpellBook.Core.Void>
    {
    }

    /// <summary>Resposta ao <see cref="IntEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas")]
    public sealed class IntEventResponse : EventResponse<int>
    {
    }

    /// <summary>Resposta ao <see cref="FloatEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas")]
    public sealed class FloatEventResponse : EventResponse<float>
    {
    }

    /// <summary>Resposta ao <see cref="BoolEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas")]
    public sealed class BoolEventResponse : EventResponse<bool>
    {
    }

    /// <summary>Resposta ao <see cref="StringEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas")]
    public sealed class StringEventResponse : EventResponse<string>
    {
    }

    /// <summary>Resposta ao <see cref="Vector3EventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas")]
    public sealed class Vector3EventResponse : EventResponse<Vector3>
    {
    }

    /// <summary>Resposta ao <see cref="GameObjectEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas")]
    public sealed class GameObjectEventResponse : EventResponse<GameObject>
    {
    }
}
