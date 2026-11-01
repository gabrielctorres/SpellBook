using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Tags.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.Tags.Tests
{
    /// <summary>Drawers em UI Toolkit do conjunto e da consulta (R15).</summary>
    public class TagDrawerTests
    {
        private TagTestFactory _factory;
        private TagDrawerHolder _holder;
        private GameplayTagDefinition _stunned;

        [SetUp]
        public void SetUp()
        {
            _factory = new TagTestFactory();
            _stunned = _factory.Create("State.Stunned");
            _holder = ScriptableObject.CreateInstance<TagDrawerHolder>();
            _holder.Tags.Add(new GameplayTag(_stunned));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);
            _factory.Dispose();
        }

        [Test]
        public void ContainerDrawer_BuildsListBoundToTags()
        {
            SerializedProperty property = new SerializedObject(_holder).FindProperty("tags");

            VisualElement root = new GameplayTagContainerDrawer().CreatePropertyGUI(property);

            var list = root.Q<ListView>();
            Assert.That(list, Is.Not.Null);
            Assert.That(list.bindingPath, Is.EqualTo(property.FindPropertyRelative("tags").propertyPath));
            Assert.That(list.reorderable, Is.True);
            Assert.That(list.showAddRemoveFooter, Is.True);
        }

        [Test]
        public void ContainerDrawer_AddCreatesEmptyRow_NotCopy()
        {
            var serializedObject = new SerializedObject(_holder);
            VisualElement root = new GameplayTagContainerDrawer().CreatePropertyGUI(serializedObject.FindProperty("tags"));
            var list = root.Q<ListView>();
            Assert.That(list?.onAdd, Is.Not.Null, "o drawer não troca o \"+\" do ListView");

            list.onAdd(list);

            serializedObject.Update();
            SerializedProperty tags = serializedObject.FindProperty("tags.tags");
            Assert.That(tags.arraySize, Is.EqualTo(2));
            Assert.That(tags.GetArrayElementAtIndex(0).FindPropertyRelative("definition").objectReferenceValue, Is.SameAs(_stunned));
            Assert.That(tags.GetArrayElementAtIndex(1).FindPropertyRelative("definition").objectReferenceValue, Is.Null);
        }

        [Test]
        public void QueryDrawer_ShowsLabelsTodasAlgumaNenhuma()
        {
            SerializedProperty property = new SerializedObject(_holder).FindProperty("query");

            VisualElement root = new GameplayTagQueryDrawer().CreatePropertyGUI(property);

            Assert.That(root.Q<Foldout>(), Is.Not.Null);
            PropertyField[] fields = root.Query<PropertyField>().ToList().ToArray();
            Assert.That(fields.Select(f => f.label), Is.EqualTo(new[] { "Todas", "Alguma", "Nenhuma" }));
            Assert.That(fields.Select(f => f.bindingPath), Is.EqualTo(new[] { "query.all", "query.any", "query.none" }));
        }

        [TestCase(typeof(GameplayTagContainerDrawer), "tags", typeof(GameplayTagContainer))]
        [TestCase(typeof(GameplayTagQueryDrawer), "query", typeof(GameplayTagQuery))]
        public void Drawers_HelpButtonUsesTypeHelpUrl(Type drawerType, string field, Type drawnType)
        {
            SerializedProperty property = new SerializedObject(_holder).FindProperty(field);
            var drawer = (PropertyDrawer)Activator.CreateInstance(drawerType);

            VisualElement root = drawer.CreatePropertyGUI(property);

            var help = root.Q<Button>(className: GameplayTagContainerDrawer.HelpButtonClass);
            Assert.That(help, Is.Not.Null, "o drawer não tem botão de ajuda");
            string expected = drawnType.GetCustomAttribute<HelpURLAttribute>().URL;
            Assert.That(help.userData, Is.EqualTo(expected));
        }
    }

    internal sealed class TagDrawerHolder : ScriptableObject
    {
        [SerializeField] private GameplayTagContainer tags = new GameplayTagContainer();
        [SerializeField] private GameplayTagQuery query = new GameplayTagQuery();

        public GameplayTagContainer Tags => tags;
    }
}
