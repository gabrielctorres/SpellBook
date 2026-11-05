#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Text;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using SpellBook.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Fonte da lista de buffs e debuffs (Spec do B-018, BF1a): um ícone por efeito na ordem em que apareceu, pelos eventos
    /// do ASC e pelo <see cref="EffectAggregate"/>; pilhas, papel, ícone e nome do asset (CA1, CA3, CA4, CA6, CA11 e CA17).
    /// </summary>
    public class ActiveEffectsSourceTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;
        private TimeFactory _time;
        private ManualTimeSource _clock;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _clock = _time.CreateManual();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Apply_AddsIcon_Expire_RemovedAndReleased_RemoveByCodeTagStacks_OnlyWhenLast()
        {
            AbilitySystemComponent asc = Asc();
            GameplayTag poison = _gas.CreateTag("Status.Veneno");
            GameplayEffect burn = Effect(EffectDurationPolicy.HasDuration, 5f);
            GameplayEffect venom = Effect(EffectDurationPolicy.HasDuration, 5f, poison);
            burn.CanStack = true;
            burn.MaxStacks = 5;
            Receiver list = Attach(new ActiveEffectsSource(), asc);

            asc.ApplyEffect(burn);
            Assert.That(list.Subjects(), Is.EqualTo(new Object[] { burn }), "aplicar acrescenta o ícone");
            asc.Tick(5f);
            Assert.That(list.Count, Is.Zero, "expirar remove");
            Assert.That(list.Removes, Is.EqualTo(1), "o id é devolvido uma vez");

            asc.RemoveEffect(asc.ApplyEffect(burn));
            Assert.That(list.Count, Is.Zero, "remover por código");

            asc.ApplyEffect(venom);
            asc.RemoveEffectsWithTag(poison);
            Assert.That(list.Count, Is.Zero, "remover por tag");

            ActiveEffectInstance stacked = asc.ApplyEffect(burn);
            asc.ApplyEffect(burn);
            asc.RemoveEffectStacks(stacked, 1);
            Assert.That(list.Count == 1 && list.Model(0).Stacks == 1, Is.True, "tirar uma pilha atualiza e não remove");
            int removes = list.Removes;
            asc.SetEffectStacks(stacked, 0);
            Assert.That(list.Count, Is.Zero, "pilhas zeradas removem");
            Assert.That(list.Removes - removes, Is.EqualTo(1), "pilhas zeradas e o OnEffectRemoved removem uma vez só");
        }

        [Test]
        public void Stacks_ByEvent_HiddenAtOneByDefault()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect burn = Effect(EffectDurationPolicy.HasDuration, 10f);
            burn.CanStack = true;
            burn.MaxStacks = 5;
            Receiver list = Attach(new ActiveEffectsSource(), asc);
            Receiver always = Attach(new ActiveEffectsSource { MinStacksToShow = 1 }, asc);

            ActiveEffectInstance instance = asc.ApplyEffect(burn);
            Assert.That(list.Model(0).Stacks == 1 && !list.Model(0).ShowStacks, Is.True, "1 pilha não aparece por padrão");
            Assert.That(always.Model(0).ShowStacks, Is.True, "o mínimo é configurável");

            asc.ApplyEffect(burn);
            Assert.That(list.Model(0).Stacks == 2 && list.Model(0).ShowStacks, Is.True, "empilhar atualiza por evento");
            asc.SetEffectStacks(instance, 4);
            Assert.That(list.Model(0).Stacks, Is.EqualTo(4));
            Assert.That(list.Adds, Is.EqualTo(1), "pilhas não criam ícone novo");
        }

        [Test]
        public void Role_FromBuffDebuff_NeutralDefault()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect buff = Effect(EffectDurationPolicy.Infinite, 0f);
            GameplayEffect debuff = Effect(EffectDurationPolicy.Infinite, 0f);
            GameplayEffect plain = Effect(EffectDurationPolicy.Infinite, 0f);
            GameplayEffect both = Effect(EffectDurationPolicy.Infinite, 0f);
            Set(buff, "isBuff", true);
            Set(debuff, "isDebuff", true);
            Set(both, "isBuff", true);
            Set(both, "isDebuff", true);
            Receiver list = Attach(new ActiveEffectsSource(), asc);

            asc.ApplyEffect(buff);
            asc.ApplyEffect(debuff);
            asc.ApplyEffect(plain);
            asc.ApplyEffect(both);

            Assert.That(new[] { list.Model(0).Role, list.Model(1).Role, list.Model(2).Role, list.Model(3).Role },
                Is.EqualTo(new[] { TextRole.Beneficial, TextRole.Harmful, TextRole.Neutral, TextRole.Neutral }));
        }

        [Test]
        public void ApplicationOrder_NewAtEnd_PositionFixed()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect a = Effect(EffectDurationPolicy.HasDuration, 10f);
            GameplayEffect b = Effect(EffectDurationPolicy.HasDuration, 3f);
            GameplayEffect c = Effect(EffectDurationPolicy.Infinite, 0f);
            a.CanStack = true;
            a.MaxStacks = 5;
            Receiver list = Attach(new ActiveEffectsSource(), asc);
            asc.ApplyEffect(a);
            ActiveEffectInstance second = asc.ApplyEffect(b);
            asc.ApplyEffect(c);

            asc.ApplyEffect(a);
            asc.Tick(1f);
            asc.ApplyEffect(b);
            Assert.That(list.Subjects(), Is.EqualTo(new Object[] { a, b, c }), "tempo, pilhas e renovação não reordenam");

            asc.RemoveEffect(second);
            asc.ApplyEffect(b);
            Assert.That(list.Subjects(), Is.EqualTo(new Object[] { a, c, b }), "o efeito que volta entra no fim");
        }

        [Test]
        public void OpenWithActiveEffects_ShowsAll()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect burn = Effect(EffectDurationPolicy.HasDuration, 10f);
            burn.CanStack = true;
            burn.MaxStacks = 5;
            GameplayEffect hidden = Effect(EffectDurationPolicy.HasDuration, 10f);
            GameplayEffect aura = Effect(EffectDurationPolicy.Infinite, 0f);
            Set(hidden, "isHidden", true);
            for (int i = 0; i < 3; i++) asc.ApplyEffect(burn);
            asc.ApplyEffect(hidden);
            asc.ApplyEffect(aura);

            Receiver list = Attach(new ActiveEffectsSource(), asc);

            Assert.That(list.Subjects(), Is.EqualTo(new Object[] { burn, aura }), "todos os visíveis, na ordem de aplicação");
            Assert.That(list.Model(0).Stacks, Is.EqualTo(3));
            asc.ApplyEffect(Effect(EffectDurationPolicy.Infinite, 0f, hidden: true));
            Assert.That(list.Count, Is.EqualTo(2), "IsHidden nunca aparece");
        }

        [Test]
        public void IconAndName_FromEffectFields()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect burn = Effect(EffectDurationPolicy.HasDuration, 10f);
            GameplayEffect bare = Effect(EffectDurationPolicy.HasDuration, 10f);
            Sprite icon = Track(Sprite.Create(Track(new Texture2D(1, 1)), new Rect(0f, 0f, 1f, 1f), Vector2.zero));
            using (var serialized = new SerializedObject(burn))
            {
                serialized.FindProperty("icon").objectReferenceValue = icon;
                serialized.FindProperty("localizedName.key").stringValue = "test.burn";
                serialized.FindProperty("localizedName.text").stringValue = "Queimadura";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Receiver list = Attach(new ActiveEffectsSource(), asc, Track(ScriptableObject.CreateInstance<TestText>()));
            asc.ApplyEffect(burn);
            asc.ApplyEffect(bare);

            Assert.That(list.Model(0).Icon, Is.SameAs(icon));
            Assert.That(list.Model(0).Name, Is.EqualTo("Burn"), "o nome pelo provedor");
            Assert.That(list.Model(0).Subject, Is.SameAs(burn), "o asset é o assunto do item");
            Assert.That(list.Model(1).Icon, Is.Null, "sem ícone: a view usa o padrão");
            Assert.That(list.Model(1).Name, Is.Null.Or.Empty);
        }

        private Receiver Attach(ActiveEffectsSource source, AbilitySystemComponent asc, ITextProvider text = null)
        {
            var receiver = new Receiver();
            UIListBinding<EffectIconModel> binding = source.CreateBinding();
            binding.SetReceiver(receiver);
            binding.Attach(asc, new UIBindContext(text));
            return receiver;
        }

        private GameplayEffect Effect(EffectDurationPolicy policy, float duration, GameplayTag tag = default, bool hidden = false)
        {
            GameplayEffect effect = tag.IsValid ? _gas.CreateEffect(policy, duration, tag) : _gas.CreateEffect(policy, duration);
            if (hidden) Set(effect, "isHidden", true);
            return effect;
        }

        private static void Set(GameplayEffect effect, string field, bool value)
        {
            using var serialized = new SerializedObject(effect);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private AbilitySystemComponent Asc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_clock);
            return asc;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        /// <summary>Receptor de teste: os itens na ordem da lista, e quantos foram acrescentados e removidos.</summary>
        private sealed class Receiver : IUIListReceiver<EffectIconModel>
        {
            private readonly List<int> _ids = new List<int>();
            private readonly List<EffectIconModel> _models = new List<EffectIconModel>();

            public int Adds;
            public int Removes;

            public int Count => _ids.Count;

            public EffectIconModel Model(int index) => _models[index];

            public void AddItem(int id, in EffectIconModel item)
            {
                int index = _ids.IndexOf(id);
                if (index >= 0)
                {
                    _models[index] = item;
                    return;
                }

                Adds++;
                _ids.Add(id);
                _models.Add(item);
            }

            public void UpdateItem(int id, in EffectIconModel item)
            {
                int index = _ids.IndexOf(id);
                if (index >= 0) _models[index] = item;
            }

            public void RemoveItem(int id)
            {
                int index = _ids.IndexOf(id);
                if (index < 0) return;

                Removes++;
                _ids.RemoveAt(index);
                _models.RemoveAt(index);
            }

            public void ClearItems()
            {
                _ids.Clear();
                _models.Clear();
            }

            public Object[] Subjects()
            {
                var subjects = new Object[_models.Count];
                for (int i = 0; i < subjects.Length; i++) subjects[i] = _models[i].Subject;
                return subjects;
            }
        }

        /// <summary>Provedor de teste: troca o nome do efeito pela chave.</summary>
        public sealed class TestText : TextProviderAsset
        {
            public override IFormatProvider Culture => System.Globalization.CultureInfo.InvariantCulture;

            public override string GetText(string key, string fallback) => key == "test.burn" ? "Burn" : fallback;
        }
    }
}
#endif
