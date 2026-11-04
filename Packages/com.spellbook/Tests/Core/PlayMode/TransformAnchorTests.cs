#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SpellBook.Core.Tests
{
    /// <summary>
    /// <c>TransformAnchor</c>: o <c>AnchorRegistrar</c> sem <c>value</c> publica o <c>Transform</c> do próprio objeto,
    /// que serve de "objeto presente" (Spec do Sandbox, T7). Campos preenchidos por <c>SerializedObject</c>.
    /// </summary>
    public class TransformAnchorTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TransformAnchor_ValueTypeIsTransform()
        {
            TransformAnchor anchor = CreateAnchor();

            System.Type valueType = anchor.ValueType;

            Assert.That(valueType, Is.EqualTo(typeof(Transform)));
        }

        [Test]
        public void Registrar_EmptyValue_ProvidesOwnTransform()
        {
            TransformAnchor anchor = CreateAnchor();
            GameObject host = CreateHost("hud");
            AddRegistrar(host, anchor);

            host.SetActive(true);

            Assert.That(anchor.Value, Is.SameAs(host.transform));
            Assert.That(anchor.UntypedValue, Is.SameAs(host.transform));
        }

        [Test]
        public void Registrar_Disable_LeavesAnchorEmpty_UntypedValueNull()
        {
            TransformAnchor anchor = CreateAnchor();
            GameObject host = CreateHost("hud");
            AddRegistrar(host, anchor);
            host.SetActive(true);

            host.SetActive(false);

            Assert.That(anchor.HasValue, Is.False);
            Assert.That(anchor.UntypedValue == null, Is.True);
        }

        [Test]
        public void Registrar_DestroyedObject_AnchorReadsEmpty()
        {
            TransformAnchor anchor = CreateAnchor();
            GameObject host = CreateHost("hud");
            AddRegistrar(host, anchor);
            host.SetActive(true);

            Object.DestroyImmediate(host);

            Assert.That(anchor.HasValue, Is.False);
            Assert.That(anchor.Value == null, Is.True);
            Assert.That(anchor.UntypedValue == null, Is.True);
        }

        private TransformAnchor CreateAnchor()
        {
            return Track(ScriptableObject.CreateInstance<TransformAnchor>());
        }

        // Inativo até ter os campos: o AddComponent chamaria o Awake e o OnEnable sem a âncora
        private GameObject CreateHost(string name)
        {
            GameObject host = Track(new GameObject(name));
            host.SetActive(false);
            return host;
        }

        private static void AddRegistrar(GameObject host, RuntimeAnchor anchor)
        {
            var registrar = host.AddComponent<AnchorRegistrar>();
            var so = new SerializedObject(registrar);
            so.FindProperty("anchor").objectReferenceValue = anchor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }
    }
}
#endif
