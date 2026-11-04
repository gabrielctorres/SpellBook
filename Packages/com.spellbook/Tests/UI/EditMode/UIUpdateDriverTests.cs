using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Time;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.UI.Tests
{
    /// <summary>
    /// Driver de tick da UI: roda só com ligação pedindo tick (C7), entrega o tempo da fonte e para com ela (CA7), aguenta
    /// saída durante o laço e não aloca (CA13, parte). Tema com campos vazios. ADR 0039, item 1.
    /// </summary>
    public class UIUpdateDriverTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void NoRequester_DriverDisabled_TickDoesNothing()
        {
            UIUpdateDriver driver = CreateDriver();
            TickBinding idle = CreateBinding(driver, wantsTick: false);

            driver.Register(idle);
            driver.Tick();

            Assert.That(driver.enabled, Is.False, "sem ligação pedindo tick, o driver não deveria rodar");
            Assert.That(driver.TickingCount, Is.Zero);
            Assert.That(idle.Ticks, Is.Zero);
        }

        [Test]
        public void TicksOnlyRequesters()
        {
            UIUpdateDriver driver = CreateDriver();
            TickBinding first = CreateBinding(driver, wantsTick: true);
            TickBinding idle = CreateBinding(driver, wantsTick: false);
            TickBinding third = CreateBinding(driver, wantsTick: true);
            driver.Register(first);
            driver.Register(idle);
            driver.Register(third);

            driver.Tick();
            bool enabledWithRequesters = driver.enabled;
            first.Want(false);
            third.Detach();
            driver.Tick();
            bool enabledAfterRelease = driver.enabled;
            idle.Want(true);

            Assert.That(enabledWithRequesters, Is.True);
            Assert.That(first.Ticks, Is.EqualTo(1));
            Assert.That(third.Ticks, Is.EqualTo(1));
            Assert.That(idle.Ticks, Is.Zero);
            Assert.That(enabledAfterRelease, Is.False, "soltar o tick e o Detach deveriam tirar a ligação da lista");
            Assert.That(driver.enabled, Is.True, "pedir o tick depois do registro deveria entrar na lista");
            Assert.That(driver.TickingCount, Is.EqualTo(1));
        }

        [Test]
        public void PausedSource_NoTimeAdvance()
        {
            UIUpdateDriver driver = CreateDriver();
            bool scaledByDefault = driver.TimeSource == TimeSourceAsset.Scaled;
            var time = Create<ManualTimeSource>();
            driver.SetTimeSource(time);
            TickBinding binding = CreateBinding(driver, wantsTick: true);
            driver.Register(binding);

            time.Advance(0.5f);
            driver.Tick();
            driver.Tick();
            driver.Tick();
            double whilePaused = binding.LastTime;
            time.Advance(0.25f);
            driver.Tick();

            Assert.That(scaledByDefault, Is.True, "fonte vazia deveria cair na TimeSourceAsset.Scaled");
            Assert.That(binding.Ticks, Is.EqualTo(4));
            Assert.That(whilePaused, Is.EqualTo(0.5d), "com a fonte parada, o tempo entregue deveria ficar parado");
            Assert.That(binding.LastTime, Is.EqualTo(0.75d));
        }

        [Test]
        public void UnregisterDuringTick_Safe()
        {
            UIUpdateDriver driver = CreateDriver();
            TickBinding first = CreateBinding(driver, wantsTick: true);
            TickBinding second = CreateBinding(driver, wantsTick: true);
            TickBinding third = CreateBinding(driver, wantsTick: true);
            driver.Register(first);
            driver.Register(second);
            driver.Register(third);
            second.OnTicked = () =>
            {
                driver.Unregister(first);
                second.Want(false);
            };

            Assert.DoesNotThrow(() => driver.Tick());
            driver.Tick();

            Assert.That(first.Ticks, Is.EqualTo(1));
            Assert.That(second.Ticks, Is.EqualTo(1));
            Assert.That(third.Ticks, Is.EqualTo(2), "a saída durante o laço não deveria pular a ligação seguinte");
            Assert.That(driver.TickingCount, Is.EqualTo(1));
        }

        [Test]
        public void Theme_NullRoles_UsesDefault_NullText_Invariant()
        {
            var theme = Create<UITheme>();

            var context = new UIBindContext(theme.Text, null, theme);

            Assert.That(theme.Roles, Is.SameAs(TextRoleTheme.Default));
            Assert.That(theme.GetColor(TextRole.Harmful), Is.EqualTo(TextRoleTheme.Default.GetColor(TextRole.Harmful)));
            Assert.That(theme.Text, Is.Null);
            Assert.That(theme.Culture, Is.SameAs(CultureInfo.InvariantCulture));
            Assert.That(context.Theme, Is.SameAs(theme));
            Assert.That(context.Culture, Is.SameAs(CultureInfo.InvariantCulture));
        }

        [Test]
        public void Theme_Roles_UsesItsColors()
        {
            var roles = Create<TextRoleTheme>();
            SetObjectField(roles, "harmful", Color.blue);
            var theme = Create<UITheme>();
            SetObjectField(theme, "roles", roles);

            Color harmful = theme.GetColor(TextRole.Harmful);

            Assert.That(theme.Roles, Is.SameAs(roles));
            Assert.That(harmful, Is.EqualTo(Color.blue));
        }

        [Test]
        public void Tick10000_DoesNotAllocate()
        {
            UIUpdateDriver driver = CreateDriver();
            var time = Create<ManualTimeSource>();
            driver.SetTimeSource(time);
            TickBinding first = CreateBinding(driver, wantsTick: true);
            TickBinding second = CreateBinding(driver, wantsTick: true);
            driver.Register(first);
            driver.Register(second);
            driver.Tick();

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    driver.Tick();
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(first.Ticks, Is.EqualTo(10001));
            Assert.That(second.Ticks, Is.EqualTo(10001));
        }

        private UIUpdateDriver CreateDriver()
        {
            var gameObject = new GameObject("driver");
            _created.Add(gameObject);
            return gameObject.AddComponent<UIUpdateDriver>();
        }

        private static TickBinding CreateBinding(UIUpdateDriver owner, bool wantsTick)
        {
            var binding = new TickBinding();
            binding.Attach(owner.gameObject, new UIBindContext(null));
            binding.Want(wantsTick);
            return binding;
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _created.Add(instance);
            return instance;
        }

        private static void SetObjectField(Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (value is Color color)
            {
                property.colorValue = color;
            }
            else
            {
                property.objectReferenceValue = (Object)value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class TickBinding : UIBinding
        {
            public int Ticks;
            public double LastTime;
            public Action OnTicked;

            public void Want(bool on) => RequestTick(on);

            public override void Tick(double time)
            {
                Ticks++;
                LastTime = time;
                OnTicked?.Invoke();
            }

            protected override void OnAttach(Object owner)
            {
            }

            protected override void OnDetach()
            {
            }
        }
    }
}
