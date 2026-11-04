using System;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
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
}
