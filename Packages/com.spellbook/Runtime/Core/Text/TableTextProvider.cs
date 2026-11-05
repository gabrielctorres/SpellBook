using System;
using System.Collections.Generic;
using System.Globalization;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Core.Text
{
    /// <summary>
    /// Provedor de texto por <see cref="TextTable"/> (ADR 0038, item 2). A busca segue a cultura do provedor, depois as
    /// culturas pai (<c>pt-BR</c> → <c>pt</c>), depois as tabelas neutras e, por último, o texto padrão do campo. O
    /// dicionário é montado na primeira leitura depois de cada troca; ler uma chave já resolvida não aloca.
    /// </summary>
    [HelpURL(DocUrls.Text + "#tabletextprovider")]
    [CreateAssetMenu(menuName = "SpellBook/Text/Table Text Provider", fileName = "New Table Text Provider")]
    public class TableTextProvider : TextProviderAsset, IValidatable
    {
        [SerializeField, Tooltip("Tabelas lidas pelo provedor. Na mesma cultura, a primeira tabela com a chave vence.")]
        private List<TextTable> tables = new List<TextTable>();

        [SerializeField, Tooltip("Cultura dos textos e dos números, como pt-BR. Vazio: cultura invariante.")]
        private string cultureName = string.Empty;

        [NonSerialized] private Dictionary<string, string> _texts;
        [NonSerialized] private CultureInfo _culture;

        /// <inheritdoc />
        public override IFormatProvider Culture => _culture ?? (_culture = ParseCulture(cultureName));

        /// <inheritdoc />
        public override string GetText(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key))
            {
                return fallback;
            }

            if (_texts == null)
            {
                Build();
            }

            return _texts.TryGetValue(key, out string text) ? text : fallback;
        }

        /// <summary>Troca a cultura (nula: invariante) e dispara <see cref="TextProviderAsset.Changed"/>.</summary>
        public void SetCulture(CultureInfo culture)
        {
            cultureName = culture == null ? string.Empty : culture.Name;
            Invalidate();
            RaiseChanged();
        }

        public void Validate(ValidationResult result)
        {
            // Por cultura: chave → índice da primeira tabela que a define
            var firstByCulture = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            var warned = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < tables.Count; i++)
            {
                string path = $"{nameof(tables)}.Array.data[{i}]";
                TextTable table = tables[i];
                if (table == null)
                {
                    result.AddError($"Tabela vazia em tables (posição {i}). Escolha uma tabela ou remova a linha.", path);
                    continue;
                }

                if (!firstByCulture.TryGetValue(table.CultureName, out Dictionary<string, int> first))
                {
                    first = new Dictionary<string, int>(StringComparer.Ordinal);
                    firstByCulture.Add(table.CultureName, first);
                }

                IReadOnlyList<TextTable.Entry> entries = table.Entries;
                for (int e = 0; e < entries.Count; e++)
                {
                    string key = entries[e].Key;
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    if (!first.TryGetValue(key, out int owner))
                    {
                        first.Add(key, i);
                    }
                    else if (owner != i && FindText(tables[owner], key) != entries[e].Text &&
                             warned.Add(table.CultureName + "\n" + key))
                    {
                        result.AddWarning(
                            $"Chave '{key}' com textos diferentes nas tabelas '{tables[owner].name}' e '{table.name}' " +
                            $"da cultura '{table.CultureName}'. Vale a primeira.", path);
                    }
                }
            }
        }

        private void OnValidate()
        {
            Invalidate();
            RaiseChanged();
        }

        private void Invalidate()
        {
            _texts = null;
            _culture = null;
        }

        private void Build()
        {
            _texts = new Dictionary<string, string>(StringComparer.Ordinal);
            var culture = (CultureInfo)Culture;
            for (CultureInfo c = culture; !string.IsNullOrEmpty(c.Name); c = c.Parent)
            {
                AddTables(c.Name);
            }

            AddTables(string.Empty);
        }

        private void AddTables(string culture)
        {
            for (int i = 0; i < tables.Count; i++)
            {
                TextTable table = tables[i];
                if (table == null || !string.Equals(table.CultureName, culture, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                IReadOnlyList<TextTable.Entry> entries = table.Entries;
                for (int e = 0; e < entries.Count; e++)
                {
                    string key = entries[e].Key;
                    if (!string.IsNullOrEmpty(key) && !_texts.ContainsKey(key))
                    {
                        _texts.Add(key, entries[e].Text);
                    }
                }
            }
        }

        private static string FindText(TextTable table, string key)
        {
            IReadOnlyList<TextTable.Entry> entries = table.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].Key, key, StringComparison.Ordinal))
                {
                    return entries[i].Text;
                }
            }

            return null;
        }

        private static CultureInfo ParseCulture(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return CultureInfo.InvariantCulture;
            }

            try
            {
                return CultureInfo.GetCultureInfo(name);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }
}
