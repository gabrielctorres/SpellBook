using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using SpellBook.UI;
using SpellBook.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Validação e resumo da lista de efeitos (Spec do B-018, Design 4; R13, R14 e CA19 do PRD): cada mensagem com o
    /// binder como objeto e o campo para o clique.
    /// </summary>
    public class EffectListValidationTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;
        private UnityEditor.Editor _editor;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            if (_editor != null) Object.DestroyImmediate(_editor);
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void Binder_NoSourceNoPrefabPrefabWithoutView_IsError()
        {
            EffectListBinder noSource = Binder(null, IconPrefab(withView: true));
            EffectListBinder noPrefab = Binder(new ActiveEffectsSource(), null);
            GameObject plain = IconPrefab(withView: false);
            EffectListBinder noView = Binder(new ActiveEffectsSource(), plain);

            AssertMessages(noSource, (ValidationSeverity.Error, "source", "Sem fonte. Escolha o que o binder lê do dono."));
            AssertMessages(noPrefab, (ValidationSeverity.Error, "itemPrefab", "Sem prefab de item. Ligue o prefab com a view do item."));
            AssertMessages(noView, (ValidationSeverity.Error, "itemPrefab",
                $"O prefab {plain.name} não tem na raiz um componente que implemente IView<EffectIconModel>. Ligue um prefab que desenhe esse modelo."));
        }

        [Test]
        public void Binder_NegativeMaxVisible_IsError()
        {
            EffectListBinder binder = Binder(new ActiveEffectsSource(), IconPrefab(withView: true));
            Set(binder, so => so.FindProperty("maxVisible").intValue = -1);

            AssertMessages(binder, (ValidationSeverity.Error, "maxVisible",
                "Max Visible é -1: use 0 para mostrar todos ou um limite maior que 0."));
        }

        [Test]
        public void Source_InvalidTagInQuery_IsError_EmptyQueryHasNoMessage()
        {
            EffectListBinder empty = Binder(new ActiveEffectsSource(), IconPrefab(withView: true));
            EffectListBinder invalid = Binder(new ActiveEffectsSource(), IconPrefab(withView: true));
            Set(invalid, so => so.FindProperty("source.filter.query.all.tags").InsertArrayElementAtIndex(0));

            Assert.That(SpellBookEditor.CollectMessages(new Object[] { empty }), Is.Empty, "consulta vazia aceita todos os efeitos");
            AssertMessages(invalid, (ValidationSeverity.Error, "source.filter.query.all.tags.Array.data[0]",
                "Tag vazia em source.filter.query.all (posição 0). Escolha uma tag ou remova a linha."));
        }

        [Test]
        public void Source_NegativeEndingSoon_IsError()
        {
            EffectListBinder binder = Binder(new ActiveEffectsSource(), IconPrefab(withView: true));
            Set(binder, so => so.FindProperty("source.endingSoonSeconds").floatValue = -2f);

            AssertMessages(binder, (ValidationSeverity.Error, "source.endingSoonSeconds",
                "Ending Soon Seconds é negativo: use 0 para sinalizar só no fim ou os segundos finais do efeito."));
        }

        [Test]
        public void Source_ZeroEndingSoon_HasNoMessage()
        {
            EffectListBinder binder = Binder(new ActiveEffectsSource(), IconPrefab(withView: true));
            Set(binder, so => so.FindProperty("source.endingSoonSeconds").floatValue = 0f);

            Assert.That(SpellBookEditor.CollectMessages(new Object[] { binder }), Is.Empty, "0 sinaliza só no fim e é válido");
        }

        [Test]
        public void Binder_Valid_HasNoMessages()
        {
            var source = new ActiveEffectsSource();
            source.SetFilter(new EffectListFilter(EffectRoleFilter.Debuffs, null, true));
            EffectListBinder binder = Binder(source, IconPrefab(withView: true));
            Set(binder, so => so.FindProperty("maxVisible").intValue = 8);

            Assert.That(SpellBookEditor.CollectMessages(new Object[] { binder }), Is.Empty);
        }

        [Test]
        public void Summary_SourceRolePurgableAndMaxVisible()
        {
            var debuffs = new ActiveEffectsSource();
            debuffs.SetFilter(new EffectListFilter(EffectRoleFilter.Debuffs, null, true));
            EffectListBinder limited = Binder(debuffs, IconPrefab(withView: true));
            Set(limited, so => so.FindProperty("maxVisible").intValue = 8);
            EffectListBinder all = Binder(new ActiveEffectsSource(), IconPrefab(withView: true));

            Assert.That(Summary(limited), Is.EqualTo("Lista de efeitos · Só debuffs · só purgáveis · até 8 · dono: no objeto ou pai"));
            Assert.That(Summary(all), Is.EqualTo("Lista de efeitos · Buffs e debuffs · dono: no objeto ou pai"), "0 é sem limite");
        }

        // Cada mensagem com a severidade, o caminho, o texto e o binder como contexto, para o clique
        private static void AssertMessages(Object target, params (ValidationSeverity Severity, string Path, string Text)[] expected)
        {
            IReadOnlyList<ValidationMessage> messages = SpellBookEditor.CollectMessages(new[] { target });

            Assert.That(messages.Select(m => m.Message).ToArray(), Is.EqualTo(expected.Select(e => e.Text).ToArray()));
            Assert.That(messages.Select(m => m.Severity).ToArray(), Is.EqualTo(expected.Select(e => e.Severity).ToArray()));
            Assert.That(messages.Select(m => m.PropertyPath).ToArray(), Is.EqualTo(expected.Select(e => e.Path).ToArray()));
            Assert.That(messages.Select(m => m.Context), Has.All.SameAs(target), "a mensagem precisa apontar o objeto, para o clique");
        }

        private string Summary(EffectListBinder binder)
        {
            if (_editor != null) Object.DestroyImmediate(_editor);
            _editor = UnityEditor.Editor.CreateEditor(binder);
            Label summary = _editor.CreateInspectorGUI().Q<Label>(className: UIBinderEditor.SummaryClass);
            Assert.That(summary, Is.Not.Null, "falta a linha de resumo");
            return summary.text;
        }

        private EffectListBinder Binder(ActiveEffectsSource source, GameObject itemPrefab)
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            var binder = asc.gameObject.AddComponent<EffectListBinder>();
            binder.SetSource(source);
            GasFactory.SetObjectField(binder, "itemPrefab", itemPrefab);
            return binder;
        }

        private GameObject IconPrefab(bool withView)
        {
            var prefab = new GameObject(withView ? "EffectIcon" : "Plain");
            _created.Add(prefab);
            if (withView) prefab.AddComponent<IconRecorder>();
            return prefab;
        }

        private static void Set(Object target, System.Action<SerializedObject> change)
        {
            var so = new SerializedObject(target);
            change(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public sealed class IconRecorder : MonoBehaviour, IView<EffectIconModel>
        {
            public void Render(in EffectIconModel model)
            {
            }
        }
    }
}
