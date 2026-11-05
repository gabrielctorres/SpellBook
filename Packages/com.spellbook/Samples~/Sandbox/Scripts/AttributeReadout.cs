using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.GAS.Attributes;
using UnityEngine;

namespace SpellBook.Samples.Sandbox
{
    /// <summary>
    /// Mostra no Inspector o valor atual de atributos do próprio objeto durante o Play. Atualiza só quando o
    /// valor muda, sem <c>Update</c>.
    /// </summary>
    [RequireComponent(typeof(AttributeSet))]
    [HelpURL(DocUrls.Samples + "#attributereadout")]
    public sealed class AttributeReadout : MonoBehaviour
    {
        [SerializeField, Tooltip("Attributes shown and the current value of each.")]
        private List<Entry> entries = new List<Entry>();

        private AttributeSet _attributeSet;

        private void Awake()
        {
            _attributeSet = GetComponent<AttributeSet>();
        }

        // No Start, porque o AttributeSet cria os estados no Awake dele.
        private void Start()
        {
            foreach (Entry entry in entries)
            {
                entry.Bind(_attributeSet);
            }
        }

        private void OnDestroy()
        {
            foreach (Entry entry in entries)
            {
                entry.Unbind();
            }
        }

        [Serializable]
        private sealed class Entry
        {
            [SerializeField, Tooltip("Attribute shown.")]
            private AttributeDefinition attribute;

            [SerializeField, Tooltip("Current value, updated during Play.")]
            private float current;

            [NonSerialized]
            private AttributeState _state;

            public void Bind(AttributeSet attributeSet)
            {
                if (attribute == null || !attributeSet.TryGetState(attribute, out _state))
                {
                    return;
                }

                current = _state.CurrentValue;
                _state.OnValueChanged += HandleValueChanged;
            }

            public void Unbind()
            {
                if (_state != null)
                {
                    _state.OnValueChanged -= HandleValueChanged;
                    _state = null;
                }
            }

            private void HandleValueChanged(float oldValue, float newValue)
            {
                current = newValue;
            }
        }
    }
}
