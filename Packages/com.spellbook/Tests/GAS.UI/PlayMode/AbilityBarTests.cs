#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using SpellBook.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Barra de habilidades (SL6): um slot por entrada do loadout, na ordem dele; conceder e remover por evento; dono pela
    /// âncora religado no respawn; slots do spawner sem crescer os vivos; trocar o loadout refaz a barra; atualizar sem
    /// alocar (CA1, CA8, CA11, CA13, CA14 e R2).
    /// </summary>
    public class AbilityBarTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;
        private TimeFactory _time;
        private ManualTimeSource _clock;
        private GameplayTag _slotQ;
        private GameplayTag _slotW;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _clock = _time.CreateManual();
            _slotQ = _gas.CreateTag("Slot.Q");
            _slotW = _gas.CreateTag("Slot.W");
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
        public void TwoEntries_TwoSlots_ListOrder_ReorderChangesOrder()
        {
            AbilitySystemComponent asc = Asc();
            GameplayAbility fireball = _gas.CreateAbility();
            GameplayAbility heal = _gas.CreateAbility();
            AbilityLoadout loadout = Loadout((_slotQ, fireball), (_slotW, heal));
            Rig rig = CreateBar(ActorResolver.ForExplicit(asc), loadout);
            Object[] first = Subjects(rig);

            Modify(loadout, so => so.FindProperty("entries").MoveArrayElement(1, 0));
            rig.Source.SetLoadout(loadout);

            Assert.That(first, Is.EqualTo(new Object[] { fireball, heal }), "um slot por entrada, na ordem da lista");
            Assert.That(Subjects(rig), Is.EqualTo(new Object[] { heal, fireball }), "a ordem do asset muda a ordem visual");
            Assert.That(rig.Spawner.Live.Count, Is.EqualTo(2));
        }

        [Test]
        public void GrantRemove_EmptyByEvent_NoUpdate()
        {
            AbilitySystemComponent asc = Asc();
            GameplayAbility fireball = _gas.CreateAbility();
            Rig rig = CreateBar(ActorResolver.ForExplicit(asc), Loadout((_slotQ, fireball)));
            SlotState notGranted = Slot(rig, 0).Last.State;

            asc.GiveAbility(fireball);
            SlotState granted = Slot(rig, 0).Last.State;
            asc.RemoveAbility(fireball);

            Assert.That(notGranted, Is.EqualTo(SlotState.Empty), "não concedida: o slot existe e mostra vazio");
            Assert.That(granted, Is.EqualTo(SlotState.Ready));
            Assert.That(Slot(rig, 0).Last.State, Is.EqualTo(SlotState.Empty));
            Assert.That(rig.Driver.TickingCount, Is.Zero, "conceder e remover chegam por evento, sem tick");
        }

        [Test]
        public void AnchorRespawn_Rebinds()
        {
            GameplayAbility fireball = _gas.CreateAbility();
            GameplayAbility heal = _gas.CreateAbility();
            AbilitySystemComponent before = Asc();
            before.GiveAbility(fireball);
            before.GiveAbility(heal);
            var anchor = Track(ScriptableObject.CreateInstance<AbilitySystemAnchor>());
            anchor.Provide(before);
            Rig rig = CreateBar(ActorResolver.ForAnchor(anchor), Loadout((_slotQ, fireball), (_slotW, heal)));
            SlotState shownBefore = Slot(rig, 0).Last.State;

            AbilitySystemComponent after = Asc();
            after.GiveAbility(heal);
            anchor.Provide(after);
            SlotState respawned = Slot(rig, 0).Last.State;
            int renders = Slot(rig, 0).Renders;
            before.RemoveAbility(fireball);
            int rendersAfterOld = Slot(rig, 0).Renders;
            after.GiveAbility(fireball);

            Assert.That(shownBefore, Is.EqualTo(SlotState.Ready));
            Assert.That(respawned, Is.EqualTo(SlotState.Empty), "o ASC novo não tem a habilidade");
            Assert.That(Slot(rig, 1).Last.State, Is.EqualTo(SlotState.Ready));
            Assert.That(rendersAfterOld, Is.EqualTo(renders), "o ASC antigo não mexe mais na barra");
            Assert.That(Slot(rig, 0).Last.State, Is.EqualTo(SlotState.Ready), "o ASC novo atualiza por evento");
            Assert.That(rig.Spawner.Live.Count, Is.EqualTo(2), "religar devolve e pede os mesmos dois slots");
        }

        [Test]
        public void Slots_FromSpawner_Released_LiveStable()
        {
            AbilitySystemComponent asc = Asc();
            GameplayAbility fireball = _gas.CreateAbility();
            GameplayAbility heal = _gas.CreateAbility();
            AbilityLoadout two = Loadout((_slotQ, fireball), (_slotW, heal));
            AbilityLoadout one = Loadout((_slotQ, fireball));
            Rig rig = CreateBar(ActorResolver.ForExplicit(asc), two);
            int spawnedFirst = rig.Spawner.SpawnCount;
            var seen = new HashSet<GameObject>(rig.Spawner.Live);

            for (int i = 0; i < 50; i++)
            {
                rig.Source.SetLoadout(one);
                Assert.That(rig.Spawner.Live.Count, Is.EqualTo(1), "o slot que sobra volta ao spawner");
                rig.Source.SetLoadout(two);
                seen.UnionWith(rig.Spawner.Live);
            }

            Assert.That(spawnedFirst, Is.EqualTo(2), "os slots vêm do spawner");
            Assert.That(rig.Spawner.ReleaseCount, Is.GreaterThanOrEqualTo(50));
            Assert.That(rig.Spawner.Live.Count, Is.EqualTo(2), "a contagem de vivos não cresce");
            Assert.That(seen.Count, Is.EqualTo(2), "o spawner reaproveita os mesmos objetos");
        }

        [Test]
        public void SetLoadout_Rebuilds()
        {
            AbilitySystemComponent asc = Asc();
            GameplayTag stunned = _gas.CreateTag("Stunned");
            GameplayAbility fireball = _gas.CreateAbility();
            GameplayAbility heal = _gas.CreateAbility();
            GameplayAbility frost = _gas.CreateAbility();
            asc.GiveAbility(frost);
            Rig rig = CreateBar(ActorResolver.ForExplicit(asc), Loadout((_slotQ, fireball), (_slotW, heal)));

            rig.Source.SetLoadout(Loadout((_slotQ, frost)));
            int renders = Slot(rig, 0).Renders;
            asc.AddTag(stunned);
            asc.RemoveTag(stunned);

            Assert.That(Subjects(rig), Is.EqualTo(new Object[] { frost }));
            Assert.That(Slot(rig, 0).Last.State, Is.EqualTo(SlotState.Ready));
            Assert.That(Slot(rig, 0).Renders - renders, Is.EqualTo(2), "um desenho por evento: os leitores antigos saíram");
            Assert.That(Slot(rig, 0).Last.Subject, Is.SameAs(frost), "um leitor antigo não sobrescreve o slot");
        }

        [Test]
        public void BarUpdates10000_DoNotAllocate()
        {
            GameplayTag stunned = _gas.CreateTag("Stunned");
            AbilitySystemComponent asc = Asc();
            GameplayAbility fireball = _gas.CreateAbilityWithCooldown(100f);
            GameplayAbility heal = _gas.CreateAbility();
            fireball.BlockedTags.Add(stunned);
            asc.GiveAbility(fireball);
            asc.GiveAbility(heal);
            Rig rig = CreateBar(ActorResolver.ForExplicit(asc), Loadout((_slotQ, fireball), (_slotW, heal)));
            asc.TryActivateAbility(fireball, Vector3.zero);
            Updates(rig, asc, stunned, 10);

            Assert.That(() => Updates(rig, asc, stunned, 10000), Is.Not.AllocatingGCMemory());
            Assert.That(Slot(rig, 0).Last.State, Is.EqualTo(SlotState.OnCooldown));
            Assert.That(Slot(rig, 1).Last.State, Is.EqualTo(SlotState.Ready));
            Assert.That(Slot(rig, 0).Renders, Is.GreaterThan(20000), "cada tick e cada tag desenham o slot");
        }

        [Test]
        public void Preview_OneSlotPerEntry_InOrder()
        {
            GameplayAbility fireball = _gas.CreateAbilityWithCooldown(8f);
            GameplayAbility heal = _gas.CreateAbility();
            var source = new AbilityLoadoutSource();
            source.SetLoadout(Loadout((_slotQ, fireball), (_slotW, heal)));
            var items = new List<SlotModel>();

            source.CreatePreview(new UIPreviewContext(1), items);
            items.Clear();
            source.CreatePreview(new UIPreviewContext(1), items);

            Assert.That(items.Count, Is.EqualTo(2), "um slot de prévia por entrada");
            Assert.That(items[0].Subject, Is.SameAs(fireball));
            Assert.That(items[1].Subject, Is.SameAs(heal));
            Assert.That(items[0].CooldownRemaining, Is.EqualTo(4f).Within(1e-4f), "a mesma prévia do slot: metade da recarga");
            Assert.That(items[1].Charges, Is.EqualTo(2));
            Assert.That(Text(items[0].KeyLabel), Is.EqualTo("Q"), "sem provedor, o rótulo da prévia");
            Assert.That(Text(items[1].KeyLabel), Is.EqualTo("Q"), "o buffer da prévia é guardado");
            Assert.That(items[0].KeyLabel.Start, Is.EqualTo(0), "a segunda chamada escreve o buffer do zero");
            Assert.That(items[1].KeyLabel.Start, Is.EqualTo(1), "a segunda chamada escreve o buffer do zero");
        }

        // Uma atualização por tag (entra e sai) e um tick com a recarga correndo
        private void Updates(Rig rig, AbilitySystemComponent asc, GameplayTag tag, int count)
        {
            for (int i = 0; i < count; i++)
            {
                asc.AddTag(tag);
                asc.RemoveTag(tag);
                _clock.Advance(0.001f);
                asc.Tick(0.001f);
                rig.Driver.Tick();
            }
        }

        private AbilitySystemComponent Asc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_clock);
            return asc;
        }

        /// <summary>Loadout com as entradas na ordem dada, gravado pelo <see cref="SerializedObject"/>.</summary>
        private AbilityLoadout Loadout(params (GameplayTag Slot, GameplayAbility Ability)[] entries)
        {
            var loadout = Track(ScriptableObject.CreateInstance<AbilityLoadout>());
            Modify(loadout, so =>
            {
                SerializedProperty list = so.FindProperty("entries");
                for (int i = 0; i < entries.Length; i++)
                {
                    list.InsertArrayElementAtIndex(i);
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("slot.definition").objectReferenceValue = entries[i].Slot.GetDefinition();
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("ability").objectReferenceValue = entries[i].Ability;
                }
            });
            return loadout;
        }

        // Barra sob um driver com o relógio manual; os slots nascem do spawner de teste no container
        private Rig CreateBar(ActorResolver owner, AbilityLoadout loadout)
        {
            var rig = new Rig();
            rig.Driver = Track(new GameObject("tela")).AddComponent<UIUpdateDriver>();
            rig.Driver.SetTimeSource(_clock);
            rig.Spawner = Track(ScriptableObject.CreateInstance<RecordingSpawnerAsset>());
            rig.Container = Track(new GameObject("slots")).transform;

            GameObject prefab = Track(new GameObject("slot"));
            prefab.SetActive(false);
            prefab.AddComponent<Recorder>();
            prefab.AddComponent<UIItemContext>();

            GameObject bar = Track(new GameObject("barra"));
            bar.SetActive(false);
            bar.transform.SetParent(rig.Driver.transform);
            AbilityBarBinder binder = bar.AddComponent<AbilityBarBinder>();
            binder.SetOwner(owner);
            rig.Source = new AbilityLoadoutSource();
            rig.Source.SetLoadout(loadout);
            binder.SetSource(rig.Source);
            Modify(binder, so =>
            {
                so.FindProperty("itemPrefab").objectReferenceValue = prefab;
                so.FindProperty("spawner").objectReferenceValue = rig.Spawner;
                so.FindProperty("container").objectReferenceValue = rig.Container;
            });
            bar.SetActive(true);
            return rig;
        }

        // O slot ativo de posição index na ordem visual; os devolvidos ao spawner ficam inativos no container
        private static Recorder Slot(Rig rig, int index)
        {
            foreach (Transform child in rig.Container)
            {
                if (child.gameObject.activeSelf && index-- == 0) return child.GetComponent<Recorder>();
            }

            Assert.Fail($"sem slot ativo na posição pedida; ativos: {rig.Spawner.Live.Count}");
            return null;
        }

        // Assunto de cada slot ativo, na ordem visual do container
        private static Object[] Subjects(Rig rig)
        {
            var subjects = new List<Object>();
            foreach (Transform child in rig.Container)
            {
                if (child.gameObject.activeSelf) subjects.Add(child.GetComponent<Recorder>().Last.Subject);
            }

            return subjects.ToArray();
        }

        private static string Text(TextSpan span) => span.IsEmpty ? string.Empty : span.Buffer.ToString(span);

        private static void Modify(Object target, System.Action<SerializedObject> edit)
        {
            var serialized = new SerializedObject(target);
            edit(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private sealed class Rig
        {
            public UIUpdateDriver Driver;
            public RecordingSpawnerAsset Spawner;
            public Transform Container;
            public AbilityLoadoutSource Source;
        }

        /// <summary>View de slot de teste: guarda o último modelo e conta os desenhos.</summary>
        public sealed class Recorder : MonoBehaviour, IView<SlotModel>
        {
            public int Renders;
            public SlotModel Last;

            public void Render(in SlotModel model)
            {
                Renders++;
                Last = model;
            }
        }
    }
}
#endif
