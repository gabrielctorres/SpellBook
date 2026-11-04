using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using SpellBook.GAS.Thinkers;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Receitas do thinker de ponta a ponta (B-014, TK8; CA16): a aura presa a uma unidade (R12) e a renovação por pulso
    /// (R13). Os ASCs têm <c>autoTick</c> desligado e o pulso é um <c>Tick</c> de 1 s; as unidades têm um
    /// <see cref="SphereCollider"/>.
    /// </summary>
    public class ThinkerRecipesTests
    {
        private GasFactory _gas;
        private SpawnFactory _spawn;
        private RecordingSpawnerAsset _spawner;
        private readonly List<GameObject> _units = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _spawn = new SpawnFactory();
            _spawner = _spawn.Track(ScriptableObject.CreateInstance<RecordingSpawnerAsset>());
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
            _spawn.Dispose();
            Physics.SyncTransforms();
            _gas.Dispose();
        }

        // R12: efeito infinito no dono que, "Ao ser aplicado", cria um thinker que o segue e termina com o efeito
        [UnityTest]
        public IEnumerator Recipe_AuraOnUnit_EffectCreatesFollowingThinker()
        {
            GameplayTag blessing = _gas.CreateTag("Aura.Bencao");
            var aura = new AuraBehaviour { Effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f, blessing) };
            aura.Area.Radius = ScalableFloat.Constant(3f);
            GameplayThinker model = _gas.CreateThinkerPrefab(0f, 1f, t =>
            {
                t.Behaviour = aura;
                t.FollowOwner = true;
                t.EndWithExecution = true;
            });
            GameplayEffect buff = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            buff.OnApplied.Add(new CreateThinkerAction { Prefab = model, Spawner = _spawner, Position = ThinkerPosition.Caster });
            AbilitySystemComponent unit = CreateUnit(Vector3.zero);
            AbilitySystemComponent ally = CreateUnit(new Vector3(2f, 0f, 0f));
            AbilitySystemComponent far = CreateUnit(new Vector3(10f, 0f, 0f));

            ActiveEffectInstance instance = unit.ApplyEffect(buff);
            Assert.That(_spawner.Live, Has.Count.EqualTo(1), "o efeito não criou o thinker");
            GameplayThinker thinker = _spawner.Live[0].GetComponent<GameplayThinker>();
            Assert.That(ally.HasTag(blessing), Is.True, "o vizinho não recebeu a aura ao nascer");
            Assert.That(unit.HasTag(blessing), Is.False, "a aura incluiu o dono");
            Assert.That(far.HasTag(blessing), Is.False);

            unit.transform.position = new Vector3(9f, 0f, 0f);
            yield return null;
            Assert.That(thinker.transform.position, Is.EqualTo(new Vector3(9f, 0f, 0f)), "o thinker não seguiu a unidade");
            Physics.SyncTransforms();
            thinker.AbilitySystem.Tick(1f);
            Assert.That(ally.HasTag(blessing), Is.False, "quem ficou para trás manteve a aura");
            Assert.That(far.HasTag(blessing), Is.True, "a aura não acompanhou a unidade");

            unit.RemoveEffect(instance);
            unit.Tick(0.1f);

            Assert.That(thinker.HasEnded, Is.True, "remover o efeito não terminou o thinker");
            Assert.That(far.HasTag(blessing), Is.False, "o término não tirou a aura");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(1));
        }

        // R13: "A cada pulso" com ForEachTarget + ApplyEffect na área; o efeito dura mais que o intervalo e renova
        [Test]
        public void Recipe_PulseRenewal_ForEachApplyRenews()
        {
            GameplayTag renewal = _gas.CreateTag("Aura.Renovacao");
            GameplayEffect effect = _gas.CreateDurationEffect(1.5f, renewal);
            var forEach = new ForEachTargetAction
            {
                Target = new AreaSelector { Origin = AreaOrigin.FirstEventTarget, Radius = ScalableFloat.Constant(3f) },
            };
            forEach.Actions.Add(new ApplyEffectAction { Effects = new List<GameplayEffect> { effect } });
            GameplayThinker thinker = _gas.CreateThinkerPrefab(0f, 1f, t =>
            {
                t.EndWithExecution = false;
                t.OnPulse.Add(forEach);
            });
            thinker.gameObject.SetActive(true);
            AbilitySystemComponent caster = CreateUnit(new Vector3(50f, 0f, 0f));
            AbilitySystemComponent target = CreateUnit(new Vector3(1f, 0f, 0f));
            thinker.Begin(caster, null, _spawner);

            Step(thinker, target);
            Assert.That(target.ActiveEffects, Has.Count.EqualTo(1), "o pulso não aplicou o efeito");
            ActiveEffectInstance applied = target.ActiveEffects[0];
            Assert.That(applied.Context.Instigator, Is.SameAs(caster), "o instigador não é o criador");
            for (int i = 0; i < 4; i++) Step(thinker, target);

            Assert.That(target.ActiveEffects, Has.Count.EqualTo(1), "o pulso empilhou em vez de renovar");
            Assert.That(target.ActiveEffects[0], Is.SameAs(applied), "o pulso trocou a instância em vez de renovar");
            Assert.That(applied.IsActive, Is.True, "o efeito venceu entre pulsos");

            target.transform.position = new Vector3(20f, 0f, 0f);
            Physics.SyncTransforms();
            Step(thinker, target);

            Assert.That(target.HasTag(renewal), Is.False, "quem saiu manteve o efeito depois da duração");
        }

        // Um segundo do thinker (o pulso) e do alvo (a duração do efeito)
        private static void Step(GameplayThinker thinker, AbilitySystemComponent target)
        {
            thinker.AbilitySystem.Tick(1f);
            target.Tick(1f);
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
    }
}
