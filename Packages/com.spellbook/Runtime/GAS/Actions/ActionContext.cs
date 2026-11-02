using System.Collections.Generic;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using UnityEngine;

namespace SpellBook.GAS.Actions
{
    /// <summary>Evento do efeito que disparou a lista de ações.</summary>
    public enum EffectEvent
    {
        /// <summary>A execução não vem de um efeito.</summary>
        None,
        Applied,
        Tick,
        Removed,
        TakeDamage,
        Death,
        AbilityExecuted,
    }

    /// <summary>
    /// Dados de uma execução de ações. O ASC guarda os contextos em pool: depois de passar um contexto a
    /// <see cref="AbilitySystemComponent.RunActions"/>, o executor fica com ele e o devolve ao pool quando a execução
    /// termina ou é cancelada. Não guarde a referência depois disso.
    /// </summary>
    public sealed class ActionContext
    {
        private readonly List<AbilitySystemComponent> _eventTargets = new List<AbilitySystemComponent>();
        private readonly List<AbilitySystemComponent> _targets = new List<AbilitySystemComponent>();
        private Dictionary<string, object> _customData;
        private EffectContext _effect;

        /// <summary>Quem lançou: o dono da habilidade, ou quem aplicou o efeito.</summary>
        public AbilitySystemComponent Instigator { get; set; }
        /// <summary>ASC em que a execução roda.</summary>
        public AbilitySystemComponent Owner { get; internal set; }
        /// <summary>Alvos do evento que disparou a lista.</summary>
        public List<AbilitySystemComponent> EventTargets => _eventTargets;
        /// <summary>Alvos resolvidos para a ação corrente.</summary>
        public List<AbilitySystemComponent> Targets => _targets;
        /// <summary>Ponto do evento. Só vale com <see cref="HasPoint"/>.</summary>
        public Vector3 Point { get; private set; }
        public bool HasPoint { get; private set; }
        public GameplayAbility Ability { get; internal set; }
        /// <summary>Spec da ativação. Nulo fora de uma ativação.</summary>
        public AbilitySpec Spec { get; internal set; }
        public int Level { get; internal set; } = 1;
        /// <summary>Instância do efeito que disparou a lista. Nula fora de efeito e no efeito instantâneo.</summary>
        public ActiveEffectInstance EffectInstance { get; internal set; }
        /// <summary>Asset do efeito que disparou a lista, também no efeito instantâneo. Nulo fora de efeito.</summary>
        public GameplayEffect EffectDefinition { get; internal set; }
        /// <summary>Evento do efeito que disparou a lista. <see cref="EffectEvent.None"/> fora de efeito.</summary>
        public EffectEvent EffectEvent { get; internal set; }
        /// <summary>Se a execução vem de um efeito. Só então <see cref="Effect"/> vale.</summary>
        public bool HasEffect { get; private set; }
        /// <summary>Cópia do <see cref="EffectContext"/> do efeito que disparou a lista.</summary>
        public EffectContext Effect => _effect;
        /// <summary>Quem causou o evento de reação ou de gameplay. Nulo nos outros eventos.</summary>
        public AbilitySystemComponent EventInstigator { get; internal set; }
        /// <summary>Verdadeiro depois que o dono da execução é cancelado.</summary>
        public bool IsCancelled { get; internal set; }

        /// <summary>Dados livres da execução, criados no primeiro acesso.</summary>
        public Dictionary<string, object> CustomData => _customData ??= new Dictionary<string, object>();

        public void SetPoint(Vector3 point)
        {
            Point = point;
            HasPoint = true;
        }

        internal void SetEffect(in EffectContext effect)
        {
            _effect = effect;
            HasEffect = true;
        }

        /// <summary>
        /// Contexto novo com <paramref name="point"/> como ponto, herdando instigador, habilidade e nível, sem vínculo
        /// de cancelamento. O <see cref="CustomData"/> é copiado raso.
        /// </summary>
        public ActionContext CreateDerived(Vector3 point)
        {
            ActionContext derived = Owner != null ? Owner.Runner.RentContext() : new ActionContext();
            derived.Owner = Owner;
            derived.Instigator = Instigator;
            derived.Ability = Ability;
            derived.Level = Level;
            derived.SetPoint(point);
            if (_customData != null)
            {
                foreach (KeyValuePair<string, object> pair in _customData) derived.CustomData[pair.Key] = pair.Value;
            }
            return derived;
        }

        internal void Reset()
        {
            _eventTargets.Clear();
            _targets.Clear();
            _customData?.Clear();
            _effect = default;
            HasEffect = false;
            Instigator = null;
            Owner = null;
            Point = default;
            HasPoint = false;
            Ability = null;
            Spec = null;
            Level = 1;
            EffectInstance = null;
            EffectDefinition = null;
            EffectEvent = EffectEvent.None;
            EventInstigator = null;
            IsCancelled = false;
        }
    }
}
