using System;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Attributes
{
    /// <summary>
    /// Anula a alteração (<c>Delta = 0</c>) quando o efeito tem alguma das <see cref="DamageTags"/>. Só a alteração deste
    /// atributo é anulada: o efeito continua aplicado (tags, outros modificadores). Lista vazia: não anula nada.
    /// </summary>
    [Serializable]
    [TypeCategory("Dano/Imunidade", Summary = "Anula a alteração de efeitos com certas tags de dano.",
        Line = "Imune a {damageTags.tags} tag(s) de dano")]
    [HelpURL(DocUrls.Damage)]
    public sealed class ImmuneToTags : AttributeChangeProcessor
    {
        [Tooltip("Tipos de dano anulados, pela hierarquia. Vazio: não anula nada.")]
        [SerializeField] private GameplayTagContainer damageTags = new GameplayTagContainer();

        /// <summary>Tipos de dano anulados. Nunca nulo.</summary>
        public GameplayTagContainer DamageTags => damageTags ??= new GameplayTagContainer();

        public override void Process(ref AttributeChange change, in EffectContext context, AttributeChangeQueue queue)
        {
            if (DamageTags.Count > 0 && change.HasDamageTag(damageTags)) change.Delta = 0f;
        }
    }
}
