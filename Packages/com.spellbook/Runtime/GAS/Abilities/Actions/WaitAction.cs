using System;
using System.Collections;
using SpellBook.Core.Inspector;

namespace SpellBook.GAS.Abilities.Actions
{
    [Serializable, TypeCategory("Fluxo", Summary = "Espera alguns segundos no tempo do sistema de habilidades do ator.")]
    public class WaitAction : AbilityAction
    {
        /// <summary>Segundos do tempo do ASC. Zero ou menos espera exatamente um quadro, como na 2.x.</summary>
        public float Duration;
        public override IEnumerator Execute(AbilityContext context)
        {
            if (Duration <= 0f)
            {
                yield return null;
                yield break;
            }

            yield return context.WaitSeconds(Duration);
        }
    }
}

