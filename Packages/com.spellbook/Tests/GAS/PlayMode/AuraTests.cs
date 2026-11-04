using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using SpellBook.GAS.Thinkers;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// <see cref="AuraBehaviour"/> (B-014, TK6; ADR 0035): entra, sai, término, reaplicação, filtros, nível, estado por
    /// thinker e reentrada no pulso. Os ASCs têm <c>autoTick</c> desligado e o pulso é um <c>Tick</c> de 1 s. As unidades
    /// têm um <see cref="SphereCollider"/> e a física é sincronizada antes de cada pulso.
    /// </summary>
    public class AuraTests
    {
        private GasFactory _gas;
        private OneSlotSpawner _spawner;
        private GameplayTag _blessing;
        private GameplayEffect _effect;
        private readonly List<GameObject> _units = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _spawner = ScriptableObject.CreateInstance<OneSlotSpawner>();
            _blessing = _gas.CreateTag("Aura.Bencao");
            _effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f, _blessing);
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
            Object.Destroy(_spawner);
        }

        [Test]
        public void Enter_Applies_InsideAtBirth_Applies_Exit_RemovesOwnInstance()
        {
            AbilitySystemComponent caster = CreateUnit(new Vector3(50f, 0f, 0f));
            AbilitySystemComponent inside = CreateUnit(new Vector3(1f, 0f, 0f));
            AbilitySystemComponent entering = CreateUnit(new Vector3(10f, 0f, 0f));
            GameplayThinker thinker = CreateAura(Vector3.zero, Aura(5f));

            thinker.Begin(caster, null, _spawner);

            Assert.That(inside.HasTag(_blessing), Is.True, "quem estava dentro não recebeu o efeito ao nascer a aura");
            Assert.That(inside.ActiveEffects[0].Context.Instigator, Is.SameAs(caster), "o instigador não é o criador");
            Assert.That(entering.HasTag(_blessing), Is.False);

            Move(entering, new Vector3(2f, 0f, 0f));
            Pulse(thinker);
            Assert.That(entering.HasTag(_blessing), Is.True, "quem entrou não recebeu o efeito");
            ActiveEffectInstance applied = inside.ActiveEffects[0];

            Move(inside, new Vector3(20f, 0f, 0f));
            Pulse(thinker);
            Assert.That(inside.ActiveEffects, Is.Empty, "quem saiu manteve o efeito");
            Assert.That(applied.IsActive, Is.False, "a instância que a aura aplicou continua ativa");
            Assert.That(entering.HasTag(_blessing), Is.True, "quem continua dentro perdeu o efeito");
            Assert.That(entering.ActiveEffects, Has.Count.EqualTo(1), "o pulso reaplicou em quem já estava dentro");
        }

        [Test]
        public void End_RemovesFromAll_OtherSourceSameEffectKept()
        {
            AbilitySystemComponent caster = CreateUnit(new Vector3(50f, 0f, 0f));
            AbilitySystemComponent other = CreateUnit(new Vector3(-50f, 0f, 0f));
            AbilitySystemComponent first = CreateUnit(new Vector3(1f, 0f, 0f));
            AbilitySystemComponent second = CreateUnit(new Vector3(-1f, 0f, 0f));
            AbilitySystemComponent fromOther = CreateUnit(new Vector3(0f, 0f, 1f));
            ActiveEffectInstance otherInstance = fromOther.ApplyEffect(_effect, other);
            GameplayThinker thinker = CreateAura(Vector3.zero, Aura(5f));
            thinker.Begin(caster, null, _spawner);
            Assert.That(first.HasTag(_blessing) && second.HasTag(_blessing), Is.True);

            thinker.End(ActionEndReason.Released);

            Assert.That(first.ActiveEffects, Is.Empty, "o término não removeu o efeito de todos");
            Assert.That(second.ActiveEffects, Is.Empty, "o término não removeu o efeito de todos");
            Assert.That(otherInstance.IsActive, Is.True, "a aura removeu o efeito de outra fonte");
            Assert.That(fromOther.HasTag(_blessing), Is.True);
            Assert.That(_spawner.Releases, Is.EqualTo(1));
        }

        [Test]
        public void Purged_ReappliedNextPulseIfInside()
        {
            AbilitySystemComponent caster = CreateUnit(new Vector3(50f, 0f, 0f));
            AbilitySystemComponent stays = CreateUnit(new Vector3(1f, 0f, 0f));
            AbilitySystemComponent leaves = CreateUnit(new Vector3(-1f, 0f, 0f));
            GameplayThinker thinker = CreateAura(Vector3.zero, Aura(5f));
            thinker.Begin(caster, null, _spawner);

            stays.RemoveEffects(_effect);
            leaves.RemoveEffects(_effect);
            Assert.That(stays.HasTag(_blessing), Is.False, "a remoção por outra via foi desfeita na hora");
            Move(leaves, new Vector3(-20f, 0f, 0f));
            Pulse(thinker);

            Assert.That(stays.HasTag(_blessing), Is.True, "quem continua dentro não recebeu o efeito de novo no pulso seguinte");
            Assert.That(stays.ActiveEffects, Has.Count.EqualTo(1));
            Assert.That(leaves.HasTag(_blessing), Is.False, "quem saiu recebeu o efeito de novo");
        }

        [Test]
        public void DisabledWithoutEnd_BeginAgain_UndoesPreviousCycle_EndRemoves()
        {
            AbilitySystemComponent caster = CreateUnit(new Vector3(50f, 0f, 0f));
            AbilitySystemComponent inside = CreateUnit(new Vector3(1f, 0f, 0f));
            GameplayThinker thinker = CreateAura(Vector3.zero, Aura(5f));
            thinker.Begin(caster, null, _spawner);
            ActiveEffectInstance previous = inside.ActiveEffects[0];

            thinker.gameObject.SetActive(false);
            Assert.That(previous.IsActive, Is.True, "desligar sem término removeu o efeito");
            thinker.gameObject.SetActive(true);
            thinker.Begin(caster, null, _spawner);

            Assert.That(previous.IsActive, Is.False, "o novo ciclo não desfez o que a aura aplicou no anterior");
            Assert.That(inside.ActiveEffects, Has.Count.EqualTo(1));
            Assert.That(inside.HasTag(_blessing), Is.True, "quem está dentro não recebeu o efeito no novo ciclo");

            thinker.End(ActionEndReason.Released);
            Assert.That(inside.ActiveEffects, Is.Empty, "o término deixou o efeito do ciclo anterior");
            Assert.That(inside.HasTag(_blessing), Is.False);
        }

        [Test]
        public void Filter_TagOnTarget_IncludeOwner_Max_Order_StopsMatchingLoses()
        {
            GameplayTag alive = _gas.CreateTag("Estado.Vivo");
            AbilitySystemComponent caster = CreateUnit(new Vector3(0.5f, 0f, 0f));
            AbilitySystemComponent near = CreateUnit(new Vector3(1f, 0f, 0f));
            AbilitySystemComponent untagged = CreateUnit(new Vector3(1.5f, 0f, 0f));
            AbilitySystemComponent middle = CreateUnit(new Vector3(2f, 0f, 0f));
            AbilitySystemComponent far = CreateUnit(new Vector3(3f, 0f, 0f));
            AbilitySystemComponent last = CreateUnit(new Vector3(4f, 0f, 0f));
            foreach (AbilitySystemComponent unit in new[] { caster, near, middle, far, last }) unit.AddTag(alive);
            var filter = new TagQueryCondition { Subject = ConditionSubject.TargetUnit };
            filter.Query.All.Add(alive);
            AuraBehaviour aura = Aura(5f);
            aura.Area.Filter = filter;
            aura.Area.MaxTargets = 3;
            aura.Area.Order = AreaOrder.Nearest;
            GameplayThinker thinker = CreateAura(Vector3.zero, aura);

            thinker.Begin(caster, null, _spawner);
            Assert.That(Blessed(caster, near, untagged, middle, far, last),
                Is.EqualTo(new[] { false, true, false, true, true, false }), "sem o dono: os 3 mais perto com a tag");

            aura.Area.IncludeOwner = true;
            near.RemoveTag(alive);
            Pulse(thinker);
            Assert.That(Blessed(caster, near, untagged, middle, far, last),
                Is.EqualTo(new[] { true, false, false, true, true, false }),
                "com o dono, o criador entra; quem deixou de casar perde o efeito");
        }

        [Test]
        public void RadiusAndInterval_ByLevel_NoRecreate()
        {
            AbilitySystemComponent target = CreateUnit(new Vector3(3f, 0f, 0f));
            int pulses = 0;
            AuraBehaviour aura = Aura(0f);
            aura.Area.Radius = ScalableFloat.FromTable("raio");
            GameplayThinker thinker = CreateAura(Vector3.zero, aura, t =>
            {
                t.PulseInterval = ScalableFloat.FromTable("intervalo");
                t.OnPulse.Add(new CallbackAction(_ => pulses++));
            });
            GameplayAbility ability = _gas.CreateAbility(new CallbackAction(c => thinker.Begin(c.Owner, c, _spawner)));
            ability.Values.Add("raio", ScalableFloat.FromArray(2f, 4f, 6f));
            ability.Values.Add("intervalo", ScalableFloat.FromArray(2f, 2f, 1f));
            var so = new SerializedObject(ability);
            so.FindProperty("maxLevel").intValue = 3;
            so.ApplyModifiedPropertiesWithoutUndo();

            AbilitySystemComponent levelOne = CreateUnit(new Vector3(50f, 0f, 0f));
            levelOne.TryActivateAbility(levelOne.GiveAbility(ability, 1), Vector3.zero);
            Pulse(thinker);
            Assert.That(target.HasTag(_blessing), Is.False, "no nível 1 o raio é 2");
            Assert.That(pulses, Is.EqualTo(0), "no nível 1 o intervalo é 2");
            thinker.End(ActionEndReason.Released);

            GameObject reused = _spawner.Spawn(null, Vector3.zero, Quaternion.identity);
            AbilitySystemComponent levelThree = CreateUnit(new Vector3(-50f, 0f, 0f));
            levelThree.TryActivateAbility(levelThree.GiveAbility(ability, 3), Vector3.zero);
            Pulse(thinker);

            Assert.That(reused, Is.SameAs(thinker.gameObject), "o thinker foi recriado");
            Assert.That(target.HasTag(_blessing), Is.True, "no nível 3 o raio é 6");
            Assert.That(pulses, Is.EqualTo(1), "no nível 3 o intervalo é 1");
        }

        [Test]
        public void TwoAurasSameAsset_NoSharedState()
        {
            AbilitySystemComponent caster = CreateUnit(new Vector3(50f, 0f, 0f));
            AbilitySystemComponent nearFirst = CreateUnit(new Vector3(1f, 0f, 0f));
            AbilitySystemComponent nearSecond = CreateUnit(new Vector3(21f, 0f, 0f));
            AuraBehaviour shared = Aura(3f);
            GameplayThinker first = CreateAura(Vector3.zero, shared);
            GameplayThinker second = CreateAura(new Vector3(20f, 0f, 0f), shared);
            first.Begin(caster, null, _spawner);
            second.Begin(caster, null, _spawner);
            ActiveEffectInstance secondInstance = nearSecond.ActiveEffects[0];

            first.End(ActionEndReason.Released);
            Pulse(second);

            Assert.That(nearFirst.ActiveEffects, Is.Empty, "a primeira aura não removeu o próprio efeito");
            Assert.That(secondInstance.IsActive, Is.True, "o término da primeira aura removeu o efeito da segunda");
            Assert.That(nearSecond.ActiveEffects, Has.Count.EqualTo(1));
        }

        [Test]
        public void PulseActionCreatesDestroysThinkers_RemovesAuraEffect_NoCorruption()
        {
            AbilitySystemComponent caster = CreateUnit(new Vector3(100f, 0f, 0f));
            AbilitySystemComponent unitA = CreateUnit(new Vector3(1f, 0f, 0f));
            AbilitySystemComponent unitB = CreateUnit(new Vector3(21f, 0f, 0f));
            AbilitySystemComponent unitC = CreateUnit(new Vector3(41f, 0f, 0f));
            GameplayThinker thinkerB = CreateAura(new Vector3(20f, 0f, 0f), Aura(3f));
            GameplayThinker thinkerC = CreateAura(new Vector3(40f, 0f, 0f), Aura(3f));
            bool acted = false;
            GameplayThinker thinkerA = CreateAura(Vector3.zero, Aura(3f), t => t.OnPulse.Add(new CallbackAction(_ =>
            {
                if (acted) return;
                acted = true;
                unitA.RemoveEffects(_effect);
                thinkerB.End(ActionEndReason.Released);
                thinkerC.Begin(caster, null, _spawner);
            })));
            thinkerA.Begin(caster, null, _spawner);
            thinkerB.Begin(caster, null, _spawner);

            Pulse(thinkerA);
            Assert.That(unitA.HasTag(_blessing), Is.False, "a remoção no pulso foi desfeita no mesmo pulso");
            Assert.That(unitB.HasTag(_blessing), Is.False, "o thinker destruído no pulso deixou o efeito");
            Assert.That(unitC.HasTag(_blessing), Is.True, "o thinker criado no pulso não aplicou");
            Pulse(thinkerA);
            Assert.That(unitA.ActiveEffects, Has.Count.EqualTo(1), "a aura não reaplicou depois da remoção no pulso");

            // Um ouvinte da aplicação termina a própria aura no meio do pulso
            AbilitySystemComponent unitX = CreateUnit(new Vector3(60.5f, 0f, 0f));
            AbilitySystemComponent unitY = CreateUnit(new Vector3(61.5f, 0f, 0f));
            GameplayThinker thinkerD = null;
            GameplayEffect ending = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f, _blessing);
            ending.OnApplied.Add(new CallbackAction(_ => thinkerD.End(ActionEndReason.Released)));
            AuraBehaviour endingAura = Aura(3f);
            endingAura.Effect = ending;
            thinkerD = CreateAura(new Vector3(60f, 0f, 0f), endingAura);

            thinkerD.Begin(caster, null, _spawner);

            Assert.That(thinkerD.HasEnded, Is.True);
            Assert.That(unitX.ActiveEffects, Is.Empty, "a aplicação em curso ficou depois do término");
            Assert.That(unitY.ActiveEffects, Is.Empty, "a aura seguiu aplicando depois do término");
        }

        private AuraBehaviour Aura(float radius)
        {
            var aura = new AuraBehaviour { Effect = _effect };
            aura.Area.Radius = ScalableFloat.Constant(radius);
            return aura;
        }

        // Thinker de pulso 1 s, sem duração e sem terminar com a execução, ativo na posição
        private GameplayThinker CreateAura(Vector3 position, AuraBehaviour aura, Action<GameplayThinker> configure = null)
        {
            GameplayThinker thinker = _gas.CreateThinkerPrefab(0f, 1f, t =>
            {
                t.Behaviour = aura;
                t.EndWithExecution = false;
                t.transform.position = position;
                configure?.Invoke(t);
            });
            thinker.gameObject.SetActive(true);
            return thinker;
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

        private static void Move(AbilitySystemComponent unit, Vector3 position)
        {
            unit.transform.position = position;
            Physics.SyncTransforms();
        }

        private static void Pulse(GameplayThinker thinker) => thinker.AbilitySystem.Tick(1f);

        private bool[] Blessed(params AbilitySystemComponent[] units)
        {
            var blessed = new bool[units.Length];
            for (int i = 0; i < units.Length; i++) blessed[i] = units[i].HasTag(_blessing);
            return blessed;
        }

        /// <summary>Spawner de uma vaga: devolver só desativa, e criar reativa o último devolvido.</summary>
        private sealed class OneSlotSpawner : SpawnerAsset
        {
            private GameObject _released;

            public int Releases { get; private set; }

            public override GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            {
                GameObject instance = _released;
                _released = null;
                if (instance != null) instance.SetActive(true);
                return instance;
            }

            public override void Release(GameObject instance)
            {
                Releases++;
                instance.SetActive(false);
                _released = instance;
            }
        }

        [Serializable]
        private sealed class CallbackAction : GameplayAction
        {
            private readonly Action<ActionContext> _callback;

            public CallbackAction(Action<ActionContext> callback) => _callback = callback;

            public override bool UsesTargets => false;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _callback(context);
                return ActionStatus.Done;
            }
        }
    }
}
