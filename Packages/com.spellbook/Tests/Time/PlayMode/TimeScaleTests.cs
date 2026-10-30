using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.Time.Tests
{
    /// <summary>Fontes escalada e não escalada com <c>Time.timeScale = 0</c> (CA45; Spec, risco 9).</summary>
    public class TimeScaleTests
    {
        private const int Frames = 5;

        private readonly List<Object> _created = new List<Object>();
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = UnityEngine.Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Time.timeScale = _timeScale;

            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            TimeSourceAsset.ResetAllOnSessionStart();
        }

        [UnityTest]
        public IEnumerator Scaled_DoesNotAccumulateWithTimeScaleZero()
        {
            ScaledTimeSource source = Create<ScaledTimeSource>();
            UnityEngine.Time.timeScale = 0f;
            yield return null;
            double before = source.ElapsedTime;

            for (int i = 0; i < Frames; i++)
            {
                yield return null;
            }

            Assert.That(source.ElapsedTime, Is.EqualTo(before));
            Assert.That(source.DeltaTime, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator Unscaled_AccumulatesWithTimeScaleZero()
        {
            UnscaledTimeSource source = Create<UnscaledTimeSource>();
            UnityEngine.Time.timeScale = 0f;
            yield return null;
            double before = source.ElapsedTime;

            for (int i = 0; i < Frames; i++)
            {
                yield return null;
            }

            Assert.That(source.ElapsedTime, Is.GreaterThan(before));
            Assert.That(source.DeltaTime, Is.GreaterThan(0f));
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _created.Add(instance);
            return instance;
        }
    }
}
