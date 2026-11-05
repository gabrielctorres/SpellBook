using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Values;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>Formato do <c>ScalableFloat</c> no resumo de uma linha do <c>TypeCategory.Line</c> (Spec do B-009, seção 2; R22).</summary>
    public class ScalableFloatSummaryTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private CultureInfo _culture;

        [SetUp]
        public void SetUp()
        {
            _culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        }

        [TearDown]
        public void TearDown()
        {
            CultureInfo.CurrentCulture = _culture;
            foreach (UnityEngine.Object created in _created)
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void Summary_ScalableFloat_ConstantTableAndPerLevel()
        {
            var fields = new ScalableSummaryFields
            {
                scale = ScalableFloat.Constant(2.5f),
                tableScale = ScalableFloat.FromTable("Dano"),
                arrayScale = ScalableFloat.FromArray(1f, 2f),
            };
            var holder = ScriptableObject.CreateInstance<ScalableSummaryHolder>();
            holder.node = fields;
            _created.Add(holder);

            string line = SummaryFormatter.Format(new SerializedObject(holder).FindProperty(nameof(ScalableSummaryHolder.node)));

            Assert.That(line, Is.EqualTo("2,5 %Dano por nível"));
        }
    }

    internal sealed class ScalableSummaryHolder : ScriptableObject
    {
        [SerializeReference] internal ScalableSummaryFields node;
    }

    [Serializable]
    [TypeCategory("Teste", Line = "{Scale} {TableScale} {ArrayScale}")]
    internal sealed class ScalableSummaryFields
    {
        [SerializeField] internal ScalableFloat scale = ScalableFloat.Constant(0f);
        [SerializeField] internal ScalableFloat tableScale = ScalableFloat.Constant(0f);
        [SerializeField] internal ScalableFloat arrayScale = ScalableFloat.Constant(0f);
    }
}
