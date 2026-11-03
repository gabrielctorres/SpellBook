#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Triggers;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Eventos de gameplay e gatilhos de habilidade (EV10: CA21, CA22; cenários C13 e C14).</summary>
    public class GameplayEventTriggerTests
    {
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
        public void SendEvent_ReachesTrigger_NoListenerNoAlloc_InvalidTagIgnored()
        {
            GameplayTag hit = _gas.CreateTag("Evento.Acerto");
            GameplayTag other = _gas.CreateTag("Evento.Outro");
            var count = new CountAction();
            GameplayAbility ability = _gas.CreateAbility(count);
            ability.Triggers.Add(new GameplayEventTrigger { Tag = hit });
            AbilitySystemComponent asc = _gas.CreateAsc();
            asc.GiveAbility(ability);
            var received = new List<(GameplayTag, float)>();
            asc.OnGameplayEvent += (GameplayTag tag, in GameplayEventData data) => received.Add((tag, data.Magnitude));

            asc.SendGameplayEvent(hit, new GameplayEventData(asc, null, 3f));
            asc.SendGameplayEvent(other, new GameplayEventData(asc, null, 1f));
            asc.SendGameplayEvent(default, new GameplayEventData(asc, null, 2f));

            Assert.That(count.Count, Is.EqualTo(1), "só o evento da tag do gatilho ativa a habilidade");
            Assert.That(received, Is.EqualTo(new[] { (hit, 3f), (other, 1f) }), "a tag inválida é ignorada");

            // Sem ouvinte e sem gatilho, o envio não faz nada e não aloca
            AbilitySystemComponent silent = _gas.CreateAsc();
            var data = new GameplayEventData(asc, silent, 1f);
            silent.SendGameplayEvent(hit, data);
            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++) silent.SendGameplayEvent(hit, data);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Trigger_UsesCheckOrder_FailureReported_SelfTriggerNoLoop()
        {
            GameplayTag hit = _gas.CreateTag("Evento.Acerto");
            GameplayTag stunned = _gas.CreateTag("Stunned");
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            AbilitySystemComponent asc = _gas.CreateAsc((mana, 5f));
            var count = new CountAction();
            GameplayAbility ability = _gas.CreateAbility(count);
            ability.BlockedTags = new List<GameplayTag> { stunned };
            ability.CostEffect = Cost(mana, 10f);
            ability.Triggers.Add(new GameplayEventTrigger { Tag = hit });
            asc.GiveAbility(ability);
            var failures = new List<AbilityActivationResult>();
            asc.OnAbilityFailed += (failed, failure) => failures.Add(failure.Result);

            // Tags antes do custo, pela ordem da ADR 0022; a falha vai para o evento, sem exceção
            asc.AddTag(stunned);
            Assert.DoesNotThrow(() => asc.SendGameplayEvent(hit, new GameplayEventData(asc, null, 0f)));
            asc.RemoveTag(stunned);
            asc.SendGameplayEvent(hit, new GameplayEventData(asc, null, 0f));

            Assert.That(failures, Is.EqualTo(new[]
            {
                AbilityActivationResult.BlockedByTags,
                AbilityActivationResult.CannotAffordCost
            }));
            Assert.That(count.Count, Is.EqualTo(0));

            // A habilidade que envia ao dono a tag do próprio gatilho roda uma vez e não tenta de novo
            GameplayTag echo = _gas.CreateTag("Evento.Eco");
            var echoCount = new CountAction();
            GameplayAbility self = _gas.CreateAbility(echoCount, new SendGameplayEventAction { Tag = echo, Target = new CasterSelector() });
            self.Triggers.Add(new GameplayEventTrigger { Tag = echo });
            asc.GiveAbility(self);
            failures.Clear();

            asc.SendGameplayEvent(echo, new GameplayEventData(asc, null, 0f));

            Assert.That(echoCount.Count, Is.EqualTo(1));
            Assert.That(failures, Is.Empty, "o disparo para o spec em curso é ignorado, sem AlreadyActive");
        }

        [Test]
        public void Trigger_NestedDepthCappedAt8_WarnsOnce()
        {
            const int chain = 10;
            var tags = new GameplayTag[chain + 1];
            for (int i = 0; i <= chain; i++) tags[i] = _gas.CreateTag("Evento.Passo" + i);
            AbilitySystemComponent asc = _gas.CreateAsc();
            var count = new CountAction();
            for (int i = 0; i < chain; i++)
            {
                GameplayAbility link = _gas.CreateAbility(count, new SendGameplayEventAction { Tag = tags[i + 1], Target = new CasterSelector() });
                link.Triggers.Add(new GameplayEventTrigger { Tag = tags[i] });
                asc.GiveAbility(link);
            }

            LogAssert.Expect(LogType.Warning, new Regex("8"));
            asc.SendGameplayEvent(tags[0], new GameplayEventData(asc, null, 0f));
            asc.SendGameplayEvent(tags[0], new GameplayEventData(asc, null, 0f));

            Assert.That(count.Count, Is.EqualTo(16), "8 disparos aninhados por envio; o aviso sai uma vez só");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TagAddedRemoved_Triggers()
        {
            GameplayTag state = _gas.CreateTag("State");
            GameplayTag stunned = _gas.CreateTag("Stunned", state);
            var added = new CountAction();
            var removed = new CountAction();
            GameplayAbility onAdded = _gas.CreateAbility(added);
            onAdded.Triggers.Add(new TagAddedTrigger { Tag = state });
            GameplayAbility onRemoved = _gas.CreateAbility(removed);
            onRemoved.Triggers.Add(new TagRemovedTrigger { Tag = stunned });
            AbilitySystemComponent asc = _gas.CreateAsc();
            asc.GiveAbility(onAdded);
            asc.GiveAbility(onRemoved);

            asc.AddTag(stunned); // filha de State: dispara o gatilho de State
            asc.AddTag(stunned); // a contagem sobe, mas a tag já estava: não dispara
            asc.RemoveTag(stunned);
            int removedWhileHeld = removed.Count;
            asc.RemoveTag(stunned);

            Assert.That(added.Count, Is.EqualTo(1));
            Assert.That(removedWhileHeld, Is.EqualTo(0));
            Assert.That(removed.Count, Is.EqualTo(1));

            // Retirada a habilidade, o gatilho sai do índice
            asc.RemoveAbility(onAdded);
            asc.AddTag(stunned);
            Assert.That(added.Count, Is.EqualTo(1));
        }

        [Test]
        public void Trigger_PassesEventTargetAsUnit()
        {
            GameplayTag hit = _gas.CreateTag("Evento.Acerto");
            AbilitySystemComponent asc = _gas.CreateAsc();
            AbilitySystemComponent enemy = _gas.CreateAsc();
            enemy.transform.position = new Vector3(4f, 0f, 0f);
            var capture = new CaptureAction();
            GameplayAbility ability = _gas.CreateAbility(capture);
            ability.Triggers.Add(new GameplayEventTrigger { Tag = hit });
            asc.GiveAbility(ability);

            asc.SendGameplayEvent(hit, new GameplayEventData(asc, enemy, 0f));
            Assert.That(capture.Targets, Is.EqualTo(new[] { enemy }));
            Assert.That(capture.Point, Is.EqualTo(enemy.transform.position), "sem ponto, vale a posição da unidade");

            var point = new Vector3(1f, 2f, 3f);
            asc.SendGameplayEvent(hit, new GameplayEventData(asc, enemy, 0f, point));
            Assert.That(capture.Targets, Is.EqualTo(new[] { enemy }));
            Assert.That(capture.Point, Is.EqualTo(point), "o ponto do evento vence a posição da unidade");

            // A ação envia o evento ao ASC de cada alvo, com quem roda a lista como instigador
            var sent = new List<(AbilitySystemComponent, AbilitySystemComponent, float)>();
            enemy.OnGameplayEvent += (GameplayTag tag, in GameplayEventData data) => sent.Add((data.Instigator, data.Target, data.Magnitude));
            GameplayAbility sender = _gas.CreateAbility(new SendGameplayEventAction { Tag = hit, Magnitude = ScalableFloat.Constant(7f) });
            asc.GiveAbility(sender);
            asc.TryActivateAbility(sender, AbilityTarget.AtUnit(enemy));
            Assert.That(sent, Is.EqualTo(new[] { (asc, enemy, 7f) }));
        }

        private GameplayEffect Cost(AttributeDefinition attribute, float amount)
        {
            GameplayEffect cost = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            cost.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(-amount) }
            });
            return cost;
        }

        private sealed class CountAction : GameplayAction
        {
            public int Count;

            public override bool UsesTargets => false;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Count++;
                return ActionStatus.Done;
            }
        }

        private sealed class CaptureAction : GameplayAction
        {
            public readonly List<AbilitySystemComponent> Targets = new List<AbilitySystemComponent>();
            public Vector3 Point;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Targets.Clear();
                Targets.AddRange(context.Targets);
                Point = context.Point;
                return ActionStatus.Done;
            }
        }
    }
}
#endif
