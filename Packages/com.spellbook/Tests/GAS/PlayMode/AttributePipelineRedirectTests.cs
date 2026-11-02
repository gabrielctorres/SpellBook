#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Redirecionamento, alteração em curso e alocação do pipeline (B-010, DM1b: CA3, CA6, CA24).</summary>
    public partial class AttributePipelineTests
    {
        [Test]
        public void Processor_Redirect_AppliedBeforeOriginal_WithReport()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition shield = CreateAttribute("Escudo");
            SetProcessors(health, new RedirectFraction { Target = shield, Fraction = 0.5f }, new ScaleDelta { Factor = 2f });
            AbilitySystemComponent asc = CreateManualAsc((health, 100f), (shield, 30f));
            GameplayEffect hit = Instant((health, ModifierOperation.Add, -80f));
            var reports = new List<AttributeChangeReport>();
            asc.OnAttributeChangeApplied += (in AttributeChangeReport report) => reports.Add(report);

            asc.ApplyEffect(hit);

            Assert.That(asc.GetAttributeBaseValue(shield), Is.EqualTo(0f), "escudo");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(20f), "vida");
            Assert.That(reports.Count, Is.EqualTo(2), "relatórios");
            AttributeChangeReport toShield = reports[0];
            Assert.That(toShield.Attribute, Is.SameAs(shield), "a redirecionada vem antes");
            Assert.That(toShield.RedirectedFrom, Is.SameAs(health), "RedirectedFrom");
            Assert.That(toShield.Requested, Is.EqualTo(-40f), "Requested do escudo");
            Assert.That(toShield.Final, Is.EqualTo(-40f), "Final do escudo");
            Assert.That(toShield.Applied, Is.EqualTo(-30f), "Applied do escudo, limitado ao mínimo");
            Assert.That(toShield.Effect, Is.SameAs(hit), "Effect do escudo");
            AttributeChangeReport toHealth = reports[1];
            Assert.That(toHealth.Attribute, Is.SameAs(health), "original");
            Assert.That(toHealth.RedirectedFrom, Is.Null, "a original não tem RedirectedFrom");
            Assert.That(toHealth.Requested, Is.EqualTo(-80f), "Requested");
            Assert.That(toHealth.Final, Is.EqualTo(-80f), "Final: (-80 + 40) x 2");
            Assert.That(toHealth.Applied, Is.EqualTo(-80f), "Applied");
        }

        [Test]
        public void Redirect_RunsTargetProcessors_RepeatGuarded()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition shield = CreateAttribute("Escudo");
            SetProcessors(health, new RedirectFraction { Target = shield, Fraction = 1f });
            SetProcessors(shield, new ScaleDelta { Factor = 0.5f }, new RedirectFraction { Target = health, Fraction = 1f });
            AbilitySystemComponent asc = CreateManualAsc((health, 100f), (shield, 100f));
            LogAssert.Expect(LogType.Warning, new Regex("Vida"));

            asc.ApplyAttributeChange(health, -40f);
            asc.ApplyAttributeChange(health, -40f);

            // A vida manda -40 ao escudo; o escudo corta a -20 e devolve tudo à vida, que já está na cadeia e recebe direto
            Assert.That(asc.GetAttributeBaseValue(shield), Is.EqualTo(100f), "escudo");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(60f), "vida");
            LogAssert.NoUnexpectedReceived(); // aviso único por definição
        }

        [Test]
        public void Redirect_DeeperThanEight_AppliedDirectWithWarning()
        {
            var chain = new AttributeDefinition[10];
            var initial = new (AttributeDefinition, float)[chain.Length];
            for (int i = 0; i < chain.Length; i++)
            {
                chain[i] = CreateAttribute("Elo" + i);
                initial[i] = (chain[i], 100f);
            }

            for (int i = 0; i < chain.Length - 1; i++)
            {
                SetProcessors(chain[i], new RedirectFraction { Target = chain[i + 1], Fraction = 1f });
            }

            AbilitySystemComponent asc = CreateManualAsc(initial);
            LogAssert.Expect(LogType.Warning, new Regex("Elo8"));

            asc.ApplyAttributeChange(chain[0], -10f);

            for (int i = 0; i < 8; i++)
            {
                Assert.That(asc.GetAttributeBaseValue(chain[i]), Is.EqualTo(100f), "Elo" + i);
            }

            Assert.That(asc.GetAttributeBaseValue(chain[8]), Is.EqualTo(90f), "o nono recebe direto, sem os processadores");
            Assert.That(asc.GetAttributeBaseValue(chain[9]), Is.EqualTo(100f), "Elo9");
        }

        [Test]
        public void CodeChange_EmptyContext_NoException()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            SetProcessors(health, new ReadsSource());
            AbilitySystemComponent asc = CreateManualAsc((health, 100f));
            AttributeChangeReport received = default;
            asc.OnAttributeChangeApplied += (in AttributeChangeReport report) => received = report;

            float applied = 0f;
            Assert.DoesNotThrow(() => applied = asc.ApplyAttributeChange(health, -10f));

            Assert.That(applied, Is.EqualTo(-10f), "devolvido");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(90f), "base");
            Assert.That(received.IsValid, Is.True, "relatório");
            Assert.That(received.Effect, Is.Null, "efeito");
            Assert.That(received.Context.Instigator, Is.Null, "instigador");
            Assert.That(received.RedirectedFrom, Is.Null, "RedirectedFrom");
        }

        [Test]
        public void ActiveChangeSource_OnlyDuringApplication()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition mana = CreateAttribute("Mana");
            AbilitySystemComponent asc = CreateManualAsc((health, 100f), (mana, 100f));
            GameplayEffect hit = Instant((health, ModifierOperation.Add, -10f));
            bool before = asc.TryGetActiveChangeSource(out _, out _);
            bool inside = false;
            GameplayEffect insideEffect = null;
            AbilitySystemComponent insideTarget = null;
            GameplayEffect nestedEffect = hit;
            GameplayEffect restoredEffect = null;
            asc.OnAttributeChangeApplied += (in AttributeChangeReport report) =>
            {
                if (report.Attribute != health) return;
                inside = asc.TryGetActiveChangeSource(out insideEffect, out EffectContext context);
                insideTarget = context.Target;
                asc.ApplyAttributeChange(mana, -5f); // pipeline aninhado, por código
                asc.TryGetActiveChangeSource(out restoredEffect, out _);
            };
            asc.OnAttributeChangeApplied += (in AttributeChangeReport report) =>
            {
                if (report.Attribute == mana) asc.TryGetActiveChangeSource(out nestedEffect, out _);
            };

            asc.ApplyEffect(hit);
            bool after = asc.TryGetActiveChangeSource(out GameplayEffect afterEffect, out EffectContext afterContext);

            Assert.That(before, Is.False, "antes da aplicação");
            Assert.That(inside, Is.True, "durante a aplicação");
            Assert.That(insideEffect, Is.SameAs(hit), "efeito durante a aplicação");
            Assert.That(insideTarget, Is.SameAs(asc), "contexto durante a aplicação");
            Assert.That(nestedEffect, Is.Null, "o nível aninhado tem a própria origem");
            Assert.That(restoredEffect, Is.SameAs(hit), "a origem volta ao fim do nível aninhado");
            Assert.That(after, Is.False, "depois da aplicação");
            Assert.That(afterEffect, Is.Null, "efeito fora da aplicação");
            Assert.That(afterContext.Target, Is.Null, "contexto fora da aplicação");
        }

        [Test]
        public void ThreeProcessors_10000Changes_DoNotAllocate()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition shield = CreateAttribute("Escudo");
            SetProcessors(health,
                new ScaleDelta { Factor = 0.5f },
                new RedirectFraction { Target = shield, Fraction = 0.5f },
                new AddDelta { Amount = 1f });
            health.MinValue = -1000000f;
            shield.MinValue = -1000000f;
            AbilitySystemComponent asc = CreateManualAsc((health, 0f), (shield, 0f));
            int reports = 0;
            asc.OnAttributeChangeApplied += (in AttributeChangeReport report) => reports++;
            for (int i = 0; i < 10; i++) asc.ApplyAttributeChange(health, -4f); // aquecimento

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++) asc.ApplyAttributeChange(health, -4f);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(reports, Is.EqualTo(20020), "a redirecionada e a original por chamada");
        }

        [Serializable]
        private sealed class RedirectFraction : AttributeChangeProcessor
        {
            public AttributeDefinition Target;
            public float Fraction;

            public override void Process(ref AttributeChange change, in EffectContext context, AttributeChangeQueue queue)
            {
                if (!queue.TryGetState(Target, out _)) return;
                float moved = change.Delta * Fraction;
                queue.Redirect(Target, moved);
                change.Delta -= moved;
            }
        }

        /// <summary>Lê o efeito e as origens do contexto como um processador de dano leria, sem conferir nulo no contexto.</summary>
        [Serializable]
        private sealed class ReadsSource : AttributeChangeProcessor
        {
            public override void Process(ref AttributeChange change, in EffectContext context, AttributeChangeQueue queue)
            {
                bool fromEffect = change.Effect != null;
                bool hasInstigator = context.Instigator != null;
                if (fromEffect || hasInstigator || context.Level != 0 || queue.Owner == null) change.Delta = 0f;
            }
        }
    }
}
#endif
