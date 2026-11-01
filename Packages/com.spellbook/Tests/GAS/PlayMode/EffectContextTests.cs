#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>EffectContext, EffectSpec e nível na avaliação (B-007, VN5: CA7, CA13, CA14, CA16, CA17, CA31).</summary>
    public class EffectContextTests
    {
        private GasFactory _gas;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

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

        // Guarda o contexto da ativação, para montar specs e projéteis com a habilidade e o nível reais.
        private sealed class CaptureContextAction : AbilityAction
        {
            public AbilityContext Context;

            public override IEnumerator Execute(AbilityContext context)
            {
                Context = context;
                yield break;
            }
        }

        // Expõe a colisão do projétil sem física.
        private sealed class TestProjectile : Projectile
        {
            public void Hit(GameObject other) => HandleCollision(other);
        }

        private AbilitySystemComponent CreateAsc(params (AttributeDefinition, float)[] attributes)
        {
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            GasFactory.SetAutoTick(asc, false);
            return asc;
        }

        private GameplayAbility CreateAbility(int maxLevel, params AbilityAction[] actions)
        {
            GameplayAbility ability = _gas.CreateAbility(actions);
            var so = new SerializedObject(ability);
            so.FindProperty("maxLevel").intValue = maxLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ability;
        }

        private static AttributeEffectModifier Add(AttributeDefinition attribute, ScalableFloat value)
        {
            return new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = value }
            };
        }

        private GameplayEffect CreateInstantAdd(AttributeDefinition attribute, ScalableFloat value)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(Add(attribute, value));
            return effect;
        }

        private static AbilityContext Activate(AbilitySystemComponent caster, GameplayAbility ability, int level, CaptureContextAction capture)
        {
            caster.GiveAbility(ability, level);
            caster.TryActivateAbility(ability, Vector3.zero);
            return capture.Context;
        }

        private AbilityContext ActivateAndCapture(AbilitySystemComponent asc, int level, Vector3 targetPosition, out GameplayAbility ability)
        {
            var capture = new CaptureContextAction();
            ability = CreateAbility(4, capture);
            ability.Values.Add("damage", ScalableFloat.FromArray(50f, 75f, 100f, 125f));
            asc.GiveAbility(ability, level);
            asc.TryActivateAbility(ability, targetPosition);
            return capture.Context;
        }

        [UnityTest]
        public IEnumerator Ability_Level3_Cooldown6_Damage100()
        {
            AttributeDefinition damage = _gas.CreateAttribute("Damage");
            AbilitySystemComponent caster = CreateAsc((damage, 0f));
            GameplayEffect damageEffect = CreateInstantAdd(damage, ScalableFloat.FromTable("damage"));
            GameplayAbility ability = CreateAbility(4, new ApplyEffectAction
            {
                Effects = new List<GameplayEffect> { damageEffect },
                ApplyToSource = true
            });
            ability.Values.Add("damage", ScalableFloat.FromArray(50f, 75f, 100f, 125f));
            ability.Values.Add("cooldown", ScalableFloat.FromArray(8f, 7f, 6f, 5f));
            ability.CooldownEffect = _gas.CreateDurationEffect(0f);
            ability.CooldownEffect.Duration = ScalableFloat.FromTable("cooldown");
            caster.GiveAbility(ability, 3);

            caster.TryActivateAbility(ability, Vector3.zero);
            yield return null;

            Assert.That(caster.GetCooldownTimeRemaining(ability.CooldownEffect), Is.EqualTo(6f), "recarga de %cooldown no nível 3");
            Assert.That(caster.GetAttributeValue(damage), Is.EqualTo(100f), "dano de %damage no nível 3");
        }

        [Test]
        public void GetCooldownDuration_Level3_ReadsTableAtAbilityLevel()
        {
            AbilitySystemComponent caster = CreateAsc();
            GameplayAbility ability = CreateAbility(3);
            ability.Values.Add("cooldown", ScalableFloat.FromArray(8f, 7f, 6f));
            ability.CooldownEffect = _gas.CreateDurationEffect(0f);
            ability.CooldownEffect.Duration = ScalableFloat.FromTable("cooldown");
            caster.GiveAbility(ability, 3);

            float duration = ability.GetCooldownDuration(caster);

            Assert.That(duration, Is.EqualTo(6f), "recarga informada no nível 3 da tabela 8/7/6");
        }

        [UnityTest]
        public IEnumerator ProjectileAtLevel3_EffectReadsLevel3()
        {
            AttributeDefinition damage = _gas.CreateAttribute("Damage");
            AbilitySystemComponent caster = CreateAsc();
            AbilitySystemComponent target = CreateAsc((damage, 0f));
            AbilityContext activation = ActivateAndCapture(caster, 3, Vector3.zero, out _);
            GameplayEffect damageEffect = CreateInstantAdd(damage, ScalableFloat.FromTable("damage"));
            var go = new GameObject("Projectile_Test");
            _objects.Add(go);
            var projectile = go.AddComponent<TestProjectile>();
            projectile.Setup(null, activation, new List<AbilityAction>
            {
                new ApplyEffectAction { Effects = new List<GameplayEffect> { damageEffect } }
            });

            projectile.Hit(target.gameObject);
            yield return null;

            Assert.That(target.GetAttributeValue(damage), Is.EqualTo(100f), "o impacto deveria ler o nível 3 da habilidade");
        }

        [UnityTest]
        public IEnumerator Context_InstigatorAbilityLevelImpactPoint()
        {
            AbilitySystemComponent caster = CreateAsc();
            AbilitySystemComponent target = CreateAsc();
            var impact = new Vector3(1f, 2f, 3f);
            AbilityContext activation = ActivateAndCapture(caster, 2, impact, out GameplayAbility ability);
            activation.Targets.Add(target);
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            var action = new ApplyEffectAction { Effects = new List<GameplayEffect> { effect } };

            IEnumerator run = action.Execute(activation);
            while (run.MoveNext()) { }
            yield return null;

            Assert.That(target.ActiveEffects, Has.Count.EqualTo(1));
            EffectContext context = target.ActiveEffects[0].Context;
            Assert.That(context.Instigator, Is.SameAs(caster));
            Assert.That(context.Target, Is.SameAs(target));
            Assert.That(context.Ability, Is.SameAs(ability));
            Assert.That(context.Level, Is.EqualTo(2));
            Assert.That(context.Table, Is.SameAs(ability.Values));
            Assert.That(context.HasImpactPoint, Is.True);
            Assert.That(context.ImpactPoint, Is.EqualTo(impact));
        }

        [Test]
        public void NoAbility_DefaultLevel_TableFallback()
        {
            AttributeDefinition armor = _gas.CreateAttribute("Armor");
            AbilitySystemComponent target = CreateAsc((armor, 0f));
            GameplayEffect effect = _gas.CreateDurationEffect(0f);
            effect.Duration = ScalableFloat.FromArray(4f, 5f, 6f);
            effect.Modifiers.Add(Add(armor, ScalableFloat.FromTable("armor", 7f)));
            var so = new SerializedObject(effect);
            so.FindProperty("defaultLevel").intValue = 3;
            so.ApplyModifiedPropertiesWithoutUndo();

            ActiveEffectInstance instance = target.ApplyEffect(effect, target);

            Assert.That(effect.DefaultLevel, Is.EqualTo(3));
            Assert.That(instance.Context.Level, Is.EqualTo(3), "sem habilidade, vale o DefaultLevel");
            Assert.That(instance.Context.Ability, Is.Null);
            Assert.That(instance.AppliedDuration, Is.EqualTo(6f), "duração no DefaultLevel");
            Assert.That(target.GetAttributeValue(armor), Is.EqualTo(7f), "%nome sem tabela vale o Fallback");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OldApplyEffect_SameResult()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AttributeDefinition armor = _gas.CreateAttribute("Armor");
            AbilitySystemComponent oldTarget = CreateAsc((health, 100f), (armor, 0f));
            AbilitySystemComponent specTarget = CreateAsc((health, 100f), (armor, 0f));
            GameplayEffect hit = CreateInstantAdd(health, ScalableFloat.Constant(-30f));
            GameplayEffect buff = _gas.CreateDurationEffect(5f);
            buff.Modifiers.Add(Add(armor, ScalableFloat.Constant(4f)));

            oldTarget.ApplyEffect(hit, oldTarget);
            ActiveEffectInstance oldBuff = oldTarget.ApplyEffect(buff, 8f, oldTarget);
            specTarget.ApplyEffect(EffectSpec.Create(hit, specTarget));
            EffectSpec buffSpec = EffectSpec.Create(buff, specTarget).WithDurationOverride(8f);
            EffectApplicationResult result = specTarget.TryApplyEffect(in buffSpec, out ActiveEffectInstance specBuff);

            Assert.That(oldTarget.GetAttributeValue(health), Is.EqualTo(70f));
            Assert.That(oldTarget.GetAttributeValue(armor), Is.EqualTo(4f));
            Assert.That(oldBuff.AppliedDuration, Is.EqualTo(8f));
            Assert.That(oldBuff.Context.Instigator, Is.SameAs(oldTarget));
            Assert.That(oldBuff.Context.Level, Is.EqualTo(1));
            Assert.That(result, Is.EqualTo(EffectApplicationResult.Applied));
            Assert.That(specTarget.GetAttributeValue(health), Is.EqualTo(70f));
            Assert.That(specTarget.GetAttributeValue(armor), Is.EqualTo(4f));
            Assert.That(specBuff.AppliedDuration, Is.EqualTo(8f));
        }

        [Test]
        public void DurationByLevel_4sAtL1_6sAtL3_RenewKeepsApplied()
        {
            AbilitySystemComponent target = CreateAsc();
            GameplayEffect effect = _gas.CreateDurationEffect(0f);
            effect.Duration = ScalableFloat.FromTable("duracao");
            var capture = new CaptureContextAction();
            GameplayAbility ability = CreateAbility(3, capture);
            ability.Values.Add("duracao", ScalableFloat.FromArray(4f, 5f, 6f));
            // Um conjurador por nível: a primeira ativação ainda roda no mesmo quadro
            AbilityContext levelOne = Activate(CreateAsc(), ability, 1, capture);
            AbilityContext levelThree = Activate(CreateAsc(), ability, 3, capture);

            ActiveEffectInstance instance = target.ApplyEffect(EffectSpec.FromAbility(effect, levelOne));
            Assert.That(instance.AppliedDuration, Is.EqualTo(4f), "nível 1");
            target.Tick(1f);
            EffectApplicationResult renewed = target.TryApplyEffect(EffectSpec.FromAbility(effect, levelThree), out _);

            Assert.That(renewed, Is.EqualTo(EffectApplicationResult.Refreshed));
            Assert.That(instance.TimeRemaining, Is.EqualTo(4f), "renovar volta à duração aplicada");
            target.RemoveEffect(instance);
            ActiveEffectInstance reapplied = target.ApplyEffect(EffectSpec.FromAbility(effect, levelThree));
            Assert.That(reapplied.AppliedDuration, Is.EqualTo(6f), "nível 3");
        }

        [Test]
        public void TryApplyInstantSpec_DoesNotAllocate()
        {
            AttributeDefinition damage = _gas.CreateAttribute("Damage");
            AbilitySystemComponent caster = CreateAsc();
            AbilitySystemComponent target = CreateAsc((damage, 0f));
            AbilityContext activation = ActivateAndCapture(caster, 3, Vector3.zero, out _);
            GameplayEffect effect = CreateInstantAdd(damage, ScalableFloat.FromTable("damage"));
            EffectSpec spec = EffectSpec.FromAbility(effect, activation);
            target.TryApplyEffect(in spec, out _);

            Assert.That(() =>
            {
                target.TryApplyEffect(in spec, out _);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(target.GetAttributeValue(damage), Is.GreaterThanOrEqualTo(200f));
        }
    }
}
#endif
