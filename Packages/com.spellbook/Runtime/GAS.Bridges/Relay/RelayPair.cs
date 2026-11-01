using System;
using SpellBook.Core.Validation;
using SpellBook.Events;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;
using Void = SpellBook.Core.Void;

namespace SpellBook.GAS.Bridges
{
    /// <summary>
    /// Par do <see cref="AbilitySystemEventRelay"/> para um evento do ASC: canal tipado, canal de valor simples e
    /// <c>UnityEvent</c> local, cada um opcional (ADR 0031, item 2).
    /// </summary>
    [Serializable]
    public sealed class RelayPair<T>
    {
        [SerializeField, Tooltip("Canal com os dados completos do evento.")]
        private EventChannel<T> channel;

        [SerializeField, Tooltip("Canal de valor simples: Void, Float, String ou GameObject. O valor depende do evento.")]
        private EventChannel valueChannel;

        [SerializeField, Tooltip("Chamado depois dos canais, só neste objeto.")]
        private UnityEvent<T> response = new UnityEvent<T>();

        /// <summary>Canal tipado, depois canal de valor, depois <c>UnityEvent</c>, cada um só se configurado.</summary>
        internal void Raise(in T data, Object sender, RelayValueSelector<T> values)
        {
            if (channel != null) channel.Raise(data, sender);
            if (valueChannel != null) RaiseValue(in data, sender, values);
            response?.Invoke(data);
        }

        /// <summary>
        /// Acusa canal de valor de tipo fora da lista e chamada do <c>UnityEvent</c> sem alvo ou sem método. O
        /// <paramref name="path"/> é o nome do campo do par no relay.
        /// </summary>
        internal void Validate(ValidationResult result, string path)
        {
            if (valueChannel != null && !IsSupportedValueChannel(valueChannel))
            {
                result.AddError($"{path}: canal de valor de {valueChannel.PayloadType.Name} não é suportado; use Void, " +
                    "Float, String ou GameObject.", path + ".valueChannel");
            }

            if (response == null) return;
            for (int i = 0; i < response.GetPersistentEventCount(); i++)
            {
                if (response.GetPersistentTarget(i) == null)
                {
                    result.AddError($"{path}: chamada {i} sem alvo: escolha o objeto ou remova a chamada.", path + ".response");
                }
                else if (string.IsNullOrEmpty(response.GetPersistentMethodName(i)))
                {
                    result.AddError($"{path}: chamada {i} sem método: escolha a função ou remova a chamada.", path + ".response");
                }
            }
        }

        private static bool IsSupportedValueChannel(EventChannel candidate)
        {
            return candidate is EventChannel<Void> || candidate is EventChannel<float> || candidate is EventChannel<string>
                || candidate is EventChannel<GameObject>;
        }

        // Teste de tipo, sem alocação. Tipo fora da lista, ou valor que o evento não tem, não dispara.
        private void RaiseValue(in T data, Object sender, RelayValueSelector<T> values)
        {
            switch (valueChannel)
            {
                case EventChannel<Void> voidChannel:
                    voidChannel.Raise(default, sender);
                    break;
                case EventChannel<float> floatChannel when values.TryGetFloat(in data, out float number):
                    floatChannel.Raise(number, sender);
                    break;
                case EventChannel<string> stringChannel:
                    stringChannel.Raise(values.GetText(in data), sender);
                    break;
                case EventChannel<GameObject> objectChannel:
                    objectChannel.Raise(values.GetObject(in data), sender);
                    break;
            }
        }
    }

    /// <summary>Regra fixa que tira o valor simples de um payload do GAS (Spec, Design 3).</summary>
    internal abstract class RelayValueSelector<T>
    {
        /// <summary>Valor do canal Float; <c>false</c> quando o evento não tem número.</summary>
        public abstract bool TryGetFloat(in T data, out float value);

        /// <summary>Valor do canal String. Usa <c>Object.name</c>, que aloca.</summary>
        public abstract string GetText(in T data);

        /// <summary>Valor do canal GameObject.</summary>
        public abstract GameObject GetObject(in T data);

        protected static GameObject ObjectOf(Component component) => component != null ? component.gameObject : null;

        protected static string NameOf(Object asset) => asset != null ? asset.name : string.Empty;
    }
}
