using System;
using UnityEngine;

namespace SpellBook.Core.Inspector
{
    /// <summary>
    /// Mostra o campo no Inspector só quando o campo irmão serializado <see cref="SiblingField"/>, <c>bool</c> ou <c>enum</c>,
    /// é igual a <see cref="Value"/> (ADR 0026, item 5). Não aceita expressão, propriedade nem método. Em lista, vale por
    /// elemento, não para a lista inteira. Não combina com outro atributo de drawer no mesmo campo, como <c>[TypePicker]</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ShowIfAttribute : PropertyAttribute
    {
        public ShowIfAttribute(string siblingField, object value)
        {
            SiblingField = siblingField;
            Value = value;
        }

        /// <summary>Nome do campo irmão, no mesmo objeto ou struct, que decide a visibilidade.</summary>
        public string SiblingField { get; }

        /// <summary>Valor do irmão que mostra o campo: um <c>bool</c> ou um valor do <c>enum</c> do irmão.</summary>
        public object Value { get; }
    }
}
