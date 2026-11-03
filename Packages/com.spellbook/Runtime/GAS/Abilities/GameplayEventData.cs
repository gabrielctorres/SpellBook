using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Abilities
{
    /// <summary>Ouvinte de um evento de gameplay: a tag do evento e os dados dele.</summary>
    public delegate void GameplayEventHandler(GameplayTag tag, in GameplayEventData data);

    /// <summary>
    /// Dados de um evento de gameplay enviado por <see cref="AbilitySystemComponent.SendGameplayEvent"/>: quem causou, a
    /// unidade e o ponto do evento, uma magnitude, tags e, se veio de um efeito, o contexto dele.
    /// </summary>
    public readonly struct GameplayEventData
    {
        /// <summary>Evento sem ponto, com a unidade <paramref name="target"/> (pode ser nula).</summary>
        public GameplayEventData(AbilitySystemComponent instigator, AbilitySystemComponent target, float magnitude,
            GameplayTagContainer tags = null)
        {
            Instigator = instigator;
            Target = target;
            Magnitude = magnitude;
            Tags = tags;
            HasPoint = false;
            Point = Vector3.zero;
            HasEffectContext = false;
            EffectContext = default;
        }

        /// <summary>Evento com o ponto <paramref name="point"/>, que vale mais que a posição da unidade.</summary>
        public GameplayEventData(AbilitySystemComponent instigator, AbilitySystemComponent target, float magnitude,
            Vector3 point, GameplayTagContainer tags = null)
            : this(instigator, target, magnitude, tags)
        {
            HasPoint = true;
            Point = point;
        }

        private GameplayEventData(in GameplayEventData source, in EffectContext effectContext)
        {
            this = source;
            HasEffectContext = true;
            EffectContext = effectContext;
        }

        /// <summary>Quem causou o evento.</summary>
        public AbilitySystemComponent Instigator { get; }

        /// <summary>Unidade do evento. O gatilho a usa como unidade-alvo da ativação.</summary>
        public AbilitySystemComponent Target { get; }

        /// <summary>O evento tem um ponto em <see cref="Point"/>.</summary>
        public bool HasPoint { get; }

        /// <summary>Ponto do evento; <see cref="Vector3.zero"/> quando <see cref="HasPoint"/> é falso.</summary>
        public Vector3 Point { get; }

        /// <summary>Valor livre do evento, como o dano de um acerto.</summary>
        public float Magnitude { get; }

        /// <summary>Tags do evento. Pode ser nula.</summary>
        public GameplayTagContainer Tags { get; }

        /// <summary>O evento veio de um efeito, e <see cref="EffectContext"/> é o contexto dele.</summary>
        public bool HasEffectContext { get; }

        /// <summary>Contexto do efeito de origem; vazio quando <see cref="HasEffectContext"/> é falso.</summary>
        public EffectContext EffectContext { get; }

        /// <summary>Cópia destes dados com o contexto do efeito de origem.</summary>
        public GameplayEventData WithEffectContext(in EffectContext effectContext)
        {
            return new GameplayEventData(in this, in effectContext);
        }
    }
}
