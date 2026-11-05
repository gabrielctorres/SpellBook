using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Aviso do efeito visível sem ícone (Spec do B-018, Design 4; R14 e CA19 do PRD): só o efeito que aparece na lista
    /// de efeitos, com duração ou infinito e não oculto, avisa que cai no ícone padrão.
    /// </summary>
    public class EffectIconValidationTests
    {
        private const string Warning = "Efeito visível com duração e sem ícone: na lista de efeitos, cai no ícone padrão. " +
            "Escolha um ícone ou marque Is Hidden.";

        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
        }

        [Test]
        public void VisibleEffectWithDurationWithoutIcon_Warning_InstantOrHiddenNot()
        {
            GameplayEffect timed = _gas.CreateEffect(EffectDurationPolicy.HasDuration, 5f);
            GameplayEffect infinite = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            GameplayEffect withIcon = _gas.CreateEffect(EffectDurationPolicy.HasDuration, 5f);
            GasFactory.SetObjectField(withIcon, "icon", _gas.CreateSprite());
            GameplayEffect instant = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            GameplayEffect hidden = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            var so = new SerializedObject(hidden);
            so.FindProperty("isHidden").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssertIconWarning(timed);
            AssertIconWarning(infinite);
            Assert.That(SpellBookEditor.CollectMessages(new Object[] { withIcon }), Is.Empty, "com ícone");
            Assert.That(SpellBookEditor.CollectMessages(new Object[] { instant }), Is.Empty, "instantâneo não aparece na lista");
            Assert.That(SpellBookEditor.CollectMessages(new Object[] { hidden }), Is.Empty, "oculto não aparece na lista");
        }

        // Um aviso só, no campo do ícone e com o efeito como contexto, para o clique
        private static void AssertIconWarning(GameplayEffect effect)
        {
            IReadOnlyList<ValidationMessage> messages = SpellBookEditor.CollectMessages(new Object[] { effect });

            Assert.That(messages.Select(m => m.Message).ToArray(), Is.EqualTo(new[] { Warning }), effect.DurationPolicy.ToString());
            Assert.That(messages[0].Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(messages[0].PropertyPath, Is.EqualTo("icon"));
            Assert.That(messages[0].Context, Is.SameAs(effect), "a mensagem precisa apontar o efeito, para o clique");
        }
    }
}
