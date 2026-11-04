using System.Collections.Generic;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.GAS.Description;
using SpellBook.UI;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Converte as <see cref="DescriptionLine"/> do B-008 em <see cref="TooltipLine"/> (Spec UI-3, Design 4). Guarda o
    /// contexto de descrição, a lista de linhas e o buffer dos valores de uma ligação, reaproveitados a cada montagem:
    /// montar de novo não aloca. O rótulo vem do provedor e o valor do <see cref="DescriptionWriter"/>, sem marcação.
    /// </summary>
    internal sealed class TooltipLineWriter
    {
        private readonly List<TooltipLine> _lines = new List<TooltipLine>(8);
        private readonly TextBuffer _values = new TextBuffer(128);

        /// <summary>O contexto montado pelo <see cref="Begin"/>; quem descreve acrescenta as linhas nele.</summary>
        public DescriptionContext Context { get; } = new DescriptionContext();

        /// <summary>As linhas convertidas pelo último <see cref="Convert"/>.</summary>
        public IReadOnlyList<TooltipLine> Lines => _lines;

        /// <summary>Limpa as linhas e os valores e monta o contexto de descrição.</summary>
        public void Begin(GameplayAbility ability, int level, AbilitySystemComponent source, ITextProvider text,
            ModifierTextFormatter formatter)
        {
            Context.Reset(ability, level, source);
            Context.Text = text;
            Context.Formatter = formatter;
            _lines.Clear();
            _values.Clear();
        }

        /// <summary>Verdadeiro se o contexto já tem uma linha do tipo <paramref name="kind"/>.</summary>
        public bool Has(DescriptionLineKind kind)
        {
            List<DescriptionLine> lines = Context.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Kind == kind) return true;
            }

            return false;
        }

        /// <summary>Converte as linhas do contexto, na ordem, para <see cref="Lines"/>.</summary>
        public void Convert()
        {
            List<DescriptionLine> lines = Context.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                _lines.Add(ToTooltipLine(lines[i]));
            }
        }

        // Template e rótulo com {0}/{1} (cargas) saem inteiros no valor, com o rótulo vazio
        private TooltipLine ToTooltipLine(in DescriptionLine line)
        {
            string label = line.Label.Resolve(Context.Text) ?? string.Empty;
            int start = _values.Length;
            bool whole = line.Source is TemplateElement || label.IndexOf('{') >= 0;
            if (whole)
            {
                DescriptionWriter.WriteLine(in line, Context, _values);
                label = string.Empty;
            }
            else
            {
                DescriptionWriter.WriteValue(in line, Context, _values);
            }

            return new TooltipLine(label, _values.SpanFrom(start), line.Role, KindOf(line.Kind), line.Attribute);
        }

        private static TooltipLineKind KindOf(DescriptionLineKind kind)
        {
            switch (kind)
            {
                case DescriptionLineKind.Attribute: return TooltipLineKind.Value;
                case DescriptionLineKind.Modifier: return TooltipLineKind.Modifier;
                case DescriptionLineKind.Cost: return TooltipLineKind.Cost;
                case DescriptionLineKind.Cooldown: return TooltipLineKind.Cooldown;
                case DescriptionLineKind.Duration: return TooltipLineKind.Duration;
                case DescriptionLineKind.Charges: return TooltipLineKind.Charges;
                default: return TooltipLineKind.Description;
            }
        }
    }
}
