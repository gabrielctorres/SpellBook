using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.GAS.Attributes
{
    public enum AttributeType
    {
        Primary, // Has base value and modifiers
        Derived  // Calculated entirely from a formula
    }

    /// <summary>De onde vem um limite do atributo: a constante da definição ou o valor atual de outro atributo.</summary>
    public enum LimitSource
    {
        Constant,
        Attribute
    }

    /// <summary>O que acontece com o valor base quando o máximo vindo de outro atributo muda.</summary>
    public enum MaxChangePolicy
    {
        KeepValue, // A base fica, limitada ao novo máximo
        KeepRatio, // A base mantém a proporção entre o mínimo e o máximo
        AddDelta   // A base soma a diferença do máximo
    }

    [CreateAssetMenu(fileName = "NewAttribute", menuName = "GAS/Attribute Definition")]
    [HelpURL(DocUrls.Gas + "#attributedefinition")]
    public class AttributeDefinition : ScriptableObject, IValidatable
    {
        public Sprite Icon;

        public string DisplayName;

        public AttributeType Type = AttributeType.Primary;

        [ShowIf(nameof(Type), AttributeType.Derived)]
        [Tooltip("Obrigatório quando Type = Derived")]
        public Formula Formula;

        [Header("Limits")]
        public LimitSource MinSource = LimitSource.Constant;

        [ShowIf(nameof(MinSource), LimitSource.Attribute)]
        [Tooltip("Atributo do mesmo ASC cujo valor atual é o mínimo")]
        public AttributeDefinition MinAttribute;

        [Tooltip("Mínimo constante; com MinSource = Attribute, é o valor reserva quando a fonte falta ou fecha um ciclo")]
        public float MinValue = 0f;

        public LimitSource MaxSource = LimitSource.Constant;

        [ShowIf(nameof(MaxSource), LimitSource.Attribute)]
        [Tooltip("Atributo do mesmo ASC cujo valor atual é o máximo")]
        public AttributeDefinition MaxAttribute;

        [Tooltip("Máximo constante; com MaxSource = Attribute, é o valor reserva quando a fonte falta ou fecha um ciclo")]
        public float MaxValue = 9999f;

        [ShowIf(nameof(MaxSource), LimitSource.Attribute)]
        [Tooltip("O que acontece com o valor base quando o máximo muda")]
        public MaxChangePolicy OnMaxChange = MaxChangePolicy.KeepValue;

        [Header("Display")]
        [Tooltip("Como o valor aparece na interface: tipo, casas, prefixo, sufixo, direção boa, ordem, visibilidade e categoria")]
        public AttributeDisplay Display;

        [Header("Damage")]
        [SerializeReference, TypePicker]
        [Tooltip("Processadores que mudam cada alteração da base por efeito instantâneo, tique ou código, na ordem da lista. Modificadores persistentes não passam por eles.")]
        private List<AttributeChangeProcessor> processors = new List<AttributeChangeProcessor>();

        [SerializeField]
        [Tooltip("Meta-atributo: a alteração passa pelos processadores, que a convertem em alterações de outros atributos, e a variação final é descartada. A base continua 0 e nenhum evento de valor dispara.")]
        private bool isMeta;

        /// <summary>A categoria do atributo, de <see cref="AttributeDisplay.Category"/> (R18).</summary>
        public AttributeCategory Category => Display.Category;

        /// <summary>Pipeline de pré-alteração, na ordem de execução (ADR 0033). Entrada nula é pulada.</summary>
        public IReadOnlyList<AttributeChangeProcessor> Processors => processors;

        /// <summary>Meta-atributo (ADR 0033, item 6): só os processadores agem; a variação final nunca chega à base.</summary>
        public bool IsMeta => isMeta;

        /// <summary>
        /// Erros: mínimo constante acima do máximo constante, fonte de limite sem atributo, apontando para o próprio
        /// atributo, fechando um ciclo de fontes ou derivada de uma fórmula que lê este atributo, e <c>Decimals</c>
        /// negativo. Aviso: <c>Hidden</c> sem categoria (R26).
        /// </summary>
        public void Validate(ValidationResult result)
        {
            if (MinSource == LimitSource.Constant && MaxSource == LimitSource.Constant && MinValue > MaxValue)
            {
                result.AddError($"Min Value ({MinValue}) acima de Max Value ({MaxValue}): vale o mínimo.", nameof(MinValue));
            }

            ValidateSource(result, MinSource, MinAttribute, nameof(MinAttribute));
            ValidateSource(result, MaxSource, MaxAttribute, nameof(MaxAttribute));

            if (Display.Decimals < 0)
            {
                result.AddError($"Decimals negativo ({Display.Decimals}).", nameof(Display) + "." + nameof(AttributeDisplay.Decimals));
            }

            if (Display.Hidden && Display.Category == null)
            {
                result.AddWarning("Atributo oculto sem categoria.", nameof(Display) + "." + nameof(AttributeDisplay.Category));
            }
        }

        private void ValidateSource(ValidationResult result, LimitSource source, AttributeDefinition attribute, string path)
        {
            if (source != LimitSource.Attribute)
            {
                return;
            }

            if (attribute == null)
            {
                result.AddError("Fonte de limite Attribute sem atributo.", path);
            }
            else if (attribute == this)
            {
                result.AddError("A fonte de limite é o próprio atributo.", path);
            }
            else if (ReachesThis(attribute, new HashSet<AttributeDefinition>()))
            {
                result.AddError($"Ciclo de fontes de limite: {name} → {attribute.name} → ... → {name}.", path);
            }
            else if (DerivesFromThis(attribute))
            {
                result.AddError($"A fonte de limite {attribute.name} é derivada de uma fórmula que lê {name}.", path);
            }
        }

        private bool ReachesThis(AttributeDefinition node, HashSet<AttributeDefinition> visited)
        {
            if (node == null || !visited.Add(node))
            {
                return false;
            }

            return (node.MinSource == LimitSource.Attribute && (node.MinAttribute == this || ReachesThis(node.MinAttribute, visited)))
                || (node.MaxSource == LimitSource.Attribute && (node.MaxAttribute == this || ReachesThis(node.MaxAttribute, visited)));
        }

        private bool DerivesFromThis(AttributeDefinition source)
        {
            if (source.Type != AttributeType.Derived || source.Formula == null)
            {
                return false;
            }

            foreach (AttributeDefinition dependency in source.Formula.GetSourceDependencies())
            {
                if (dependency == this)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

