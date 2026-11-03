using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>Ações de fluxo <c>Chance</c> e <c>ForEachTarget</c> e a <c>RemoveEffectAction</c> (B-012, EV5; CA14, CA18, CA19).</summary>
    public class FlowAndRemoveActionsTests
    {
        private static readonly object Owner = new object();

        private GasFactory _gas;

        [SetUp]
        public void SetUp() => _gas = new GasFactory();

        [TearDown]
        public void TearDown() => _gas.Dispose();

        [TestCase(0.2f, 1)]
        [TestCase(0.5f, 0)]
        public void Chance30_At02Runs_At05Not(float roll, int expectedRuns)
        {
            AbilitySystemComponent caster = _gas.CreateAsc();
            caster.SetRandomSource(new FixedRandomSource(roll));
            var child = new CountAction();
            var chance = new ChanceAction
            {
                Probability = ScalableFloat.Constant(0.3f),
                Actions = new List<GameplayAction> { child },
            };

            caster.RunActions(new GameplayAction[] { chance }, GasFactory.CreateActionContext(caster, Vector3.zero), Owner);

            Assert.That(child.Runs, Is.EqualTo(expectedRuns), $"sorteio {roll} com chance de 0,3");
        }

        [Test]
        public void ForEach3_RunsThreeTimes_CurrentTargetDefault_ChildChangesSetSafe()
        {
            AbilitySystemComponent caster = _gas.CreateAsc();
            AbilitySystemComponent first = _gas.CreateAsc();
            AbilitySystemComponent second = _gas.CreateAsc();
            AbilitySystemComponent third = _gas.CreateAsc();
            var record = new RecordTargetsAction();
            var forEach = new ForEachTargetAction
            {
                // A filha que mexe nos alvos do evento roda antes da que registra, a cada repetição
                Actions = new List<GameplayAction> { new ReplaceEventTargetsAction(caster), record },
            };

            caster.RunActions(new GameplayAction[] { forEach },
                GasFactory.CreateActionContext(caster, Vector3.zero, first, second, third), Owner);

            Assert.That(record.Runs, Is.EqualTo(3), "uma rodada das filhas por alvo do conjunto");
            Assert.That(record.Seen, Is.EqualTo(new[] { first, second, third }),
                "sem seletor, cada filha recebe só o alvo corrente, e mudar os alvos do evento não muda o conjunto");
        }

        [Test]
        public void RemoveEffect_ByAssetAndTag_NoneDoesNothing()
        {
            GameplayTag burn = _gas.CreateTag("Status.Burn");
            AbilitySystemComponent caster = _gas.CreateAsc();
            AbilitySystemComponent enemy = _gas.CreateAsc();
            GameplayEffect poison = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            poison.CanStack = true;
            poison.MaxStacks = 5;
            GameplayEffect burning = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f, burn);
            enemy.ApplyEffect(poison);
            enemy.ApplyEffect(poison);
            ActiveEffectInstance burnInstance = enemy.ApplyEffect(burning);
            var afterNone = new RecordRemovedAction();
            var afterAsset = new RecordRemovedAction();
            var afterTag = new RecordRemovedAction();

            caster.RunActions(new GameplayAction[]
            {
                new RemoveEffectAction(), afterNone,
                new RemoveEffectAction { Effect = poison }, afterAsset,
            }, GasFactory.CreateActionContext(caster, Vector3.zero, enemy), Owner);

            Assert.That(afterNone.Removed, Is.EqualTo(0), "sem efeito e sem tag, nada sai");
            Assert.That(afterAsset.Removed, Is.EqualTo(2), "pelo asset: as duas pilhas do veneno");
            Assert.That(enemy.ActiveEffects, Is.EqualTo(new[] { burnInstance }), "a remoção pelo asset tirou outro efeito");

            caster.RunActions(new GameplayAction[] { new RemoveEffectAction { Tag = burn }, afterTag },
                GasFactory.CreateActionContext(caster, Vector3.zero, enemy), Owner);

            Assert.That(afterTag.Removed, Is.EqualTo(1), "pela tag: a pilha da queimadura");
            Assert.That(enemy.ActiveEffects, Is.Empty);
        }

        private sealed class FixedRandomSource : IRandomSource
        {
            private readonly float _value;

            public FixedRandomSource(float value) => _value = value;

            public float Value() => _value;

            public int Range(int maxExclusive) => 0;
        }

        [Serializable]
        private sealed class CountAction : GameplayAction
        {
            public int Runs;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Runs++;
                return ActionStatus.Done;
            }
        }

        /// <summary>Conta as chamadas e copia os alvos resolvidos para <see cref="Seen"/>.</summary>
        [Serializable]
        private sealed class RecordTargetsAction : GameplayAction
        {
            public readonly List<AbilitySystemComponent> Seen = new List<AbilitySystemComponent>();
            public int Runs;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Runs++;
                Seen.AddRange(context.Targets);
                return ActionStatus.Done;
            }
        }

        /// <summary>Troca os alvos do evento por <c>replacement</c>, como uma <c>TargetingAction</c> em Replace.</summary>
        [Serializable]
        private sealed class ReplaceEventTargetsAction : GameplayAction
        {
            private readonly AbilitySystemComponent _replacement;

            public ReplaceEventTargetsAction(AbilitySystemComponent replacement) => _replacement = replacement;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                context.EventTargets.Clear();
                context.EventTargets.Add(_replacement);
                return ActionStatus.Done;
            }
        }

        [Serializable]
        private sealed class RecordRemovedAction : GameplayAction
        {
            public int Removed = -1;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Removed = context.LastRemovedStacks;
                return ActionStatus.Done;
            }
        }
    }
}
