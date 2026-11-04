using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Perfuração, "um acerto por alvo" e ASC no pai do <see cref="Projectile"/> (B-014, TK2; CA17, CA18, CA21 a CA24 e
    /// a parte do projétil do CA27). O choque é simulado pelo <c>HandleCollision</c>, salvo no teste de física.
    /// </summary>
    public class ProjectilePierceTests
    {
        private GasFactory _gas;
        private TimeFactory _time;
        private SpawnFactory _spawn;
        private ManualTimeSource _clock;
        private RecordingSpawnerAsset _spawner;
        private List<AbilitySystemComponent> _hits;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _spawn = new SpawnFactory();
            _clock = _time.CreateManual();
            _spawner = _spawn.Track(ScriptableObject.CreateInstance<RecordingSpawnerAsset>());
            _hits = new List<AbilitySystemComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _time.Dispose();
            _spawn.Dispose();
        }

        [Test]
        public void Pierce2_HitsThree_ImpactOncePerTarget_EndsOnThird()
        {
            AbilitySystemComponent a = _gas.CreateAsc(), b = _gas.CreateAsc(), c = _gas.CreateAsc(), d = _gas.CreateAsc();
            CollidingProjectile projectile = CreateArmed(p => p.Pierce = 2);

            projectile.Collide(a.gameObject);
            projectile.Collide(a.gameObject); // o colisor continua em contato
            projectile.Collide(b.gameObject);
            int releasedBeforeThird = _spawner.ReleaseCount;
            projectile.Collide(c.gameObject);
            projectile.Collide(d.gameObject);

            Assert.That(_hits, Is.EqualTo(new[] { a, b, c }), "\"No impacto\" não rodou uma vez por alvo até o terceiro");
            Assert.That(releasedBeforeThird, Is.EqualTo(0), "o projétil terminou antes de gastar a perfuração");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(1), "o projétil não terminou uma vez no terceiro alvo");
        }

        [Test]
        public void Pierce_EveryImpact_KeepsAbilityContext()
        {
            CollidingProjectile projectile = _gas.CreateProjectile<CollidingProjectile>(_clock, p => p.Pierce = 1);
            var seen = new List<GameplayAbility>();
            var onImpact = new List<GameplayAction> { new RecordAbilityAction(seen) };
            GameplayAbility ability = _gas.CreateAbility(new SetupProjectileAction(projectile, _spawner, onImpact));
            AbilitySystemComponent caster = _gas.CreateAsc();
            caster.GiveAbility(ability);
            caster.TryActivateAbility(ability, Vector3.zero);

            projectile.Collide(_gas.CreateAsc().gameObject);
            projectile.Collide(_gas.CreateAsc().gameObject);

            Assert.That(seen, Is.EqualTo(new[] { ability, ability }), "um impacto perdeu a habilidade de quem lançou");
        }

        [Test]
        public void Pierce_Obstacle_EndsAtOnce()
        {
            CollidingProjectile projectile = CreateArmed(p => p.Pierce = 2);
            GameObject wall = _spawn.Track(new GameObject("Wall_Test"));

            projectile.Collide(wall);

            Assert.That(_spawner.ReleaseCount, Is.EqualTo(1), "o colisor sem ASC não terminou o projétil com perfuração");
        }

        [UnityTest]
        public IEnumerator Unlimited_PassesAll_EndsByLifetime()
        {
            CollidingProjectile projectile = CreateArmed(p =>
            {
                p.UnlimitedPierce = true;
                p.Lifetime = 1f;
            });
            var targets = new AbilitySystemComponent[5];
            for (int i = 0; i < targets.Length; i++) targets[i] = _gas.CreateAsc();

            foreach (AbilitySystemComponent target in targets) projectile.Collide(target.gameObject);
            int releasedAfterTargets = _spawner.ReleaseCount;
            _clock.Advance(1.2f);
            yield return new WaitForFixedUpdate();

            Assert.That(_hits, Is.EqualTo(targets), "a perfuração sem limite não atravessou todos");
            Assert.That(releasedAfterTargets, Is.EqualTo(0), "a perfuração sem limite terminou num alvo");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(1), "o projétil não terminou por tempo");
        }

        [UnityTest]
        public IEnumerator ChildCollider_HitsParentAsc_TwoCollidersOneHit()
        {
            AbilitySystemComponent target = _gas.CreateAsc();
            _gas.AddChildCollider(target);
            _gas.AddChildCollider(target);
            CreateArmed(p =>
            {
                p.Pierce = 1;
                var body = p.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                p.gameObject.AddComponent<SphereCollider>().isTrigger = true;
            });

            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(_hits, Is.EqualTo(new[] { target }), "os dois colisores filhos não contaram como um acerto no ASC do pai");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(0), "o segundo colisor do mesmo alvo gastou a perfuração");
        }

        [Test]
        public void NoNewFields_BehavesAsToday()
        {
            AbilitySystemComponent a = _gas.CreateAsc(), b = _gas.CreateAsc();
            CollidingProjectile projectile = CreateArmed(null);
            CollidingProjectile againstWall = CreateArmed(null);

            projectile.Collide(a.gameObject);
            projectile.Collide(b.gameObject); // choque no mesmo passo, depois do término
            againstWall.Collide(_spawn.Track(new GameObject("Wall_Test")));

            Assert.That(projectile.Pierce, Is.EqualTo(0));
            Assert.That(projectile.UnlimitedPierce, Is.False);
            Assert.That(projectile.HitEachTargetOnce, Is.True);
            Assert.That(_hits, Is.EqualTo(new AbilitySystemComponent[] { a, null }), "o impacto não rodou uma vez por projétil");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(2), "cada projétil não terminou no primeiro choque");
        }

        [UnityTest]
        public IEnumerator DamageOwner_Invulnerability_Lifetime_Homing_Unchanged()
        {
            AbilitySystemComponent caster = _gas.CreateAsc();
            GameObject casterChild = _gas.AddChildCollider(caster);
            AbilitySystemComponent enemy = _gas.CreateAsc();
            CollidingProjectile ignoresOwner = CreateArmed(p => p.Pierce = 1, caster);
            CollidingProjectile hitsOwner = CreateArmed(p => p.DamageOwner = true, caster);
            CollidingProjectile invulnerable = CreateArmed(p => p.InitialInvulnerabilityDuration = 1f, caster);
            CollidingProjectile mortal = CreateArmed(p => p.Lifetime = 1f, caster);
            CollidingProjectile homing = CreateArmed(p => p.IsHoming = true, caster);
            homing.HomingTurnSpeed = 1000f;
            homing.Target = enemy.transform;
            enemy.transform.position = new Vector3(10f, 0f, 0f);

            ignoresOwner.Collide(caster.gameObject);
            ignoresOwner.Collide(casterChild);
            hitsOwner.Collide(caster.gameObject);
            invulnerable.Collide(enemy.gameObject);
            int hitsBeforeTime = _hits.Count;
            _clock.Advance(1.2f);
            yield return new WaitForFixedUpdate();
            invulnerable.Collide(enemy.gameObject);

            Assert.That(hitsBeforeTime, Is.EqualTo(1), "o dono ou a invulnerabilidade não foram ignorados");
            Assert.That(_hits, Is.EqualTo(new[] { caster, enemy }), "o dono com DamageOwner ou o choque depois da invulnerabilidade não acertaram");
            Assert.That(mortal == null || !mortal.gameObject.activeSelf, Is.True, "o Lifetime não terminou o projétil");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(3), "voltaram ao pool outros projéteis além dos três que terminaram");
            Assert.That(Vector3.Angle(homing.Direction, Vector3.right), Is.LessThan(1f), "o projétil teleguiado não virou para o alvo");
        }

        [Test]
        public void FromPool_HitSetCleared()
        {
            AbilitySystemComponent a = _gas.CreateAsc(), b = _gas.CreateAsc();
            CollidingProjectile prefab = _gas.CreateProjectile<CollidingProjectile>(_clock, p => p.Pierce = 1);
            prefab.gameObject.SetActive(false);
            CollidingProjectile first = Spawn(prefab);
            first.Collide(a.gameObject);
            first.ReturnToPool();

            CollidingProjectile second = Spawn(prefab);
            second.Collide(a.gameObject);
            int releasedAfterRepeat = _spawner.ReleaseCount;
            second.Collide(b.gameObject);

            Assert.That(second, Is.SameAs(first), "o spawner de teste não reaproveitou a instância");
            Assert.That(_hits, Is.EqualTo(new[] { a, a, b }), "o \"já acertou\" do uso anterior sobreviveu ao pool");
            Assert.That(releasedAfterRepeat, Is.EqualTo(1), "o contador de impactos do uso anterior sobreviveu ao pool");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(2), "o projétil reaproveitado não terminou depois de gastar a perfuração");
        }

        [Test]
        public void HitSetBeyond32_CountsAsRepeated()
        {
            CollidingProjectile projectile = CreateArmed(p => p.UnlimitedPierce = true);
            var targets = new AbilitySystemComponent[33];
            for (int i = 0; i < targets.Length; i++) targets[i] = _gas.CreateAsc();

            foreach (AbilitySystemComponent target in targets) projectile.Collide(target.gameObject);
            projectile.Collide(targets[0].gameObject);

            Assert.That(_hits, Has.Count.EqualTo(32), "o vetor \"já acertou\" não limitou os acertos a 32");
            Assert.That(_hits, Has.No.Member(targets[32]), "o alvo além do limite foi acertado");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(0), "o limite do vetor terminou o projétil");
        }

        [Test]
        public void Impacts_1000_WithPierce_DoNotAllocate()
        {
            AbilitySystemComponent caster = _gas.CreateAsc();
            GameObject target = _gas.CreateAsc().gameObject;
            var counter = new CountAction();
            CollidingProjectile projectile = _gas.CreateProjectile<CollidingProjectile>(_clock, p =>
            {
                p.UnlimitedPierce = true;
                p.HitEachTargetOnce = false;
            });
            projectile.Setup(_spawner, GasFactory.CreateActionContext(caster, Vector3.zero), new List<GameplayAction> { counter });
            projectile.Collide(target);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++) projectile.Collide(target);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(counter.Count, Is.EqualTo(1001), "nem todo choque rodou o impacto");
        }

        /// <summary>Projétil com <c>Setup</c> feito, que registra em <c>_hits</c> o alvo de cada impacto.</summary>
        private CollidingProjectile CreateArmed(Action<CollidingProjectile> configure, AbilitySystemComponent caster = null)
        {
            CollidingProjectile projectile = _gas.CreateProjectile(_clock, configure);
            projectile.Setup(_spawner, caster != null ? caster : _gas.CreateAsc(),
                new List<GameplayAction> { new RecordTargetAction(_hits) });
            return projectile;
        }

        private CollidingProjectile Spawn(CollidingProjectile prefab)
        {
            var projectile = _spawner.Spawn(prefab.gameObject, Vector3.zero, Quaternion.identity).GetComponent<CollidingProjectile>();
            projectile.Setup(_spawner, _gas.CreateAsc(), new List<GameplayAction> { new RecordTargetAction(_hits) });
            return projectile;
        }

        /// <summary>Projétil que expõe o <c>HandleCollision</c> para simular um choque sem física.</summary>
        private sealed class CollidingProjectile : Projectile
        {
            public void Collide(GameObject other) => HandleCollision(other);
        }

        /// <summary>Registra o alvo do impacto, ou <c>null</c> quando não há.</summary>
        private sealed class RecordTargetAction : GameplayAction
        {
            private readonly List<AbilitySystemComponent> _into;

            public RecordTargetAction(List<AbilitySystemComponent> into) => _into = into;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _into.Add(context.EventTargets.Count > 0 ? context.EventTargets[0] : null);
                return ActionStatus.Done;
            }
        }

        private sealed class RecordAbilityAction : GameplayAction
        {
            private readonly List<GameplayAbility> _into;

            public RecordAbilityAction(List<GameplayAbility> into) => _into = into;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _into.Add(context.Ability);
                return ActionStatus.Done;
            }
        }

        private sealed class CountAction : GameplayAction
        {
            public int Count;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Count++;
                return ActionStatus.Done;
            }
        }

        /// <summary>Faz o <c>Setup</c> do projétil com o contexto da ativação, como a <c>SpawnProjectileAction</c>.</summary>
        private sealed class SetupProjectileAction : GameplayAction
        {
            private readonly Projectile _projectile;
            private readonly RecordingSpawnerAsset _spawner;
            private readonly List<GameplayAction> _onImpact;

            public SetupProjectileAction(Projectile projectile, RecordingSpawnerAsset spawner, List<GameplayAction> onImpact)
            {
                _projectile = projectile;
                _spawner = spawner;
                _onImpact = onImpact;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _projectile.Setup(_spawner, context, _onImpact);
                return ActionStatus.Done;
            }
        }
    }
}
