using System;
using UnityEngine;

namespace SpellBook.Core.Inspector
{
    /// <summary>
    /// Marca um campo <c>[SerializeReference]</c>, simples ou lista, para receber o seletor de tipo no Inspector (ADR 0026, item 4).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TypePickerAttribute : PropertyAttribute
    {
    }
}
