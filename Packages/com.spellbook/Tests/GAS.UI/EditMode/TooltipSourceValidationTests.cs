using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Validação das fontes de tooltip do GAS (Design 6, R14; CA16): sem assunto configurado e sem
    /// <see cref="UIItemContext"/> no objeto validado, erro no campo do assunto, com o objeto clicável.
    /// </summary>
    public class TooltipSourceValidationTests
    {
        private const string AbilityError =
            "Fonte sem habilidade e sem UIItemContext no objeto. Escolha a habilidade ou use o tooltip num item de lista.";

        private const string EffectError =
            "Fonte sem efeito e sem UIItemContext no objeto. Escolha o efeito ou use o tooltip num item de lista.";

        private const string AttributeError =
            "Fonte sem atributo e sem UIItemContext no objeto. Escolha o atributo ou use o tooltip num item de lista.";

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
            foreach (Object item in _created.Where(o => o != null)) Object.DestroyImmediate(item);
            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void AbilitySource_NoAbility_NoItemContext_IsError()
        {
            AssertErrorEverywhereWithoutItem(new AbilityTooltipSource(), "source.ability", AbilityError);
        }

        [Test]
        public void EffectSource_NoEffect_NoItemContext_IsError()
        {
            AssertErrorEverywhereWithoutItem(new EffectTooltipSource(), "source.effect", EffectError);
        }

        [Test]
        public void AttributeSource_NoAttribute_NoItemContext_IsError()
        {
            AssertErrorEverywhereWithoutItem(new AttributeTooltipSource(), "source.attribute", AttributeError);
        }

        [Test]
        public void Sources_EmptyWithItemContext_HaveNoMessages()
        {
            Component trigger = Host("Slot", withItem: true);

            Assert.That(Validate(new AbilityTooltipSource(), trigger).Messages, Is.Empty, "o assunto vem do item");
            Assert.That(Validate(new EffectTooltipSource(), trigger).Messages, Is.Empty, "o assunto vem do item");
            Assert.That(Validate(new AttributeTooltipSource(), trigger).Messages, Is.Empty, "o assunto vem do item");
        }

        [Test]
        public void Sources_Configured_HaveNoMessages()
        {
            Component trigger = Host("Item", withItem: false);
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);

            Assert.That(Validate(new AbilityTooltipSource { Ability = _gas.CreateAbility() }, trigger).Messages, Is.Empty);
            Assert.That(Validate(new EffectTooltipSource { Effect = effect }, trigger).Messages, Is.Empty);
            Assert.That(Validate(new AttributeTooltipSource { Attribute = _gas.CreateAttribute("Força") }, trigger).Messages,
                Is.Empty);
        }

        // Sem objeto, num objeto sem item e num filho de um item (o item conta só no próprio objeto): o mesmo erro
        private void AssertErrorEverywhereWithoutItem(UISourceBase source, string propertyPath, string text)
        {
            Component plain = Host("Plain", withItem: false);
            Component child = Host("Child", withItem: false);
            child.transform.SetParent(Host("Parent", withItem: true).transform, false);

            foreach (Object context in new Object[] { null, plain, child })
            {
                ValidationResult result = Validate(source, context);
                Assert.That(result.Messages.Select(m => m.Message), Is.EqualTo(new[] { text }), $"contexto {Name(context)}");
                Assert.That(result.Messages[0].Severity, Is.EqualTo(ValidationSeverity.Error));
                Assert.That(result.Messages[0].PropertyPath, Is.EqualTo(propertyPath));
                Assert.That(result.Messages[0].Context, Is.SameAs(context), "a mensagem leva ao objeto validado");
            }
        }

        private static ValidationResult Validate(UISourceBase source, Object context)
        {
            var result = new ValidationResult(context);
            source.Validate(result, "source");
            return result;
        }

        // Componente que faz o papel do gatilho: o validado, no objeto com ou sem UIItemContext
        private Component Host(string name, bool withItem)
        {
            var host = new GameObject(name);
            _created.Add(host);
            if (withItem)
            {
                host.AddComponent<UIItemContext>();
            }

            return host.transform;
        }

        private static string Name(Object context) => context != null ? context.name : "nulo";
    }
}
