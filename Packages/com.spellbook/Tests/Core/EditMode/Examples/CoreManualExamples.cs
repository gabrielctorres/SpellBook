// --8<-- [start:mathtools-using]
using SpellBook.Core;

// --8<-- [end:mathtools-using]
using System;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Conditions;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Inspector;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Core.Tests.Examples
{
    /// <summary>
    /// Exemplos da página <c>inicio/conceitos.md</c> do manual. Cada trecho entre os marcadores <c>--8&lt;--</c> aparece na
    /// página, e os testes conferem o que o texto em volta diz dele (guia do manual, parte 9).
    /// </summary>
    public class CoreManualExamples
    {
        [Test]
        public void AndCondition_EmptyList_IsTrue_AndPickerOffersOnlyThisContext()
        {
            var condition = new MyAndCondition();

            bool result = condition.Evaluate(new MyContext { Health = 10f, MaxHealth = 100f });

            Type[] candidates = TypePickerCatalog.GetCandidates(typeof(Condition<MyContext>)).Select(e => e.Type).ToArray();
            Assert.That(result, Is.True);
            Assert.That(candidates, Is.EquivalentTo(new[] { typeof(MyAndCondition), typeof(LowHealthCondition) }));
        }

        [Test]
        public void LowHealthCondition_LineAndEvaluate()
        {
            var holder = ScriptableObject.CreateInstance<ConditionHolder>();
            holder.condition = new LowHealthCondition();
            SerializedProperty property = new SerializedObject(holder).FindProperty(nameof(ConditionHolder.condition));

            string line = SummaryFormatter.Format(property);
            bool low = holder.condition.Evaluate(new MyContext { Health = 20f, MaxHealth = 100f });
            bool high = holder.condition.Evaluate(new MyContext { Health = 50f, MaxHealth = 100f });

            Object.DestroyImmediate(holder);
            Assert.That(line, Is.EqualTo("Vida abaixo de 30%"));
            Assert.That(low, Is.True);
            Assert.That(high, Is.False);
        }

        [Test]
        public void RuntimeSet_AddIgnoresDuplicate_RemoveIgnoresMissing()
        {
            var set = ScriptableObject.CreateInstance<EnemySet>();
            var enemy = new GameObject("Inimigo").AddComponent<Enemy>();
            var other = new GameObject("Outro").AddComponent<Enemy>();

            set.Add(enemy);
            set.Add(enemy);
            set.Remove(other);
            int count = set.Items.Count;

            Object.DestroyImmediate(enemy.gameObject);
            Object.DestroyImmediate(other.gameObject);
            Object.DestroyImmediate(set);
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void MathTools_Map_DistanceToVolume()
        {
            float distance = 5f;

            // --8<-- [start:mathtools-map]
            float volume = MathTools.Map(distance, 0f, 20f, 1f, 0f);
            // --8<-- [end:mathtools-map]

            Assert.That(volume, Is.EqualTo(0.75f).Within(1e-5f));
        }
    }

    /// <summary>Contexto de exemplo das condições: a vida atual e a máxima.</summary>
    public struct MyContext
    {
        public float Health;
        public float MaxHealth;
    }

    // --8<-- [start:and-condition]
    [Serializable]
    public sealed class MyAndCondition : AndConditionBase<MyContext> { }
    // --8<-- [end:and-condition]

    // --8<-- [start:low-health-condition]
    [Serializable]
    [TypeCategory("Exemplo", Line = "Vida abaixo de {Threshold}{Percent?%}")]
    public sealed class LowHealthCondition : Condition<MyContext>
    {
        [SerializeField] private float threshold = 30f;
        [SerializeField] private bool percent = true;

        public override bool Evaluate(in MyContext context)
        {
            float health = percent ? context.Health / context.MaxHealth * 100f : context.Health;
            return health < threshold;
        }
    }
    // --8<-- [end:low-health-condition]

    /// <summary>Item de exemplo do <see cref="EnemySet"/>.</summary>
    public class Enemy : MonoBehaviour
    {
    }

    // --8<-- [start:enemy-set]
    [CreateAssetMenu(menuName = "Game/Enemy Set")]
    public class EnemySet : RuntimeSet<Enemy> { }
    // --8<-- [end:enemy-set]

    /// <summary>Guarda uma condição num campo serializado, para o resumo de uma linha do Inspector.</summary>
    internal sealed class ConditionHolder : ScriptableObject
    {
        [SerializeReference] public Condition<MyContext> condition;
    }
}
