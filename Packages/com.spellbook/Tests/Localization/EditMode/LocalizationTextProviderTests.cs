using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.Localization.Tests
{
    /// <summary>
    /// <c>LocalizationTextProvider</c> lendo uma String Table do Unity Localization (Spec do B-008, Design 2; ADR 0038,
    /// item 6). O pacote recebe um <c>LocalizationSettings</c> de teste, com locales e tabelas em memória, sem Addressables.
    /// </summary>
    public class LocalizationTextProviderTests
    {
        private const string TableName = "SpellBookTests";
        private const string HelloKey = "gas.test.hello";

        private readonly List<Object> _created = new List<Object>();
        private LocalizationSettings _previous;
        private LocalizationSettings _settings;
        private Locale _ptBr;
        private Locale _enUs;

        [SetUp]
        public void SetUp()
        {
            _previous = LocalizationSettings.Instance;
            _ptBr = Track(Locale.CreateLocale("pt-BR"));
            _enUs = Track(Locale.CreateLocale("en-US"));

            var tables = new TestTableProvider(_created);
            tables.Add(_ptBr, HelloKey, "Olá");
            tables.Add(_enUs, HelloKey, "Hello");

            _settings = Track(ScriptableObject.CreateInstance<LocalizationSettings>());
            _settings.SetAvailableLocales(new TestLocales(_ptBr, _enUs));
            _settings.SetStringDatabase(new LocalizedStringDatabase { TableProvider = tables });
            LocalizationSettings.Instance = _settings;
            _settings.SetSelectedLocale(_ptBr);
        }

        [TearDown]
        public void TearDown()
        {
            LocalizationSettings.Instance = _previous;
            foreach (Object created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void EntryInStringTable_ReturnsLocalized()
        {
            LocalizationTextProvider provider = CreateProvider();

            Assert.That(provider.GetText(HelloKey, "padrão"), Is.EqualTo("Olá"));
        }

        [Test]
        public void MissingEntry_ReturnsFallback()
        {
            LocalizationTextProvider provider = CreateProvider();

            Assert.That(provider.GetText("gas.test.missing", "padrão"), Is.EqualTo("padrão"));
            Assert.That(provider.GetText(null, "padrão"), Is.EqualTo("padrão"));
            Assert.That(provider.GetText(string.Empty, "padrão"), Is.EqualTo("padrão"));
        }

        [Test]
        public void LocaleChange_RaisesChanged_CultureFollowsLocale()
        {
            LocalizationTextProvider provider = CreateProvider();
            Assert.That(provider.Culture, Is.EqualTo(CultureInfo.GetCultureInfo("pt-BR")));
            Assert.That(provider.GetText(HelloKey, "padrão"), Is.EqualTo("Olá"));
            int changed = 0;
            provider.Changed += () => changed++;

            _settings.SetSelectedLocale(_enUs);

            Assert.That(changed, Is.EqualTo(1));
            Assert.That(provider.Culture, Is.EqualTo(CultureInfo.GetCultureInfo("en-US")));
            Assert.That(provider.GetText(HelloKey, "padrão"), Is.EqualTo("Hello"));
        }

        [Test]
        public void ResolvedKey_DoesNotAllocate()
        {
            LocalizationTextProvider provider = CreateProvider();
            int total = 0;
            TestDelegate read10000 = () =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    total += provider.GetText(HelloKey, "padrão").Length;
                    total += provider.GetText("gas.test.missing", "padrão").Length;
                    total += provider.Culture.GetHashCode() & 1;
                }
            };
            read10000(); // aquecimento: a primeira leitura carrega a tabela e preenche o cache

            Assert.That(read10000, Is.Not.AllocatingGCMemory());
            Assert.That(total, Is.GreaterThan(0));
        }

        private LocalizationTextProvider CreateProvider()
        {
            LocalizationTextProvider provider = Track(ScriptableObject.CreateInstance<LocalizationTextProvider>());
            var serialized = new SerializedObject(provider);
            serialized.FindProperty("table.m_TableCollectionName").stringValue = TableName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return provider;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        /// <summary>Locales em memória, sem a busca por Addressables do <c>LocalesProvider</c>.</summary>
        private sealed class TestLocales : ILocalesProvider
        {
            public TestLocales(params Locale[] locales)
            {
                Locales = new List<Locale>(locales);
            }

            public List<Locale> Locales { get; }

            public Locale GetLocale(LocaleIdentifier id) => Locales.Find(l => l.Identifier == id);

            public void AddLocale(Locale locale) => Locales.Add(locale);

            public bool RemoveLocale(Locale locale) => Locales.Remove(locale);
        }

        /// <summary>Uma String Table por locale, criada em memória (padrão do <c>ITableProvider</c> do pacote).</summary>
        private sealed class TestTableProvider : ITableProvider
        {
            private readonly List<Object> _created;
            private readonly Dictionary<string, Dictionary<string, string>> _entries =
                new Dictionary<string, Dictionary<string, string>>();
            private SharedTableData _shared;

            public TestTableProvider(List<Object> created)
            {
                _created = created;
            }

            public void Add(Locale locale, string key, string text)
            {
                if (!_entries.TryGetValue(locale.Identifier.Code, out Dictionary<string, string> entries))
                {
                    entries = new Dictionary<string, string>();
                    _entries.Add(locale.Identifier.Code, entries);
                }

                entries[key] = text;
            }

            public AsyncOperationHandle<TTable> ProvideTableAsync<TTable>(string tableCollectionName, Locale locale)
                where TTable : LocalizationTable
            {
                if (typeof(TTable) != typeof(StringTable) || tableCollectionName != TableName)
                {
                    return default;
                }

                if (_shared == null)
                {
                    _shared = ScriptableObject.CreateInstance<SharedTableData>();
                    _shared.TableCollectionName = TableName;
                    _created.Add(_shared);
                }

                var table = ScriptableObject.CreateInstance<StringTable>();
                _created.Add(table);
                table.SharedData = _shared;
                table.LocaleIdentifier = locale.Identifier;
                if (_entries.TryGetValue(locale.Identifier.Code, out Dictionary<string, string> entries))
                {
                    foreach (KeyValuePair<string, string> entry in entries)
                    {
                        table.AddEntry(entry.Key, entry.Value);
                    }
                }

                return Addressables.ResourceManager.CreateCompletedOperation(table as TTable, null);
            }
        }
    }
}
