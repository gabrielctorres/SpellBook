using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Text;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Description;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Validação da descrição da habilidade: elemento nulo, texto vazio, nome do template fora da tabela, custo, recarga e
    /// cargas ausentes, e a mesma chave com textos diferentes, cada mensagem no campo do elemento (Spec do B-008, Design 7;
    /// R18 e CA14).
    /// </summary>
    public class DescriptionValidationTests
    {
        private const string Element0 = "Description.Elements.Array.data[0]";
        private const string Element1 = "Description.Elements.Array.data[1]";

        private GasFactory _gas;

        [SetUp]
        public void SetUp() => _gas = new GasFactory();

        [TearDown]
        public void TearDown() => _gas.Dispose();

        [Test]
        public void NullElement_Error()
        {
            GameplayAbility ability = WithElements(new TextElement { Text = new LocalizedText(null, "Ok") }, null);

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, Element1);
        }

        [Test]
        public void EmptyTextElement_Error()
        {
            GameplayAbility ability = WithElements(new TextElement());

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, Element0 + ".text");
        }

        [Test]
        public void TemplateUnknownName_Error_NamesIt()
        {
            GameplayAbility ability = WithElements(Template(null, "Causa {danno} de dano"));
            ability.Values.Add("dano", ScalableFloat.Constant(50f));

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Error, Element0 + ".template");
            Assert.That(message.Message, Does.Contain("danno"));
        }

        [Test]
        public void TemplateKnownNamesAndShortcuts_NoMessages()
        {
            GameplayAbility ability = WithElements(Template(null, "Causa {dano} ({@cooldown}, {@cost}) {não é nome}"));
            ability.Values.Add("dano", ScalableFloat.FromArray(50f, 75f, 100f));

            Assert.That(Messages(ability), Is.Empty);
        }

        [Test]
        public void CostElementWithoutCost_Warns()
        {
            GameplayAbility ability = WithElements(new CostElement());

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Warning, Element0);
        }

        [Test]
        public void CooldownElementWithoutCooldown_Warns()
        {
            GameplayAbility ability = WithElements(new CooldownElement());

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Warning, Element0);
        }

        [Test]
        public void ChargesElementWithoutCharges_Warns()
        {
            GameplayAbility ability = WithElements(new ChargesElement());

            ValidationMessage message = Single(ability);

            AssertClickable(ability, message, ValidationSeverity.Warning, Element0);
        }

        [Test]
        public void SameKeyDifferentDefaults_Warns_SameDefault_NoMessage()
        {
            GameplayAbility different = WithElements(
                new TextElement { Text = new LocalizedText("game.ability.fire", "Fogo") },
                Template("game.ability.fire", "Chamas"));
            GameplayAbility same = WithElements(
                new TextElement { Text = new LocalizedText("game.ability.fire", "Fogo") },
                Template("game.ability.fire", "Fogo"),
                new TextElement { Text = new LocalizedText("game.ability.ice", "Gelo") });

            ValidationMessage message = Single(different);

            AssertClickable(different, message, ValidationSeverity.Warning, Element1 + ".template");
            Assert.That(message.Message, Does.Contain("game.ability.fire"));
            Assert.That(Messages(same), Is.Empty);
        }

        [Test]
        public void ValidDescription_NoMessages()
        {
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(10f);
            ability.CostEffect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            ability.Charges.Enabled = true;
            ability.Values.Add("dano", ScalableFloat.Constant(50f));
            SetElements(ability,
                new TextElement { Text = new LocalizedText("game.ability.fire", "Fogo") },
                Template(null, "Causa {dano}"),
                new CostElement(),
                new CooldownElement(),
                new ChargesElement(),
                new AttributeElement(),
                new EffectModifiersElement(),
                new DurationElement());

            Assert.That(Messages(ability), Is.Empty);
        }

        [Test]
        public void CustomElement_ValidateCalledWithItsPath()
        {
            var custom = new RecordingElement();
            GameplayAbility ability = WithElements(new TextElement { Text = new LocalizedText(null, "Ok") }, custom);

            ValidationMessage message = Single(ability);

            Assert.That(custom.Owner, Is.SameAs(ability));
            Assert.That(custom.Path, Is.EqualTo(Element1));
            AssertClickable(ability, message, ValidationSeverity.Warning, Element1);
        }

        [Test]
        public void ProjectValidator_ReportsAbilityAsContext()
        {
            GameplayAbility ability = WithElements(new TextElement());
            var messages = new List<ValidationMessage>();

            ProjectValidator.Validate(new Object[] { ability }, messages);

            ValidationMessage message = messages.Single();
            Assert.That(message.Context, Is.SameAs(ability));
            Assert.That(message.PropertyPath, Is.EqualTo(Element0 + ".text"));
        }

        private GameplayAbility WithElements(params DescriptionElement[] elements)
        {
            GameplayAbility ability = _gas.CreateAbility();
            SetElements(ability, elements);
            return ability;
        }

        private static void SetElements(GameplayAbility ability, params DescriptionElement[] elements)
        {
            ability.Description = new AbilityDescription();
            ability.Description.Elements.AddRange(elements);
        }

        private static TemplateElement Template(string key, string text)
        {
            return new TemplateElement { Template = new LocalizedText(key, text) };
        }

        private static IReadOnlyList<ValidationMessage> Messages(GameplayAbility ability)
        {
            var result = new ValidationResult(ability);
            ability.Validate(result);
            return result.Messages;
        }

        private static ValidationMessage Single(GameplayAbility ability)
        {
            IReadOnlyList<ValidationMessage> messages = Messages(ability);
            Assert.That(messages.Select(m => $"{m.Severity}: {m.Message} ({m.PropertyPath})").ToList(), Has.Count.EqualTo(1));
            return messages[0];
        }

        // O caminho leva ao campo no Inspector da habilidade: a mensagem é clicável
        private static void AssertClickable(GameplayAbility ability, ValidationMessage message, ValidationSeverity severity,
            string propertyPath)
        {
            Assert.That(message.Severity, Is.EqualTo(severity), message.Message);
            Assert.That(message.Context, Is.SameAs(ability));
            Assert.That(message.PropertyPath, Is.EqualTo(propertyPath));
            Assert.That(new SerializedObject(ability).FindProperty(propertyPath), Is.Not.Null, "o campo existe na habilidade");
        }

        /// <summary>Elemento próprio que guarda o caminho e o dono recebidos e deixa um aviso nele.</summary>
        [Serializable]
        private sealed class RecordingElement : DescriptionElement
        {
            [NonSerialized] public string Path;
            [NonSerialized] public GameplayAbility Owner;

            public override void Describe(DescriptionContext context)
            {
            }

            public override void Validate(ValidationResult result, string propertyPath, GameplayAbility owner)
            {
                Path = propertyPath;
                Owner = owner;
                result.AddWarning("Elemento próprio validado.", propertyPath);
            }
        }
    }
}
