using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using SpellBook.GAS.Values;
using UnityEngine;

namespace SpellBook.GAS.Thinkers
{
    /// <summary>
    /// Aura do <see cref="GameplayThinker"/> (R9 a R11): a cada pulso, aplica o <see cref="Effect"/> em quem entrou na
    /// <see cref="Area"/> e remove de quem saiu ou deixou de casar com o filtro dela. Remove só o que aplicou, pela
    /// instância; um efeito igual de outra fonte fica. Quem perdeu o efeito por outra via e continua dentro o recebe no
    /// pulso seguinte (P3). O primeiro pulso é no começo da vida, e o término remove o efeito de todos. O
    /// <see cref="AreaSelector"/> procura com o criador como dono (ADR 0035), e o centro padrão é o thinker. O conjunto
    /// de quem está dentro fica no estado do thinker (ADR 0013).
    /// </summary>
    [HelpURL(DocUrls.Thinkers + "#aurabehaviour")]
    [Serializable, TypeCategory("Mundo", Summary = "Aplica um efeito em quem está no raio e o remove quando sai.", Line = "Aura {effect}")]
    public sealed class AuraBehaviour : ThinkerBehaviour
    {
        private const int InitialCapacity = 32;

        [SerializeField, Tooltip("Efeito aplicado em quem está dentro.")]
        private GameplayEffect effect;

        [SerializeReference, TypePicker, Tooltip("Quem está dentro: raio, filtro de candidato, incluir o dono, máximo e ordem. " +
            "A origem \"primeiro alvo do evento\" é o thinker.")]
        private AreaSelector area = new AreaSelector { Origin = AreaOrigin.FirstEventTarget };

        /// <summary>Efeito aplicado em quem está dentro.</summary>
        public GameplayEffect Effect { get => effect; set => effect = value; }

        /// <summary>Seletor de quem está dentro, avaliado no nível e na tabela da habilidade de criação.</summary>
        public AreaSelector Area { get => area; set => area = value; }

        public override object CreateState() => new State();

        /// <summary>Acusa a aura sem efeito e o raio constante zero ou negativo (erros), e o efeito instantâneo (aviso).</summary>
        public override void Validate(ValidationResult result)
        {
            if (effect == null) result.AddError("Aura sem efeito: escolha o efeito aplicado em quem está dentro.", nameof(effect));
            else if (effect.DurationPolicy == EffectDurationPolicy.Instant)
            {
                result.AddWarning("Efeito instantâneo na aura: ele é aplicado na entrada, e não há o que remover na saída.", nameof(effect));
            }

            ScalableFloat radius = area?.Radius;
            if (radius != null && radius.Mode == ScalableFloat.ScalableMode.Constant && radius.Evaluate(1) <= 0f)
            {
                result.AddError("Raio da aura zero ou negativo: ninguém fica dentro.", nameof(area) + ".radius");
            }
        }

        internal override bool Pulses => true;

        internal override void CollectScalables(List<ScalableFloat> results)
        {
            if (area?.Radius != null) results.Add(area.Radius);
        }

        // Raio no nível 1, sem a tabela da habilidade, em volta do thinker
        internal override void DrawGizmos(GameplayThinker thinker)
        {
            if (area?.Radius == null) return;
            Gizmos.DrawWireSphere(thinker.transform.position, Mathf.Max(0f, area.Radius.Evaluate(1)));
        }

        // Um ciclo anterior sem término (thinker desligado) deixou entradas: desfaz o que a aura aplicou nele
        public override void OnBegin(object state, GameplayThinker thinker, ActionContext context)
        {
            if (!(state is State aura)) return;
            UndoAll(aura);
            if (!thinker.HasEnded) Pulse(aura, thinker, context);
        }

        public override void OnPulse(object state, GameplayThinker thinker, ActionContext context)
        {
            if (state is State aura) Pulse(aura, thinker, context);
        }

        public override void OnEnd(object state, GameplayThinker thinker, ActionContext context)
        {
            if (state is State aura) UndoAll(aura);
        }

        private static void UndoAll(State aura)
        {
            List<Entry> entries = aura.Entries;
            // Tira da lista antes de remover: um ouvinte da remoção vê o estado já coerente
            while (entries.Count > 0)
            {
                Entry entry = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                Undo(entry);
            }
            aura.Scratch.Clear();
        }

        private void Pulse(State aura, GameplayThinker thinker, ActionContext context)
        {
            List<AbilitySystemComponent> inside = aura.Scratch;
            inside.Clear();
            if (effect != null && area != null) Select(context, inside);

            // Saiu, deixou de casar, foi destruído ou perdeu o efeito por outra via
            List<Entry> entries = aura.Entries;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (i >= entries.Count) continue;
                Entry entry = entries[i];
                bool lost = entry.Instance != null && !entry.Instance.IsActive;
                if (entry.Target != null && !lost && inside.Contains(entry.Target)) continue;
                entries.RemoveAt(i);
                if (!lost) Undo(entry);
                if (thinker.HasEnded) return;
            }

            // Entrou: aplica com o instigador, a habilidade e o nível de criação
            for (int i = 0; i < inside.Count; i++)
            {
                AbilitySystemComponent target = inside[i];
                if (target == null || IndexOf(entries, target) >= 0) continue;
                EffectSpec spec = EffectSpec.FromAbility(effect, context);
                EffectApplicationResult result = target.TryApplyEffect(in spec, out ActiveEffectInstance instance);
                bool applied = result == EffectApplicationResult.Applied || result == EffectApplicationResult.Stacked ||
                    result == EffectApplicationResult.Refreshed || result == EffectApplicationResult.AppliedInstant;
                if (!applied) continue;
                var entry = new Entry(target, instance, result);
                // Um ouvinte da aplicação terminou o thinker: a aplicação em curso não passa do término
                if (thinker.HasEnded)
                {
                    Undo(entry);
                    return;
                }
                entries.Add(entry);
            }
            inside.Clear();
        }

        // Procura com o criador como dono (ADR 0035): "incluir o dono" e o filtro falam de quem criou o thinker
        private void Select(ActionContext context, List<AbilitySystemComponent> results)
        {
            AbilitySystemComponent own = context.Owner;
            AbilitySystemComponent creator = context.Instigator;
            if (creator != null) context.Owner = creator;
            try { area.Select(context, results); }
            finally { context.Owner = own; }
        }

        // Desfaz só o que a aura aplicou: a instância nova inteira, ou a pilha que somou à de outra fonte
        private static void Undo(Entry entry)
        {
            if (entry.Target == null || entry.Instance == null || !entry.Instance.IsActive) return;
            if (entry.Result == EffectApplicationResult.Applied) entry.Target.RemoveEffect(entry.Instance);
            else if (entry.Result == EffectApplicationResult.Stacked) entry.Instance.RemoveStacks(1);
        }

        private static int IndexOf(List<Entry> entries, AbilitySystemComponent target)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Target == target) return i;
            }
            return -1;
        }

        // Um alvo dentro e o que a aura fez nele. Sem instância: efeito instantâneo, nada a remover.
        private readonly struct Entry
        {
            public readonly AbilitySystemComponent Target;
            public readonly ActiveEffectInstance Instance;
            public readonly EffectApplicationResult Result;

            public Entry(AbilitySystemComponent target, ActiveEffectInstance instance, EffectApplicationResult result)
            {
                Target = target;
                Instance = instance;
                Result = result;
            }
        }

        private sealed class State
        {
            public readonly List<Entry> Entries = new List<Entry>(InitialCapacity);
            public readonly List<AbilitySystemComponent> Scratch = new List<AbilitySystemComponent>(InitialCapacity);
        }
    }
}
