using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core.Text;

namespace SpellBook.Core.Tests.Text
{
    /// <summary><c>LocalizedText</c>, <c>ITextProvider</c> e <c>TextKey</c> (Spec do B-008, Design 1; ADR 0038, itens 1 e 3; CA9).</summary>
    public class LocalizedTextTests
    {
        [Test]
        public void LocalizedText_NullProvider_ReturnsText()
        {
            var withKey = new LocalizedText("gas.ability.fireball", "Bola de fogo");
            var noText = new LocalizedText("gas.ability.fireball", null);

            Assert.That(withKey.Resolve(null), Is.EqualTo("Bola de fogo"));
            Assert.That(noText.Resolve(null), Is.Empty);
            Assert.That(noText.Text, Is.Empty);
            Assert.That(default(LocalizedText).Resolve(null), Is.Empty);
        }

        [Test]
        public void LocalizedText_KeyFound_ProviderText_KeyMissing_Fallback()
        {
            var provider = new TestTextProvider("gas.ability.fireball", "Fireball");
            var found = new LocalizedText("gas.ability.fireball", "Bola de fogo");
            var missing = new LocalizedText("gas.ability.frost", "Raio de gelo");
            var noKey = new LocalizedText(null, "Sem chave");

            Assert.That(found.Resolve(provider), Is.EqualTo("Fireball"));
            Assert.That(missing.Resolve(provider), Is.EqualTo("Raio de gelo"));
            Assert.That(noKey.Resolve(provider), Is.EqualTo("Sem chave"));
        }

        [Test]
        public void LocalizedText_IsEmpty_OnlyWithoutKeyAndText()
        {
            Assert.That(default(LocalizedText).IsEmpty, Is.True);
            Assert.That(new LocalizedText(string.Empty, string.Empty).IsEmpty, Is.True);
            Assert.That(new LocalizedText("gas.a.b", null).IsEmpty, Is.False);
            Assert.That(new LocalizedText(null, "x").IsEmpty, Is.False);
            Assert.That(new LocalizedText("gas.a.b", "x").Key, Is.EqualTo("gas.a.b"));
        }

        [Test]
        public void TextKey_Valid_gas_activation_no_charges_Invalid_Upper_Space_SingleSegment()
        {
            Assert.That(TextKey.IsValid("gas.activation.no_charges"), Is.True);
            Assert.That(TextKey.IsValid("ui.hud.slot_2"), Is.True);
            Assert.That(TextKey.IsValid("gas.x"), Is.True);

            Assert.That(TextKey.IsValid("gas.Activation.no_charges"), Is.False, "maiúscula");
            Assert.That(TextKey.IsValid("gas.activation.no charges"), Is.False, "espaço");
            Assert.That(TextKey.IsValid("gas"), Is.False, "um segmento");
            Assert.That(TextKey.IsValid("gas..x"), Is.False, "segmento vazio");
            Assert.That(TextKey.IsValid(".gas.x"), Is.False, "começa com ponto");
            Assert.That(TextKey.IsValid("gas.x."), Is.False, "termina com ponto");
            Assert.That(TextKey.IsValid("gas.ação"), Is.False, "acento");
            Assert.That(TextKey.IsValid(string.Empty), Is.False);
            Assert.That(TextKey.IsValid(null), Is.False);
        }

        /// <summary>Provedor de teste: uma chave fixa, cultura invariante.</summary>
        private sealed class TestTextProvider : ITextProvider
        {
            private readonly Dictionary<string, string> _texts = new Dictionary<string, string>();

            public TestTextProvider(string key, string text)
            {
                _texts[key] = text;
            }

            public IFormatProvider Culture => CultureInfo.InvariantCulture;

            public string GetText(string key, string fallback)
            {
                if (string.IsNullOrEmpty(key)) return fallback;
                return _texts.TryGetValue(key, out string text) ? text : fallback;
            }

            public event Action Changed
            {
                add { }
                remove { }
            }
        }
    }
}
