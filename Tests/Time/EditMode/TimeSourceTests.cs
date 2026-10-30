using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Time.Tests
{
    /// <summary>Fontes de tempo do módulo Time (ADR 0014; Spec 4.3; CA44, parte).</summary>
    public class TimeSourceTests
    {
        private const string ScaledMenu = "SpellBook/Time/Scaled Time Source";
        private const string UnscaledMenu = "SpellBook/Time/Unscaled Time Source";

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            TimeSourceAsset.ResetAllOnSessionStart();
        }

        [Test]
        public void Manual_NewInstance_StartsAtZero()
        {
            ManualTimeSource source = CreateManual();

            Assert.That(source.ElapsedTime, Is.EqualTo(0d));
            Assert.That(source.DeltaTime, Is.EqualTo(0f));
        }

        [Test]
        public void Manual_AdvanceAccumulates()
        {
            ManualTimeSource source = CreateManual();

            source.Advance(0.5f);
            source.Advance(0.25f);

            Assert.That(source.ElapsedTime, Is.EqualTo(0.75d));
            Assert.That(source.DeltaTime, Is.EqualTo(0.25f));
        }

        [Test]
        public void Manual_NegativeAdvanceThrows()
        {
            ManualTimeSource source = CreateManual();
            source.Advance(1f);

            Assert.Throws<ArgumentOutOfRangeException>(() => source.Advance(-0.1f));
            Assert.That(source.ElapsedTime, Is.EqualTo(1d));
            Assert.That(source.DeltaTime, Is.EqualTo(1f));
        }

        [Test]
        public void Manual_ResetOnSessionStart()
        {
            ManualTimeSource source = CreateManual();
            source.Advance(2f);

            TimeSourceAsset.ResetAllOnSessionStart();

            Assert.That(source.ElapsedTime, Is.EqualTo(0d));
            Assert.That(source.DeltaTime, Is.EqualTo(0f));
        }

        [Test]
        public void Manual_ResetTwice_StillRegistered()
        {
            ManualTimeSource source = CreateManual();
            source.Advance(2f);
            TimeSourceAsset.ResetAllOnSessionStart();
            source.Advance(3f);

            TimeSourceAsset.ResetAllOnSessionStart();

            Assert.That(source.ElapsedTime, Is.EqualTo(0d), "a fonte saiu do registro no primeiro reset");
            Assert.That(source.DeltaTime, Is.EqualTo(0f));
        }

        [Test]
        public void SessionHook_IsSubsystemRegistration()
        {
            MethodInfo hook = typeof(TimeSourceAsset).GetMethod(
                nameof(TimeSourceAsset.ResetAllOnSessionStart),
                BindingFlags.Static | BindingFlags.NonPublic);

            RuntimeInitializeOnLoadMethodAttribute attribute =
                hook?.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();

            Assert.That(attribute, Is.Not.Null, "gancho sem [RuntimeInitializeOnLoadMethod]");
            Assert.That(attribute.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));
        }

        [Test]
        public void Menu_HasScaledAndUnscaled()
        {
            Dictionary<Type, string> menus = TimeSourceMenus();

            Assert.That(menus.TryGetValue(typeof(ScaledTimeSource), out string scaled), Is.True, "ScaledTimeSource sem menu");
            Assert.That(scaled, Is.EqualTo(ScaledMenu));
            Assert.That(menus.TryGetValue(typeof(UnscaledTimeSource), out string unscaled), Is.True, "UnscaledTimeSource sem menu");
            Assert.That(unscaled, Is.EqualTo(UnscaledMenu));
        }

        [Test]
        public void Menu_ManualHasNoEntry()
        {
            Assert.That(TimeSourceMenus().Keys, Has.No.Member(typeof(ManualTimeSource)));
        }

        [Test]
        public void Default_IsScaled()
        {
            TimeSourceAsset first = TimeSourceAsset.Scaled;
            TimeSourceAsset second = TimeSourceAsset.Scaled;

            Assert.That(first, Is.InstanceOf<ScaledTimeSource>());
            Assert.That(second, Is.SameAs(first));
            Assert.That(first.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
        }

        [Test]
        public void Scaled_ReadsUnityScaledTime()
        {
            ScaledTimeSource source = Create<ScaledTimeSource>();

            Assert.That(source.ElapsedTime, Is.EqualTo(UnityEngine.Time.timeAsDouble));
            Assert.That(source.DeltaTime, Is.EqualTo(UnityEngine.Time.deltaTime));
        }

        [Test]
        public void Unscaled_ReadsUnityUnscaledTime()
        {
            UnscaledTimeSource source = Create<UnscaledTimeSource>();

            Assert.That(source.ElapsedTime, Is.EqualTo(UnityEngine.Time.unscaledTimeAsDouble));
            Assert.That(source.DeltaTime, Is.EqualTo(UnityEngine.Time.unscaledDeltaTime));
        }

        private ManualTimeSource CreateManual()
        {
            return Create<ManualTimeSource>();
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _created.Add(instance);
            return instance;
        }

        private static Dictionary<Type, string> TimeSourceMenus()
        {
            return typeof(TimeSourceAsset).Assembly.GetTypes()
                .Where(t => typeof(TimeSourceAsset).IsAssignableFrom(t))
                .Select(t => (Type: t, Menu: t.GetCustomAttribute<CreateAssetMenuAttribute>(false)))
                .Where(entry => entry.Menu != null)
                .ToDictionary(entry => entry.Type, entry => entry.Menu.menuName);
        }
    }
}
