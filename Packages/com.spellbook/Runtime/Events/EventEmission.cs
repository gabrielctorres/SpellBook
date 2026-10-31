using System;
using SpellBook.Core.Validation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Events
{
    /// <summary>
    /// Disparo configurado no Inspector: um canal e o payload fixo enviado a ele. Usado pelo <see cref="EventEmitter"/>.
    /// A instância é criada por código a partir do canal escolhido (<see cref="CreateFor"/>), não por seletor de tipo
    /// (ADR 0013).
    /// </summary>
    [Serializable]
    public abstract class EventEmission : IValidatable
    {
        /// <summary>Canal disparado, ou <c>null</c> se nenhum foi escolhido.</summary>
        public abstract EventChannel Channel { get; }

        /// <summary>Tipo do payload da emissão.</summary>
        public abstract Type PayloadType { get; }

        /// <summary>
        /// Emissão do tipo de payload de <paramref name="channel"/>, já com o canal e o payload padrão. Com canal nulo,
        /// devolve <c>null</c>.
        /// </summary>
        public static EventEmission CreateFor(EventChannel channel)
        {
            if (channel == null) return null;

            Type type = typeof(EventEmission<>).MakeGenericType(channel.PayloadType);
            var emission = (EventEmission)Activator.CreateInstance(type);
            emission.AssignChannel(channel);
            return emission;
        }

        /// <summary>Dispara o canal com o payload, informando quem disparou. Sem canal, não faz nada.</summary>
        public abstract void Raise(Object sender);

        public void Validate(ValidationResult result)
        {
            if (Channel == null)
            {
                result.AddError("Emissão sem canal: escolha o Channel.");
            }
        }

        internal abstract void AssignChannel(EventChannel channel);
    }

    /// <summary>Emissão com payload <typeparamref name="T"/>.</summary>
    [Serializable]
    public sealed class EventEmission<T> : EventEmission
    {
        [SerializeField, Tooltip("Canal disparado.")]
        private EventChannel<T> channel;

        [SerializeField, Tooltip("Payload enviado a cada disparo.")]
        private T payload;

        public override EventChannel Channel => channel;

        public override Type PayloadType => typeof(T);

        public override void Raise(Object sender)
        {
            if (channel != null)
            {
                channel.Raise(payload, sender);
            }
        }

        internal override void AssignChannel(EventChannel value)
        {
            channel = value as EventChannel<T>;
            if (channel == null)
            {
                throw new ArgumentException($"O canal {value.name} não é um EventChannel<{typeof(T).Name}>.", nameof(value));
            }
        }
    }
}
