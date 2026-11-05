using SpellBook.Core;
using SpellBook.Events;
using SpellBook.GAS.Attributes;
using UnityEngine;

namespace SpellBook.Samples.Sandbox
{
    /// <summary>
    /// Dispara o canal de dano recebido com o valor real de cada queda da Vida do próprio objeto, inclusive os
    /// tiques de efeitos periódicos. Substituto local do relay de eventos do ASC (B-004), sem API nova no pacote.
    /// </summary>
    [RequireComponent(typeof(AttributeSet))]
    [HelpURL(DocUrls.Samples + "#damagetakenrelay")]
    public sealed class DamageTakenRelay : MonoBehaviour
    {
        [SerializeField, Tooltip("Observed health attribute.")]
        private AttributeDefinition health;

        [SerializeField, Tooltip("Channel raised with the damage of each drop.")]
        private FloatEventChannel damageTaken;

        private AttributeSet _attributeSet;
        private AttributeState _health;

        private void Awake()
        {
            _attributeSet = GetComponent<AttributeSet>();
        }

        // No Start, porque o AttributeSet cria os estados no Awake dele.
        private void Start()
        {
            if (_attributeSet.TryGetState(health, out _health))
            {
                _health.OnValueChanged += HandleValueChanged;
            }
            else
            {
                Debug.LogWarning("DamageTakenRelay: the health attribute is not in the AttributeSet.", this);
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnValueChanged -= HandleValueChanged;
            }
        }

        private void HandleValueChanged(float oldValue, float newValue)
        {
            if (newValue < oldValue && damageTaken != null)
            {
                damageTaken.Raise(oldValue - newValue, this);
            }
        }
    }
}
