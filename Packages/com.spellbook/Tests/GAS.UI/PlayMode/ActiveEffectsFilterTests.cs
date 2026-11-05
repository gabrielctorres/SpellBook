#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using SpellBook.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Filtros, fim próximo e respawn da lista de efeitos (Spec do B-018, BF2): papel, consulta de tags, purgável e
    /// <see cref="GameplayEffect.IsHidden"/> filtram, e trocar o filtro refaz a lista; <c>EndingSoon</c> nos últimos N
    /// segundos da maior restante; o respawn pela âncora refaz a lista e devolve os ícones antigos (CA5, CA9 e CA12).
    /// </summary>
    public class ActiveEffectsFilterTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;
        private TimeFactory _time;
        private ManualTimeSource _clock;
        private UIUpdateDriver _driver;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _clock = _time.CreateManual();
            _driver = Track(new GameObject("tela")).AddComponent<UIUpdateDriver>();
            _driver.SetTimeSource(_clock);
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
        public void Filter_Debuffs_Query_Purgable_HiddenNever_FilterChangeRemoves()
        {
            AbilitySystemComponent asc = Asc();
            GameplayTag poison = _gas.CreateTag("Status.Veneno");
            GameplayEffect buff = Effect(), debuff = Effect(), venom = Effect(poison), purge = Effect(), hidden = Effect(), plain = Effect();
            GameplayEffect both = Effect();
            Set(buff, "isBuff", true);
            Set(debuff, "isDebuff", true);
            Set(venom, "isDebuff", true);
            Set(purge, "isDebuff", true);
            Set(purge, "isPurgable", true);
            Set(hidden, "isDebuff", true);
            Set(hidden, "isHidden", true);
            Set(both, "isBuff", true);
            Set(both, "isDebuff", true);
            ActiveEffectsSource debuffsSource = Source(new EffectListFilter(EffectRoleFilter.Debuffs));
            var venomQuery = new GameplayTagQuery();
            venomQuery.Any.Add(poison);
            Receiver all = Attach(new ActiveEffectsSource());
            Receiver debuffs = Attach(debuffsSource);
            Receiver venoms = Attach(Source(new EffectListFilter(EffectRoleFilter.Both, venomQuery)));
            Receiver purgable = Attach(Source(new EffectListFilter(EffectRoleFilter.Both, null, true)));
            asc.ApplyEffect(buff);
            asc.ApplyEffect(debuff);
            asc.ApplyEffect(venom);
            asc.ApplyEffect(purge);
            asc.ApplyEffect(hidden);
            asc.ApplyEffect(plain);
            asc.ApplyEffect(both);
            Bind(asc, all, debuffs, venoms, purgable);

            Assert.That(all.Subjects(), Is.EqualTo(new Object[] { buff, debuff, venom, purge, plain, both }), "IsHidden nunca aparece");
            Assert.That(debuffs.Subjects(), Is.EqualTo(new Object[] { debuff, venom, purge }), "só debuffs: sem buff nem neutro");
            Assert.That(venoms.Subjects(), Is.EqualTo(new Object[] { venom }), "a consulta casa as tags concedidas");
            Assert.That(purgable.Subjects(), Is.EqualTo(new Object[] { purge }), "só purgáveis");

            venomQuery.None.Add(poison);
            asc.RemoveEffectsWithTag(poison);
            asc.ApplyEffect(venom);
            Assert.That(debuffs.Subjects(), Is.EqualTo(new Object[] { debuff, purge, venom }), "o filtro vale para o que chega depois");
            Assert.That(venoms.Subjects(), Is.EqualTo(new Object[] { venom }), "mudar a consulta de fora não muda o filtro");

            var noVenomQuery = new GameplayTagQuery();
            noVenomQuery.None.Add(poison);
            int removes = debuffs.Removes;
            debuffsSource.SetFilter(new EffectListFilter(EffectRoleFilter.Debuffs, noVenomQuery));
            Assert.That(debuffs.Subjects(), Is.EqualTo(new Object[] { debuff, purge }), "o efeito que deixa de casar sai");
            Assert.That(debuffs.Removes, Is.GreaterThan(removes), "trocar o filtro devolve os ícones");

            debuffsSource.SetFilter(new EffectListFilter(EffectRoleFilter.Buffs));
            Assert.That(debuffs.Subjects(), Is.EqualTo(new Object[] { buff }), "só buffs: sem o efeito com os dois papéis");
            Assert.That(debuffsSource.Filter.Role, Is.EqualTo(EffectRoleFilter.Buffs), "o filtro lido é o aplicado");
            debuffsSource.SetFilter(null);
            Assert.That(debuffs.Subjects(), Is.EqualTo(new Object[] { buff, debuff, purge, plain, both, venom }),
                "filtro nulo aceita todos os visíveis, na ordem de aplicação");
        }

        [Test]
        public void EndingSoon_LastNSeconds_OfLongestInstance()
        {
            AbilitySystemComponent target = Asc();
            AbilitySystemComponent first = Asc();
            AbilitySystemComponent second = Asc();
            GameplayEffect burn = Effect(EffectDurationPolicy.HasDuration, 10f);
            GameplayEffect aura = Effect(EffectDurationPolicy.Infinite, 0f);
            Receiver list = Attach(new ActiveEffectsSource());
            Receiver wide = Attach(new ActiveEffectsSource { EndingSoonSeconds = 5f });
            Bind(target, list, wide);

            target.ApplyEffect(burn, first);
            target.ApplyEffect(aura, first);
            Assert.That(list.Model(0).EndingSoon, Is.False, "10 s restantes: longe do fim");
            Advance(target, 5.5f);
            Assert.That(list.Model(0).EndingSoon || !wide.Model(0).EndingSoon, Is.False, "4,5 s: só a lista de 5 s avisa");
            Advance(target, 1.5f);
            Assert.That(list.Model(0).EndingSoon, Is.True, "3 s: o padrão avisa");

            target.ApplyEffect(burn, second);
            _driver.Tick();
            Assert.That(list.Model(0).EndingSoon, Is.False, "a outra fonte renova: segue a maior restante");
            Advance(target, 8f);
            Assert.That(list.Model(0).EndingSoon, Is.True);
            Assert.That(list.Model(1).EndingSoon || wide.Model(1).EndingSoon, Is.False, "infinito nunca avisa");
        }

        [Test]
        public void AnchorRespawn_RebuildsForNewAsc_ReleasesOldIcons()
        {
            GameplayEffect burn = Effect(EffectDurationPolicy.HasDuration, 10f);
            GameplayEffect aura = Effect(EffectDurationPolicy.Infinite, 0f);
            GameplayEffect venom = Effect(EffectDurationPolicy.HasDuration, 10f);
            AbilitySystemComponent before = Asc();
            ActiveEffectInstance burning = before.ApplyEffect(burn);
            before.ApplyEffect(aura);
            var anchor = Track(ScriptableObject.CreateInstance<AbilitySystemAnchor>());
            anchor.Provide(before);
            Rig rig = CreateList(ActorResolver.ForAnchor(anchor));
            Object[] shownBefore = Subjects(rig);
            int releasesBefore = rig.Spawner.ReleaseCount;

            AbilitySystemComponent after = Asc();
            after.ApplyEffect(venom);
            anchor.Provide(after);
            Object[] respawned = Subjects(rig);
            int releasesAfter = rig.Spawner.ReleaseCount;
            before.ApplyEffect(venom);
            before.RemoveEffect(burning);
            Object[] afterOld = Subjects(rig);
            after.ApplyEffect(aura);

            Assert.That(shownBefore, Is.EqualTo(new Object[] { burn, aura }));
            Assert.That(respawned, Is.EqualTo(new Object[] { venom }), "a lista se refaz para o ASC novo");
            Assert.That(releasesAfter - releasesBefore, Is.EqualTo(2), "os dois ícones do ASC antigo voltam ao spawner");
            Assert.That(afterOld, Is.EqualTo(new Object[] { venom }), "o ASC antigo não mexe mais na lista");
            Assert.That(Subjects(rig), Is.EqualTo(new Object[] { venom, aura }), "o ASC novo atualiza por evento");
            Assert.That(rig.Spawner.Live.Count, Is.EqualTo(2), "os vivos não crescem");
            Assert.That(rig.Driver.TickingCount, Is.EqualTo(1), "o tick segue o ASC novo");
        }

        private void Advance(AbilitySystemComponent asc, float seconds)
        {
            asc.Tick(seconds);
            _clock.Advance(seconds);
            _driver.Tick();
        }

        private static ActiveEffectsSource Source(EffectListFilter filter)
        {
            var source = new ActiveEffectsSource();
            source.SetFilter(filter);
            return source;
        }

        private Receiver Attach(ActiveEffectsSource source)
        {
            var receiver = new Receiver { Binding = source.CreateBinding() };
            receiver.Binding.SetReceiver(receiver);
            _driver.Register(receiver.Binding);
            return receiver;
        }

        private static void Bind(AbilitySystemComponent asc, params Receiver[] receivers)
        {
            foreach (Receiver receiver in receivers) receiver.Binding.Attach(asc, new UIBindContext(null));
        }

        private GameplayEffect Effect(params GameplayTag[] tags) => _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f, tags);

        private GameplayEffect Effect(EffectDurationPolicy policy, float duration) => _gas.CreateEffect(policy, duration);

        private static void Set(GameplayEffect effect, string field, bool value)
        {
            using var serialized = new SerializedObject(effect);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private AbilitySystemComponent Asc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_clock);
            return asc;
        }

        // Lista sob um driver com o relógio manual; os ícones nascem do spawner de teste no container
        private Rig CreateList(ActorResolver owner)
        {
            var rig = new Rig { Driver = Track(new GameObject("hud")).AddComponent<UIUpdateDriver>() };
            rig.Driver.SetTimeSource(_clock);
            rig.Spawner = Track(ScriptableObject.CreateInstance<RecordingSpawnerAsset>());
            rig.Container = Track(new GameObject("icones")).transform;

            GameObject prefab = Track(new GameObject("icone"));
            prefab.SetActive(false);
            prefab.AddComponent<IconRecorder>();

            GameObject list = Track(new GameObject("lista"));
            list.SetActive(false);
            list.transform.SetParent(rig.Driver.transform);
            EffectListBinder binder = list.AddComponent<EffectListBinder>();
            binder.SetOwner(owner);
            binder.SetSource(new ActiveEffectsSource());
            using (var serialized = new SerializedObject(binder))
            {
                serialized.FindProperty("itemPrefab").objectReferenceValue = prefab;
                serialized.FindProperty("spawner").objectReferenceValue = rig.Spawner;
                serialized.FindProperty("container").objectReferenceValue = rig.Container;
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

        private sealed class Rig
        {
            public UIUpdateDriver Driver;
            public RecordingSpawnerAsset Spawner;
            public Transform Container;
        }

        /// <summary>Receptor de teste: os itens na ordem da lista e quantos foram removidos.</summary>
        private sealed class Receiver : IUIListReceiver<EffectIconModel>
        {
            private readonly List<int> _ids = new List<int>();
            private readonly List<EffectIconModel> _models = new List<EffectIconModel>();

            public UIListBinding<EffectIconModel> Binding;
            public int Removes;

            public EffectIconModel Model(int index) => _models[index];

            public void AddItem(int id, in EffectIconModel item)
            {
                int index = _ids.IndexOf(id);
                if (index >= 0)
                {
                    _models[index] = item;
                    return;
                }

                _ids.Add(id);
                _models.Add(item);
            }

            public void UpdateItem(int id, in EffectIconModel item)
            {
                int index = _ids.IndexOf(id);
                if (index >= 0) _models[index] = item;
            }

            public void RemoveItem(int id)
            {
                int index = _ids.IndexOf(id);
                if (index < 0) return;

                Removes++;
                _ids.RemoveAt(index);
                _models.RemoveAt(index);
            }

            public void ClearItems()
            {
                Removes += _ids.Count;
                _ids.Clear();
                _models.Clear();
            }

            public Object[] Subjects()
            {
                var subjects = new Object[_models.Count];
                for (int i = 0; i < subjects.Length; i++) subjects[i] = _models[i].Subject;
                return subjects;
            }
        }

        /// <summary>View de ícone de teste: guarda o último modelo.</summary>
        public sealed class IconRecorder : MonoBehaviour, IView<EffectIconModel>
        {
            public EffectIconModel Last;

            public void Render(in EffectIconModel model) => Last = model;
        }
    }
}
#endif
