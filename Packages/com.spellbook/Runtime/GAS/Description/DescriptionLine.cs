using System;
using SpellBook.Core;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;
using SpellBook.Tags;

namespace SpellBook.GAS.Description
{
    /// <summary>O que uma <see cref="DescriptionLine"/> descreve.</summary>
    public enum DescriptionLineKind
    {
        Text,
        Attribute,
        Modifier,
        Cost,
        Cooldown,
        Duration,
        Charges,
        Failure
    }

    /// <summary>Marcas de uma <see cref="DescriptionLine"/>.</summary>
    [Flags]
    public enum DescriptionLineFlags
    {
        None = 0,

        /// <summary>O valor depende de nível, de valor do chamador, de atributo ou de fórmula.</summary>
        Variable = 1,

        /// <summary>A linha não pôde ser montada por inteiro (por exemplo, nome fora da tabela).</summary>
        Invalid = 2,

        /// <summary>Sem fim (duração infinita).</summary>
        Permanent = 4
    }

    /// <summary>
    /// Uma linha de descrição: rótulo por chave, valor numérico, operação, papel de cor e marcas, sem cor nem marcação
    /// (ADR 0023, item 2). O texto do valor é escrito sob pedido pelo <see cref="DescriptionWriter"/>.
    /// </summary>
    public readonly struct DescriptionLine
    {
        /// <summary>Linha sem operação; use <see cref="ForModifier"/> para a linha de um modificador.</summary>
        public DescriptionLine(
            DescriptionLineKind kind,
            LocalizedText label,
            float value,
            TextRole role,
            DescriptionLineFlags flags = DescriptionLineFlags.None,
            AttributeDefinition attribute = null,
            float secondaryValue = 0f,
            GameplayTag tag = default)
            : this(kind, label, value, secondaryValue, default, false, role, attribute, tag, flags)
        {
        }

        private DescriptionLine(
            DescriptionLineKind kind,
            LocalizedText label,
            float value,
            float secondaryValue,
            ModifierOperation operation,
            bool hasOperation,
            TextRole role,
            AttributeDefinition attribute,
            GameplayTag tag,
            DescriptionLineFlags flags)
        {
            Kind = kind;
            Label = label;
            Value = value;
            SecondaryValue = secondaryValue;
            Operation = operation;
            HasOperation = hasOperation;
            Role = role;
            Attribute = attribute;
            Tag = tag;
            Flags = flags;
        }

        public DescriptionLineKind Kind { get; }

        /// <summary>Rótulo por chave, com o texto padrão.</summary>
        public LocalizedText Label { get; }

        /// <summary>Valor numérico, já com pilhas e nível.</summary>
        public float Value { get; }

        /// <summary>Segundo valor (cargas: intervalo de recarga); 0 quando não há.</summary>
        public float SecondaryValue { get; }

        /// <summary>Operação do modificador; só vale com <see cref="HasOperation"/>.</summary>
        public ModifierOperation Operation { get; }

        public bool HasOperation { get; }

        /// <summary>Papel de cor; a view escolhe a cor (<see cref="TextRoleTheme"/>).</summary>
        public TextRole Role { get; }

        /// <summary>Atributo descrito; pode ser nulo.</summary>
        public AttributeDefinition Attribute { get; }

        /// <summary>Tag do motivo de falha; vazia nas outras linhas.</summary>
        public GameplayTag Tag { get; }

        public DescriptionLineFlags Flags { get; }

        /// <summary>Linha de um modificador de <paramref name="attribute"/> com a operação.</summary>
        public static DescriptionLine ForModifier(
            DescriptionLineKind kind,
            AttributeDefinition attribute,
            LocalizedText label,
            float value,
            ModifierOperation operation,
            TextRole role,
            DescriptionLineFlags flags = DescriptionLineFlags.None)
        {
            return new DescriptionLine(kind, label, value, 0f, operation, true, role, attribute, default, flags);
        }
    }
}
