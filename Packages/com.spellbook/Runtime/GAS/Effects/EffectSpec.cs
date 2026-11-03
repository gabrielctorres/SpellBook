using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using UnityEngine;

namespace SpellBook.GAS.Effects
{
    /// <summary>
    /// Pedido de aplicação de um efeito (ADR 0030): o efeito, o contexto e, opcionalmente, uma duração que substitui a do efeito.
    /// É struct e é passado por <c>in</c>; montar e aplicar não aloca.
    /// </summary>
    public struct EffectSpec
    {
        public GameplayEffect Effect { get; private set; }
        public EffectContext Context { get; private set; }
        public bool HasDurationOverride { get; private set; }

        /// <summary>Duração em segundos que substitui a do efeito; só vale com <see cref="HasDurationOverride"/>.</summary>
        public float DurationOverride { get; private set; }

        /// <summary>Spec sem habilidade: nível <see cref="GameplayEffect.DefaultLevel"/> e nenhuma tabela.</summary>
        public static EffectSpec Create(GameplayEffect effect, AbilitySystemComponent instigator)
        {
            return ForAbility(effect, instigator, null, effect != null ? effect.DefaultLevel : 1);
        }

        /// <summary>
        /// Spec de uma ação: instigador, habilidade e nível do <paramref name="context"/>, e o
        /// <see cref="ActionContext.Point"/> como ponto de impacto. Contexto nulo vira <see cref="Create"/> sem instigador.
        /// </summary>
        public static EffectSpec FromAbility(GameplayEffect effect, ActionContext context)
        {
            if (context == null) return Create(effect, null);

            EffectSpec spec = context.Ability != null
                ? ForAbility(effect, context.Instigator, context.Ability, context.Level)
                : Create(effect, context.Instigator);
            return spec.WithImpactPoint(context.Point);
        }

        internal static EffectSpec ForAbility(GameplayEffect effect, AbilitySystemComponent instigator, GameplayAbility ability, int level)
        {
            return new EffectSpec
            {
                Effect = effect,
                Context = new EffectContext(instigator, null, ability, level, false, Vector3.zero, null)
            };
        }

        public EffectSpec WithCallerValues(IReadOnlySetByCaller values)
        {
            EffectSpec copy = this;
            copy.Context = Context.WithCallerValues(values);
            return copy;
        }

        public EffectSpec WithImpactPoint(Vector3 point)
        {
            EffectSpec copy = this;
            copy.Context = Context.WithImpactPoint(point);
            return copy;
        }

        public EffectSpec WithDurationOverride(float seconds)
        {
            EffectSpec copy = this;
            copy.HasDurationOverride = true;
            copy.DurationOverride = seconds;
            return copy;
        }
    }
}
