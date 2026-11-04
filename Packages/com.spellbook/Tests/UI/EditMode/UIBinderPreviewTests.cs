using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.UI.Tests
{
    /// <summary>Prévia do binder em edição: desenha o modelo da fonte e não liga nada (CA10, parte; ADR 0039, item 7).</summary>
    public class UIBinderPreviewTests
    {
        private GameObject _gameObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void EditMode_RendersPreview_DoesNotBind()
        {
            _gameObject = new GameObject("binder");
            _gameObject.SetActive(false);
            PreviewBinder binder = _gameObject.AddComponent<PreviewBinder>();
            PreviewView view = _gameObject.AddComponent<PreviewView>();
            var source = new PreviewSource();
            binder.SetOwner(ActorResolver.ForExplicit(_gameObject.transform));
            binder.SetSource(source);
            binder.SetView(view);

            _gameObject.SetActive(true);
            int rendersOnEnable = view.Renders;
            binder.Rebind();

            Assert.That(Application.isPlaying, Is.False);
            Assert.That(rendersOnEnable, Is.EqualTo(1));
            Assert.That(view.Renders, Is.EqualTo(2));
            Assert.That(view.Last, Is.EqualTo(50));
            Assert.That(source.CreatedBindings, Is.Zero, "a prévia não pode criar ligação");
            LogAssert.NoUnexpectedReceived();
        }

        public readonly struct PreviewModel
        {
            public readonly int Value;

            public PreviewModel(int value) => Value = value;
        }

        [Serializable]
        public sealed class PreviewSource : UISource<PreviewModel>
        {
            public int CreatedBindings;

            public override Type OwnerType => typeof(Transform);

            public override UIBinding<PreviewModel> CreateBinding()
            {
                CreatedBindings++;
                return null;
            }

            public override PreviewModel CreatePreview(in UIPreviewContext context) => new PreviewModel(50);
        }

        public sealed class PreviewView : MonoBehaviour, IView<PreviewModel>
        {
            public int Renders;
            public int Last;

            public void Render(in PreviewModel model)
            {
                Renders++;
                Last = model.Value;
            }
        }

        public sealed class PreviewBinder : UIBinder<PreviewModel>
        {
        }
    }
}
