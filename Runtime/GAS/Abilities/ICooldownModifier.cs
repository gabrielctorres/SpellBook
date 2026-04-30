using UnityEngine;
using SpellBook.GAS.Attributes;

namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Interface para habilidades que suportam modificação dinâmica de cooldown via atributos.
    /// </summary>
    public interface ICooldownModifier
    {
        AttributeDefinition CooldownReductionAttribute { get; }
    }
}
