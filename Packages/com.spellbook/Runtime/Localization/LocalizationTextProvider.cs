using System;
using System.Collections.Generic;
using System.Globalization;
using SpellBook.Core;
using SpellBook.Core.Text;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace SpellBook.Localization
{
    /// <summary>
    /// Provedor de texto que lê uma String Table do Unity Localization (ADR 0038, item 6). A cultura é a do
    /// <c>Locale</c> selecionado; a troca de locale limpa o cache e dispara <see cref="TextProviderAsset.Changed"/>.
    /// Ler uma chave já resolvida não aloca.
    /// </summary>
    [HelpURL(DocUrls.Text + "#localizationtextprovider")]
    [CreateAssetMenu(menuName = "SpellBook/Text/Localization Text Provider", fileName = "New Localization Text Provider")]
    public class LocalizationTextProvider : TextProviderAsset
    {
        [SerializeField, Tooltip("String Table Collection do Unity Localization com os textos das chaves.")]
        private TableReference table;

        // Chave → texto do locale selecionado; null guarda a chave ausente, para não buscar de novo
        [NonSerialized] private readonly Dictionary<string, string> _texts = new Dictionary<string, string>(StringComparer.Ordinal);
        [NonSerialized] private CultureInfo _culture;
        [NonSerialized] private bool _subscribed;

        /// <inheritdoc />
        public override IFormatProvider Culture => _culture ?? (_culture = CultureOf(LocalizationSettings.SelectedLocale));

        /// <inheritdoc />
        public override string GetText(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key))
            {
                return fallback;
            }

            if (!_texts.TryGetValue(key, out string text))
            {
                text = Find(key);
                _texts.Add(key, text);
            }

            return text ?? fallback;
        }

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
                _subscribed = false;
            }
        }

        private void OnValidate()
        {
            _texts.Clear();
            RaiseChanged();
        }

        private void OnLocaleChanged(Locale locale)
        {
            _texts.Clear();
            _culture = CultureOf(locale);
            RaiseChanged();
        }

        private string Find(string key)
        {
            if (table.ReferenceType == TableReference.Type.Empty)
            {
                return null;
            }

            StringTable strings = LocalizationSettings.StringDatabase.GetTable(table);
            StringTableEntry entry = strings == null ? null : strings.GetEntry(key);
            string text = entry?.GetLocalizedString();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static CultureInfo CultureOf(Locale locale)
        {
            CultureInfo culture = locale == null ? null : locale.Identifier.CultureInfo;
            return culture ?? CultureInfo.InvariantCulture;
        }
    }
}
