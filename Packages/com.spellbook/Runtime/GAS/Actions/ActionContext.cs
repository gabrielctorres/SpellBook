using System.Collections.Generic;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using UnityEngine;

namespace SpellBook.GAS.Actions
{
    /// <summary>
    /// Dados de uma execução de ações, passados a cada <see cref="GameplayAction"/> da lista. Sem contexto,
    /// <see cref="AbilitySystemComponent.RunActions"/> cria um com o ASC como dono e instigador.
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
        /// <summary>Se a execução vem de um efeito. Só então <see cref="Effect"/> vale.</summary>
        public bool HasEffect { get; private set; }
        /// <summary>Cópia do <see cref="EffectContext"/> do efeito que disparou a lista.</summary>
        public EffectContext Effect => _effect;
        /// <summary>Quem causou o evento de reação ou de gameplay. Nulo nos outros eventos.</summary>
        public AbilitySystemComponent EventInstigator { get; internal set; }

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
    }
}
