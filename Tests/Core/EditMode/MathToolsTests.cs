using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SpellBook.Core.Tests
{
    /// <summary>
    /// <c>MathTools</c> é uma classe estática do Core, sem <c>MonoBehaviour</c> em volta (CA43, decisão I31).
    /// Os testes de comportamento caracterizam os métodos atuais, que mudam de lugar sem mudar de resultado.
    /// </summary>
    public class MathToolsTests
    {
        private const int RandomSamples = 1000;
        private const float Tolerance = 1e-4f;

        [Test]
        public void MathTools_IsStaticInCoreNamespace()
        {
            Type type = typeof(DocUrls).Assembly.GetType("SpellBook.Core.MathTools");

            Assert.That(type, Is.Not.Null, "SpellBook.Core.MathTools não existe no assembly SpellBook.Core.");
            Assert.That(type.IsAbstract && type.IsSealed, Is.True, "MathTools deve ser static.");
            Assert.That(type.DeclaringType, Is.Null, "MathTools não pode estar aninhada em outro tipo.");
        }

        [Test]
        public void NoMonoBehaviourNamedMath()
        {
            IEnumerable<string> found = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("SpellBook.", StringComparison.Ordinal))
                .SelectMany(a => a.GetTypes())
                .Where(t => t.Name == "Math" && typeof(MonoBehaviour).IsAssignableFrom(t))
                .Select(t => t.AssemblyQualifiedName);

            Assert.That(found, Is.Empty);
        }

        [TestCase(5f, 0f, 10f, 0f, 100f, 50f)]
        [TestCase(0f, 0f, 10f, 20f, 40f, 20f)]
        [TestCase(10f, 0f, 10f, 20f, 40f, 40f)]
        [TestCase(2f, 0f, 10f, 10f, 0f, 8f)]
        [TestCase(15f, 0f, 10f, 0f, 100f, 150f)]
        public void Map_ConvertsLinearlyBetweenRanges(float value, float inMin, float inMax, float outMin, float outMax, float expected)
        {
            float result = MathTools.Map(value, inMin, inMax, outMin, outMax);

            Assert.That(result, Is.EqualTo(expected).Within(Tolerance));
        }

        [TestCase(0f, true)]
        [TestCase(0.5f, true)]
        [TestCase(1f, true)]
        [TestCase(-0.01f, false)]
        [TestCase(1.01f, false)]
        public void IsInRange_IncludesBothBounds(float value, bool expected)
        {
            bool result = MathTools.IsInRange(value, 0f, 1f);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void MidPoint_IsHalfwayBetweenPoints()
        {
            Vector3 result = MathTools.MidPoint(new Vector3(0f, 0f, 0f), new Vector3(2f, 4f, -6f));

            Assert.That(result, Is.EqualTo(new Vector3(1f, 2f, -3f)));
        }

        [Test]
        public void CenterOfMass_IsAverageOfPoints()
        {
            Vector3[] points = { new Vector3(0f, 0f, 0f), new Vector3(3f, 0f, 0f), new Vector3(0f, 6f, 9f) };

            Vector3 result = MathTools.CenterOfMass(points);

            Assert.That(result, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void RandomPointInCircle_IsInsideRadius()
        {
            var center = new Vector2(3f, -2f);
            const float radius = 2.5f;
            UnityEngine.Random.State previous = UnityEngine.Random.state;
            try
            {
                for (int i = 0; i < RandomSamples; i++)
                {
                    Vector2 point = MathTools.RandomPointInCircle(center, radius);

                    Assert.That(Vector2.Distance(center, point), Is.LessThanOrEqualTo(radius + Tolerance));
                }
            }
            finally
            {
                UnityEngine.Random.state = previous;
            }
        }

        [Test]
        public void RandomPointInSphere_IsInsideRadius()
        {
            var center = new Vector3(1f, 2f, 3f);
            const float radius = 4f;
            UnityEngine.Random.State previous = UnityEngine.Random.state;
            try
            {
                for (int i = 0; i < RandomSamples; i++)
                {
                    Vector3 point = MathTools.RandomPointInSphere(center, radius);

                    Assert.That(Vector3.Distance(center, point), Is.LessThanOrEqualTo(radius + Tolerance));
                }
            }
            finally
            {
                UnityEngine.Random.state = previous;
            }
        }
    }
}
