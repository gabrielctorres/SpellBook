using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Targeting;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Seletor de área e <c>TargetingAction</c> com seletor (B-012, EV4b; CA13 e parte do CA34).</summary>
    public class AreaSelectorTests
    {
        private static readonly object Owner = new object();

        private GasFactory _gas;
        private readonly List<GameObject> _units = new List<GameObject>();

        [SetUp]
        public void SetUp() => _gas = new GasFactory();

        [TearDown]
        public void TearDown()
        {
            // No PlayMode, o Dispose usa Destroy, que só age no fim do quadro: os colliders seguiriam no próximo teste.
            foreach (GameObject unit in _units)
            {
                if (unit != null) Object.DestroyImmediate(unit);
            }
            _units.Clear();
            Physics.SyncTransforms();
            Physics2D.SyncTransforms();
            _gas.Dispose();
        }

        [Test]
        public void Area_Max2_ExcludesOwner_IncludeOwnerAddsIt()
        {
            AbilitySystemComponent caster = CreateUnit3D(Vector3.zero);
            AbilitySystemComponent near = CreateUnit3D(new Vector3(1f, 0f, 0f));
            AbilitySystemComponent middle = CreateUnit3D(new Vector3(2f, 0f, 0f));
            CreateUnit3D(new Vector3(3f, 0f, 0f));
            CreateUnit3D(new Vector3(9f, 0f, 0f));
            var area = new AreaSelector { Radius = ScalableFloat.Constant(5f), MaxTargets = 2 };

            List<AbilitySystemComponent> withoutOwner = Select(caster, area);
            area.IncludeOwner = true;
            List<AbilitySystemComponent> withOwner = Select(caster, area);

            Assert.That(withoutOwner, Is.EqualTo(new[] { near, middle }), "os 2 mais perto, sem o dono");
            Assert.That(withOwner, Is.EqualTo(new[] { caster, near }), "com o dono, ele é o mais perto");
        }

        [Test]
        public void RandomN_Distinct_SameSeedSame()
        {
            AbilitySystemComponent caster = CreateUnit3D(Vector3.zero);
            var candidates = new List<AbilitySystemComponent>();
            for (int i = 1; i <= 6; i++) candidates.Add(CreateUnit3D(new Vector3(i * 0.5f, 0f, 0f)));
            var area = new AreaSelector
            {
                Radius = ScalableFloat.Constant(5f), MaxTargets = 3, Order = AreaOrder.Random,
            };

            caster.SetRandomSource(new SeededRandomSource(42));
            List<AbilitySystemComponent> first = Select(caster, area);
            caster.SetRandomSource(new SeededRandomSource(42));
            List<AbilitySystemComponent> second = Select(caster, area);

            Assert.That(first, Has.Count.EqualTo(3));
            Assert.That(first, Is.Unique, "o sorteio não repete alvo");
            Assert.That(first, Is.SubsetOf(candidates), "só candidatos dentro do raio, sem o dono");
            Assert.That(second, Is.EqualTo(first), "a mesma semente sorteia os mesmos alvos, na mesma ordem");
        }

        [Test]
        public void Area2DAnd3D()
        {
            AbilitySystemComponent caster = CreateUnit3D(new Vector3(0f, 0f, 50f));
            AbilitySystemComponent unit3D = CreateUnit3D(new Vector3(1f, 0f, 0f));
            AbilitySystemComponent unit2D = CreateUnit2D(new Vector3(-1f, 0f, 0f));
            var area = new AreaSelector { Origin = AreaOrigin.Point, Radius = ScalableFloat.Constant(3f) };

            List<AbilitySystemComponent> found3D = Select(caster, area);
            area.Physics = PhysicsMode.Physics2D;
            List<AbilitySystemComponent> found2D = Select(caster, area);

            Assert.That(found3D, Is.EqualTo(new[] { unit3D }), "a física 3D acha só o collider 3D, em volta do ponto");
            Assert.That(found2D, Is.EqualTo(new[] { unit2D }), "a física 2D acha só o collider 2D, em volta do ponto");
        }

        [Test]
        public void TargetingAction_AreaSelector_ReplacesOrAppendsEventTargets()
        {
            AbilitySystemComponent caster = CreateUnit3D(Vector3.zero);
            AbilitySystemComponent enemy = CreateUnit3D(new Vector3(1f, 0f, 0f));
            AbilitySystemComponent previous = CreateUnit3D(new Vector3(20f, 0f, 0f));
            var area = new AreaSelector { Origin = AreaOrigin.Caster, Radius = ScalableFloat.Constant(2f) };
            var replace = new TargetingAction { Target = area, Mode = TargetingAction.TargetMode.Replace };
            var append = new TargetingAction { Target = area, Mode = TargetingAction.TargetMode.Append };
            var afterReplace = new RecordTargetsAction { Target = new EventTargetsSelector() };
            var afterAppend = new RecordTargetsAction { Target = new EventTargetsSelector() };

            caster.RunActions(new GameplayAction[] { replace, afterReplace },
                GasFactory.CreateActionContext(caster, Vector3.zero, previous), Owner);
            caster.RunActions(new GameplayAction[] { append, afterAppend },
                GasFactory.CreateActionContext(caster, Vector3.zero, previous, enemy), Owner);

            Assert.That(afterReplace.Seen, Is.EqualTo(new[] { enemy }), "Replace troca os alvos do evento");
            Assert.That(afterAppend.Seen, Is.EqualTo(new[] { previous, enemy }), "Append soma sem repetir");
        }

        [Test]
        public void Sequence3Actions_Area_10000_DoNotAllocate()
        {
            AbilitySystemComponent caster = CreateUnit3D(Vector3.zero);
            for (int i = 1; i <= 4; i++) CreateUnit3D(new Vector3(i, 0f, 0f));
            CreateUnit2D(new Vector3(0f, 1f, 0f));
            var counter = new CountTargetsAction
            {
                Target = new AreaSelector { Radius = ScalableFloat.Constant(10f), MaxTargets = 3, Order = AreaOrder.Random },
            };
            var actions = new GameplayAction[]
            {
                new CountTargetsAction { Target = new CasterSelector() },
                new CountTargetsAction { Target = new AreaSelector { Radius = ScalableFloat.Constant(10f), MaxTargets = 2 } },
                counter,
            };
            caster.RunActions(actions, null, Owner);
            counter.Total = 0;

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++) caster.RunActions(actions, null, Owner);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(counter.Total, Is.EqualTo(30000), "o sorteio achou 3 alvos em cada execução");
        }

        private List<AbilitySystemComponent> Select(AbilitySystemComponent caster, TargetSelector selector)
        {
            var record = new RecordTargetsAction { Target = selector };
            caster.RunActions(new GameplayAction[] { record },
                GasFactory.CreateActionContext(caster, Vector3.zero), Owner);
            return record.Seen;
        }

        private AbilitySystemComponent CreateUnit3D(Vector3 position)
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            asc.transform.position = position;
            asc.gameObject.AddComponent<SphereCollider>().radius = 0.25f;
            _units.Add(asc.gameObject);
            asc.gameObject.SetActive(true);
            Physics.SyncTransforms();
            return asc;
        }

        private AbilitySystemComponent CreateUnit2D(Vector3 position)
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            asc.transform.position = position;
            asc.gameObject.AddComponent<CircleCollider2D>().radius = 0.25f;
            _units.Add(asc.gameObject);
            asc.gameObject.SetActive(true);
            Physics2D.SyncTransforms();
            return asc;
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
    }
}
