using System;
using System.Collections.Generic;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>Uma alteração da prévia de dano: o atributo, a base antes e depois, e a variação aplicada.</summary>
    public readonly struct DamagePreviewRow
    {
        public DamagePreviewRow(AttributeDefinition attribute, float before, float applied, AttributeDefinition redirectedFrom)
        {
            Attribute = attribute;
            Before = before;
            Applied = applied;
            RedirectedFrom = redirectedFrom;
        }

        public AttributeDefinition Attribute { get; }

        /// <summary>Base do atributo antes desta alteração.</summary>
        public float Before { get; }

        /// <summary>Quanto a base mudou de fato, depois dos processadores e dos limites.</summary>
        public float Applied { get; }

        /// <summary>Base do atributo depois desta alteração.</summary>
        public float After => Before + Applied;

        /// <summary>Atributo cujo processador gerou esta alteração; nulo na alteração original.</summary>
        public AttributeDefinition RedirectedFrom { get; }
    }

    /// <summary>
    /// Prévia "dano → processadores → alterações" do <see cref="AttributeDefinitionEditor"/> (ADR 0033). Roda o mesmo
    /// pipeline do jogo, pelo <see cref="AbilitySystemComponent.ApplyAttributeChange"/>, num ASC temporário oculto
    /// (<see cref="HideFlags.HideAndDontSave"/>) com o atributo e os atributos que os processadores citam. O ASC nasce
    /// com o modelo e é destruído no <see cref="Dispose"/>.
    /// </summary>
    public sealed class DamagePreviewModel : IDisposable
    {
        /// <summary>Valor inicial de um atributo sem valor dado por <see cref="SetValue"/>.</summary>
        public const float DefaultValue = 100f;

        private readonly AttributeDefinition _definition;
        private readonly List<AttributeDefinition> _attributes = new List<AttributeDefinition>();
        private readonly Dictionary<AttributeDefinition, float> _values = new Dictionary<AttributeDefinition, float>();
        private readonly List<DamagePreviewRow> _rows = new List<DamagePreviewRow>();
        private readonly AttributeChangeHandler _onApplied;
        private GameObject _host;
        private AbilitySystemComponent _asc;
        private bool _disposed;

        public DamagePreviewModel(AttributeDefinition definition)
        {
            _definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            _onApplied = Record;
            RefreshAttributes();
        }

        /// <summary>Dano de exemplo, positivo: a prévia aplica <c>-SampleDamage</c> ao atributo.</summary>
        public float SampleDamage { get; set; }

        /// <summary>O atributo da prévia e, depois dele, os atributos citados pelos processadores, sem repetição.</summary>
        public IReadOnlyList<AttributeDefinition> Attributes => _attributes;

        /// <summary>Valor inicial do atributo na prévia; <see cref="DefaultValue"/> se nenhum foi dado.</summary>
        public float GetValue(AttributeDefinition attribute)
        {
            return attribute != null && _values.TryGetValue(attribute, out float value) ? value : DefaultValue;
        }

        /// <summary>Muda o valor inicial do atributo na prévia; vale no próximo <see cref="Compute"/>.</summary>
        public void SetValue(AttributeDefinition attribute, float value)
        {
            if (attribute != null) _values[attribute] = value;
        }

        /// <summary>
        /// Relê os atributos citados pelos processadores (o asset pode ter mudado) e devolve se a lista mudou. O ASC
        /// temporário é refeito só quando a lista muda.
        /// </summary>
        public bool RefreshAttributes()
        {
            var cited = new List<AttributeDefinition> { _definition };
            CollectCited(_definition, cited);
            if (Same(cited, _attributes)) return false;

            _attributes.Clear();
            _attributes.AddRange(cited);
            DestroyHost();
            return true;
        }

        /// <summary>
        /// Põe cada atributo no valor inicial, aplica o dano de exemplo e devolve as alterações na ordem em que foram
        /// aplicadas: as redirecionadas antes da original. Depois do <see cref="Dispose"/>, devolve uma lista vazia.
        /// </summary>
        public IReadOnlyList<DamagePreviewRow> Compute()
        {
            _rows.Clear();
            if (_disposed) return _rows;

            RefreshAttributes();
            EnsureHost();
            for (int i = 0; i < _attributes.Count; i++)
            {
                if (_asc.AttributeSet.TryGetState(_attributes[i], out AttributeState state))
                {
                    state.SetBaseValue(GetValue(_attributes[i]));
                }
            }

            _asc.OnAttributeChangeApplied += _onApplied;
            try
            {
                _asc.ApplyAttributeChange(_definition, -SampleDamage);
            }
            finally
            {
                _asc.OnAttributeChangeApplied -= _onApplied;
            }

            return _rows;
        }

        /// <summary>Destrói o ASC temporário. A prévia não o recria depois.</summary>
        public void Dispose()
        {
            _disposed = true;
            DestroyHost();
        }

        private void Record(in AttributeChangeReport report)
        {
            float after = _asc.AttributeSet.GetBaseValue(report.Attribute);
            _rows.Add(new DamagePreviewRow(report.Attribute, after - report.Applied, report.Applied, report.RedirectedFrom));
        }

        // Inativo, o ASC não roda Awake nem Update: o AttributeSet cria os estados na primeira leitura
        private void EnsureHost()
        {
            if (_host != null) return;

            _host = new GameObject("DamagePreview") { hideFlags = HideFlags.HideAndDontSave };
            _host.SetActive(false);
            _asc = _host.AddComponent<AbilitySystemComponent>();
            AttributeSet set = _host.GetComponent<AttributeSet>();
            using (var so = new SerializedObject(set))
            {
                SerializedProperty list = so.FindProperty("startingAttributes");
                list.arraySize = _attributes.Count;
                for (int i = 0; i < _attributes.Count; i++)
                {
                    SerializedProperty item = list.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative(nameof(AttributeSet.InitialAttribute.Definition)).objectReferenceValue = _attributes[i];
                    item.FindPropertyRelative(nameof(AttributeSet.InitialAttribute.InitialValue)).floatValue = GetValue(_attributes[i]);
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            using (var so = new SerializedObject(_asc))
            {
                so.FindProperty("attributeSet").objectReferenceValue = set;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private void DestroyHost()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            _host = null;
            _asc = null;
        }

        // Lê as referências a AttributeDefinition dentro de cada processador pelo SerializedProperty, como o Inspector:
        // vale também para processadores escritos pelo usuário
        private static void CollectCited(AttributeDefinition definition, List<AttributeDefinition> cited)
        {
            using (var so = new SerializedObject(definition))
            {
                SerializedProperty processors = so.FindProperty("processors");
                for (int i = 0; processors != null && i < processors.arraySize; i++)
                {
                    SerializedProperty element = processors.GetArrayElementAtIndex(i);
                    SerializedProperty end = element.GetEndProperty();
                    SerializedProperty child = element.Copy();
                    if (!child.NextVisible(true)) continue;

                    while (!SerializedProperty.EqualContents(child, end))
                    {
                        if (child.propertyType == SerializedPropertyType.ObjectReference
                            && child.objectReferenceValue is AttributeDefinition attribute
                            && !cited.Contains(attribute))
                        {
                            cited.Add(attribute);
                        }

                        if (!child.NextVisible(true)) break;
                    }
                }
            }
        }

        private static bool Same(List<AttributeDefinition> a, List<AttributeDefinition> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }
    }
}
