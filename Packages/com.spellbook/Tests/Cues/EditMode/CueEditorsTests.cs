using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Cues.Editor;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.Cues.Tests
{
    /// <summary>Inspector dos cues: resumo de uma linha, "Testar" e prefab de áudio do pacote (CA19).</summary>
    public class CueEditorsTests
    {
        private CuesFactory _cues;
        private TimeFactory _times;
        private ManualTimeSource _time;
        private RecordingSpawnerAsset _spawner;
        private UnityEditor.Editor _editor;

        [SetUp]
        public void SetUp()
        {
            _cues = new CuesFactory();
            _times = new TimeFactory();
            _time = _times.CreateManual();
            _spawner = _cues.CreateSpawner();
        }

        [TearDown]
        public void TearDown()
        {
            if (_editor != null) Object.DestroyImmediate(_editor);
            _cues.Dispose();
            _times.Dispose();
        }

        [Test]
        public void ItemLine_SummaryFromTypeCategory()
        {
            GameObject prefab = _cues.CreateObject("Fogo_Impacto");
            var vfx = new VfxCueItem { Prefab = prefab, Placement = CuePlacement.AttachToTarget, End = CueEnd.ParticlesStop };
            CueDefinition cue = _cues.CreateCue(_cues.CreateTag("Cue.Hit"), CueKind.Looping, vfx);
            _cues.SetSounds(cue, SoundSelection.All,
                new SoundCueItem { Clip = _cues.CreateClip("Estalo", 1f) },
                new SoundCueItem { Clip = _cues.CreateClip("Chama", 1f), Loop = true });
            var so = new SerializedObject(cue);

            Assert.That(SummaryFormatter.Format(so.FindProperty("items").GetArrayElementAtIndex(0)),
                Is.EqualTo("VFX Fogo_Impacto · Attach To Target · Particles Stop"));
            Assert.That(SummaryFormatter.Format(so.FindProperty("sounds").GetArrayElementAtIndex(0)), Is.EqualTo("Som Estalo"));
            Assert.That(SummaryFormatter.Format(so.FindProperty("sounds").GetArrayElementAtIndex(1)), Is.EqualTo("Som Chama · loop"));
        }

        [Test]
        public void DefinitionEditor_ListsItemAndSoundLines_TestDisabledOutsidePlay()
        {
            var vfx = new VfxCueItem { Prefab = _cues.CreateObject("Fogo"), End = CueEnd.FixedDuration };
            CueDefinition cue = _cues.CreateCue(_cues.CreateTag("Cue.Hit"), CueKind.Burst, vfx);
            _cues.SetSounds(cue, SoundSelection.RandomOne, new SoundCueItem { Clip = _cues.CreateClip("Estalo", 1f) });

            _editor = UnityEditor.Editor.CreateEditor(cue);
            VisualElement root = _editor.CreateInspectorGUI();

            Assert.That(_editor, Is.InstanceOf<CueDefinitionEditor>());
            string[] lines = root.Query<Label>(className: CueDefinitionEditor.LineClass).ToList().Select(l => l.text).ToArray();
            Assert.That(lines, Is.EqualTo(new[] { "VFX Fogo · World · Fixed Duration", "Som Estalo" }));
            Button test = root.Q<Button>(CueDefinitionEditor.TestButtonName);
            Assert.That(test, Is.Not.Null, "falta o botão Testar");
            Assert.That(test.enabledSelf, Is.False, "fora do Play o Testar fica desligado");
        }

        [Test]
        public void Test_BurstExecutes_LoopingTogglesAddRemove()
        {
            GameplayTag burstTag = _cues.CreateTag("Cue.Burst");
            GameplayTag loopTag = _cues.CreateTag("Cue.Loop");
            GameObject prefab = _cues.CreateObject("Vfx");
            CueDefinition burst = _cues.CreateCue(burstTag, CueKind.Burst, new VfxCueItem { Prefab = prefab, End = CueEnd.FixedDuration });
            CueDefinition loop = _cues.CreateCue(loopTag, CueKind.Looping, new VfxCueItem { Prefab = prefab, End = CueEnd.OnRemove });
            CueRouter router = _cues.CreateRouter(_time, _spawner, null, burst, loop);
            var tester = new CueTester();
            CueParameters point = CueParameters.At(Vector3.one);

            Assert.That(tester.Toggle(burst, router, point), Is.True);
            Assert.That(tester.IsLooping, Is.False, "um Burst não fica ligado no botão");
            Assert.That(_spawner.SpawnCount, Is.EqualTo(1));

            Assert.That(tester.Toggle(loop, router, point), Is.True);
            Assert.That(tester.IsLooping, Is.True);
            Assert.That(router.ActiveCount, Is.EqualTo(2));

            tester.Toggle(loop, router, point);

            Assert.That(tester.IsLooping, Is.False, "o segundo clique para o Looping");
            Assert.That(_spawner.SpawnCount, Is.EqualTo(2), "parar não toca de novo");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(1), "o objeto do Looping volta ao spawner");
        }

        [Test]
        public void Test_Point_SelectionThenCamera()
        {
            GameObject selection = _cues.CreateObject("Selecao");
            selection.transform.position = new Vector3(1f, 2f, 3f);
            GameObject camera = _cues.CreateObject("Camera");
            camera.transform.SetPositionAndRotation(new Vector3(4f, 5f, 6f), Quaternion.Euler(0f, 90f, 0f));

            CueParameters onSelection = CueTester.Point(selection.transform, camera.transform);
            CueParameters onCamera = CueTester.Point(null, camera.transform);
            CueParameters nowhere = CueTester.Point(null, null);

            Assert.That(onSelection.Target, Is.SameAs(selection.transform));
            Assert.That(onSelection.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(onCamera.Target, Is.Null);
            Assert.That(onCamera.Position, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            Assert.That(Quaternion.Angle(onCamera.Rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
            Assert.That(nowhere.Position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void UsePackageAudioPrefab_FillsRouterField()
        {
            CueRouter router = _cues.CreateRouter(_time, _spawner, null);
            var so = new SerializedObject(router);

            CueRouterEditor.UsePackageAudioPrefab(so);

            GameObject expected = CuesFactory.LoadPackageAudioPrefab();
            Assert.That(expected, Is.Not.Null, "o prefab do pacote não existe");
            Assert.That(new SerializedObject(router).FindProperty("audioPrefab").objectReferenceValue, Is.SameAs(expected));
        }
    }
}
