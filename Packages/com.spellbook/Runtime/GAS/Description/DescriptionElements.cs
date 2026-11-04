using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace SpellBook.GAS.Description
{
    /// <summary>Uma linha de texto, com chave opcional para o provedor.</summary>
    [Serializable, MovedFrom(false, sourceNamespace: "SpellBook.GAS.Abilities")]
    [TypeCategory("Descrição/Texto", Summary = "Uma linha de texto, com chave opcional.", Line = "{text.text}")]
    public sealed class TextElement : DescriptionElement
    {
        [SerializeField] private LocalizedText text;

        public LocalizedText Text
        {
            get => text;
            set => text = value;
        }

        public override void Describe(DescriptionContext context)
        {
            context.Lines.Add(new DescriptionLine(DescriptionLineKind.Text, text, 0f, TextRole.None, source: this));
        }
    }

    /// <summary>
    /// Valor atual de um atributo no ASC de origem. Sem ASC, ou sem o atributo nele, o valor é 0 e a linha sai como
    /// variável.
    /// </summary>
    [Serializable, MovedFrom(false, sourceNamespace: "SpellBook.GAS.Abilities")]
    [TypeCategory("Descrição/Atributo", Summary = "Valor atual de um atributo de quem lança.", Line = "{attribute}")]
    public sealed class AttributeElement : DescriptionElement
    {
        [FormerlySerializedAs("Attribute")]
        [SerializeField] private AttributeDefinition attribute;

        public AttributeDefinition Attribute
        {
            get => attribute;
            set => attribute = value;
        }

        public override void Describe(DescriptionContext context)
        {
            if (attribute == null) return;

            float value = 0f;
            bool found = context.Source != null && context.Source.TryGetAttributeValue(attribute, out value);
            context.Lines.Add(new DescriptionLine(
                DescriptionLineKind.Attribute,
                attribute.DisplayName,
                value,
                TextRole.Value,
                found ? DescriptionLineFlags.None : DescriptionLineFlags.Variable,
                attribute,
                source: this));
        }
    }

    /// <summary>Uma linha por modificador de um efeito, pelo <see cref="EffectDescriber"/>.</summary>
    [Serializable]
    [TypeCategory("Descrição/Modificadores do efeito", Summary = "Uma linha por modificador de um efeito.", Line = "{effect}")]
    public sealed class EffectModifiersElement : DescriptionElement
    {
        [SerializeField] private GameplayEffect effect;

        public GameplayEffect Effect
        {
            get => effect;
            set => effect = value;
        }

        /// <summary>Descreve o efeito com <c>context.Effect</c> apontando para ele durante a chamada.</summary>
        public override void Describe(DescriptionContext context)
        {
            GameplayEffect previous = context.Effect;
            context.Effect = effect;
            try
            {
                EffectDescriber.Describe(effect, context);
            }
            finally
            {
                context.Effect = previous;
            }
        }
    }

    /// <summary>
    /// Texto com <c>{nome}</c> lido da tabela de valores da habilidade no nível do contexto, e com os atalhos
    /// <c>{@cooldown}</c>, <c>{@cost}</c> e <c>{@charges}</c>, que usam a conta dos elementos de recarga, custo e cargas.
    /// Gera uma linha <see cref="DescriptionLineKind.Text"/>; o <see cref="DescriptionWriter"/> a expande. Nome fora da
    /// tabela ou atalho sem dado marca a linha como <see cref="DescriptionLineFlags.Invalid"/> e o trecho fica literal.
    /// Chaves com outro conteúdo são texto.
    /// </summary>
    [Serializable, MovedFrom(false, sourceNamespace: "SpellBook.GAS.Abilities")]
    [TypeCategory("Descrição/Template", Summary = "Texto com valores da tabela por nível e atalhos de recarga, custo e cargas.",
        Line = "{template.text}")]
    public sealed class TemplateElement : DescriptionElement
    {
        private enum PartKind : byte
        {
            Literal,
            Name,
            Cooldown,
            Cost,
            Charges,
            UnknownShortcut
        }

        /// <summary>Trecho do template; fora do literal, inclui as chaves.</summary>
        private readonly struct Part
        {
            public Part(PartKind kind, int start, int length)
            {
                Kind = kind;
                Start = start;
                Length = length;
            }

            public PartKind Kind { get; }
            public int Start { get; }
            public int Length { get; }
        }

        [SerializeField] private LocalizedText template;

        // Trechos do último texto analisado; refeitos quando o texto resolvido muda, na mesma lista.
        [NonSerialized] private string _parsedText;
        [NonSerialized] private List<Part> _parts;

        public LocalizedText Template
        {
            get => template;
            set => template = value;
        }

        /// <summary>Acrescenta a linha do template, com as marcas de valor variável e de nome ou atalho inválido.</summary>
        public override void Describe(DescriptionContext context)
        {
            string text = template.Resolve(context.Text);
            List<Part> parts = PartsOf(text);
            DescriptionLineFlags flags = DescriptionLineFlags.None;
            for (int i = 0; i < parts.Count; i++)
            {
                flags |= FlagsOf(parts[i], text, context);
            }

            context.Lines.Add(new DescriptionLine(DescriptionLineKind.Text, template, 0f, TextRole.None, flags, source: this));
        }

        /// <summary>Escreve <paramref name="text"/> (o template resolvido) com os nomes e atalhos expandidos. Não aloca.</summary>
        internal void Write(string text, DescriptionContext context, TextBuffer into)
        {
            List<Part> parts = PartsOf(text);
            for (int i = 0; i < parts.Count; i++)
            {
                Part part = parts[i];
                if (!TryWritePart(part, text, context, into)) into.Append(text, part.Start, part.Length);
            }
        }

        private bool TryWritePart(in Part part, string text, DescriptionContext context, TextBuffer into)
        {
            DescriptionLine line;
            switch (part.Kind)
            {
                case PartKind.Name:
                    AbilityValues values = context.Ability != null ? context.Ability.Values : null;
                    int index = values != null ? values.IndexOf(NameOf(part, text)) : -1;
                    if (index < 0) return false;
                    WriteNumber(values.Evaluate(index, context.Level), context, into);
                    return true;
                case PartKind.Cooldown:
                    if (!CooldownElement.TryLine(context, this, out line)) return false;
                    DescriptionWriter.WriteValue(line, context, into);
                    return true;
                case PartKind.Charges:
                    if (!ChargesElement.TryLine(context, this, out line)) return false;
                    DescriptionWriter.WriteValue(line, context, into);
                    return true;
                case PartKind.Cost:
                    List<AttributeEffectModifier> modifiers = CostElement.ModifiersOf(context);
                    if (modifiers == null) return false;
                    EffectContext evaluation = context.EvaluationContext;
                    bool any = false;
                    for (int i = 0; i < modifiers.Count; i++)
                    {
                        if (!CostElement.TryLine(modifiers[i], in evaluation, this, out line)) continue;
                        if (any) into.Append(", ");
                        DescriptionWriter.WriteValue(line, context, into);
                        any = true;
                    }
                    return any;
                default:
                    return false;
            }
        }

        private DescriptionLineFlags FlagsOf(in Part part, string text, DescriptionContext context)
        {
            DescriptionLine line;
            switch (part.Kind)
            {
                case PartKind.Literal:
                    return DescriptionLineFlags.None;
                case PartKind.Name:
                    AbilityValues values = context.Ability != null ? context.Ability.Values : null;
                    int index = values != null ? values.IndexOf(NameOf(part, text)) : -1;
                    if (index < 0) return DescriptionLineFlags.Invalid;
                    ScalableFloat value = values.GetEntry(index)?.Value;
                    return value != null ? CooldownElement.FlagsOf(value) : DescriptionLineFlags.None;
                case PartKind.Cooldown:
                    return CooldownElement.TryLine(context, this, out line) ? line.Flags : DescriptionLineFlags.Invalid;
                case PartKind.Charges:
                    return ChargesElement.TryLine(context, this, out line) ? line.Flags : DescriptionLineFlags.Invalid;
                case PartKind.Cost:
                    List<AttributeEffectModifier> modifiers = CostElement.ModifiersOf(context);
                    if (modifiers == null) return DescriptionLineFlags.Invalid;
                    EffectContext evaluation = context.EvaluationContext;
                    DescriptionLineFlags flags = DescriptionLineFlags.Invalid;
                    for (int i = 0; i < modifiers.Count; i++)
                    {
                        if (CostElement.TryLine(modifiers[i], in evaluation, this, out line))
                        {
                            flags = (flags & ~DescriptionLineFlags.Invalid) | line.Flags;
                        }
                    }
                    return flags;
                default:
                    return DescriptionLineFlags.Invalid;
            }
        }

        // Sem casas quando o valor, em duas casas, é inteiro; senão, as casas necessárias até duas.
        private static void WriteNumber(float value, DescriptionContext context, TextBuffer into)
        {
            double hundredths = Math.Round(value, 2);
            string format = hundredths == Math.Floor(hundredths) ? "F0" : Math.Round(value, 1) == hundredths ? "F1" : "F2";
            into.Append((float)hundredths, format, context.Culture);
        }

        private static ReadOnlySpan<char> NameOf(in Part part, string text) => text.AsSpan(part.Start + 1, part.Length - 2);

        private List<Part> PartsOf(string text)
        {
            text ??= string.Empty;
            if (_parts != null && string.Equals(text, _parsedText, StringComparison.Ordinal)) return _parts;

            _parts ??= new List<Part>();
            _parts.Clear();
            _parsedText = text;
            int literal = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '{') continue;

                int close = text.IndexOf('}', i + 1);
                if (close < 0) break;

                PartKind kind = KindOf(text.AsSpan(i + 1, close - i - 1));
                if (kind == PartKind.Literal) continue;

                if (i > literal) _parts.Add(new Part(PartKind.Literal, literal, i - literal));
                _parts.Add(new Part(kind, i, close - i + 1));
                literal = close + 1;
                i = close;
            }

            if (text.Length > literal) _parts.Add(new Part(PartKind.Literal, literal, text.Length - literal));
            return _parts;
        }

        private static PartKind KindOf(ReadOnlySpan<char> inner)
        {
            if (inner.Length < 2 || inner[0] != '@') return AbilityValues.IsIdentifier(inner) ? PartKind.Name : PartKind.Literal;

            ReadOnlySpan<char> shortcut = inner.Slice(1);
            if (!AbilityValues.IsIdentifier(shortcut)) return PartKind.Literal;
            if (shortcut.SequenceEqual("cooldown".AsSpan())) return PartKind.Cooldown;
            if (shortcut.SequenceEqual("cost".AsSpan())) return PartKind.Cost;
            if (shortcut.SequenceEqual("charges".AsSpan())) return PartKind.Charges;
            return PartKind.UnknownShortcut;
        }
    }
}
