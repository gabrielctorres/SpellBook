using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.GAS.Values;
using UnityEngine;

namespace SpellBook.GAS.Effects
{
    /// <summary>
    /// De onde veio uma aplicação de efeito (ADR 0030): quem aplicou, em quem, por qual habilidade, em que nível e onde.
    /// Os valores por nível do efeito são avaliados com o <see cref="Level"/> e a <see cref="Table"/> deste contexto.
    /// </summary>
    public readonly struct EffectContext
    {
        /// <summary>Quem aplicou o efeito. Nas aplicações sem origem, o próprio alvo.</summary>
        public AbilitySystemComponent Instigator { get; }

        /// <summary>Quem recebe o efeito. Preenchido pelo ASC na aplicação; nulo num spec ainda não aplicado.</summary>
        public AbilitySystemComponent Target { get; }

        /// <summary>Habilidade de origem; nula num efeito aplicado sem habilidade.</summary>
        public GameplayAbility Ability { get; }

        /// <summary>Nível da aplicação: o da habilidade ou, sem ela, o <see cref="GameplayEffect.DefaultLevel"/>.</summary>
        public int Level { get; }

        public bool HasImpactPoint { get; }

        /// <summary>Ponto de impacto; só tem sentido com <see cref="HasImpactPoint"/>.</summary>
        public Vector3 ImpactPoint { get; }

        /// <summary>Valores por nome de quem aplicou; nulo quando não há.</summary>
        public IReadOnlySetByCaller CallerValues { get; }

        /// <summary>Tabela da habilidade de origem, lida pelo modo <c>Table</c> do <see cref="ScalableFloat"/>; nula sem habilidade.</summary>
        public AbilityValues Table => Ability != null ? Ability.Values : null;

        internal EffectContext(
            AbilitySystemComponent instigator,
            AbilitySystemComponent target,
            GameplayAbility ability,
            int level,
            bool hasImpactPoint,
            Vector3 impactPoint,
            IReadOnlySetByCaller callerValues)
        {
            Instigator = instigator;
            Target = target;
            Ability = ability;
            Level = level < 1 ? 1 : level;
            HasImpactPoint = hasImpactPoint;
            ImpactPoint = impactPoint;
            CallerValues = callerValues;
        }

        /// <summary>Cópia para a aplicação em <paramref name="target"/>; sem instigador, o alvo vira o instigador.</summary>
        internal EffectContext ForApplication(AbilitySystemComponent target)
        {
            AbilitySystemComponent instigator = Instigator != null ? Instigator : target;
            return new EffectContext(instigator, target, Ability, Level, HasImpactPoint, ImpactPoint, CallerValues);
        }

        internal EffectContext WithImpactPoint(Vector3 point)
        {
            return new EffectContext(Instigator, Target, Ability, Level, true, point, CallerValues);
        }

        internal EffectContext WithCallerValues(IReadOnlySetByCaller values)
        {
            return new EffectContext(Instigator, Target, Ability, Level, HasImpactPoint, ImpactPoint, values);
        }
    }
}
