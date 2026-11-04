using NUnit.Framework;
using SpellBook.Core.Text;
using UnityEngine;

namespace SpellBook.UI.Tests
{
    /// <summary><c>SlotModel</c> e <c>SlotState</c> (Spec do B-016, Design 2; R3 do PRD).</summary>
    public class SlotModelTests
    {
        [Test]
        public void Default_IsEmptySlot()
        {
            SlotModel model = default;

            Assert.That(model.State, Is.EqualTo(SlotState.Empty));
            Assert.That(model.Subject, Is.Null);
            Assert.That(model.HasCharges, Is.False);
            Assert.That(model.KeyLabel.IsEmpty, Is.True);
            Assert.That(model.FailureVisible, Is.False);
            Assert.That(model.IsActive, Is.False);
        }

        [Test]
        public void With_FillsOnlyItsFields()
        {
            var buffer = new TextBuffer();
            TextSpan key = buffer.Append("Q");
            TextSpan failure = buffer.Append("Sem mana");
            var ability = ScriptableObject.CreateInstance<TextTable>();
            try
            {
                SlotModel model = new SlotModel(SlotState.OnCooldown, null, ability)
                    .WithKeyLabel(key)
                    .WithCooldown(0.25f, 6f)
                    .WithCharges(2, 3, 0.5f)
                    .WithFailure("gas.activation.no_resource", failure, true, 4)
                    .WithReadyPulse(7)
                    .WithActivity(true, false, true)
                    .WithAutocast(true, false);

                Assert.That(model.State, Is.EqualTo(SlotState.OnCooldown));
                Assert.That(model.Subject, Is.SameAs(ability));
                Assert.That(buffer.ToString(model.KeyLabel), Is.EqualTo("Q"));
                Assert.That(model.CooldownFraction, Is.EqualTo(0.25f));
                Assert.That(model.CooldownRemaining, Is.EqualTo(6f));
                Assert.That(model.HasCharges, Is.True);
                Assert.That(model.Charges, Is.EqualTo(2));
                Assert.That(model.MaxCharges, Is.EqualTo(3));
                Assert.That(model.ChargeFraction, Is.EqualTo(0.5f));
                Assert.That(model.FailureKey, Is.EqualTo("gas.activation.no_resource"));
                Assert.That(buffer.ToString(model.FailureText), Is.EqualTo("Sem mana"));
                Assert.That(model.FailureVisible, Is.True);
                Assert.That(model.FailureCount, Is.EqualTo(4));
                Assert.That(model.ReadyPulseCount, Is.EqualTo(7));
                Assert.That(model.IsActive, Is.True);
                Assert.That(model.IsToggledOn, Is.False);
                Assert.That(model.IsChanneling, Is.True);
                Assert.That(model.AutocastAllowed, Is.True);
                Assert.That(model.AutocastEnabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public void With_ClampsFractionsAndNegatives()
        {
            SlotModel model = new SlotModel(SlotState.Ready, null, null)
                .WithCooldown(1.5f, -2f)
                .WithCharges(-1, -3, -0.5f);

            Assert.That(model.CooldownFraction, Is.EqualTo(1f));
            Assert.That(model.CooldownRemaining, Is.EqualTo(0f));
            Assert.That(model.Charges, Is.EqualTo(0));
            Assert.That(model.MaxCharges, Is.EqualTo(0));
            Assert.That(model.ChargeFraction, Is.EqualTo(0f));
        }
    }
}
