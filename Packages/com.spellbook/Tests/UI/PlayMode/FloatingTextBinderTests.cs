#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Text;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.UI.Tests
{
    /// <summary>
    /// Números flutuantes (R13, R14): nascem só pelo <see cref="SpawnerAsset"/>, avançam pelo tempo do driver e voltam ao
    /// spawner no fim, sem crescer a contagem de vivos (CA8, parte); spawner sem instância ou falta de driver pulam o
    /// número com aviso; 1000 números não alocam (CA13, parte). ADR 0014, 0015 e 0039.
    /// </summary>
    public class FloatingTextBinderTests
    {
        private static readonly Vector3 HitPoint = new Vector3(1f, 2f, 3f);

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
        public void Publish_SpawnsThroughSpawner_ReleasedAtEnd_LiveCountStable()
        {
            Rig rig = CreateRig(withDriver: true);

            rig.Owner.Raise(HitPoint, 30f);
            NumberView view = rig.Spawner.Live.Count == 1 ? rig.Spawner.Live[0].GetComponent<NumberView>() : null;
            Assert.That(view, Is.Not.Null, "o número deveria nascer pelo spawner");
            string text = view.Last.Text.Buffer.ToString(view.Last.Text);
            rig.Driver.Tick();
            float atStart = view.Progress;
            rig.Time.Advance(0.5f);
            rig.Driver.Tick();
            float halfway = view.Progress;
            int liveHalfway = rig.Spawner.Live.Count;
            rig.Time.Advance(0.5f);
            rig.Driver.Tick();

            Assert.That(view.Shows, Is.EqualTo(1));
            Assert.That(text, Is.EqualTo("-30"));
            Assert.That(view.Last.WorldPosition, Is.EqualTo(HitPoint));
            Assert.That(view.Last.Role, Is.EqualTo(TextRole.Harmful));
            Assert.That(view.Last.Style, Is.SameAs(rig.Style));
            Assert.That(atStart, Is.EqualTo(0f));
            Assert.That(halfway, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(liveHalfway, Is.EqualTo(1));
            Assert.That(view.Progress, Is.EqualTo(1f));
            Assert.That(rig.Spawner.Live, Is.Empty, "no fim da duração, o número deveria voltar ao spawner");
            Assert.That(rig.Spawner.ReleaseCount, Is.EqualTo(1));
            Assert.That(rig.Driver.TickingCount, Is.Zero, "sem números vivos, o binder deveria soltar o tick");

            var instances = new HashSet<GameObject>();
            int maxLive = 0;
            for (int i = 0; i < 20; i++)
            {
                rig.Owner.Raise(HitPoint, 5f);
                instances.Add(rig.Spawner.Live[rig.Spawner.Live.Count - 1]);
                maxLive = Mathf.Max(maxLive, rig.Spawner.Live.Count);
                rig.Driver.Tick();
                rig.Time.Advance(0.6f);
                rig.Driver.Tick();
            }

            rig.Time.Advance(1f);
            rig.Driver.Tick();
            Assert.That(maxLive, Is.EqualTo(2), "a contagem de vivos não deveria crescer");
            Assert.That(instances.Count, Is.LessThanOrEqualTo(2), "o pool deveria reaproveitar as instâncias");
            Assert.That(rig.Spawner.Live, Is.Empty);
            Assert.That(rig.Spawner.ReleaseCount, Is.EqualTo(21));
        }

        [Test]
        public void NullSpawn_SkipsWithWarning_NoException()
        {
            Rig rig = CreateRig(withDriver: true);
            rig.Spawner.ReturnsNull = true;
            LogAssert.Expect(LogType.Warning, new Regex("spawner"));

            rig.Owner.Raise(HitPoint, 30f);
            rig.Owner.Raise(HitPoint, 10f);
            rig.Driver.Tick();

            Assert.That(rig.Spawner.Live, Is.Empty);
            Assert.That(rig.Driver.TickingCount, Is.Zero, "sem número vivo, o binder não deveria pedir tick");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NoDriver_SkipsWithWarning()
        {
            Rig rig = CreateRig(withDriver: false);
            LogAssert.Expect(LogType.Warning, new Regex("UIUpdateDriver"));

            rig.Owner.Raise(HitPoint, 30f);
            rig.Owner.Raise(HitPoint, 10f);

            Assert.That(rig.Spawner.SpawnCount, Is.Zero, "sem driver, o número nunca voltaria ao spawner");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PausedTime_NumberStays()
        {
            Rig rig = CreateRig(withDriver: true);
            rig.Owner.Raise(HitPoint, 30f);
            NumberView view = rig.Spawner.Live[0].GetComponent<NumberView>();
            rig.Driver.Tick();
            rig.Time.Advance(0.5f);
            rig.Driver.Tick();

            rig.Driver.Tick();
            rig.Driver.Tick();
            rig.Driver.Tick();
            float whilePaused = view.Progress;
            int liveWhilePaused = rig.Spawner.Live.Count;
            rig.Time.Advance(0.6f);
            rig.Driver.Tick();

            Assert.That(whilePaused, Is.EqualTo(0.5f).Within(1e-4f), "com o tempo parado, o número não deveria avançar");
            Assert.That(liveWhilePaused, Is.EqualTo(1));
            Assert.That(rig.Spawner.Live, Is.Empty);
        }

        [Test]
        public void Disable_ReleasesLiveNumbers()
        {
            Rig rig = CreateRig(withDriver: true);
            rig.Owner.Raise(HitPoint, 30f);
            rig.Owner.Raise(HitPoint, 10f);
            int liveBefore = rig.Spawner.Live.Count;

            rig.Binder.gameObject.SetActive(false);

            Assert.That(liveBefore, Is.EqualTo(2));
            Assert.That(rig.Spawner.Live, Is.Empty, "desligar não deveria deixar número sem tick");
            Assert.That(rig.Spawner.ReleaseCount, Is.EqualTo(2));
            Assert.That(rig.Driver.TickingCount, Is.Zero);
        }

        [Test]
        public void Numbers1000_DoNotAllocate()
        {
            Rig rig = CreateRig(withDriver: true);
            Numbers(rig, 100);

            Assert.That(() => Numbers(rig, 1000), Is.Not.AllocatingGCMemory());
            Assert.That(rig.Spawner.SpawnCount, Is.EqualTo(1100));
            Assert.That(rig.Spawner.Live.Count, Is.LessThanOrEqualTo(4));
        }

        private static void Numbers(Rig rig, int count)
        {
            for (int i = 0; i < count; i++)
            {
                rig.Owner.Raise(HitPoint, 10f + i % 5);
                rig.Time.Advance(0.3f);
                rig.Driver.Tick();
            }
        }

        private Rig CreateRig(bool withDriver)
        {
            var rig = new Rig();
            rig.Time = Track(ScriptableObject.CreateInstance<ManualTimeSource>());
            if (withDriver)
            {
                rig.Driver = Track(new GameObject("tela")).AddComponent<UIUpdateDriver>();
                rig.Driver.SetTimeSource(rig.Time);
            }

            rig.Spawner = Track(ScriptableObject.CreateInstance<RecordingSpawnerAsset>());
            rig.Style = Track(ScriptableObject.CreateInstance<FloatingTextStyle>());
            Modify(rig.Style, so => so.FindProperty("duration").floatValue = 1f);
            rig.Owner = Track(new GameObject("ator")).AddComponent<NumberOwner>();

            GameObject prefab = Track(new GameObject("numero"));
            prefab.SetActive(false);
            prefab.AddComponent<NumberView>();

            GameObject host = Track(new GameObject("numeros"));
            host.transform.SetParent(withDriver ? rig.Driver.transform : null);
            host.SetActive(false);
            rig.Binder = host.AddComponent<FloatingTextBinder>();
            rig.Binder.SetOwner(ActorResolver.ForExplicit(rig.Owner));
            rig.Binder.SetSource(new NumberSource { Style = rig.Style });
            Modify(rig.Binder, so =>
            {
                so.FindProperty("prefab").objectReferenceValue = prefab;
                so.FindProperty("spawner").objectReferenceValue = rig.Spawner;
            });
            host.SetActive(true);
            return rig;
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        private static void Modify(Object target, Action<SerializedObject> edit)
        {
            var serialized = new SerializedObject(target);
            edit(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class Rig
        {
            public ManualTimeSource Time;
            public UIUpdateDriver Driver;
            public RecordingSpawnerAsset Spawner;
            public FloatingTextStyle Style;
            public NumberOwner Owner;
            public FloatingTextBinder Binder;
        }

        /// <summary>Dono de teste que avisa cada golpe com o ponto e o valor.</summary>
        public sealed class NumberOwner : MonoBehaviour
        {
            public event Action<Vector3, float> Hit;

            public void Raise(Vector3 point, float value) => Hit?.Invoke(point, value);
        }

        /// <summary>View de número de teste: guarda o último pedido, as exibições e o progresso.</summary>
        public sealed class NumberView : MonoBehaviour, IFloatingTextView
        {
            public FloatingTextRequest Last;
            public int Shows;
            public float Progress = -1f;

            public void Show(in FloatingTextRequest request)
            {
                Last = request;
                Shows++;
                Progress = -1f;
            }

            public void SetProgress(float normalized) => Progress = normalized;
        }

        // Ligação de teste: cada golpe vira um pedido "-valor" no buffer da ligação
        private sealed class NumberBinding : UIBinding<FloatingTextRequest>
        {
            private readonly TextBuffer _buffer = new TextBuffer(16);
            private readonly Action<Vector3, float> _onHit;
            private readonly FloatingTextStyle _style;
            private NumberOwner _owner;

            public NumberBinding(FloatingTextStyle style)
            {
                _style = style;
                _onHit = OnHit;
            }

            protected override void OnAttach(Object owner)
            {
                _owner = (NumberOwner)owner;
                _owner.Hit += _onHit;
            }

            protected override void OnDetach() => _owner.Hit -= _onHit;

            private void OnHit(Vector3 point, float value)
            {
                _buffer.Clear();
                TextSpan text = _buffer.Append(-value, "0", CultureInfo.InvariantCulture);
                Publish(new FloatingTextRequest(point, text, TextRole.Harmful, _style));
            }
        }

        [Serializable]
        public sealed class NumberSource : UISource<FloatingTextRequest>
        {
            public FloatingTextStyle Style;

            public override Type OwnerType => typeof(NumberOwner);

            public override UIBinding<FloatingTextRequest> CreateBinding() => new NumberBinding(Style);

            public override FloatingTextRequest CreatePreview(in UIPreviewContext context) => default;
        }
    }
}
#endif
