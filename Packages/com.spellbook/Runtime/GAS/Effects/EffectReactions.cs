using System.Collections.Generic;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using UnityEngine;

namespace SpellBook.GAS.Effects
{
    /// <summary>
    /// Despacho das reações dos efeitos ativos de um ASC (Ao receber dano, Ao morrer). O ASC cria no primeiro efeito com
    /// reação. A trava de reentrada vale entre ASCs: uma instância que já está reagindo de forma síncrona ignora uma nova
    /// reação dela mesma, e as reações aninhadas param em <see cref="MaxReactionDepth"/> (P11).
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

            AbilitySystemComponent instigator = report.Context.Instigator;
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                if (i >= _instances.Count) continue;
                ActiveEffectInstance instance = _instances[i];
                if (!instance.IsActive || !AcceptsDamage(instance.Effect, report.Attribute)) continue;
                if (!TryReact(instance, instance.Effect.OnTakeDamage, EffectEvent.TakeDamage, instigator, in report)) return;
            }
        }

        /// <summary>Reação "Ao morrer": o atributo de morte da instância chegou ao mínimo.</summary>
        public void AfterDepleted(AttributeState state, in AttributeChangeReport cause)
        {
            if (_instances.Count == 0) return;

            AbilitySystemComponent instigator = cause.IsValid ? cause.Context.Instigator : null;
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                if (i >= _instances.Count) continue;
                ActiveEffectInstance instance = _instances[i];
                if (!instance.IsActive || instance.Effect.DeathAttribute != state.Definition) continue;
                if (!TryReact(instance, instance.Effect.OnDeath, EffectEvent.Death, instigator, in cause)) return;
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
            AbilitySystemComponent instigator,
            in AttributeChangeReport change)
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
                instance.RunReaction(actions, effectEvent, instigator, in change);
            }
            finally
            {
                Reacting.RemoveAt(Reacting.Count - 1);
            }
            return true;
        }
    }
}
