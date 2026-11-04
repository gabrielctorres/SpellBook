using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Thinkers;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Cenário C18 (B-014, TK8; CA27): 1000 pulsos de aura com 20 alvos dentro e 1000 impactos de projétil com perfuração
    /// e filtro não alocam depois do aquecimento.
    /// </summary>
    public class ThinkerAllocationTests
    {
        private const int Targets = 20;

        private GasFactory _gas;
        private readonly List<GameObject> _units = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            // O Destroy do PlayMode só age no fim do quadro: os colliders seguiriam no próximo teste
            foreach (GameObject unit in _units)
            {
                if (unit != null) Object.DestroyImmediate(unit);
            }
            _units.Clear();
            Physics.SyncTransforms();
            _gas.Dispose();
        }

        [Test]
        public void Aura1000Pulses20Targets_DoNotAllocate()
        {
            GameplayTag blessing = _gas.CreateTag("Aura.Bencao");
            var aura = new AuraBehaviour { Effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f, blessing) };
            aura.Area.Radius = ScalableFloat.Constant(5f);
            GameplayThinker thinker = _gas.CreateThinkerPrefab(0f, 1f, t =>
            {
                t.Behaviour = aura;
                t.EndWithExecution = false;
            });
            thinker.gameObject.SetActive(true);
            AbilitySystemComponent caster = CreateUnit(new Vector3(50f, 0f, 0f));
            var inside = new List<AbilitySystemComponent>();
            for (int i = 0; i < Targets; i++)
            {
                float angle = i * Mathf.PI * 2f / Targets;
                inside.Add(CreateUnit(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3f));
            }
            thinker.Begin(caster, null, null);
            thinker.AbilitySystem.Tick(1f);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++) thinker.AbilitySystem.Tick(1f);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(inside.TrueForAll(u => u.HasTag(blessing) && u.ActiveEffects.Count == 1), Is.True,
                "nem todo alvo dentro tem um efeito só da aura");
        }

        [Test]
        public void Projectile1000Impacts_PierceAndFilter_DoNotAllocate()
        {
            GameplayTag marked = _gas.CreateTag("Estado.Marcado");
            var filter = new TagQueryCondition { Subject = ConditionSubject.TargetUnit };
            filter.Query.All.Add(marked);
            AbilitySystemComponent caster = _gas.CreateAsc();
            var targets = new GameObject[Targets];
            for (int i = 0; i < Targets; i++)
            {
                AbilitySystemComponent target = _gas.CreateAsc();
                // Um em cada quatro não passa no filtro: é atravessado sem impacto
                if (i % 4 != 0) target.AddTag(marked);
                targets[i] = target.gameObject;
            }
            var counter = new CountAction();
            CollidingProjectile projectile = _gas.CreateProjectile<CollidingProjectile>(null, p =>
            {
                p.UnlimitedPierce = true;
                p.HitEachTargetOnce = false;
                p.ImpactFilter = filter;
            });
            projectile.Setup(null, caster, new List<GameplayAction> { counter });
            for (int i = 0; i < Targets; i++) projectile.Collide(targets[i]);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++) projectile.Collide(targets[i % Targets]);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(counter.Count, Is.EqualTo((1000 + Targets) * 3 / 4), "o filtro não decidiu cada choque");
            Assert.That(projectile.gameObject.activeSelf, Is.True, "a perfuração ilimitada terminou o projétil");
        }

        private AbilitySystemComponent CreateUnit(Vector3 position)
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.transform.position = position;
            asc.gameObject.AddComponent<SphereCollider>().radius = 0.25f;
            _units.Add(asc.gameObject);
            asc.gameObject.SetActive(true);
            Physics.SyncTransforms();
            return asc;
        }

        /// <summary>Projétil que expõe o <c>HandleCollision</c> para simular um choque sem física.</summary>
        private sealed class CollidingProjectile : Projectile
        {
            public void Collide(GameObject other) => HandleCollision(other);
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
    }
}
