using System;
using System.Collections.Generic;
using System.Globalization;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Description;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Prévia da descrição da habilidade no Inspector (Design 7 do B-008): uma linha de texto simples por
    /// <see cref="DescriptionLine"/>, no nível de prévia, na cultura do campo "Cultura" (padrão a do Editor, guardada na
    /// sessão) e com o provedor de texto opcional. Refaz ao editar o asset e ao trocar nível, cultura ou provedor.
    /// </summary>
    public sealed class DescriptionPreviewElement : VisualElement
    {
        /// <summary>Chave do <see cref="SessionState"/> com o nome da cultura da prévia.</summary>
        public const string CultureSessionKey = "SpellBook.GAS.DescriptionPreview.Culture";
        public const string CultureFieldName = "spellbook-description-culture";
        public const string ProviderFieldName = "spellbook-description-provider";
        public const string EmptyName = "spellbook-description-empty";
        public const string LineClass = "spellbook-description-line";
        public const string InvalidLineClass = "spellbook-description-line--invalid";

        private readonly SerializedObject _serializedObject;
        private readonly TextField _culture;
        private readonly ObjectField _provider;
        private readonly VisualElement _lines;
        private readonly Label _empty;
        private readonly DescriptionContext _context = new DescriptionContext();
        private readonly PreviewTextProvider _text = new PreviewTextProvider();
        private readonly TextBuffer _buffer = new TextBuffer();

        public DescriptionPreviewElement(SerializedObject serializedObject)
        {
            _serializedObject = serializedObject ?? throw new ArgumentNullException(nameof(serializedObject));

            _culture = new TextField("Cultura") { name = CultureFieldName };
            _culture.SetValueWithoutNotify(SessionState.GetString(CultureSessionKey, CultureInfo.CurrentCulture.Name));
            _culture.RegisterValueChangedCallback(evt => SetCulture(evt.newValue));
            _provider = new ObjectField("Provedor de texto")
            {
                name = ProviderFieldName,
                objectType = typeof(TextProviderAsset),
                allowSceneObjects = false
            };
            _provider.RegisterValueChangedCallback(evt => SetProvider(evt.newValue as TextProviderAsset));
            _lines = new VisualElement();
            _empty = new Label("A descrição está vazia. Adicione um elemento em Description.") { name = EmptyName };

            Add(_culture);
            Add(_provider);
            Add(_lines);
            Add(_empty);

            _text.Culture = ParseCulture(_culture.value);
            _context.Text = _text;
            this.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            Refresh();
        }

        /// <summary>Nível em que a prévia avalia os valores.</summary>
        public int Level { get; private set; } = 1;

        /// <summary>Muda o nível e refaz a prévia, só se o nível mudou.</summary>
        public void SetLevel(int level)
        {
            if (level == Level) return;

            Level = level;
            Refresh();
        }

        /// <summary>
        /// Muda a cultura dos números pelo nome (vazio: invariante; desconhecido: a do Editor), guarda o nome na sessão e
        /// refaz a prévia.
        /// </summary>
        public void SetCulture(string cultureName)
        {
            cultureName = cultureName ?? string.Empty;
            _culture.SetValueWithoutNotify(cultureName);
            SessionState.SetString(CultureSessionKey, cultureName);
            _text.Culture = ParseCulture(cultureName);
            Refresh();
        }

        /// <summary>Muda o provedor dos textos por chave (nulo: textos padrão) e refaz a prévia. O provedor não é alterado.</summary>
        public void SetProvider(TextProviderAsset provider)
        {
            _provider.SetValueWithoutNotify(provider);
            _text.Inner = provider;
            Refresh();
        }

        /// <summary>Descreve a habilidade de novo e refaz as linhas.</summary>
        public void Refresh()
        {
            _lines.Clear();
            if (_serializedObject.targetObject is GameplayAbility ability)
            {
                _context.Reset(ability, Level);
                ability.Describe(_context);
                List<DescriptionLine> lines = _context.Lines;
                for (int i = 0; i < lines.Count; i++)
                {
                    _lines.Add(LineLabel(lines[i]));
                }
            }

            _empty.style.display = _lines.childCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private Label LineLabel(in DescriptionLine line)
        {
            _buffer.Clear();
            DescriptionWriter.WriteLine(line, _context, _buffer);
            var label = new Label(_buffer.ToString()) { enableRichText = false };
            label.AddToClassList(LineClass);
            if ((line.Flags & DescriptionLineFlags.Invalid) != 0) label.AddToClassList(InvalidLineClass);
            return label;
        }

        private static CultureInfo ParseCulture(string name)
        {
            try
            {
                return CultureInfo.GetCultureInfo(name ?? string.Empty);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.CurrentCulture;
            }
        }

        /// <summary>Textos pelo provedor escolhido (ou os padrão) e números na cultura do campo da prévia.</summary>
        private sealed class PreviewTextProvider : ITextProvider
        {
            public TextProviderAsset Inner;

            public IFormatProvider Culture { get; set; } = CultureInfo.InvariantCulture;

            public event Action Changed
            {
                add { }
                remove { }
            }

            public string GetText(string key, string fallback)
            {
                return Inner != null ? Inner.GetText(key, fallback) : fallback;
            }
        }
    }
}
