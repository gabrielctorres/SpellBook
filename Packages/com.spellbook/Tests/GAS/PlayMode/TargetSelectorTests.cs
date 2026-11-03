using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Seletores de alvo por ação e alvo padrão do evento (B-012, EV4a; CA11).</summary>
    public class TargetSelectorTests
    {
        private static readonly object Owner = new object();

        private GasFactory _gas;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void SetUp() => _gas = new GasFactory();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _objects.Clear();
            _gas.Dispose();
        }

        [Test]
        public void CasterAndEventTargets_SameList_DifferentAscs()
        {
            AbilitySystemComponent caster = _gas.CreateAsc();
            AbilitySystemComponent enemy = _gas.CreateAsc();
            var damage = new RecordTargetsAction { Target = new EventTargetsSelector() };
            var heal = new RecordTargetsAction { Target = new CasterSelector() };
            var point = new RecordTargetsAction { Target = new PointSelector() };

            caster.RunActions(new GameplayAction[] { damage, heal, point },
                GasFactory.CreateActionContext(caster, Vector3.one, enemy), Owner);

            Assert.That(damage.Seen, Is.EqualTo(new[] { enemy }), "o seletor dos alvos do evento");
            Assert.That(heal.Seen, Is.EqualTo(new[] { caster }), "o seletor de quem lançou");
            Assert.That(point.Seen, Is.Empty, "o seletor de ponto não escolhe unidade");
        }

        [TestCase("ability")]
        [TestCase("impact")]
        [TestCase("effect")]
        public void NoSelector_UsesEventDefault_PerEventTable(string eventName)
        {
            AbilitySystemComponent caster = _gas.CreateAsc();
            AbilitySystemComponent enemy = _gas.CreateAsc();
            var record = new RecordTargetsAction();

            switch (eventName)
            {
                case "ability":
                    // Alvo da habilidade: o que a ação anterior pôs nos alvos do evento
                    GameplayAbility ability = _gas.CreateAbility(new AddEventTargetAction(enemy), record);
                    caster.GiveAbility(ability);
                    caster.TryActivateAbility(ability, Vector3.zero);
                    break;
                case "impact":
                    var go = new GameObject("Projectile");
                    _objects.Add(go);
                    var projectile = go.AddComponent<TestProjectile>();
                    projectile.Setup(null, caster, new List<GameplayAction> { record });
                    projectile.Impact(Vector3.one, enemy);
                    break;
                case "effect":
                    GameplayEffect effect = _gas.CreateDurationEffect(5f);
                    effect.OnApplied.Add(record);
                    enemy.ApplyEffect(effect, caster);
                    break;
            }

            Assert.That(record.Seen, Is.EqualTo(new[] { enemy }));
        }

        [Test]
        public void Selection_DoesNotAllocate()
        {
            AbilitySystemComponent caster = _gas.CreateAsc();
            AbilitySystemComponent enemy = _gas.CreateAsc();
            var counter = new CountTargetsAction();
            var actions = new GameplayAction[]
            {
                new AddEventTargetAction(enemy),
                new CountTargetsAction { Target = new CasterSelector() },
                new CountTargetsAction { Target = new EventTargetsSelector() },
                new CountTargetsAction { Target = new PointSelector() },
                counter,
            };
            caster.RunActions(actions, null, Owner);
            counter.Total = 0;

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++) caster.RunActions(actions, null, Owner);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(counter.Total, Is.EqualTo(10000), "a ação sem seletor viu o alvo do evento em cada execução");
        }

        [Test]
        public void SetRandomSource_ContextReadsAscSource_NullThrows()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            var source = new SeededRandomSource(7);
            var record = new RecordRandomAction();

            asc.SetRandomSource(source);
            asc.RunActions(new GameplayAction[] { record }, null, Owner);

            Assert.That(record.Seen, Is.SameAs(source));
            Assert.Throws<ArgumentNullException>(() => asc.SetRandomSource(null));
            Assert.That(asc.Random, Is.SameAs(source));
        }

        [Serializable]
        private sealed class RecordRandomAction : GameplayAction
        {
            public IRandomSource Seen;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Seen = context.Random;
                return ActionStatus.Done;
            }
        }

        /// <summary>Copia os alvos resolvidos da ação para <see cref="Seen"/>.</summary>
        [Serializable]
        private sealed class RecordTargetsAction : GameplayAction
        {
            public readonly List<AbilitySystemComponent> Seen = new List<AbilitySystemComponent>();

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Seen.AddRange(context.Targets);
                return ActionStatus.Done;
            }
        }

        /// <summary>Soma em <see cref="Total"/> quantos alvos a ação recebeu, sem alocar.</summary>
        [Serializable]
        private sealed class CountTargetsAction : GameplayAction
        {
            public int Total;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Total += context.Targets.Count;
                return ActionStatus.Done;
            }
        }

        /// <summary>Põe <c>target</c> nos alvos do evento, como a <c>TargetingAction</c> faria.</summary>
        [Serializable]
        private sealed class AddEventTargetAction : GameplayAction
        {
            private readonly AbilitySystemComponent _target;

            public AddEventTargetAction(AbilitySystemComponent target) => _target = target;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                context.EventTargets.Add(_target);
                return ActionStatus.Done;
            }
        }

        /// <summary>Projétil que expõe o impacto, para o teste não depender de física.</summary>
        private sealed class TestProjectile : Projectile
        {
            public void Impact(Vector3 point, AbilitySystemComponent hit) => HandleImpact(point, hit);
        }
    }
}
