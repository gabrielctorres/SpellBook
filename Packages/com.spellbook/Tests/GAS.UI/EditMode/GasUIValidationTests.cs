using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Regras de validação do lado do GAS (R17; CA16, com a parte da UI do UI13), com o objeto clicável, e a linha de
    /// resumo das fontes no Inspector do binder (R16).
    /// </summary>
    public class GasUIValidationTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void BarSource_NoAttribute_IsError()
        {
            ValidationResult result = ValidateSource(new AttributeBarSource());

            AssertSingle(result, ValidationSeverity.Error, "source.current", "Fonte sem atributo. Escolha o atributo lido do dono.");
        }

        [Test]
        public void TextSource_NoAttribute_IsError()
        {
            ValidationResult result = ValidateSource(new AttributeTextSource());

            AssertSingle(result, ValidationSeverity.Error, "source.current", "Fonte sem atributo. Escolha o atributo lido do dono.");
        }

        [Test]
        public void AttributeSource_MaxEqualsCurrent_IsWarning()
        {
            AttributeDefinition vida = _gas.CreateAttribute("Vida");

            ValidationResult result = ValidateSource(new AttributeBarSource { Current = vida, Max = vida });

            AssertSingle(result, ValidationSeverity.Warning, "source.max",
                "O máximo é o próprio atributo, e a barra fica sempre cheia. Escolha outro atributo ou deixe vazio para usar o máximo efetivo.");
        }

        [Test]
        public void AttributeSource_Valid_HasNoMessages()
        {
            AttributeDefinition vida = _gas.CreateAttribute("Vida");
            AttributeDefinition vidaMax = _gas.CreateAttribute("VidaMax");

            Assert.That(ValidateSource(new AttributeBarSource { Current = vida, Max = vidaMax }).Messages, Is.Empty);
            Assert.That(ValidateSource(new AttributeBarSource { Current = vida }).Messages, Is.Empty);
            Assert.That(ValidateSource(new AttributeTextSource { Current = vida }).Messages, Is.Empty);
        }

        [Test]
        public void TagSource_TagModeWithoutTag_IsError()
        {
            ValidationResult result = ValidateSource(new TagSource { Mode = TagSourceMode.Tag });

            AssertSingle(result, ValidationSeverity.Error, "source.tag", "Modo Tag sem tag. Escolha a tag conferida no dono.");
        }

        [Test]
        public void TagSource_QueryModeEmpty_IsError()
        {
            GameplayTag atordoado = _gas.CreateTag("Atordoado");

            ValidationResult result = ValidateSource(new TagSource { Mode = TagSourceMode.Query, Tag = atordoado });

            AssertSingle(result, ValidationSeverity.Error, "source.query",
                "Modo Query com a consulta vazia. Preencha Todas, Alguma ou Nenhuma.");
        }

        [Test]
        public void TagSource_Valid_HasNoMessages()
        {
            GameplayTag atordoado = _gas.CreateTag("Atordoado");
            var query = new TagSource { Mode = TagSourceMode.Query };
            query.Query.All.Add(atordoado);

            Assert.That(ValidateSource(new TagSource { Mode = TagSourceMode.Tag, Tag = atordoado }).Messages, Is.Empty);
            Assert.That(ValidateSource(query).Messages, Is.Empty);
        }

        [Test]
        public void BarBinder_SourceWithoutAttribute_PointsAtBinderSourcePath()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            var binder = asc.gameObject.AddComponent<BarBinder>();
            binder.SetView(asc.gameObject.AddComponent<BarRecorder>());
            binder.SetSource(new AttributeBarSource());

            IReadOnlyList<ValidationMessage> messages = SpellBookEditor.CollectMessages(new Object[] { binder });

            Assert.That(messages.Select(m => m.Message), Is.EqualTo(new[] { "Fonte sem atributo. Escolha o atributo lido do dono." }));
            Assert.That(messages[0].Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(messages[0].PropertyPath, Is.EqualTo("source.current"));
            Assert.That(messages[0].Context, Is.SameAs(binder), "a mensagem precisa apontar o binder, para o clique");
        }

        [Test]
        public void Rules_RuleWithoutStyle_IsError()
        {
            FloatingTextRules rules = Rules(Track(ScriptableObject.CreateInstance<FloatingTextStyle>()), null);

            IReadOnlyList<ValidationMessage> messages = SpellBookEditor.CollectMessages(new Object[] { rules });

            Assert.That(messages.Select(m => m.Message), Is.EqualTo(new[] { "Regra sem estilo em rules (posição 1). Escolha o estilo do número." }));
            Assert.That(messages[0].Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(messages[0].PropertyPath, Is.EqualTo("rules.Array.data[1].style"));
            Assert.That(messages[0].Context, Is.SameAs(rules), "a mensagem precisa apontar o asset, para o clique");
        }

        [Test]
        public void Rules_Valid_HasNoMessages()
        {
            FloatingTextRules rules = Rules(Track(ScriptableObject.CreateInstance<FloatingTextStyle>()));

            Assert.That(SpellBookEditor.CollectMessages(new Object[] { rules }), Is.Empty);
            Assert.That(SpellBookEditor.CollectMessages(new Object[] { Rules() }), Is.Empty);
        }

        [Test]
        public void Sources_SummaryLine()
        {
            AttributeDefinition vida = _gas.CreateAttribute("Vida");
            AttributeDefinition vidaMax = _gas.CreateAttribute("VidaMax");
            FloatingTextRules rules = Rules();
            rules.name = "Regras";

            Assert.That(Summary(new AttributeBarSource { Current = vida, Max = vidaMax }), Is.EqualTo("Barra · Vida / VidaMax"));
            Assert.That(Summary(new AttributeTextSource { Current = vida }), Is.EqualTo("Texto · Vida"));
            Assert.That(Summary(new TagSource { Mode = TagSourceMode.Tag }), Is.EqualTo("Indicador · Tag"));
            Assert.That(Summary(new FloatingTextSource { Rules = rules }), Is.EqualTo("Números · Regras"));
        }

        private static ValidationResult ValidateSource(UISourceBase source)
        {
            var result = new ValidationResult(null);
            source.Validate(result, "source");
            return result;
        }

        private static void AssertSingle(ValidationResult result, ValidationSeverity severity, string propertyPath, string text)
        {
            Assert.That(result.Messages.Select(m => m.Message), Is.EqualTo(new[] { text }));
            Assert.That(result.Messages[0].Severity, Is.EqualTo(severity));
            Assert.That(result.Messages[0].PropertyPath, Is.EqualTo(propertyPath));
        }

        // Asset de regras com uma regra por estilo, na ordem; estilo nulo deixa a regra sem estilo
        private FloatingTextRules Rules(params FloatingTextStyle[] styles)
        {
            FloatingTextRules rules = Track(ScriptableObject.CreateInstance<FloatingTextRules>());
            var serialized = new SerializedObject(rules);
            SerializedProperty list = serialized.FindProperty("rules");
            list.arraySize = styles.Length;
            for (int i = 0; i < styles.Length; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("style").objectReferenceValue = styles[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return rules;
        }

        private string Summary(UISourceBase source)
        {
            SourceHolder holder = Track(ScriptableObject.CreateInstance<SourceHolder>());
            holder.source = source;
            return SummaryFormatter.Format(new SerializedObject(holder).FindProperty(nameof(SourceHolder.source)));
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        public sealed class BarRecorder : MonoBehaviour, IView<BarModel>
        {
            public void Render(in BarModel model)
            {
            }
        }
    }

    internal sealed class SourceHolder : ScriptableObject
    {
        [SerializeReference] internal UISourceBase source;
    }
}
