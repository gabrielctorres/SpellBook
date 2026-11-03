using System.Collections.Generic;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using UnityEngine;

namespace SpellBook.GAS.Effects
{
    /// <summary>
    /// Despacho das reações dos efeitos ativos de um ASC (Ao receber dano, Ao morrer, Ao executar habilidade). O ASC cria
    /// no primeiro efeito com reação. A trava de reentrada vale entre ASCs: uma instância que já está reagindo de forma
    /// síncrona ignora uma nova reação dela mesma, e as reações aninhadas param em <see cref="MaxReactionDepth"/> (P11).
    /// </summary>
    internal sealed class EffectReactions
    {
        internal const int MaxReactionDepth = 8;

        // Instâncias reagindo agora, em todos os ASCs: o vai e volta entre dois espinhos passa por ASCs diferentes
        private static readonly List<ActiveEffectInstance> Reacting = new List<ActiveEffectInstance>();

        private readonly AbilitySystemComponent _owner;
        private readonly List<ActiveEffectInstance> _instances = new List<ActiveEffectInstance>();
        private bool _warnedDepth;

        public EffectReactions(AbilitySystemComponent owner) => _owner = owner;

        public void Add(ActiveEffectInstance instance) => _instances.Add(instance);

        public void Remove(ActiveEffectInstance instance) => _instances.Remove(instance);

        /// <summary>Reação "Ao receber dano": uma vez por alteração original com variação pedida negativa vinda de efeito.</summary>
        public void AfterChangeApplied(in AttributeChangeReport report)
        {
            if (_instances.Count == 0) return;
            if (report.Effect == null || report.RedirectedFrom != null || report.Requested >= 0f) return;

            EffectEventCause cause = EffectEventCause.FromChange(in report, report.Context.Instigator);
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                if (i >= _instances.Count) continue;
                ActiveEffectInstance instance = _instances[i];
                if (!instance.IsActive || !AcceptsDamage(instance.Effect, report.Attribute)) continue;
                if (!TryReact(instance, instance.Effect.OnTakeDamage, EffectEvent.TakeDamage, in cause)) return;
            }
        }

        /// <summary>Reação "Ao morrer": o atributo de morte da instância chegou ao mínimo.</summary>
        public void AfterDepleted(AttributeState state, in AttributeChangeReport cause)
        {
            if (_instances.Count == 0) return;

            EffectEventCause eventCause = EffectEventCause.FromChange(in cause, cause.IsValid ? cause.Context.Instigator : null);
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                if (i >= _instances.Count) continue;
                ActiveEffectInstance instance = _instances[i];
                if (!instance.IsActive || instance.Effect.DeathAttribute != state.Definition) continue;
                if (!TryReact(instance, instance.Effect.OnDeath, EffectEvent.Death, in eventCause)) return;
            }
        }

        /// <summary>Reação "Ao executar habilidade": o dono ativou uma habilidade, antes do <c>OnActivate</c> dela.</summary>
        public void AfterAbilityActivated(in AbilityTarget target)
        {
            if (_instances.Count == 0) return;

            EffectEventCause cause = EffectEventCause.FromActivation(_owner, in target);
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                if (i >= _instances.Count) continue;
                ActiveEffectInstance instance = _instances[i];
                if (!instance.IsActive) continue;
                if (!TryReact(instance, instance.Effect.OnAbilityExecuted, EffectEvent.AbilityExecuted, in cause)) return;
            }
        }

        private static bool AcceptsDamage(GameplayEffect effect, AttributeDefinition attribute)
        {
            IReadOnlyList<AttributeDefinition> attributes = effect.DamageAttributes;
            if (attributes.Count == 0) return true;
            for (int i = 0; i < attributes.Count; i++)
            {
                if (attributes[i] == attribute) return true;
            }
            return false;
        }

        // Falso quando a profundidade chegou ao teto: as reações seguintes deste despacho também são ignoradas
        private bool TryReact(
            ActiveEffectInstance instance,
            List<GameplayAction> actions,
            EffectEvent effectEvent,
            in EffectEventCause cause)
        {
            if (actions == null || actions.Count == 0 || Reacting.Contains(instance)) return true;

            if (Reacting.Count >= MaxReactionDepth)
            {
                if (!_warnedDepth)
                {
                    _warnedDepth = true;
                    Debug.LogWarning($"[GAS] Reações de '{_owner.name}' passaram de {MaxReactionDepth} reações aninhadas; as seguintes foram ignoradas.", _owner);
                }
                return false;
            }

            Reacting.Add(instance);
            try
            {
                instance.RunReaction(actions, effectEvent, in cause);
            }
            finally
            {
                Reacting.RemoveAt(Reacting.Count - 1);
            }
            return true;
        }
    }

    /// <summary>
    /// Causa de uma lista do efeito: quem causou o evento, o alvo do evento, a alteração e o ponto. O
    /// <see cref="ActiveEffectInstance"/> monta o contexto só a partir dela.
    /// </summary>
    internal readonly struct EffectEventCause
    {
        public readonly AbilitySystemComponent EventInstigator;
        public readonly AbilitySystemComponent EventTarget;
        public readonly AttributeChangeReport Change;
        public readonly bool HasPoint;
        public readonly Vector3 Point;

        private EffectEventCause(
            AbilitySystemComponent eventInstigator,
            AbilitySystemComponent eventTarget,
            in AttributeChangeReport change,
            bool hasPoint,
            Vector3 point)
        {
            EventInstigator = eventInstigator;
            EventTarget = eventTarget;
            Change = change;
            HasPoint = hasPoint;
            Point = point;
        }

        /// <summary>Aplicado, tique e removido: o alvo do evento é o dono do efeito.</summary>
        public static EffectEventCause Lifecycle(AbilitySystemComponent owner) => new EffectEventCause(null, owner, default, false, default);

        /// <summary>Dano e morte: o instigador e o alvo do evento são quem causou a alteração.</summary>
        public static EffectEventCause FromChange(in AttributeChangeReport change, AbilitySystemComponent instigator)
        {
            return new EffectEventCause(instigator, instigator, in change, false, default);
        }

        /// <summary>Execução de habilidade: o instigador é o dono que ativou, e o alvo do evento é a unidade-alvo.</summary>
        public static EffectEventCause FromActivation(AbilitySystemComponent owner, in AbilityTarget target)
        {
            return new EffectEventCause(owner, target.Unit, default, target.HasPoint, target.Point);
        }
    }
}
