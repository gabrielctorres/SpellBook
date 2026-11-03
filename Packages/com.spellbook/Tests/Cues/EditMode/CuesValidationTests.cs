using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Validation;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEngine;

namespace SpellBook.Cues.Tests
{
    /// <summary>Validação da definição, dos itens, da biblioteca e do roteador de cues (CA20, parte do R15).</summary>
    public class CuesValidationTests
    {
        private CuesFactory _cues;
        private TimeFactory _times;
        private ManualTimeSource _time;

        [SetUp]
        public void SetUp()
        {
            _cues = new CuesFactory();
            _times = new TimeFactory();
            _time = _times.CreateManual();
        }

        [TearDown]
        public void TearDown()
        {
            _cues.Dispose();
            _times.Dispose();
        }

        [Test]
        public void Definition_NoTag_IsError()
        {
            CueDefinition cue = _cues.CreateCue(default, CueKind.Burst, Vfx());

            ValidationMessage message = Validate(cue).Single();

            AssertMessage(message, ValidationSeverity.Error, "tag", "Sem tag. Escolha a tag que toca este cue.");
        }

        [Test]
        public void Definition_NoItemsNoSounds_IsWarning()
        {
            CueDefinition cue = _cues.CreateCue(_cues.CreateTag("Cue.Empty"), CueKind.Burst);

            ValidationMessage message = Validate(cue).Single();

            AssertMessage(message, ValidationSeverity.Warning, "items", "Sem itens e sem sons. O cue não toca nada.");
        }

        [Test]
        public void Definition_NullItemOrSound_IsError()
        {
            CueDefinition cue = _cues.CreateCue(_cues.CreateTag("Cue.Null"), CueKind.Burst, Vfx(), null);
            _cues.SetSounds(cue, SoundSelection.All, Sound(), null);

            IReadOnlyList<ValidationMessage> messages = Validate(cue);

            Assert.That(messages.Count, Is.EqualTo(2));
            AssertMessage(messages[0], ValidationSeverity.Error, "items.Array.data[1]",
                "Item vazio em items (posição 1). Escolha um tipo ou remova a linha.");
            AssertMessage(messages[1], ValidationSeverity.Error, "sounds.Array.data[1]",
                "Som vazio em sounds (posição 1). Escolha um som ou remova a linha.");
        }

        [Test]
        public void Definition_NegativeMaxSimultaneous_IsError()
        {
            CueDefinition cue = _cues.CreateCue(
                _cues.CreateTag("Cue.Max"), CueKind.Burst, -1, CueLimitPolicy.DropNew, Vfx());

            ValidationMessage message = Validate(cue).Single();

            AssertMessage(message, ValidationSeverity.Error, "maxSimultaneous",
                "Máximo de cópias negativo. Use 0 para sem limite.");
        }

        [Test]
        public void Vfx_NoPrefab_NegativeDuration_AreErrors()
        {
            var noPrefab = new VfxCueItem { Prefab = null };
            VfxCueItem negative = Vfx();
            negative.End = CueEnd.FixedDuration;
            negative.Duration = -1f;
            CueDefinition cue = _cues.CreateCue(_cues.CreateTag("Cue.Vfx"), CueKind.Burst, noPrefab, negative);

            IReadOnlyList<ValidationMessage> messages = Validate(cue);

            Assert.That(messages.Count, Is.EqualTo(2));
            AssertMessage(messages[0], ValidationSeverity.Error, "items.Array.data[0].Prefab",
                "VFX sem prefab. Escolha o prefab criado pelo cue.");
            AssertMessage(messages[1], ValidationSeverity.Error, "items.Array.data[1].Duration",
                "Duração negativa. Use 0 ou mais segundos.");
        }

        [Test]
        public void Vfx_OnRemoveInBurst_IsError()
        {
            VfxCueItem onRemove = Vfx();
            onRemove.End = CueEnd.OnRemove;
            CueDefinition burst = _cues.CreateCue(_cues.CreateTag("Cue.Burst"), CueKind.Burst, onRemove);
            CueDefinition looping = _cues.CreateCue(_cues.CreateTag("Cue.Looping"), CueKind.Looping, onRemove);

            ValidationMessage message = Validate(burst).Single();

            AssertMessage(message, ValidationSeverity.Error, "items.Array.data[0].End",
                "O fim OnRemove num cue Burst nunca chega. Use outro fim ou mude o cue para Looping.");
            Assert.That(Validate(looping), Is.Empty);
        }

        [Test]
        public void Sound_NoClip_IsError()
        {
            CueDefinition cue = _cues.CreateCue(_cues.CreateTag("Cue.NoClip"), CueKind.Burst);
            _cues.SetSounds(cue, SoundSelection.All, new SoundCueItem());

            ValidationMessage message = Validate(cue).Single();

            AssertMessage(message, ValidationSeverity.Error, "sounds.Array.data[0].Clip",
                "Som sem clipe. Escolha o clipe tocado.");
        }

        [Test]
        public void Sound_InvertedRanges_VolumeOutsideZeroOne_AreErrors()
        {
            SoundCueItem inverted = Sound();
            inverted.Volume = new Vector2(0.8f, 0.2f);
            inverted.Pitch = new Vector2(1.2f, 0.9f);
            SoundCueItem loud = Sound();
            loud.Volume = new Vector2(0.5f, 1.5f);
            SoundCueItem negative = Sound();
            negative.Volume = new Vector2(-0.1f, 0.5f);
            CueDefinition cue = _cues.CreateCue(_cues.CreateTag("Cue.Ranges"), CueKind.Burst);
            _cues.SetSounds(cue, SoundSelection.All, inverted, loud, negative);

            IReadOnlyList<ValidationMessage> messages = Validate(cue);

            Assert.That(messages.Count, Is.EqualTo(4));
            AssertMessage(messages[0], ValidationSeverity.Error, "sounds.Array.data[0].Volume",
                "Faixa de volume invertida: o mínimo é maior que o máximo.");
            AssertMessage(messages[1], ValidationSeverity.Error, "sounds.Array.data[0].Pitch",
                "Faixa de altura invertida: o mínimo é maior que o máximo.");
            AssertMessage(messages[2], ValidationSeverity.Error, "sounds.Array.data[1].Volume",
                "Volume fora de 0 a 1.");
            AssertMessage(messages[3], ValidationSeverity.Error, "sounds.Array.data[2].Volume",
                "Volume fora de 0 a 1.");
        }

        [Test]
        public void Sound_LoopInBurst_IsError()
        {
            SoundCueItem loop = Sound();
            loop.Loop = true;
            CueDefinition burst = _cues.CreateCue(_cues.CreateTag("Cue.Burst"), CueKind.Burst);
            _cues.SetSounds(burst, SoundSelection.All, loop);
            CueDefinition looping = _cues.CreateCue(_cues.CreateTag("Cue.Looping"), CueKind.Looping);
            _cues.SetSounds(looping, SoundSelection.All, loop);

            ValidationMessage message = Validate(burst).Single();

            AssertMessage(message, ValidationSeverity.Error, "sounds.Array.data[0].Loop",
                "Loop num cue Burst não repete. Desligue o loop ou mude o cue para Looping.");
            Assert.That(Validate(looping), Is.Empty);
        }

        [Test]
        public void Library_NullDefinition_RepeatedTag_AreErrors()
        {
            GameplayTag hit = _cues.CreateTag("Cue.Hit");
            CueDefinition first = _cues.CreateCue(hit, CueKind.Burst, Vfx());
            CueDefinition repeated = _cues.CreateCue(hit, CueKind.Burst, Vfx());
            CueDefinition noTag = _cues.CreateCue(default, CueKind.Burst, Vfx());
            CueLibrary library = _cues.CreateLibrary(first, null, noTag, repeated, noTag);

            IReadOnlyList<ValidationMessage> messages = Validate(library);

            Assert.That(messages.Count, Is.EqualTo(2));
            AssertMessage(messages[0], ValidationSeverity.Error, "cues.Array.data[1]",
                "Cue vazio em cues (posição 1). Escolha uma definição ou remova a linha.");
            AssertMessage(messages[1], ValidationSeverity.Error, "cues.Array.data[3]",
                "Tag 'Cue.Hit' repetida em cues. Vale a primeira; remova a repetição.");
        }

        [Test]
        public void Router_NoLibrary_NoAnchor_AreWarnings()
        {
            CueRouter router = _cues.CreateRouter(_time, null, null);
            _cues.SetLibraries(router, (CueLibrary)null);

            IReadOnlyList<ValidationMessage> messages = Validate(router);

            Assert.That(messages.Count, Is.EqualTo(2));
            AssertMessage(messages[0], ValidationSeverity.Warning, "libraries",
                "Sem biblioteca. O roteador não acha cue nenhum.");
            AssertMessage(messages[1], ValidationSeverity.Warning, "anchor",
                "Sem âncora. As ações de cue não acham este roteador.");
        }

        [Test]
        public void Router_SoundsWithoutAudioPrefab_IsWarning()
        {
            CueDefinition cue = _cues.CreateCue(_cues.CreateTag("Cue.Sound"), CueKind.Burst);
            _cues.SetSounds(cue, SoundSelection.All, Sound());
            CueRouter router = _cues.CreateRouter(_time, null, _cues.CreateAnchor(), cue);

            ValidationMessage message = Validate(router).Single();

            AssertMessage(message, ValidationSeverity.Warning, "audioPrefab",
                "Sem prefab de áudio. Os sons das bibliotecas não tocam.");
        }

        [Test]
        public void ValidSetup_HasNoMessages()
        {
            CueDefinition vfx = _cues.CreateCue(_cues.CreateTag("Cue.Vfx"), CueKind.Burst, Vfx());
            CueDefinition sound = _cues.CreateCue(_cues.CreateTag("Cue.Sound"), CueKind.Looping);
            SoundCueItem loop = Sound();
            loop.Loop = true;
            _cues.SetSounds(sound, SoundSelection.RandomOne, loop);
            CueRouter router = _cues.CreateRouter(_time, null, _cues.CreateAnchor(), vfx, sound);
            _cues.SetAudioPrefab(router, _cues.CreateAudioPrefab());

            Assert.That(Validate(vfx), Is.Empty);
            Assert.That(Validate(sound), Is.Empty);
            Assert.That(Validate(_cues.CreateLibrary(vfx, sound)), Is.Empty);
            Assert.That(Validate(router), Is.Empty);
        }

        [Test]
        public void ProjectValidator_ReportsCueAssets()
        {
            CueDefinition cue = _cues.CreateCue(default, CueKind.Burst, Vfx());
            CueLibrary library = _cues.CreateLibrary(cue, null);
            CueRouter router = _cues.CreateRouter(_time, null, _cues.CreateAnchor(), cue);
            var messages = new List<ValidationMessage>();

            ProjectValidator.Validate(new Object[] { cue, library, router.gameObject }, messages);

            Assert.That(messages.Count, Is.EqualTo(2));
            Assert.That(messages[0].Context, Is.SameAs(cue));
            Assert.That(messages[0].PropertyPath, Is.EqualTo("tag"));
            Assert.That(messages[1].Context, Is.SameAs(library));
            Assert.That(messages[1].PropertyPath, Is.EqualTo("cues.Array.data[1]"));
        }

        private VfxCueItem Vfx()
        {
            return new VfxCueItem { Prefab = _cues.CreateObject("Vfx_Test") };
        }

        private SoundCueItem Sound()
        {
            return new SoundCueItem { Clip = _cues.CreateClip("Clip_Test", 1f) };
        }

        private static IReadOnlyList<ValidationMessage> Validate(Object target)
        {
            return SpellBookEditor.CollectMessages(new[] { target });
        }

        private static void AssertMessage(
            ValidationMessage message, ValidationSeverity severity, string propertyPath, string text)
        {
            Assert.That(message.Severity, Is.EqualTo(severity));
            Assert.That(message.PropertyPath, Is.EqualTo(propertyPath));
            Assert.That(message.Message, Is.EqualTo(text));
        }
    }
}
