using System;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Inspector;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.Core.Tests.Inspector
{
    /// <summary>O seletor de tipo usa o drawer do tipo concreto (ADR 0026, emenda de 2026-11-01; RL7a).</summary>
    public class TypePickerTypeDrawerTests
    {
        private TypeDrawerHolder _holder;
        private SerializedObject _serializedObject;

        [SetUp]
        public void SetUp()
        {
            _holder = ScriptableObject.CreateInstance<TypeDrawerHolder>();
            _serializedObject = new SerializedObject(_holder);
        }

        [TearDown]
        public void TearDown()
        {
            _serializedObject.Dispose();
            Object.DestroyImmediate(_holder);
        }

        [Test]
        public void ConcreteTypeWithDrawer_BodyComesFromTypeDrawer()
        {
            VisualElement root = Draw(new DrawnPick());

            Assert.That(root.Q<Label>(DrawnPickDrawer.BodyName), Is.Not.Null);
            Assert.That(root.Query<PropertyField>().ToList(), Is.Empty);
        }

        [Test]
        public void ConcreteTypeWithoutDrawer_OnePropertyFieldPerChild()
        {
            VisualElement root = Draw(new PlainPick());

            Assert.That(root.Query<PropertyField>().ToList().Select(f => f.bindingPath),
                Is.EqualTo(new[] { "single.Value", "single.Extra" }));
        }

        [Test]
        public void DerivedType_UsesBaseDrawer_OnlyWithUseForChildren()
        {
            VisualElement withChildren = Draw(new FamilyChildPick());
            Assert.That(withChildren.Q<Label>(FamilyPickDrawer.BodyName), Is.Not.Null);
            Assert.That(TypePickerCatalog.FindTypeDrawer(typeof(FamilyChildPick)), Is.EqualTo(typeof(FamilyPickDrawer)));

            VisualElement withoutChildren = Draw(new LoneChildPick());
            Assert.That(withoutChildren.Q<Label>(LonePickDrawer.BodyName), Is.Null);
            Assert.That(withoutChildren.Query<PropertyField>().ToList(), Has.Count.EqualTo(1));
            Assert.That(TypePickerCatalog.FindTypeDrawer(typeof(LoneChildPick)), Is.Null);
        }

        [Test]
        public void ExactTypeDrawer_WinsOverBaseDrawer()
        {
            VisualElement root = Draw(new FamilyExactPick());

            Assert.That(root.Q<Label>(FamilyExactPickDrawer.BodyName), Is.Not.Null);
            Assert.That(root.Q<Label>(FamilyPickDrawer.BodyName), Is.Null);
            Assert.That(TypePickerCatalog.FindTypeDrawer(typeof(FamilyExactPick)), Is.EqualTo(typeof(FamilyExactPickDrawer)));
        }

        [Test]
        public void AttributeDrawers_NotUsedAsTypeDrawer_NoRecursion()
        {
            Assert.That(TypePickerCatalog.FindTypeDrawer(typeof(TypePickerAttribute)), Is.Null);
            Assert.That(TypePickerCatalog.FindTypeDrawer(typeof(PickMarkerAttribute)), Is.Null);
            Assert.That(TypePickerCatalog.FindTypeDrawer(typeof(PickMarkerChildAttribute)), Is.Null);
        }

        [Test]
        public void PickingAnotherType_SwapsBody()
        {
            var field = (TypePickerField)Draw(new PlainPick());

            TypePickerCatalog.Assign(_serializedObject.FindProperty("single"), typeof(DrawnPick));
            field.Refresh(_serializedObject.FindProperty("single"));

            Assert.That(field.Q<Label>(DrawnPickDrawer.BodyName), Is.Not.Null);
            Assert.That(field.Query<PropertyField>().ToList(), Is.Empty);
        }

        [Test]
        public void TypeDrawerReturningNull_FallsBackToChildren()
        {
            VisualElement root = Draw(new ImguiPick());

            Assert.That(TypePickerCatalog.FindTypeDrawer(typeof(ImguiPick)), Is.EqualTo(typeof(ImguiPickDrawer)));
            Assert.That(root.Query<PropertyField>().ToList().Select(f => f.bindingPath),
                Is.EqualTo(new[] { "single.Value" }));
        }

        private VisualElement Draw(DrawerPickBase value)
        {
            _holder.single = value;
            _serializedObject.Update();
            return new TypePickerDrawer().CreatePropertyGUI(_serializedObject.FindProperty("single"));
        }
    }

    internal sealed class TypeDrawerHolder : ScriptableObject
    {
        [SerializeReference, TypePicker] internal DrawerPickBase single;
    }

    [Serializable]
    internal abstract class DrawerPickBase
    {
        public int Value;
    }

    [Serializable]
    internal class PlainPick : DrawerPickBase
    {
        public int Extra;
    }

    [Serializable]
    internal class DrawnPick : DrawerPickBase { }

    [Serializable]
    internal class FamilyPick : DrawerPickBase { }

    [Serializable]
    internal class FamilyChildPick : FamilyPick { }

    [Serializable]
    internal class FamilyExactPick : FamilyPick { }

    [Serializable]
    internal class LonePick : DrawerPickBase { }

    [Serializable]
    internal class LoneChildPick : LonePick { }

    [Serializable]
    internal class ImguiPick : DrawerPickBase { }

    internal class PickMarkerAttribute : UnityEngine.PropertyAttribute { }

    [Serializable]
    internal class PickMarkerChildAttribute : PickMarkerAttribute { }

    internal abstract class NamedBodyDrawer : PropertyDrawer
    {
        protected abstract string Name { get; }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new Label(property.propertyPath) { name = Name };
        }
    }

    [CustomPropertyDrawer(typeof(DrawnPick))]
    internal sealed class DrawnPickDrawer : NamedBodyDrawer
    {
        public const string BodyName = "drawn-pick-body";
        protected override string Name => BodyName;
    }

    [CustomPropertyDrawer(typeof(FamilyPick), true)]
    internal sealed class FamilyPickDrawer : NamedBodyDrawer
    {
        public const string BodyName = "family-pick-body";
        protected override string Name => BodyName;
    }

    [CustomPropertyDrawer(typeof(FamilyExactPick))]
    internal sealed class FamilyExactPickDrawer : NamedBodyDrawer
    {
        public const string BodyName = "family-exact-pick-body";
        protected override string Name => BodyName;
    }

    [CustomPropertyDrawer(typeof(LonePick))]
    internal sealed class LonePickDrawer : NamedBodyDrawer
    {
        public const string BodyName = "lone-pick-body";
        protected override string Name => BodyName;
    }

    /// <summary>Drawer só IMGUI: o <c>CreatePropertyGUI</c> herdado devolve <c>null</c>.</summary>
    [CustomPropertyDrawer(typeof(ImguiPick))]
    internal sealed class ImguiPickDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.LabelField(position, label);
        }
    }

    [CustomPropertyDrawer(typeof(PickMarkerAttribute), true)]
    internal sealed class PickMarkerDrawer : NamedBodyDrawer
    {
        protected override string Name => "pick-marker-body";
    }
}
