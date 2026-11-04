using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SpellBook.UI.Tests
{
    /// <summary>Regras de validação do lado da UI (R17; CA16, parte), com o binder como objeto clicável.</summary>
    public class UIBinderValidationTests
    {
        private readonly List<Object> _created = new List<Object>();
        private Scene _previewScene;

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created.Where(o => o != null))
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            if (_previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(_previewScene);
            }
        }

        [Test]
        public void Binder_NoSource_IsError()
        {
            ValidationBinder binder = Binder(null);

            AssertSingle(binder, ValidationSeverity.Error, "source", "Sem fonte. Escolha o que o binder lê do dono.");
        }

        [Test]
        public void Binder_SourceMessages_ComeUnderSourcePath()
        {
            ValidationBinder binder = Binder(new ValidationSource { Error = "Fonte quebrada." });

            AssertSingle(binder, ValidationSeverity.Error, "source", "Fonte quebrada.");
        }

        [Test]
        public void Binder_NoView_IsError()
        {
            ValidationBinder binder = Binder(new ValidationSource(), withView: false);

            AssertSingle(binder, ValidationSeverity.Error, "view", "Sem view. Ligue o componente que desenha o modelo.");
        }

        [Test]
        public void Binder_ViewWithoutModelInterface_IsError()
        {
            ValidationBinder binder = Binder(new ValidationSource(), withView: false);
            var serialized = new SerializedObject(binder);
            serialized.FindProperty("view").objectReferenceValue = binder.gameObject.AddComponent<NotAView>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssertSingle(binder, ValidationSeverity.Error, "view",
                "A view NotAView não implementa IView<ValidationModel>. Ligue um componente que desenhe esse modelo.");
        }

        [Test]
        public void FromAnchor_NoAnchor_IsError()
        {
            ValidationBinder binder = Binder(new ValidationSource());
            binder.SetOwner(ActorResolver.ForAnchor(null));

            AssertSingle(binder, ValidationSeverity.Error, "owner.anchor",
                "Modo FromAnchor sem âncora. Escolha a âncora que guarda o dono.");
        }

        [Test]
        public void FromAnchor_AnchorOfOtherType_IsError()
        {
            ValidationBinder binder = Binder(new ValidationSource());
            binder.SetOwner(ActorResolver.ForAnchor(Track(ScriptableObject.CreateInstance<ColliderTestAnchor>())));

            AssertSingle(binder, ValidationSeverity.Error, "owner.anchor",
                "A âncora guarda BoxCollider, mas a fonte lê Transform. Escolha uma âncora do tipo da fonte.");
        }

        [Test]
        public void Explicit_NoReference_IsError()
        {
            ValidationBinder binder = Binder(new ValidationSource());
            binder.SetOwner(ActorResolver.ForExplicit(null));

            AssertSingle(binder, ValidationSeverity.Error, "owner.explicitOwner",
                "Modo Explicit sem referência. Ligue o dono.");
        }

        [Test]
        public void SelfOrParent_NoOwnerInScene_IsWarning()
        {
            ValidationBinder binder = Binder(new ValidationSource { WantsCollider = true });

            AssertSingle(binder, ValidationSeverity.Warning, "owner",
                "Sem BoxCollider neste objeto nem nos pais. A view mostra o estado vazio.");
        }

        [Test]
        public void SelfOrParent_InPreviewScene_NoMessage()
        {
            ValidationBinder binder = Binder(new ValidationSource { WantsCollider = true });
            _previewScene = EditorSceneManager.NewPreviewScene();
            SceneManager.MoveGameObjectToScene(binder.gameObject, _previewScene);

            Assert.That(Validate(binder), Is.Empty, "o prefab aberto no Prefab Mode não tem o dono acima dele");
        }

        [Test]
        public void FloatingTextBinder_NoPrefab_IsError_NoViewRequired()
        {
            FloatingTextBinder binder = FloatingBinder(null);

            AssertSingle(binder, ValidationSeverity.Error, "prefab",
                "Sem prefab. Ligue o prefab do número, com um componente IFloatingTextView.");
        }

        [Test]
        public void FloatingTextBinder_PrefabWithoutView_IsError()
        {
            FloatingTextBinder binder = FloatingBinder(Track(new GameObject("SemView")));

            AssertSingle(binder, ValidationSeverity.Error, "prefab",
                "O prefab SemView não tem um componente IFloatingTextView. Os números são pulados.");
        }

        [Test]
        public void ValidSetup_HasNoMessages()
        {
            ValidationBinder binder = Binder(new ValidationSource());
            var withView = Track(new GameObject("Numero"));
            withView.AddComponent<TestFloatingView>();
            FloatingTextBinder floating = FloatingBinder(withView);
            ValidationBinder anchored = Binder(new ValidationSource());
            anchored.SetOwner(ActorResolver.ForAnchor(Track(ScriptableObject.CreateInstance<TransformAnchor>())));
            ValidationBinder explicitOwner = Binder(new ValidationSource());
            explicitOwner.SetOwner(ActorResolver.ForExplicit(binder.transform));

            Assert.That(Validate(binder), Is.Empty);
            Assert.That(Validate(floating), Is.Empty);
            Assert.That(Validate(anchored), Is.Empty);
            Assert.That(Validate(explicitOwner), Is.Empty);
        }

        private ValidationBinder Binder(ValidationSource source, bool withView = true)
        {
            var gameObject = Track(new GameObject("Binder"));
            var binder = gameObject.AddComponent<ValidationBinder>();
            binder.SetSource(source);
            if (withView)
            {
                binder.SetView(gameObject.AddComponent<ValidationView>());
            }

            return binder;
        }

        private FloatingTextBinder FloatingBinder(GameObject prefab)
        {
            var gameObject = Track(new GameObject("Numeros"));
            var binder = gameObject.AddComponent<FloatingTextBinder>();
            binder.SetSource(new FloatingSource());
            var serialized = new SerializedObject(binder);
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return binder;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private static IReadOnlyList<ValidationMessage> Validate(Object target)
        {
            return SpellBookEditor.CollectMessages(new[] { target });
        }

        private static void AssertSingle(Object binder, ValidationSeverity severity, string propertyPath, string text)
        {
            IReadOnlyList<ValidationMessage> messages = Validate(binder);
            Assert.That(messages.Select(m => m.Message), Is.EqualTo(new[] { text }));
            Assert.That(messages[0].Severity, Is.EqualTo(severity));
            Assert.That(messages[0].PropertyPath, Is.EqualTo(propertyPath));
            Assert.That(messages[0].Context, Is.SameAs(binder), "a mensagem precisa apontar o binder, para o clique");
        }

        public readonly struct ValidationModel
        {
        }

        [Serializable]
        public sealed class ValidationSource : UISource<ValidationModel>
        {
            public string Error;
            public bool WantsCollider;

            public override Type OwnerType => WantsCollider ? typeof(BoxCollider) : typeof(Transform);

            public override UIBinding<ValidationModel> CreateBinding() => null;

            public override ValidationModel CreatePreview(in UIPreviewContext context) => default;

            public override void Validate(ValidationResult result, string propertyPath)
            {
                if (!string.IsNullOrEmpty(Error))
                {
                    result.AddError(Error, propertyPath);
                }
            }
        }

        [Serializable]
        public sealed class FloatingSource : UISource<FloatingTextRequest>
        {
            public override Type OwnerType => typeof(Transform);

            public override UIBinding<FloatingTextRequest> CreateBinding() => null;

            public override FloatingTextRequest CreatePreview(in UIPreviewContext context) => default;
        }

        public sealed class ValidationView : MonoBehaviour, IView<ValidationModel>
        {
            public void Render(in ValidationModel model)
            {
            }
        }

        public sealed class NotAView : MonoBehaviour
        {
        }

        public sealed class TestFloatingView : MonoBehaviour, IFloatingTextView
        {
            public void Show(in FloatingTextRequest request)
            {
            }

            public void SetProgress(float normalized)
            {
            }
        }

        public sealed class ValidationBinder : UIBinder<ValidationModel>
        {
        }
    }
}
