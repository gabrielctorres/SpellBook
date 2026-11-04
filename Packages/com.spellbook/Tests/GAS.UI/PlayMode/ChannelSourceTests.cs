#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.GAS.Values;
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
    /// Barra de cast (SL10): a <see cref="ChannelSource"/> mostra a canalização em curso do ASC dono, pelo tempo do ASC, e
    /// o resultado da última (CA21 a CA25, R17 a R19).
    /// </summary>
    public class ChannelSourceTests
    {
        private const string FireballKey = "test.fireball";
        private const float Tolerance = 1e-4f;

        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;
        private TimeFactory _time;
        private ManualTimeSource _clock;
        private ManualTimeSource _uiClock;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _clock = _time.CreateManual();
            _uiClock = _time.CreateManual();
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
        public void Channel3s_FractionUp_SecondsDown_CompletedThenIdle_PauseStops()
        {
            AbilitySystemComponent asc = Asc();
            GameplayAbility fireball = Channel(3f, FireballKey, "Fireball");
            fireball.Icon = Icon();
            asc.GiveAbility(fireball);
            Rig rig = Bind(ActorResolver.ForExplicit(asc));
            CastBarModel idle = rig.View.Last;

            asc.TryActivateAbility(fireball, AbilityTarget.None);
            CastBarModel start = rig.View.Last;
            Step(rig, asc, 1f);
            CastBarModel one = rig.View.Last;
            Step(rig, asc, 1f);
            CastBarModel two = rig.View.Last;
            for (int i = 0; i < 5; i++)
            {
                _uiClock.Advance(1f);
                rig.Driver.Tick();
            }

            CastBarModel paused = rig.View.Last;
            Step(rig, asc, 1f);
            CastBarModel end = rig.View.Last;

            Assert.That(idle.State, Is.EqualTo(CastBarState.Idle));
            Assert.That(start.State, Is.EqualTo(CastBarState.Channeling));
            Assert.That(start.Name, Is.EqualTo("Bola de Fogo"), "o nome vem do provedor, pela chave");
            Assert.That(start.Icon, Is.SameAs(fireball.Icon));
            Assert.That(start.Indeterminate, Is.False);
            Assert.That(start.Fraction, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(start.Remaining, Is.EqualTo(3f).Within(Tolerance));
            Assert.That(one.Fraction, Is.EqualTo(1f / 3f).Within(Tolerance), "a fração sobe pelo tempo do ASC");
            Assert.That(one.Remaining, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(two.Fraction, Is.EqualTo(2f / 3f).Within(Tolerance));
            Assert.That(two.Remaining, Is.EqualTo(1f).Within(Tolerance), "os segundos descem");
            Assert.That(paused.Fraction, Is.EqualTo(two.Fraction), "com o ASC parado, a barra não anda");
            Assert.That(paused.Remaining, Is.EqualTo(two.Remaining));
            Assert.That(end.State, Is.EqualTo(CastBarState.Idle));
            Assert.That(end.LastResult, Is.EqualTo(CastBarResult.Completed));
            Assert.That(end.ResultCount, Is.EqualTo(1));
            Assert.That(end.Name, Is.EqualTo("Bola de Fogo"), "o fim guarda o nome da que acabou");
            Assert.That(rig.Driver.TickingCount, Is.Zero, "sem canalização, sem tick");
        }

        [Test]
        public void CancelByCodeAndByTag_Cancelled_CountUp()
        {
            GameplayTag silenced = _gas.CreateTag("Silenced");
            AbilitySystemComponent asc = Asc();
            GameplayAbility channel = Channel(10f);
            channel.CancelledByTags.Add(silenced);
            AbilitySpec spec = asc.GiveAbility(channel);
            Rig rig = Bind(ActorResolver.ForExplicit(asc));

            asc.TryActivateAbility(channel, AbilityTarget.None);
            asc.CancelAbility(spec);
            CastBarModel byCode = rig.View.Last;
            asc.TryActivateAbility(channel, AbilityTarget.None);
            Assert.That(rig.View.Last.State, Is.EqualTo(CastBarState.Channeling));
            asc.AddTag(silenced);
            CastBarModel byTag = rig.View.Last;

            Assert.That(byCode.State, Is.EqualTo(CastBarState.Idle));
            Assert.That(byCode.LastResult, Is.EqualTo(CastBarResult.Cancelled));
            Assert.That(byCode.ResultCount, Is.EqualTo(1));
            Assert.That(byTag.State, Is.EqualTo(CastBarState.Idle));
            Assert.That(byTag.LastResult, Is.EqualTo(CastBarResult.Cancelled), "a tag bloqueadora cancela");
            Assert.That(byTag.ResultCount, Is.EqualTo(2), "o contador muda a cada fim");
            Assert.That(rig.Driver.TickingCount, Is.Zero);
        }

        [Test]
        public void NoDuration_Indeterminate()
        {
            AbilitySystemComponent asc = Asc();
            GameplayAbility channel = Channel(0f);
            asc.GiveAbility(channel);
            Rig rig = Bind(ActorResolver.ForExplicit(asc));

            asc.TryActivateAbility(channel, AbilityTarget.None);
            int ticking = rig.Driver.TickingCount;
            int renders = rig.View.Renders;
            Step(rig, asc, 5f);

            Assert.That(rig.View.Last.State, Is.EqualTo(CastBarState.Channeling));
            Assert.That(rig.View.Last.Indeterminate, Is.True);
            Assert.That(rig.View.Last.Fraction, Is.Zero);
            Assert.That(rig.View.Last.Remaining, Is.Zero);
            Assert.That(ticking, Is.Zero, "sem duração, sem tick");
            Assert.That(rig.View.Renders, Is.EqualTo(renders));
        }

        [Test]
        public void InstantTogglePassive_NoBar()
        {
            AbilitySystemComponent asc = Asc();
            GameplayAbility instant = _gas.CreateAbility();
            GameplayAbility toggle = _gas.CreateAbility();
            toggle.Behavior = AbilityBehavior.Toggle;
            GameplayAbility passive = _gas.CreateAbility();
            passive.Behavior = AbilityBehavior.Passive;
            GameplayAbility channel = Channel(3f);
            asc.GiveAbility(instant);
            asc.GiveAbility(toggle);
            asc.GiveAbility(channel);
            Rig rig = Bind(ActorResolver.ForExplicit(asc));

            AbilitySpec passiveSpec = asc.GiveAbility(passive);
            asc.TryActivateAbility(instant, AbilityTarget.None);
            asc.TryActivateAbility(toggle, AbilityTarget.None);
            bool toggledOn = asc.FindSpec(toggle).IsActive;
            asc.TryActivateAbility(toggle, AbilityTarget.None);
            asc.RemoveAbility(passive);
            int channelingRenders = rig.View.ChannelingRenders;
            CastBarModel idle = rig.View.Last;
            asc.TryActivateAbility(channel, AbilityTarget.None);
            CastBarModel control = rig.View.Last;

            Assert.That(passiveSpec.IsActive, Is.False, "a passiva ativou ao conceder e saiu ao retirar");
            Assert.That(toggledOn, Is.True);
            Assert.That(channelingRenders, Is.Zero, "só a canalização acende a barra");
            Assert.That(idle.State, Is.EqualTo(CastBarState.Idle));
            Assert.That(control.State, Is.EqualTo(CastBarState.Channeling), "a canalização acende");
            Assert.That(control.LastResult, Is.EqualTo(CastBarResult.None), "o fim delas não é resultado da barra");
            Assert.That(control.ResultCount, Is.Zero);
        }

        [Test]
        public void AnchorRespawn_Rebinds_Ticks10000_NoAlloc_NeverActivatesOrCancels()
        {
            GameplayAbility channel = Channel(1000f);
            AbilitySystemComponent before = Asc();
            AbilitySystemComponent after = Asc();
            AbilitySpec beforeSpec = before.GiveAbility(channel);
            AbilitySpec afterSpec = after.GiveAbility(channel);
            before.TryActivateAbility(channel, AbilityTarget.None);
            var anchor = Track(ScriptableObject.CreateInstance<AbilitySystemAnchor>());
            anchor.Provide(before);
            Rig rig = Bind(ActorResolver.ForAnchor(anchor));
            CastBarModel shownBefore = rig.View.Last;

            after.TryActivateAbility(channel, AbilityTarget.None);
            int activations = 0;
            int ends = 0;
            after.OnAbilityActivated += _ => activations++;
            after.OnAbilityEnded += (_, _) => ends++;
            Step(rig, after, 2f);
            anchor.Provide(after);
            CastBarModel respawned = rig.View.Last;
            int renders = rig.View.Renders;
            before.CancelAbility(beforeSpec);
            before.TryActivateAbility(channel, AbilityTarget.None);
            int rendersAfterOld = rig.View.Renders;
            Ticks(rig, after, 10);

            Assert.That(() => Ticks(rig, after, 10000), Is.Not.AllocatingGCMemory());
            float progressed = rig.View.Last.Fraction;
            rig.Binder.gameObject.SetActive(false);

            Assert.That(shownBefore.State, Is.EqualTo(CastBarState.Channeling), "ao ligar, acha a canalização em curso");
            Assert.That(respawned.State, Is.EqualTo(CastBarState.Channeling));
            Assert.That(respawned.Remaining, Is.EqualTo(998f).Within(Tolerance), "religada ao ASC novo");
            Assert.That(rendersAfterOld, Is.EqualTo(renders), "o ASC antigo não mexe mais na barra");
            Assert.That(progressed, Is.GreaterThan(respawned.Fraction), "os ticks movem a barra");
            Assert.That(afterSpec.IsChanneling, Is.True, "a barra não cancela");
            Assert.That(activations, Is.Zero, "a barra não ativa");
            Assert.That(ends, Is.Zero, "a barra não cancela");
        }

        [Test]
        public void TwoChannels_MostRecentShown_ThenTheOther()
        {
            AbilitySystemComponent asc = Asc();
            GameplayAbility a = Channel(4f, null, "A");
            GameplayAbility c = Channel(10f, null, "C");
            GameplayAbility x = Channel(10f, null, "X");
            GameplayAbility b = Channel(5f, null, "B");
            asc.GiveAbility(a);
            asc.GiveAbility(c);
            AbilitySpec xSpec = asc.GiveAbility(x);
            AbilitySpec bSpec = asc.GiveAbility(b);
            Rig rig = Bind(ActorResolver.ForExplicit(asc));

            asc.TryActivateAbility(x, AbilityTarget.None);
            Step(rig, asc, 0.5f);
            asc.TryActivateAbility(c, AbilityTarget.None);
            Step(rig, asc, 0.5f);
            asc.TryActivateAbility(a, AbilityTarget.None);
            Step(rig, asc, 0.5f);
            asc.TryActivateAbility(b, AbilityTarget.None);
            string newest = rig.View.Last.Name;
            int renders = rig.View.Renders;
            asc.CancelAbility(xSpec);
            int rendersAfterOther = rig.View.Renders;
            Step(rig, asc, 1.5f);
            asc.CancelAbility(bSpec);
            CastBarModel back = rig.View.Last;

            Assert.That(newest, Is.EqualTo("B"), "mostra a que começou por último");
            Assert.That(rendersAfterOther, Is.EqualTo(renders), "o fim de outra não publica");
            Assert.That(back.State, Is.EqualTo(CastBarState.Channeling));
            Assert.That(back.Name, Is.EqualTo("A"), "volta à mais recente das que sobram");
            Assert.That(back.Remaining, Is.EqualTo(2f).Within(Tolerance), "com o resto do tempo dela");
            Assert.That(back.Fraction, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(back.LastResult, Is.EqualTo(CastBarResult.Cancelled));
            Assert.That(back.ResultCount, Is.EqualTo(1));
            Assert.That(rig.Driver.TickingCount, Is.EqualTo(1), "a outra tem duração: segue com tick");
        }

        [Test]
        public void Preview_SixtyPercent_NameOrFireball()
        {
            var source = new ChannelSource();
            CastBarModel fallback = source.CreatePreview(new UIPreviewContext(1));
            GameplayAbility ability = Channel(3f, FireballKey, "Fireball");
            ability.Icon = Icon();
            source.PreviewAbility = ability;
            CastBarModel named = source.CreatePreview(new UIPreviewContext(1, Track(ScriptableObject.CreateInstance<TestText>())));

            Assert.That(fallback.State, Is.EqualTo(CastBarState.Channeling));
            Assert.That(fallback.Fraction, Is.EqualTo(0.6f).Within(Tolerance));
            Assert.That(fallback.Remaining, Is.EqualTo(1.2f).Within(Tolerance));
            Assert.That(fallback.Name, Is.EqualTo("Fireball"));
            Assert.That(fallback.Icon, Is.Null);
            Assert.That(named.Name, Is.EqualTo("Bola de Fogo"));
            Assert.That(named.Icon, Is.SameAs(ability.Icon));
            Assert.That(named.Fraction, Is.EqualTo(0.6f).Within(Tolerance));
        }

        private void Step(Rig rig, AbilitySystemComponent asc, float seconds)
        {
            _clock.Advance(seconds);
            asc.Tick(seconds);
            _uiClock.Advance(seconds);
            rig.Driver.Tick();
        }

        private void Ticks(Rig rig, AbilitySystemComponent asc, int count)
        {
            for (int i = 0; i < count; i++) Step(rig, asc, 0.01f);
        }

        private AbilitySystemComponent Asc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_clock);
            return asc;
        }

        // Canaliza pela duração; 0 canaliza até ser cancelada
        private GameplayAbility Channel(float duration, string key = null, string name = "Canal")
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Behavior = AbilityBehavior.Channel;
            ability.ChannelDuration = ScalableFloat.Constant(duration);
            ability.DisplayName = new LocalizedText(key, name);
            return ability;
        }

        private Sprite Icon()
        {
            var texture = Track(new Texture2D(1, 1));
            return Track(Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero));
        }

        // Barra sob um driver com o relógio da UI, separado do relógio do ASC; tema com o provedor de teste
        private Rig Bind(ActorResolver owner)
        {
            var theme = Track(ScriptableObject.CreateInstance<UITheme>());
            using (var serialized = new SerializedObject(theme))
            {
                serialized.FindProperty("text").objectReferenceValue = Track(ScriptableObject.CreateInstance<TestText>());
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var rig = new Rig();
            rig.Driver = Track(new GameObject("tela")).AddComponent<UIUpdateDriver>();
            rig.Driver.SetTimeSource(_uiClock);
            var bar = Track(new GameObject("barra de cast"));
            bar.SetActive(false);
            bar.transform.SetParent(rig.Driver.transform);
            rig.View = bar.AddComponent<Recorder>();
            rig.Binder = bar.AddComponent<CastBarBinder>();
            rig.Binder.SetOwner(owner);
            rig.Binder.SetTheme(theme);
            rig.Binder.SetSource(new ChannelSource());
            rig.Binder.SetView(rig.View);
            bar.SetActive(true);
            return rig;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private sealed class Rig
        {
            public UIUpdateDriver Driver;
            public CastBarBinder Binder;
            public Recorder View;
        }

        /// <summary>View de teste: guarda o último modelo e conta os desenhos.</summary>
        public sealed class Recorder : MonoBehaviour, IView<CastBarModel>
        {
            public CastBarModel Last;
            public int Renders;
            public int ChannelingRenders;

            public void Render(in CastBarModel model)
            {
                Renders++;
                if (model.State == CastBarState.Channeling) ChannelingRenders++;
                Last = model;
            }
        }

        /// <summary>Provedor de teste: o nome da Fireball; as outras chaves caem no texto padrão.</summary>
        public sealed class TestText : TextProviderAsset
        {
            public override IFormatProvider Culture => CultureInfo.InvariantCulture;

            public override string GetText(string key, string fallback) => key == FireballKey ? "Bola de Fogo" : fallback;
        }
    }
}
#endif
