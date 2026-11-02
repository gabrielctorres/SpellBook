using System;
using SpellBook.Core.Inspector;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>Resposta ao <see cref="VoidEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas", Summary = "Roda reações quando um canal sem payload dispara.")]
    public sealed class VoidEventResponse : EventResponse<SpellBook.Core.Void>
    {
    }

    /// <summary>Resposta ao <see cref="IntEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas", Summary = "Roda reações quando um canal de int dispara.")]
    public sealed class IntEventResponse : EventResponse<int>
    {
    }

    /// <summary>Resposta ao <see cref="FloatEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas", Summary = "Roda reações quando um canal de float dispara.")]
    public sealed class FloatEventResponse : EventResponse<float>
    {
    }

    /// <summary>Resposta ao <see cref="BoolEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas", Summary = "Roda reações quando um canal de bool dispara.")]
    public sealed class BoolEventResponse : EventResponse<bool>
    {
    }

    /// <summary>Resposta ao <see cref="StringEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas", Summary = "Roda reações quando um canal de string dispara.")]
    public sealed class StringEventResponse : EventResponse<string>
    {
    }

    /// <summary>Resposta ao <see cref="Vector3EventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas", Summary = "Roda reações quando um canal de Vector3 dispara.")]
    public sealed class Vector3EventResponse : EventResponse<Vector3>
    {
    }

    /// <summary>Resposta ao <see cref="GameObjectEventChannel"/>.</summary>
    [Serializable, TypeCategory("Respostas", Summary = "Roda reações quando um canal de GameObject dispara.")]
    public sealed class GameObjectEventResponse : EventResponse<GameObject>
    {
    }
}
