#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Validation;
using UnityEditor;
using UnityEngine;

namespace SpellBook.Core.Tests
{
    /// <summary>Âncora de teste de um componente: a base é genérica e abstrata.</summary>
    public class TestColliderAnchor : RuntimeAnchor<BoxCollider>
    {
    }

    /// <summary>
    /// <c>AnchorRegistrar</c>: publica um componente numa âncora no <c>OnEnable</c> e sai no <c>OnDisable</c> (ADR 0039,
    /// item 5; Spec do B-015, Design 1). Campos preenchidos por <c>SerializedObject</c>, como o Inspector faria.
    /// </summary>
    public class AnchorRegistrarTests
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
        public void Registrar_ProvidesOnEnable_UnsetsOnDisable()
        {
            TestColliderAnchor anchor = CreateAnchor();
            GameObject host = CreateHost("ator");
            var collider = host.AddComponent<BoxCollider>();
            AnchorRegistrar registrar = AddRegistrar(host, anchor, collider);

            host.SetActive(true);
            BoxCollider whileEnabled = anchor.Value;
            registrar.enabled = false;

            Assert.That(whileEnabled, Is.SameAs(collider));
            Assert.That(anchor.HasValue, Is.False);
        }

        [Test]
        public void Respawn_NewProvides_OldDisableDoesNotClear()
        {
            TestColliderAnchor anchor = CreateAnchor();
            GameObject oldHost = CreateHost("antigo");
            AddRegistrar(oldHost, anchor, oldHost.AddComponent<BoxCollider>());
            GameObject newHost = CreateHost("novo");
            var newCollider = newHost.AddComponent<BoxCollider>();
            AddRegistrar(newHost, anchor, newCollider);
            oldHost.SetActive(true);

            newHost.SetActive(true);
            oldHost.SetActive(false);

            Assert.That(anchor.Value, Is.SameAs(newCollider));
        }

        [Test]
        public void Registrar_EmptyValue_UsesComponentOfAnchorType()
        {
            TestColliderAnchor anchor = CreateAnchor();
            GameObject host = CreateHost("ator");
            var collider = host.AddComponent<BoxCollider>();
            AddRegistrar(host, anchor, null);

            host.SetActive(true);

            Assert.That(anchor.Value, Is.SameAs(collider));
        }

        [Test]
        public void Registrar_Validate_NoAnchorOrWrongType_Error()
        {
            TestColliderAnchor anchor = CreateAnchor();
            GameObject host = CreateHost("ator");
            AnchorRegistrar noAnchor = AddRegistrar(host, null, null);
            AnchorRegistrar wrongValue = AddRegistrar(host, anchor, host.transform);
            AnchorRegistrar missingComponent = AddRegistrar(host, anchor, null);
            GameObject validHost = CreateHost("válido");
            AnchorRegistrar valid = AddRegistrar(validHost, anchor, validHost.AddComponent<BoxCollider>());

            ValidationResult noAnchorResult = Validate(noAnchor);
            ValidationResult wrongValueResult = Validate(wrongValue);
            ValidationResult missingResult = Validate(missingComponent);
            ValidationResult validResult = Validate(valid);

            AssertSingleError(noAnchorResult, "anchor");
            AssertSingleError(wrongValueResult, "value");
            AssertSingleError(missingResult, "value");
            Assert.That(validResult.Messages, Is.Empty);
        }

        private static ValidationResult Validate(AnchorRegistrar registrar)
        {
            var result = new ValidationResult(registrar);
            registrar.Validate(result);
            return result;
        }

        private static void AssertSingleError(ValidationResult result, string propertyPath)
        {
            Assert.That(result.Messages, Has.Count.EqualTo(1));
            Assert.That(result.Messages[0].Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(result.Messages[0].PropertyPath, Is.EqualTo(propertyPath));
        }

        private TestColliderAnchor CreateAnchor()
        {
            return Track(ScriptableObject.CreateInstance<TestColliderAnchor>());
        }

        // Inativo até ter os campos: o AddComponent chamaria o Awake e o OnEnable sem a âncora
        private GameObject CreateHost(string name)
        {
            GameObject host = Track(new GameObject(name));
            host.SetActive(false);
            return host;
        }

        private static AnchorRegistrar AddRegistrar(GameObject host, RuntimeAnchor anchor, Component value)
        {
            var registrar = host.AddComponent<AnchorRegistrar>();
            var so = new SerializedObject(registrar);
            so.FindProperty("anchor").objectReferenceValue = anchor;
            so.FindProperty("value").objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return registrar;
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }
    }
}
#endif
