using System;
using System.Collections.Generic;
using SpellBook.Core;
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
        private readonly List<AbilitySystemComponent> _ownTargets = new List<AbilitySystemComponent>();
        private List<AbilitySystemComponent> _targets;
        private Dictionary<string, object> _customData;
        private EffectContext _effect;

        /// <summary>Quem lançou: o dono da habilidade, ou quem aplicou o efeito.</summary>
        public AbilitySystemComponent Instigator { get; set; }
        /// <summary>ASC em que a execução roda.</summary>
        public AbilitySystemComponent Owner { get; internal set; }
        /// <summary>Alvos do evento que disparou a lista.</summary>
        public List<AbilitySystemComponent> EventTargets => _eventTargets;
        /// <summary>
        /// Alvos resolvidos para a ação corrente: os do seletor dela, ou os alvos do evento quando ela não tem seletor.
        /// O executor resolve quando a ação começa, e a lista vale até ela terminar.
        /// </summary>
        public List<AbilitySystemComponent> Targets => _targets ?? _ownTargets;
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
        /// <summary>Fonte de aleatoriedade do ASC em que a execução roda. Nula sem <see cref="Owner"/>.</summary>
        public IRandomSource Random => Owner != null ? Owner.Random : null;
        /// <summary>Verdadeiro depois que o dono da execução é cancelado.</summary>
        public bool IsCancelled { get; internal set; }
        /// <summary>Soma das pilhas que a última <see cref="RemoveEffectAction"/> da execução removeu. 0 antes da primeira.</summary>
        public int LastRemovedStacks { get; internal set; }

        /// <summary>
        /// Dados livres da execução, criados no primeiro acesso. As chaves usam o comparador de
        /// <see cref="SetCustomDataComparer"/>, ou o padrão.
        /// </summary>
        public Dictionary<string, object> CustomData => _customData ??= new Dictionary<string, object>();

        /// <summary>
        /// Escolhe o comparador das chaves do <see cref="CustomData"/>. <c>null</c> usa o padrão. Chame antes de gravar a
        /// primeira chave: com chaves gravadas, lança <see cref="InvalidOperationException"/>, porque o comparador novo
        /// poderia juntar duas chaves numa.
        /// </summary>
        public void SetCustomDataComparer(IEqualityComparer<string> comparer)
        {
            comparer ??= EqualityComparer<string>.Default;
            if (_customData != null && _customData.Count > 0)
            {
                throw new InvalidOperationException("Defina o comparador do CustomData antes de gravar chaves.");
            }
            IEqualityComparer<string> current = _customData != null ? _customData.Comparer : EqualityComparer<string>.Default;
            if (Equals(current, comparer)) return;
            _customData = new Dictionary<string, object>(comparer);
        }

        public void SetPoint(Vector3 point)
        {
            Point = point;
            HasPoint = true;
        }

        // O executor aponta Targets para o buffer do quadro da ação corrente; nulo volta à lista própria
        internal void SetTargets(List<AbilitySystemComponent> targets) => _targets = targets;

        internal void SetEffect(in EffectContext effect)
        {
            _effect = effect;
            HasEffect = true;
        }

        /// <summary>
        /// Contexto novo com <paramref name="point"/> como ponto, herdando instigador, habilidade e nível, sem vínculo
        /// de cancelamento. O <see cref="CustomData"/> é copiado raso: dicionário novo, com o mesmo comparador e as mesmas
        /// referências de valor.
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
                derived.SetCustomDataComparer(_customData.Comparer);
                foreach (KeyValuePair<string, object> pair in _customData) derived.CustomData[pair.Key] = pair.Value;
            }
            return derived;
        }

        internal void Reset()
        {
            _eventTargets.Clear();
            _ownTargets.Clear();
            _targets = null;
            // Um contexto do pool nunca herda o comparador de outra execução
            if (_customData != null && !Equals(_customData.Comparer, EqualityComparer<string>.Default)) _customData = null;
            else _customData?.Clear();
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
            LastRemovedStacks = 0;
        }
    }
}
