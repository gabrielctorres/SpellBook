using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SpellBook.Core;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Values;
using UnityEngine;

namespace SpellBook.GAS.Targeting
{
    /// <summary>Física usada na busca: 3D (<c>Physics</c>) ou 2D (<c>Physics2D</c>).</summary>
    public enum PhysicsMode { Physics3D, Physics2D }

    /// <summary>Centro da busca do <see cref="AreaSelector"/>.</summary>
    public enum AreaOrigin
    {
        /// <summary>A posição de quem lançou (<see cref="ActionContext.Instigator"/>).</summary>
        Caster,
        /// <summary>O ponto do evento (<see cref="ActionContext.Point"/>).</summary>
        Point,
        /// <summary>A posição do primeiro alvo do evento que não foi destruído.</summary>
        FirstEventTarget,
    }

    /// <summary>Como o <see cref="AreaSelector"/> escolhe quem fica quando há mais candidatos que o máximo.</summary>
    public enum AreaOrder
    {
        /// <summary>Do mais perto do centro para o mais longe.</summary>
        Nearest,
        /// <summary>Sorteio sem repetição, com a fonte de aleatoriedade do ASC (<see cref="ActionContext.Random"/>).</summary>
        Random,
    }

    /// <summary>
    /// Escolhe as unidades num raio em volta de um centro, com física 2D ou 3D. O ASC é procurado no collider ou nos
    /// pais, e cada unidade conta uma vez. O ASC que roda a lista (<see cref="ActionContext.Owner"/>) fica de fora,
    /// a menos que <see cref="IncludeOwner"/> esteja ligado. O <see cref="Filter"/> decide, candidato a candidato, quem pode
    /// ser alvo (ADR 0035). A busca vê até 64 colliders.
    /// </summary>
    [Serializable, TypeCategory("Alvo", Summary = "Escolhe as unidades num raio, as mais perto ou sorteadas.", Line = "Área")]
    public sealed class AreaSelector : TargetSelector
    {
        private const int BufferSize = 64;

        // Buffers por ASC, criados na primeira busca dele e soltos junto com ele.
        private static readonly ConditionalWeakTable<AbilitySystemComponent, Buffers> BuffersByOwner =
            new ConditionalWeakTable<AbilitySystemComponent, Buffers>();

        [SerializeField] private AreaOrigin origin = AreaOrigin.Caster;
        [SerializeField] private PhysicsMode physics = PhysicsMode.Physics3D;
        [Tooltip("Raio da busca, por nível. Zero ou menos não acha ninguém.")]
        [SerializeField] private ScalableFloat radius = ScalableFloat.Constant(5f);
        [Tooltip("Permite que o ASC que roda a lista seja alvo.")]
        [SerializeField] private bool includeOwner;
        [Tooltip("0 = ilimitado.")]
        [SerializeField, Min(0)] private int maxTargets;
        [SerializeField] private AreaOrder order = AreaOrder.Nearest;
        [SerializeField] private LayerMask layers = ~0;
        [Tooltip("Filtro de candidato: o candidato fica se a condição for verdadeira. Vazio aceita todos.")]
        [SerializeReference, TypePicker] private Condition<ConditionContext> filter;

        public AreaOrigin Origin { get => origin; set => origin = value; }

        public PhysicsMode Physics { get => physics; set => physics = value; }

        /// <summary>Raio por nível, avaliado no nível da execução e na tabela da habilidade.</summary>
        public ScalableFloat Radius { get => radius; set => radius = value; }

        public bool IncludeOwner { get => includeOwner; set => includeOwner = value; }

        /// <summary>Máximo de alvos; 0 é ilimitado.</summary>
        public int MaxTargets { get => maxTargets; set => maxTargets = Mathf.Max(0, value); }

        public AreaOrder Order { get => order; set => order = value; }

        public LayerMask Layers { get => layers; set => layers = value; }

        /// <summary>
        /// Filtro de candidato (ADR 0035), avaliado uma vez por candidato com <c>Owner</c> = quem procura e
        /// <c>Target</c> = <see cref="AbilityTarget.AtUnit"/> do candidato, na habilidade, no spec e no nível da execução.
        /// <c>Targets</c> são os alvos do evento. Nulo aceita todos.
        /// </summary>
        public Condition<ConditionContext> Filter { get => filter; set => filter = value; }

        public override bool NeedsEventTargets => origin == AreaOrigin.FirstEventTarget;

        public override void Select(ActionContext context, List<AbilitySystemComponent> results)
        {
            AbilitySystemComponent owner = context.Owner != null ? context.Owner : context.Instigator;
            if (owner == null || radius == null || !TryGetCenter(context, out Vector3 center)) return;

            float range = radius.Evaluate(context.Level, context.Ability != null ? context.Ability.Values : null);
            if (range <= 0f) return;

            Buffers buffers = BuffersOf(owner);
            buffers.Seen.Clear();
            AbilitySystemComponent excluded = includeOwner ? null : owner;
            if (physics == PhysicsMode.Physics3D)
            {
                int count = UnityEngine.Physics.OverlapSphereNonAlloc(center, range, buffers.Colliders3D, layers);
                for (int i = 0; i < count; i++)
                {
                    TryAdd(context, owner, buffers, buffers.Colliders3D[i].GetComponentInParent<AbilitySystemComponent>(), excluded, results);
                }
                Array.Clear(buffers.Colliders3D, 0, count);
            }
            else
            {
                var filter = new ContactFilter2D { useTriggers = true };
                filter.SetLayerMask(layers);
                Physics2D.OverlapCircle(center, range, filter, buffers.Colliders2D);
                for (int i = 0; i < buffers.Colliders2D.Count; i++)
                {
                    TryAdd(context, owner, buffers, buffers.Colliders2D[i].GetComponentInParent<AbilitySystemComponent>(), excluded, results);
                }
                buffers.Colliders2D.Clear();
            }
            buffers.Seen.Clear();

            if (order == AreaOrder.Nearest) SortByDistance(results, center);
            else Shuffle(results, context.Random);
            if (maxTargets > 0 && results.Count > maxTargets) results.RemoveRange(maxTargets, results.Count - maxTargets);
        }

        private bool TryGetCenter(ActionContext context, out Vector3 center)
        {
            center = default;
            switch (origin)
            {
                case AreaOrigin.Caster:
                    if (context.Instigator == null) return false;
                    center = context.Instigator.transform.position;
                    return true;
                case AreaOrigin.Point:
                    center = context.Point;
                    return true;
                default:
                    List<AbilitySystemComponent> eventTargets = context.EventTargets;
                    for (int i = 0; i < eventTargets.Count; i++)
                    {
                        if (eventTargets[i] == null) continue;
                        center = eventTargets[i].transform.position;
                        return true;
                    }
                    return false;
            }
        }

        private static Buffers BuffersOf(AbilitySystemComponent owner)
        {
            if (!BuffersByOwner.TryGetValue(owner, out Buffers buffers))
            {
                buffers = new Buffers();
                BuffersByOwner.Add(owner, buffers);
            }
            return buffers;
        }

        private void TryAdd(ActionContext context, AbilitySystemComponent owner, Buffers buffers,
            AbilitySystemComponent candidate, AbilitySystemComponent excluded, List<AbilitySystemComponent> results)
        {
            if (candidate == null || candidate == excluded || !buffers.Seen.Add(candidate)) return;
            if (filter != null)
            {
                var candidateContext = new ConditionContext(owner, context.Ability, context.Level, context.Spec,
                    AbilityTarget.AtUnit(candidate), context.EventTargets, context);
                if (!filter.Evaluate(in candidateContext)) return;
            }
            results.Add(candidate);
        }

        // Inserção: estável e sem Comparison alocado.
        private static void SortByDistance(List<AbilitySystemComponent> targets, Vector3 center)
        {
            for (int i = 1; i < targets.Count; i++)
            {
                AbilitySystemComponent current = targets[i];
                float distance = (current.transform.position - center).sqrMagnitude;
                int j = i - 1;
                while (j >= 0 && (targets[j].transform.position - center).sqrMagnitude > distance)
                {
                    targets[j + 1] = targets[j];
                    j--;
                }
                targets[j + 1] = current;
            }
        }

        // Fisher-Yates parcial: sorteia só as posições que sobrevivem ao corte de maxTargets.
        private void Shuffle(List<AbilitySystemComponent> targets, IRandomSource random)
        {
            if (random == null) return;
            int count = targets.Count;
            int picks = maxTargets > 0 && maxTargets < count ? maxTargets : count - 1;
            for (int i = 0; i < picks; i++)
            {
                int j = i + random.Range(count - i);
                AbilitySystemComponent swap = targets[i];
                targets[i] = targets[j];
                targets[j] = swap;
            }
        }

        private sealed class Buffers
        {
            public readonly Collider[] Colliders3D = new Collider[BufferSize];
            public readonly List<Collider2D> Colliders2D = new List<Collider2D>(BufferSize);
            public readonly HashSet<AbilitySystemComponent> Seen = new HashSet<AbilitySystemComponent>();
        }
    }
}
