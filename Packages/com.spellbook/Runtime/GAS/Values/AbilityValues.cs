using System;
using System.Collections.Generic;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.GAS.Values
{
    /// <summary>
    /// Tabela de valores nomeados de uma habilidade (ADR 0030): cada entrada é um <see cref="ScalableFloat"/> que um
    /// <see cref="ScalableFloat"/> em modo <c>Table</c> lê pelo nome. A busca diferencia maiúsculas e não aloca.
    /// </summary>
    [Serializable]
    public sealed class AbilityValues
    {
        /// <summary>Um valor nomeado da tabela.</summary>
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string name;
            [SerializeField] private ScalableFloat value = ScalableFloat.Constant(0f);

            internal Entry(string name, ScalableFloat value)
            {
                this.name = name;
                this.value = value;
            }

            public string Name => name;

            public ScalableFloat Value
            {
                get => value;
                internal set => this.value = value;
            }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        [NonSerialized] private int version;

        /// <summary>Sobe a cada mudança na tabela; o <see cref="ScalableFloat"/> refaz o índice em cache quando muda.</summary>
        public int Version => version;

        public int Count => entries.Count;

        /// <summary>Acrescenta uma entrada no fim, sem conferir nome repetido (o <see cref="Validate"/> acusa).</summary>
        public void Add(string name, ScalableFloat value)
        {
            entries.Add(new Entry(name, value ?? throw new ArgumentNullException(nameof(value))));
            version++;
        }

        /// <summary>Troca o valor da entrada com o nome, ou a acrescenta se não existir.</summary>
        public void Set(string name, ScalableFloat value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            int index = IndexOf(name);
            if (index < 0)
            {
                Add(name, value);
                return;
            }

            entries[index].Value = value;
            version++;
        }

        /// <summary>Remove a entrada com o nome. Devolve <c>false</c> se não existir.</summary>
        public bool Remove(string name)
        {
            int index = IndexOf(name);
            if (index < 0) return false;

            entries.RemoveAt(index);
            version++;
            return true;
        }

        /// <summary>Índice da entrada com o nome (comparação ordinal), ou -1.</summary>
        public int IndexOf(string name)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i]?.Name, name, StringComparison.Ordinal)) return i;
            }

            return -1;
        }

        /// <summary>Como <see cref="IndexOf(string)"/>, com o nome num trecho de texto (por exemplo, de um template). Não aloca.</summary>
        public int IndexOf(ReadOnlySpan<char> name)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                string entryName = entries[i]?.Name;
                if (entryName != null && name.SequenceEqual(entryName.AsSpan())) return i;
            }

            return -1;
        }

        /// <summary>Valor da entrada no nível, avaliado sem tabela: uma entrada nunca lê outra.</summary>
        public float Evaluate(int index, int level)
        {
            ScalableFloat value = entries[index]?.Value;
            return value != null ? value.Evaluate(level) : 0f;
        }

        /// <summary>Valor da entrada com o nome no nível. Sem a entrada, devolve <c>false</c> e 0, sem aviso.</summary>
        public bool TryGetValue(string name, int level, out float value)
        {
            int index = IndexOf(name);
            if (index < 0)
            {
                value = 0f;
                return false;
            }

            value = Evaluate(index, level);
            return true;
        }

        /// <summary>Entrada no índice (pode ser nula, vinda do Inspector). Usado pelo validador da habilidade.</summary>
        internal Entry GetEntry(int index)
        {
            return entries[index];
        }

        /// <summary>Avisa a tabela de uma mudança feita fora da API (Inspector).</summary>
        internal void MarkChanged()
        {
            version++;
        }

        /// <summary>Acusa nome vazio, fora de letras, dígitos e <c>_</c>, ou repetido, e entrada em modo <c>Table</c>.</summary>
        public void Validate(ValidationResult result, string propertyPath)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            for (int i = 0; i < entries.Count; i++)
            {
                string path = $"{propertyPath}.entries[{i}]";
                Entry entry = entries[i];
                if (entry == null) continue;

                if (!IsIdentifier(entry.Name))
                {
                    result.AddError($"O nome \"{entry.Name}\" é inválido: use só letras, dígitos e _.", path + ".name");
                }
                else if (IndexOf(entry.Name) != i)
                {
                    result.AddError($"O nome \"{entry.Name}\" está repetido na tabela.", path + ".name");
                }

                entry.Value?.Validate(result, path + ".value", allowTable: false);
            }
        }

        /// <summary>
        /// Nome só com letras ASCII, dígitos e <c>_</c>, não vazio (string nula é vazia). Também vale para o nome do
        /// SetByCaller e para o <c>{nome}</c> do <c>TemplateElement</c>.
        /// </summary>
        internal static bool IsIdentifier(ReadOnlySpan<char> name)
        {
            if (name.IsEmpty) return false;

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool valid = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!valid) return false;
            }

            return true;
        }
    }
}
