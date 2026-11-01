using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Values;
using UnityEditor;
using UnityEngine;

namespace SpellBook.Core.Tests.Inspector
{
    /// <summary>Resumo de uma linha pelo <c>TypeCategory.Line</c> (Spec do B-009, seção 2; R22).</summary>
    public class SummaryFormatterTests
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
        public void Summary_Fields_Bool_ObjectName_ListCount_NoLineUsesTypeName()
        {
            var target = ScriptableObject.CreateInstance<SummaryHolder>();
            target.name = "Alvo";
            _created.Add(target);
            var fields = new SummaryFields
            {
                amount = 1.234f,
                count = 3,
                flag = true,
                other = false,
                target = target,
                items = new List<int> { 4, 5 },
                mode = SummaryMode.SecondOption,
                scale = ScalableFloat.Constant(2.5f),
                tableScale = ScalableFloat.FromTable("Dano"),
                arrayScale = ScalableFloat.FromArray(1f, 2f),
                label = "texto",
            };
            var empty = new SummaryFields { target = null, items = new List<int>(), scale = ScalableFloat.Constant(10f) };

            Assert.That(
                SummaryFormatter.Format(Property(fields)),
                Is.EqualTo("1,23 3 sempre Alvo 2 Second Option 2,5 %Dano por nível texto {Missing}"));
            Assert.That(SummaryFormatter.Format(Property(empty)), Does.StartWith("0 0 (nenhum) 0 First Option 10 "));
            Assert.That(SummaryFormatter.Format(Property(new SummaryPlain())), Is.EqualTo("Summary Plain"));
            Assert.That(SummaryFormatter.Format(Property(null)), Is.EqualTo("(nenhum)"));
        }

        [Test]
        public void Summary_NestedReference_UsesChildLine()
        {
            var nested = new SummaryWrap { inner = new SummaryWrap { inner = new SummaryPlain() } };
            var deep = new SummaryWrap
            {
                inner = new SummaryWrap { inner = new SummaryWrap { inner = new SummaryWrap { inner = new SummaryPlain() } } },
            };
            var emptyChild = new SummaryWrap();

            Assert.That(SummaryFormatter.Format(Property(nested)), Is.EqualTo("[[Summary Plain]]"));
            Assert.That(SummaryFormatter.Format(Property(deep)), Is.EqualTo("[[[Summary Wrap]]]"));
            Assert.That(SummaryFormatter.Format(Property(emptyChild)), Is.EqualTo("[(nenhum)]"));
        }

        private SerializedProperty Property(SummaryNode node)
        {
            var holder = ScriptableObject.CreateInstance<SummaryHolder>();
            holder.node = node;
            _created.Add(holder);
            return new SerializedObject(holder).FindProperty(nameof(SummaryHolder.node));
        }
    }

    internal sealed class SummaryHolder : ScriptableObject
    {
        [SerializeReference] internal SummaryNode node;
    }

    internal enum SummaryMode
    {
        FirstOption,
        SecondOption,
    }

    [Serializable]
    internal abstract class SummaryNode
    {
    }

    [Serializable]
    [TypeCategory("Teste", Line = "{Amount} {Count} {Flag?sempre} {Other?nunca} {Target} {Items} {Mode} {Scale} {TableScale} {ArrayScale} {Label} {Missing}")]
    internal sealed class SummaryFields : SummaryNode
    {
        [SerializeField] internal float amount;
        [SerializeField] internal int count;
        [SerializeField] internal bool flag;
        [SerializeField] internal bool other;
        [SerializeField] internal UnityEngine.Object target;
        [SerializeField] internal List<int> items = new List<int>();
        [SerializeField] internal SummaryMode mode;
        [SerializeField] internal ScalableFloat scale = ScalableFloat.Constant(0f);
        [SerializeField] internal ScalableFloat tableScale = ScalableFloat.Constant(0f);
        [SerializeField] internal ScalableFloat arrayScale = ScalableFloat.Constant(0f);
        [SerializeField] internal string label;
    }

    [Serializable]
    [TypeCategory("Teste", Line = "[{Inner}]")]
    internal sealed class SummaryWrap : SummaryNode
    {
        [SerializeReference] internal SummaryNode inner;
    }

    [Serializable]
    [TypeCategory("Teste")]
    internal sealed class SummaryPlain : SummaryNode
    {
    }
}
