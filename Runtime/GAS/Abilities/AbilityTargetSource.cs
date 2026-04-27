using System;

namespace SpellBook.GAS.Abilities
{
    public enum AbilityTargetSource
    {
        Caster,      // O herói que usou a magia
        Target,      // O inimigo atingido (se houver)
        Location     // O ponto exato do clique ou do impacto do projétil
    }
}

