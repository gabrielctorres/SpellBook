using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.Time.Tests
{
    /// <summary>Grupo pausável (CA46, parte; PRD P19 e P20; Spec 4.3, I-b).</summary>
    public class PausableTimeSourceTests
    {
        private const int Frames = 5;
        private const double Tolerance = 1e-9;

        private PausableTimeSource _source;
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = UnityEngine.Time.timeScale;
            UnityEngine.Time.timeScale = 1f;
            _source = ScriptableObject.CreateInstance<PausableTimeSource>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Time.timeScale = _timeScale;
            Object.DestroyImmediate(_source);
            TimeSourceAsset.ResetAllOnSessionStart();
        }

        [UnityTest]
        public IEnumerator Pausable_PausedHasZeroDeltaAndFrozenElapsed()
        {
            yield return null;
            _source.Pause();
            double frozen = _source.ElapsedTime;

            for (int i = 0; i < Frames; i++)
            {
                yield return null;
            }

            Assert.That(_source.IsPaused, Is.True);
            Assert.That(_source.DeltaTime, Is.EqualTo(0f));
            Assert.That(_source.ElapsedTime, Is.EqualTo(frozen));
        }

        [UnityTest]
        public IEnumerator Pausable_ResumeContinuesFromSameValue()
        {
            yield return null;
            _source.Pause();
            double frozen = _source.ElapsedTime;
            for (int i = 0; i < Frames; i++)
            {
                yield return null;
            }

            _source.Resume();

            Assert.That(_source.IsPaused, Is.False);
            Assert.That(_source.ElapsedTime, Is.EqualTo(frozen).Within(Tolerance), "salto ao retomar");
            yield return null;
            Assert.That(_source.ElapsedTime, Is.EqualTo(frozen + UnityEngine.Time.deltaTime).Within(1e-6));
            Assert.That(_source.DeltaTime, Is.EqualTo(UnityEngine.Time.deltaTime));
        }

        [UnityTest]
        public IEnumerator Pausable_ResetOnSessionStart()
        {
            yield return null;
            _source.SetPaused(true);
            for (int i = 0; i < Frames; i++)
            {
                yield return null;
            }

            TimeSourceAsset.ResetAllOnSessionStart();

            Assert.That(_source.IsPaused, Is.False);
            Assert.That(_source.ElapsedTime, Is.EqualTo(0d).Within(Tolerance));
            yield return null;
            Assert.That(_source.ElapsedTime, Is.EqualTo((double)UnityEngine.Time.deltaTime).Within(1e-6));
        }

        [UnityTest]
        public IEnumerator Pausable_AfterReset_FirstReadIsZeroEvenIfFramesPassed()
        {
            yield return null;
            TimeSourceAsset.ResetAllOnSessionStart();

            for (int i = 0; i < Frames; i++)
            {
                yield return null;
            }

            Assert.That(_source.ElapsedTime, Is.EqualTo(0d).Within(Tolerance));
        }

        [UnityTest]
        public IEnumerator Pausable_AfterReset_PauseBeforeAnyRead_ElapsedIsZero()
        {
            yield return null;
            TimeSourceAsset.ResetAllOnSessionStart();
            for (int i = 0; i < Frames; i++)
            {
                yield return null;
            }

            _source.Pause();
            for (int i = 0; i < Frames; i++)
            {
                yield return null;
            }

            Assert.That(_source.IsPaused, Is.True);
            Assert.That(_source.ElapsedTime, Is.EqualTo(0d).Within(Tolerance));
        }
    }
}
