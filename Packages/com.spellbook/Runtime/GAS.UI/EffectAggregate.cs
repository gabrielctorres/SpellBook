using System.Collections.Generic;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// As instâncias ativas de um mesmo <see cref="GameplayEffect"/> vistas como uma só (Spec UI-3, Design 4; R7 do B-018):
    /// pilhas somadas (instância sem <see cref="GameplayEffect.CanStack"/> conta 1), a maior duração restante e a duração
    /// aplicada dela, e infinito se alguma instância é infinita. Não aloca.
    /// </summary>
    internal readonly struct EffectAggregate
    {
        private EffectAggregate(int count, int stacks, float remaining, float duration, bool isInfinite, ActiveEffectInstance first)
        {
            Count = count;
            Stacks = stacks;
            Remaining = remaining;
            Duration = duration;
            IsInfinite = isInfinite;
            First = first;
        }

        /// <summary>Instâncias agregadas; 0 sem instância.</summary>
        public int Count { get; }

        /// <summary>Soma das pilhas.</summary>
        public int Stacks { get; }

        /// <summary>A maior duração restante entre as instâncias com duração, em segundos.</summary>
        public float Remaining { get; }

        /// <summary>A duração aplicada da instância de maior restante.</summary>
        public float Duration { get; }

        /// <summary>Verdadeiro se alguma instância é infinita.</summary>
        public bool IsInfinite { get; }

        /// <summary>A primeira instância, na ordem da lista; nula sem instância.</summary>
        public ActiveEffectInstance First { get; }

        /// <summary>Verdadeiro com instância e sem nenhuma infinita: o tempo restante corre.</summary>
        public bool HasDuration => Count > 0 && !IsInfinite;

        /// <summary>
        /// Agrega as instâncias de <paramref name="effect"/> em <paramref name="asc"/>, achadas por
        /// <see cref="AbilitySystemComponent.FindEffects"/> em <paramref name="instances"/>, que é limpa e reaproveitada.
        /// </summary>
        public static EffectAggregate Of(AbilitySystemComponent asc, GameplayEffect effect, List<ActiveEffectInstance> instances)
        {
            instances.Clear();
            if (asc == null || effect == null) return default;

            asc.FindEffects(effect, instances);
            return Of(instances);
        }

        /// <summary>Agrega as instâncias de <paramref name="instances"/>, pulando as nulas.</summary>
        public static EffectAggregate Of(List<ActiveEffectInstance> instances)
        {
            int count = 0;
            int stacks = 0;
            float remaining = 0f;
            float duration = 0f;
            bool infinite = false;
            bool timed = false;
            ActiveEffectInstance first = null;
            for (int i = 0; i < instances.Count; i++)
            {
                ActiveEffectInstance instance = instances[i];
                if (instance == null) continue;

                first ??= instance;
                count++;
                stacks += instance.Effect.CanStack ? instance.CurrentStacks : 1;
                if (instance.Effect.DurationPolicy == EffectDurationPolicy.Infinite)
                {
                    infinite = true;
                }
                else if (!timed || instance.TimeRemaining > remaining)
                {
                    timed = true;
                    remaining = instance.TimeRemaining;
                    duration = instance.AppliedDuration;
                }
            }

            return new EffectAggregate(count, stacks, remaining, duration, infinite, first);
        }
    }
}
