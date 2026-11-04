#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using SpellBook.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Números flutuantes do GAS (R13, R14): dano, cura e bloqueio nascem pelo binder em pool e voltam no fim (CA8); a
    /// regra do asset escolhe o estilo e a troca vale para o número seguinte (CA9); estilo por tag de dano (P2); sem
    /// Bridges nem relay (R10).
    /// </summary>
    public class FloatingTextSourceTests
    {
        private static readonly Vector3 Offset = new Vector3(0f, 2f, 0f);

        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
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
        }

        [Test]
        public void Damage30_Minus30_DamageStyle_Heal20_Plus20_HealStyle_Blocked_Immune()
        {
            AttributeDefinition vida = _gas.CreateAttribute("Vida");
            AbilitySystemComponent asc = _gas.CreateAsc((vida, 100f));
            asc.transform.position = new Vector3(5f, 0f, 1f);
            FloatingTextStyle damage = Style(TextRole.Harmful);
            FloatingTextStyle heal = Style(TextRole.Beneficial);
            FloatingTextStyle blocked = Style(TextRole.Neutral, "Imune");
            Rig rig = CreateRig(asc, Rules(blocked, (vida, ChangeSign.Negative, damage), (vida, ChangeSign.Positive, heal)));
            GameplayTag immune = _gas.CreateTag("Estado.Imune");
            asc.AddTag(immune);
            GameplayEffect immuneEffect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            immuneEffect.ApplicationImmunityTags.Add(immune);

            asc.ApplyAttributeChange(vida, -30f);
            NumberView hit = Newest(rig);
            asc.ApplyAttributeChange(vida, 20f);
            NumberView cure = Newest(rig);
            asc.ApplyEffect(immuneEffect);
            NumberView block = Newest(rig);
            int live = rig.Spawner.Live.Count;
            EndDuration(rig);

            Assert.That(hit.Text, Is.EqualTo("-30"));
            Assert.That(hit.Last.Style, Is.SameAs(damage));
            Assert.That(hit.Last.Role, Is.EqualTo(TextRole.Harmful));
            Assert.That(hit.Last.WorldPosition, Is.EqualTo(asc.transform.position + Offset));
            Assert.That(cure.Text, Is.EqualTo("+20"));
            Assert.That(cure.Last.Style, Is.SameAs(heal));
            Assert.That(cure.Last.Role, Is.EqualTo(TextRole.Beneficial));
            Assert.That(block.Text, Is.EqualTo("Imune"));
            Assert.That(block.Last.Style, Is.SameAs(blocked));
            Assert.That(live, Is.EqualTo(3), "dano, cura e bloqueio deveriam nascer pelo spawner");
            Assert.That(rig.Spawner.Live, Is.Empty, "no fim da duração, os números deveriam voltar ao spawner");

            var instances = new HashSet<GameObject>();
            for (int i = 0; i < 10; i++)
            {
                asc.ApplyAttributeChange(vida, -30f);
                asc.ApplyAttributeChange(vida, 20f);
                asc.ApplyEffect(immuneEffect);
                instances.UnionWith(rig.Spawner.Live);
                Assert.That(rig.Spawner.Live.Count, Is.EqualTo(3), "a contagem de vivos não deveria crescer");
                EndDuration(rig);
            }

            Assert.That(instances.Count, Is.EqualTo(3), "o pool deveria reaproveitar as mesmas instâncias");
            Assert.That(rig.Spawner.ReleaseCount, Is.EqualTo(rig.Spawner.SpawnCount));
        }

        [Test]
        public void RuleAsset_StyleChange_AffectsNext()
        {
            AttributeDefinition vida = _gas.CreateAttribute("Vida");
            AbilitySystemComponent asc = _gas.CreateAsc((vida, 100f));
            FloatingTextStyle damage = Style(TextRole.Harmful);
            FloatingTextStyle other = Style(TextRole.Value);
            FloatingTextRules rules = Rules(null, (vida, ChangeSign.Negative, damage));
            Rig rig = CreateRig(asc, rules);

            asc.ApplyAttributeChange(vida, -10f);
            TextRole before = Newest(rig).Last.Role;
            Modify(damage, so => so.FindProperty("role").intValue = (int)TextRole.Neutral);
            asc.ApplyAttributeChange(vida, -10f);
            TextRole afterStyleEdit = Newest(rig).Last.Role;
            Modify(rules, so => so.FindProperty("rules").GetArrayElementAtIndex(0).FindPropertyRelative("style")
                .objectReferenceValue = other);
            asc.ApplyAttributeChange(vida, -10f);

            Assert.That(before, Is.EqualTo(TextRole.Harmful));
            Assert.That(afterStyleEdit, Is.EqualTo(TextRole.Neutral), "a cor nova do estilo vale para o número seguinte");
            Assert.That(Newest(rig).Last.Style, Is.SameAs(other), "o estilo novo da regra vale para o número seguinte");
            Assert.That(Newest(rig).Last.Role, Is.EqualTo(TextRole.Value));
        }

        [Test]
        public void DamageTagRule_FireStyle()
        {
            AttributeDefinition vida = _gas.CreateAttribute("Vida");
            AbilitySystemComponent asc = _gas.CreateAsc((vida, 100f));
            GameplayTag dano = _gas.CreateTag("Dano");
            GameplayTag fogo = _gas.CreateTag("Dano.Fogo", dano);
            GameplayTag veneno = _gas.CreateTag("Dano.Veneno", dano);
            FloatingTextStyle fire = Style(TextRole.Harmful);
            FloatingTextStyle damage = Style(TextRole.Harmful);
            FloatingTextRules rules = Rules(null, (vida, ChangeSign.Negative, fire), (vida, ChangeSign.Negative, damage));
            rules.Rules[0].DamageTags.Add(fogo);
            Rig rig = CreateRig(asc, rules);

            asc.ApplyAttributeChange(vida, -10f, default, DamageEffect(fogo));
            FloatingTextStyle onFire = Newest(rig).Last.Style;
            asc.ApplyAttributeChange(vida, -10f, default, DamageEffect(veneno));
            FloatingTextStyle onPoison = Newest(rig).Last.Style;
            asc.ApplyAttributeChange(vida, -10f);

            Assert.That(onFire, Is.SameAs(fire), "efeito de fogo: a regra com a tag vence");
            Assert.That(onPoison, Is.SameAs(damage), "veneno não casa com Fogo: vale a regra seguinte");
            Assert.That(Newest(rig).Last.Style, Is.SameAs(damage), "alteração sem efeito não casa com tags preenchidas");
        }

        [Test]
        public void ZeroFinal_FromImmunity_BlockedStyle()
        {
            AttributeDefinition vida = _gas.CreateAttribute("Vida");
            GameplayTag fogo = _gas.CreateTag("Dano.Fogo");
            SetProcessors(vida, new ImmuneToTags { DamageTags = { fogo } });
            AbilitySystemComponent asc = _gas.CreateAsc((vida, 100f));
            FloatingTextStyle blocked = Style(TextRole.Neutral, "Imune");
            Rig rig = CreateRig(asc, Rules(blocked, (vida, ChangeSign.Negative, Style(TextRole.Harmful))));

            asc.ApplyAttributeChange(vida, -30f, default, DamageEffect(fogo));

            Assert.That(rig.Spawner.SpawnCount, Is.EqualTo(1));
            Assert.That(Newest(rig).Text, Is.EqualTo("Imune"));
            Assert.That(Newest(rig).Last.Style, Is.SameAs(blocked));
            Assert.That(asc.GetAttributeBaseValue(vida), Is.EqualTo(100f));
        }

        [Test]
        public void ReportBlocked_BlockedStyle()
        {
            AttributeDefinition vida = _gas.CreateAttribute("Vida");
            AbilitySystemComponent asc = _gas.CreateAsc((vida, 100f));
            FloatingTextStyle blocked = Style(TextRole.Neutral, "Bloqueado");
            Rig rig = CreateRig(asc, Rules(blocked));

            asc.ReportBlocked(_gas.CreateTag("Bloqueio.Escudo"));

            Assert.That(rig.Spawner.SpawnCount, Is.EqualTo(1));
            Assert.That(Newest(rig).Text, Is.EqualTo("Bloqueado"));
            Assert.That(Newest(rig).Last.Style, Is.SameAs(blocked));
        }

        [Test]
        public void NoRule_NoNumber()
        {
            AttributeDefinition vida = _gas.CreateAttribute("Vida");
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            GameplayTag fogo = _gas.CreateTag("Dano.Fogo");
            SetProcessors(mana, new ImmuneToTags { DamageTags = { fogo } });
            AbilitySystemComponent asc = _gas.CreateAsc((vida, 0f), (mana, 50f));
            Rig rig = CreateRig(asc, Rules(Style(TextRole.Neutral, "Imune"), (vida, ChangeSign.Negative, Style(TextRole.Harmful))));

            asc.ApplyAttributeChange(mana, -10f);
            asc.ApplyAttributeChange(mana, -10f, default, DamageEffect(fogo));
            asc.ApplyAttributeChange(vida, 15f);
            asc.ApplyAttributeChange(vida, -15f);
            asc.ApplyAttributeChange(vida, -15f);

            Assert.That(rig.Spawner.SpawnCount, Is.EqualTo(1), "só a queda real da vida tem regra; no mínimo, nada muda");
        }

        [Test]
        public void NoBridgesNoRelay()
        {
            string folder = Path.Combine(PackageInfo.FindForAssembly(typeof(FloatingTextSource).Assembly).resolvedPath, "Runtime", "GAS.UI");
            string[] files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
            string[] forbidden = { "SpellBook.GAS.Bridges", "EventRelay", "EventChannel" };

            List<string> violations = files
                .SelectMany(f => forbidden.Where(File.ReadAllText(f).Contains).Select(w => $"{Path.GetFileName(f)}: {w}"))
                .ToList();

            Assert.That(files.Select(Path.GetFileName), Does.Contain("FloatingTextSource.cs"));
            Assert.That(violations, Is.Empty, "as fontes do GAS.UI leem o ASC direto, sem Bridges nem relay (R10)");
        }

        private static NumberView Newest(Rig rig)
        {
            Assert.That(rig.Spawner.Live, Is.Not.Empty, "nenhum número vivo");
            return rig.Spawner.Live[rig.Spawner.Live.Count - 1].GetComponent<NumberView>();
        }

        private static void EndDuration(Rig rig)
        {
            rig.Driver.Tick();
            rig.Time.Advance(FloatingTextStyle.DefaultDuration);
            rig.Driver.Tick();
        }

        private GameplayEffect DamageEffect(GameplayTag tag)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.DamageTags.Add(tag);
            return effect;
        }

        private FloatingTextStyle Style(TextRole role, string fixedText = null)
        {
            FloatingTextStyle style = Track(ScriptableObject.CreateInstance<FloatingTextStyle>());
            Modify(style, so =>
            {
                so.FindProperty("role").intValue = (int)role;
                so.FindProperty("format.text").stringValue = fixedText;
            });
            return style;
        }

        private FloatingTextRules Rules(
            FloatingTextStyle blocked, params (AttributeDefinition Attribute, ChangeSign Sign, FloatingTextStyle Style)[] rules)
        {
            FloatingTextRules asset = Track(ScriptableObject.CreateInstance<FloatingTextRules>());
            Modify(asset, so =>
            {
                so.FindProperty("blocked").objectReferenceValue = blocked;
                SerializedProperty list = so.FindProperty("rules");
                list.arraySize = rules.Length;
                for (int i = 0; i < rules.Length; i++)
                {
                    SerializedProperty rule = list.GetArrayElementAtIndex(i);
                    rule.FindPropertyRelative("attribute").objectReferenceValue = rules[i].Attribute;
                    rule.FindPropertyRelative("sign").intValue = (int)rules[i].Sign;
                    rule.FindPropertyRelative("style").objectReferenceValue = rules[i].Style;
                }
            });
            return asset;
        }

        private Rig CreateRig(AbilitySystemComponent asc, FloatingTextRules rules)
        {
            var rig = new Rig
            {
                Time = Track(ScriptableObject.CreateInstance<ManualTimeSource>()),
                Spawner = Track(ScriptableObject.CreateInstance<RecordingSpawnerAsset>()),
                Driver = Track(new GameObject("tela")).AddComponent<UIUpdateDriver>(),
            };
            rig.Driver.SetTimeSource(rig.Time);

            GameObject prefab = Track(new GameObject("numero"));
            prefab.SetActive(false);
            prefab.AddComponent<NumberView>();

            GameObject host = Track(new GameObject("numeros"));
            host.transform.SetParent(rig.Driver.transform);
            host.SetActive(false);
            FloatingTextBinder binder = host.AddComponent<FloatingTextBinder>();
            binder.SetOwner(ActorResolver.ForExplicit(asc));
            binder.SetSource(new FloatingTextSource { Rules = rules, Offset = Offset });
            Modify(binder, so =>
            {
                so.FindProperty("prefab").objectReferenceValue = prefab;
                so.FindProperty("spawner").objectReferenceValue = rig.Spawner;
            });
            host.SetActive(true);
            return rig;
        }

        private static void SetProcessors(AttributeDefinition definition, params AttributeChangeProcessor[] processors)
        {
            Modify(definition, so =>
            {
                SerializedProperty list = so.FindProperty("processors");
                list.arraySize = processors.Length;
                for (int i = 0; i < processors.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).managedReferenceValue = processors[i];
                }
            });
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        private static void Modify(Object target, Action<SerializedObject> edit)
        {
            using (var serialized = new SerializedObject(target))
            {
                edit(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private sealed class Rig
        {
            public ManualTimeSource Time;
            public UIUpdateDriver Driver;
            public RecordingSpawnerAsset Spawner;
        }

        /// <summary>View de número de teste: copia o texto do pedido, que vale só durante o <c>Show</c>.</summary>
        public sealed class NumberView : MonoBehaviour, IFloatingTextView
        {
            public FloatingTextRequest Last;
            public string Text;

            public void Show(in FloatingTextRequest request)
            {
                Last = request;
                Text = request.Text.Buffer?.ToString(request.Text);
            }

            public void SetProgress(float normalized)
            {
            }
        }
    }
}
#endif
