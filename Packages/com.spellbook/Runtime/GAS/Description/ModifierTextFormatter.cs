using System;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;

namespace SpellBook.GAS.Description
{
    /// <summary>
    /// Escreve o texto do valor de uma linha. Ponto de extensão do projeto: ordem, unidade, "por segundo" (ADR 0013).
    /// Não escreva marcação; a cor é da view, pelo papel da linha.
    /// </summary>
    [Serializable]
    public abstract class ModifierTextFormatter
    {
        public abstract void WriteValue(in DescriptionLine line, TextBuffer into, IFormatProvider culture);
    }

    /// <summary>
    /// Formato do atributo (<see cref="AttributeDisplay.Format(TextBuffer, float, ModifierOperation, IFormatProvider)"/>):
    /// com operação, o texto do modificador; sem operação, o valor. Sem atributo, o número sem casas.
    /// </summary>
    [Serializable, TypeCategory("Descrição", Summary = "Formato do atributo: texto do modificador com a operação, ou o valor.")]
    public sealed class DefaultModifierTextFormatter : ModifierTextFormatter
    {
        internal static readonly DefaultModifierTextFormatter Instance = new DefaultModifierTextFormatter();

        public override void WriteValue(in DescriptionLine line, TextBuffer into, IFormatProvider culture)
        {
            AttributeDisplay display = line.Attribute != null ? line.Attribute.Display : default;
            if (line.HasOperation) display.Format(into, line.Value, line.Operation, culture);
            else display.Format(into, line.Value, culture);
        }
    }
}
