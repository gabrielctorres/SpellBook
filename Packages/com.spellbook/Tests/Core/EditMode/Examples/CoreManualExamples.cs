// --8<-- [start:mathtools-using]
using SpellBook.Core;

// --8<-- [end:mathtools-using]
// --8<-- [start:using-system]
using System;
// --8<-- [end:using-system]
// --8<-- [start:using-collections]
using System.Collections.Generic;
// --8<-- [end:using-collections]
// --8<-- [start:using-linq]
using System.Linq;
// --8<-- [end:using-linq]
// --8<-- [start:using-nunit]
using NUnit.Framework;
// --8<-- [end:using-nunit]
using SpellBook.Core.Conditions;
// --8<-- [start:using-core-editor]
using SpellBook.Core.Editor;
// --8<-- [end:using-core-editor]
using SpellBook.Core.Editor.Inspector;
// --8<-- [start:using-inspector]
using SpellBook.Core.Inspector;
// --8<-- [end:using-inspector]
// --8<-- [start:using-validation]
using SpellBook.Core.Validation;
// --8<-- [end:using-validation]
// --8<-- [start:using-unityeditor]
using UnityEditor;
// --8<-- [end:using-unityeditor]
// --8<-- [start:using-unityeditor-uielements]
using UnityEditor.UIElements;
// --8<-- [end:using-unityeditor-uielements]
// --8<-- [start:using-unityengine]
using UnityEngine;
// --8<-- [end:using-unityengine]
// --8<-- [start:using-apiupdating]
using UnityEngine.Scripting.APIUpdating;
// --8<-- [end:using-apiupdating]
// --8<-- [start:using-uielements]
using UnityEngine.UIElements;
// --8<-- [end:using-uielements]
using Object = UnityEngine.Object;
// --8<-- [start:using-sb-alias]
using SB = SpellBook.Core.Inspector;
// --8<-- [end:using-sb-alias]

namespace SpellBook.Core.Tests.Examples
{
    /// <summary>
    /// Exemplos das páginas <c>inicio/conceitos.md</c>, <c>inicio/inspector.md</c> e <c>ferramentas/validador.md</c> do manual. Cada trecho entre os marcadores <c>--8&lt;--</c> aparece na
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

        [Test]
        public void TypePicker_MinhaAcao_OffersSerializableSubtypes_WithCategoryAndSummary()
        {
            IReadOnlyList<TypePickerEntry> entries = TypePickerCatalog.GetCandidates(typeof(MinhaAcao));

            TypePickerEntry espera = entries.Single(e => e.Type == typeof(MinhaEspera));
            Assert.That(entries.Select(e => e.Type),
                Is.EquivalentTo(new[] { typeof(MinhaEspera), typeof(MinhaAcaoComAlias), typeof(MinhaEsperaRenomeada) }));
            Assert.That(espera.CategoryPath, Is.EqualTo("Fluxo/Espera"));
            Assert.That(espera.Summary, Is.EqualTo("Espera um tempo antes de seguir."));
        }

        [Test]
        public void TypeCategory_WithAlias_SetsCategory()
        {
            TypePickerEntry entry = TypePickerCatalog.GetCandidates(typeof(MinhaAcao)).Single(e => e.Type == typeof(MinhaAcaoComAlias));

            Assert.That(entry.CategoryPath, Is.EqualTo("Fluxo"));
        }

        [Test]
        public void MovedFrom_OldTypeName_LoadsRenamedType()
        {
            var exemplo = ScriptableObject.CreateInstance<Exemplo>();
            SetFirstAction(exemplo, new MinhaEsperaRenomeada());
            string json = EditorJsonUtility.ToJson(exemplo);
            string current = $"\"class\":\"{nameof(MinhaEsperaRenomeada)}\",\"ns\":\"{typeof(MinhaEsperaRenomeada).Namespace}\","
                + $"\"asm\":\"{typeof(MinhaEsperaRenomeada).Assembly.GetName().Name}\"";
            string oldJson = json.Replace(current, "\"class\":\"EsperaAntiga\",\"ns\":\"Jogo.Acoes\",\"asm\":\"Jogo\"");
            var loaded = ScriptableObject.CreateInstance<Exemplo>();

            EditorJsonUtility.FromJsonOverwrite(oldJson, loaded);
            object action = FirstAction(loaded).managedReferenceValue;

            Object.DestroyImmediate(exemplo);
            Object.DestroyImmediate(loaded);
            Assert.That(oldJson, Is.Not.EqualTo(json), "o JSON não tem o nome do tipo atual: " + json);
            Assert.That(action, Is.TypeOf<MinhaEsperaRenomeada>());
        }

        [TestCase(MinhaConfig.Modo.Fixo, true)]
        [TestCase(MinhaConfig.Modo.Aleatorio, false)]
        public void ShowIf_Enum_ShowsFieldOnlyForValue(MinhaConfig.Modo modo, bool expected)
        {
            var config = ScriptableObject.CreateInstance<MinhaConfig>();
            config.modo = modo;

            bool visible = IsVisible(config, nameof(MinhaConfig.valor));

            Object.DestroyImmediate(config);
            Assert.That(visible, Is.EqualTo(expected));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ShowIf_Bool_ShowsFieldOnlyWhenTrue(bool usarCor)
        {
            var config = ScriptableObject.CreateInstance<MinhaConfig>();
            config.usarCor = usarCor;

            bool visible = IsVisible(config, nameof(MinhaConfig.cor));

            Object.DestroyImmediate(config);
            Assert.That(visible, Is.EqualTo(usarCor));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ShowIf_WithAlias_ShowsFieldOnlyWhenTrue(bool usarCor)
        {
            var config = ScriptableObject.CreateInstance<MinhaConfigComAlias>();
            config.usarCor = usarCor;

            bool visible = IsVisible(config, nameof(MinhaConfigComAlias.cor));

            Object.DestroyImmediate(config);
            Assert.That(visible, Is.EqualTo(usarCor));
        }

        [Test]
        public void OneLineEditor_IsUsedForItsType()
        {
            var config = ScriptableObject.CreateInstance<MinhaConfig>();

            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(config);
            Type editorType = editor.GetType();

            Object.DestroyImmediate(editor);
            Object.DestroyImmediate(config);
            Assert.That(editorType, Is.EqualTo(typeof(MinhaConfigEditor)));
        }

        [Test]
        public void OneLineEditor_WithTrue_IsUsedForSubclasses()
        {
            var filha = ScriptableObject.CreateInstance<MinhaFilha>();

            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(filha);
            Type editorType = editor.GetType();

            Object.DestroyImmediate(editor);
            Object.DestroyImmediate(filha);
            Assert.That(editorType, Is.EqualTo(typeof(MinhaBaseEditor)));
        }

        [Test]
        public void ActionDrawer_CreatePropertyGUI_ShowsOnlyAlvo()
        {
            var exemplo = ScriptableObject.CreateInstance<Exemplo>();
            SetFirstAction(exemplo, new MinhaEspera());
            SerializedProperty action = FirstAction(exemplo);

            VisualElement root = new MinhaAcaoDrawer().CreatePropertyGUI(action);

            Object.DestroyImmediate(exemplo);
            Assert.That(root.childCount, Is.EqualTo(1));
            Assert.That(root[0], Is.TypeOf<PropertyField>());
            Assert.That(((PropertyField)root[0]).bindingPath, Is.EqualTo("acoes.Array.data[0].alvo"));
        }

        [Test]
        public void Porta_WithoutKeyAndZeroTime_ErrorOnKeyAndWarningOnTime()
        {
            var porta = new GameObject("Porta").AddComponent<Porta>();
            var serialized = new SerializedObject(porta);
            serialized.FindProperty("tempoAberta").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var result = new ValidationResult(porta);

            porta.Validate(result);

            Object.DestroyImmediate(porta.gameObject);
            Assert.That(result.Messages.Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.PropertyPath),
                Is.EqualTo(new[] { "chave" }));
            Assert.That(result.Messages.Where(m => m.Severity == ValidationSeverity.Warning).Select(m => m.PropertyPath),
                Is.EqualTo(new[] { "tempoAberta" }));
        }

        [Test]
        public void Porta_WithKeyAndTime_HasNoMessages()
        {
            var porta = new GameObject("Porta").AddComponent<Porta>();
            var chave = new GameObject("Chave");
            var serialized = new SerializedObject(porta);
            serialized.FindProperty("chave").objectReferenceValue = chave;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var result = new ValidationResult(porta);

            porta.Validate(result);

            Object.DestroyImmediate(porta.gameObject);
            Object.DestroyImmediate(chave);
            Assert.That(result.Messages, Is.Empty);
        }

        private static void SetFirstAction(Exemplo exemplo, MinhaAcao action)
        {
            var serialized = new SerializedObject(exemplo);
            SerializedProperty list = serialized.FindProperty("acoes");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).managedReferenceValue = action;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty FirstAction(Exemplo exemplo)
        {
            return new SerializedObject(exemplo).FindProperty("acoes").GetArrayElementAtIndex(0);
        }

        private static bool IsVisible(ScriptableObject target, string field)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(field);
            ShowIfAttribute attribute = target.GetType().GetField(field).GetCustomAttributes(typeof(ShowIfAttribute), false)
                .Cast<ShowIfAttribute>()
                .Single();
            return ShowIfRule.IsVisible(property, attribute);
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

    /// <summary>Base das ações de exemplo da página do Inspector, com o campo que o drawer mostra.</summary>
    [Serializable]
    public abstract class MinhaAcao
    {
        [SerializeField] private string alvo = "";

        /// <summary>Alvo da ação.</summary>
        public string Alvo => alvo;
    }

    // --8<-- [start:typepicker-list]

    public class Exemplo : ScriptableObject
    {
        [SerializeReference, TypePicker]
        private List<MinhaAcao> acoes = new();
    }
    // --8<-- [end:typepicker-list]

    // --8<-- [start:type-category]

    [Serializable]
    [TypeCategory("Fluxo/Espera", Summary = "Espera um tempo antes de seguir.")]
    public class MinhaEspera : MinhaAcao
    {
    }
    // --8<-- [end:type-category]

    // --8<-- [start:type-category-alias]

    [Serializable]
    [SB.TypeCategory("Fluxo")]
    public class MinhaAcaoComAlias : MinhaAcao
    {
    }
    // --8<-- [end:type-category-alias]

    // --8<-- [start:moved-from]

    [Serializable]
    [MovedFrom(autoUpdateAPI: false, sourceNamespace: "Jogo.Acoes", sourceAssembly: "Jogo", sourceClassName: "EsperaAntiga")]
    public class MinhaEsperaRenomeada : MinhaAcao
    {
    }
    // --8<-- [end:moved-from]

    // --8<-- [start:show-if]

    public class MinhaConfig : ScriptableObject
    {
        public enum Modo { Fixo, Aleatorio }

        public Modo modo;

        [ShowIf(nameof(modo), Modo.Fixo)]
        public float valor;

        public bool usarCor;

        [ShowIf(nameof(usarCor), true)]
        public Color cor;
    }
    // --8<-- [end:show-if]

    // --8<-- [start:show-if-alias]

    public class MinhaConfigComAlias : UnityEngine.ScriptableObject
    {
        public bool usarCor;

        [SB.ShowIf(nameof(usarCor), true)]
        public UnityEngine.Color cor;
    }
    // --8<-- [end:show-if-alias]

    // --8<-- [start:one-line-editor]

    [CustomEditor(typeof(MinhaConfig))]
    public sealed class MinhaConfigEditor : SpellBookEditor { }
    // --8<-- [end:one-line-editor]

    /// <summary>Base de exemplo do editor que cobre as subclasses.</summary>
    public abstract class MinhaBase : ScriptableObject
    {
    }

    /// <summary>Subclasse de exemplo, que usa o editor de <see cref="MinhaBase"/>.</summary>
    public class MinhaFilha : MinhaBase
    {
    }

    // --8<-- [start:one-line-editor-children]

    [CustomEditor(typeof(MinhaBase), true)]
    public sealed class MinhaBaseEditor : SpellBookEditor { }
    // --8<-- [end:one-line-editor-children]

    // --8<-- [start:action-drawer]

    [CustomPropertyDrawer(typeof(MinhaAcao), true)]
    public sealed class MinhaAcaoDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.Add(new PropertyField(property.FindPropertyRelative("alvo")));
            return root;
        }
    }
    // --8<-- [end:action-drawer]

    // --8<-- [start:project-validation-test]

    public class ValidacaoDoProjetoTests
    {
        [Test]
        public void Projeto_SemErrosDeValidacao()
        {
            var mensagens = new List<ValidationMessage>();

            ProjectValidator.Validate(ProjectValidator.CollectProjectTargets(), mensagens);

            Assert.That(mensagens.Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.Message), Is.Empty);
        }
    }
    // --8<-- [end:project-validation-test]

    // --8<-- [start:porta]

    public class Porta : MonoBehaviour, IValidatable
    {
        [SerializeField] private GameObject chave;
        [SerializeField] private float tempoAberta = 2f;

        public void Validate(ValidationResult result)
        {
            if (chave == null) result.AddError("Porta sem chave: escolha a Chave.", nameof(chave));
            if (tempoAberta <= 0f) result.AddWarning("Tempo aberta zero: a porta fecha na hora.", nameof(tempoAberta));
        }
    }
    // --8<-- [end:porta]
}
