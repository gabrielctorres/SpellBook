#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using SpellBook.Time;
using SpellBook.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Binder da lista de efeitos (Spec do B-018, BF3): limite visível com "+N" e promoção do oculto mais antigo, ícones
    /// do spawner sem crescer os vivos e tick sem alocar, com aplicação e remoção alocando só o que o ASC já
    /// aloca (CA7, CA14 e CA15).
    /// </summary>
    public class EffectListBinderTests
    {
        private const float LongDuration = 100000f;

        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;
        private TimeFactory _time;
        private ManualTimeSource _clock;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _clock = _time.CreateManual();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void MaxVisible8_11Effects_8PlusThree_PromotesOldestHidden()
        {
            AbilitySystemComponent asc = Asc();
            AbilitySystemComponent other = Asc();
            GameplayEffect[] effects = Effects(11);
            var instances = new ActiveEffectInstance[effects.Length];
            Rig rig = CreateList(asc, maxVisible: 8);

            for (int i = 0; i < effects.Length; i++) instances[i] = asc.ApplyEffect(effects[i]);
            Object[] shown = Subjects(rig);
            int hidden = rig.Overflow.Last.Hidden;
            asc.ApplyEffect(effects[0], other);
            int hiddenAfterSecondSource = rig.Overflow.Last.Hidden;
            asc.RemoveEffect(instances[2]);

            Assert.That(shown, Is.EqualTo(Slice(effects, 0, 1, 2, 3, 4, 5, 6, 7)), "os 8 primeiros, na ordem de aplicação");
            Assert.That(hidden, Is.EqualTo(3), "o indicador mostra +3");
            Assert.That(hiddenAfterSecondSource, Is.EqualTo(3), "o limite conta ícones, não instâncias");
            Assert.That(Subjects(rig), Is.EqualTo(Slice(effects, 0, 1, 3, 4, 5, 6, 7, 8)), "o oculto mais antigo entra no fim");
            Assert.That(rig.Overflow.Last.Hidden, Is.EqualTo(2));
            Assert.That(rig.Spawner.Live.Count, Is.EqualTo(8), "só os visíveis têm objeto");
            Assert.That(rig.Binder.Count, Is.EqualTo(10), "os ocultos seguem na lista");
        }

        [Test]
        public void Icons_FromSpawner_100ApplyRemove_LiveStable()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect[] lasting = Effects(2);
            GameplayEffect[] passing = Effects(10);
            var instances = new ActiveEffectInstance[passing.Length];
            Rig rig = CreateList(asc, maxVisible: 8);
            asc.ApplyEffect(lasting[0]);
            asc.ApplyEffect(lasting[1]);
            var seen = new HashSet<GameObject>();

            for (int round = 0; round < 10; round++)
            {
                for (int i = 0; i < passing.Length; i++) instances[i] = asc.ApplyEffect(passing[i]);
                seen.UnionWith(rig.Spawner.Live);
                Assert.That(rig.Spawner.Live.Count, Is.EqualTo(8), "12 ícones, 8 visíveis");
                for (int i = 0; i < passing.Length; i++) asc.RemoveEffect(instances[i]);
            }

            Assert.That(Subjects(rig), Is.EqualTo(lasting), "sobram os dois que não saíram");
            Assert.That(rig.Spawner.Live.Count, Is.EqualTo(2), "a contagem de vivos não cresce");
            Assert.That(rig.Spawner.ReleaseCount, Is.EqualTo(rig.Spawner.SpawnCount - 2), "todo ícone tirado volta ao spawner");
            Assert.That(rig.Spawner.SpawnCount, Is.GreaterThanOrEqualTo(100), "os ícones vêm do spawner");
            Assert.That(seen.Count, Is.EqualTo(8), "o spawner reaproveita os mesmos objetos");
        }

        // O ASC aloca uma instância por aplicação com duração (AbilitySystemComponent.ApplyDurationEffect): a lista não
        // pode acrescentar nada a isso, e o tick não aloca (leitura do CA15 no Detalhe do BF3)
        [Test]
        public void Ticks10000With8Icons_NoAlloc_1000ApplyRemove_SameAllocsAsAscAlone()
        {
            Cycle alone = Lasting(Asc(), null);
            AbilitySystemComponent asc = Asc();
            Rig rig = CreateList(asc, maxVisible: 8);
            Cycle listed = Lasting(asc, rig.Driver);
            alone.Run(10, 10);
            listed.Run(10, 10);
            int spawnsBefore = rig.Spawner.SpawnCount;
            int rendersBefore = rig.Renders();

            Assert.That(() => listed.Run(10000, 0), Is.Not.AllocatingGCMemory(), "o tick com 8 ícones não aloca");
            int ascAlone = GcAllocs(() => alone.Run(1000, 1000));
            int withList = GcAllocs(() => listed.Run(1000, 1000));
            Assert.That(ascAlone, Is.GreaterThan(0), "o ASC aloca a instância nova; o contador mede");
            Assert.That(withList, Is.EqualTo(ascAlone), "aplicar e remover com a lista aloca o mesmo que o ASC sozinho");
            Assert.That(rig.Spawner.Live.Count, Is.EqualTo(8));
            Assert.That(rig.Spawner.SpawnCount - spawnsBefore, Is.EqualTo(1000), "cada reaplicação pede um ícone ao spawner");
            Assert.That(rig.Renders() - rendersBefore, Is.GreaterThanOrEqualTo(81000), "cada tick desenha os 8 ícones, e cada reaplicação o novo");
            Assert.That(rig.Driver.TickingCount, Is.EqualTo(1), "a lista com duração pede tick");
        }

        // Blocos de GC.Alloc da thread atual, o mesmo contador do Is.Not.AllocatingGCMemory
        private static int GcAllocs(TestDelegate action)
        {
            Recorder recorder = Recorder.Get("GC.Alloc");
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            recorder.enabled = true;
            action();
            recorder.enabled = false;
            recorder.CollectFromAllThreads();
            return recorder.sampleBlockCount;
        }

        // Oito efeitos longos no ASC; o ciclo tira e reaplica o primeiro
        private Cycle Lasting(AbilitySystemComponent asc, UIUpdateDriver driver)
        {
            GameplayEffect[] effects = Effects(8);
            for (int i = 0; i < effects.Length; i++) asc.ApplyEffect(effects[i]);
            return new Cycle { Clock = _clock, Asc = asc, Driver = driver, Effect = effects[0], Instance = asc.ActiveEffects[0] };
        }

        private AbilitySystemComponent Asc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_clock);
            return asc;
        }

        private GameplayEffect[] Effects(int count)
        {
            var effects = new GameplayEffect[count];
            for (int i = 0; i < count; i++) effects[i] = _gas.CreateEffect(EffectDurationPolicy.HasDuration, LongDuration);
            return effects;
        }

        private static Object[] Slice(GameplayEffect[] effects, params int[] indices)
        {
            var slice = new Object[indices.Length];
            for (int i = 0; i < indices.Length; i++) slice[i] = effects[indices[i]];
            return slice;
        }

        // Lista sob um driver com o relógio manual; os ícones nascem do spawner de teste no container
        private Rig CreateList(AbilitySystemComponent asc, int maxVisible)
        {
            var rig = new Rig { Driver = Track(new GameObject("hud")).AddComponent<UIUpdateDriver>() };
            rig.Driver.SetTimeSource(_clock);
            rig.Spawner = Track(ScriptableObject.CreateInstance<RecordingSpawnerAsset>());
            rig.Container = Track(new GameObject("icones")).transform;

            GameObject prefab = Track(new GameObject("icone"));
            prefab.SetActive(false);
            prefab.AddComponent<IconRecorder>();
            prefab.AddComponent<UIItemContext>();

            GameObject list = Track(new GameObject("lista"));
            list.SetActive(false);
            list.transform.SetParent(rig.Driver.transform);
            rig.Overflow = list.AddComponent<OverflowRecorder>();
            rig.Binder = list.AddComponent<EffectListBinder>();
            rig.Binder.SetOwner(ActorResolver.ForExplicit(asc));
            rig.Binder.SetSource(new ActiveEffectsSource());
            using (var serialized = new SerializedObject(rig.Binder))
            {
                serialized.FindProperty("itemPrefab").objectReferenceValue = prefab;
                serialized.FindProperty("spawner").objectReferenceValue = rig.Spawner;
                serialized.FindProperty("container").objectReferenceValue = rig.Container;
                serialized.FindProperty("overflowView").objectReferenceValue = rig.Overflow;
                serialized.FindProperty("maxVisible").intValue = maxVisible;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            list.SetActive(true);
            return rig;
        }

        // Assunto de cada ícone ativo, na ordem visual do container
        private static Object[] Subjects(Rig rig)
        {
            var subjects = new List<Object>();
            foreach (Transform child in rig.Container)
            {
                if (child.gameObject.activeSelf) subjects.Add(child.GetComponent<IconRecorder>().Last.Subject);
            }

            return subjects.ToArray();
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        // Ticks do ASC e do driver (sem driver, só o ASC); depois tira o efeito e o aplica de novo (vai para o fim)
        private sealed class Cycle
        {
            public ManualTimeSource Clock;
            public AbilitySystemComponent Asc;
            public UIUpdateDriver Driver;
            public GameplayEffect Effect;
            public ActiveEffectInstance Instance;

            public void Run(int ticks, int reapplies)
            {
                for (int i = 0; i < ticks; i++)
                {
                    Clock.Advance(0.01f);
                    Asc.Tick(0.01f);
                    if (Driver != null) Driver.Tick();
                }

                for (int i = 0; i < reapplies; i++)
                {
                    Asc.RemoveEffect(Instance);
                    Instance = Asc.ApplyEffect(Effect);
                }
            }
        }

        private sealed class Rig
        {
            public UIUpdateDriver Driver;
            public RecordingSpawnerAsset Spawner;
            public Transform Container;
            public EffectListBinder Binder;
            public OverflowRecorder Overflow;

            // Desenhos somados dos ícones do container, ativos ou no pool
            public int Renders()
            {
                int renders = 0;
                for (int i = 0; i < Container.childCount; i++) renders += Container.GetChild(i).GetComponent<IconRecorder>().Renders;
                return renders;
            }
        }

        /// <summary>View de ícone de teste: guarda o último modelo e conta os desenhos.</summary>
        public sealed class IconRecorder : MonoBehaviour, IView<EffectIconModel>
        {
            public int Renders;
            public EffectIconModel Last;

            public void Render(in EffectIconModel model)
            {
                Renders++;
                Last = model;
            }
        }

        /// <summary>View de "+N" de teste: guarda o último modelo.</summary>
        public sealed class OverflowRecorder : MonoBehaviour, IView<ListOverflowModel>
        {
            public ListOverflowModel Last;

            public void Render(in ListOverflowModel model) => Last = model;
        }
    }
}
#endif
