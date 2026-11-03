using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Tags;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.Cues.Tests
{
    /// <summary>Índice por tag (CA3, C3), parâmetros e handle de cue.</summary>
    public class CueIndexTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _created)
            {
                Object.DestroyImmediate(obj);
            }

            _created.Clear();
        }

        [Test]
        public void Index_MostSpecificMatch_FallsBackToParent_NoneFalse()
        {
            GameplayTagDefinition hit = CreateTag("Cue.Hit");
            GameplayTagDefinition fire = CreateTag("Cue.Hit.Fire", hit);
            GameplayTagDefinition fireBig = CreateTag("Cue.Hit.Fire.Big", fire);
            GameplayTagDefinition ice = CreateTag("Cue.Hit.Ice", hit);
            GameplayTagDefinition other = CreateTag("Cue.Other");
            CueDefinition hitCue = CreateCue(hit);
            CueDefinition fireCue = CreateCue(fire);
            var index = new CueIndex();
            index.Rebuild(new[] { CreateLibrary(hitCue), CreateLibrary(fireCue) });

            bool exact = index.TryFind(new GameplayTag(fire), out CueDefinition exactCue);
            bool child = index.TryFind(new GameplayTag(fireBig), out CueDefinition childCue);
            bool sibling = index.TryFind(new GameplayTag(ice), out CueDefinition siblingCue);
            bool none = index.TryFind(new GameplayTag(other), out CueDefinition noneCue);
            bool invalid = index.TryFind(default, out CueDefinition invalidCue);

            Assert.That(exact, Is.True);
            Assert.That(exactCue, Is.SameAs(fireCue));
            Assert.That(child, Is.True);
            Assert.That(childCue, Is.SameAs(fireCue));
            Assert.That(sibling, Is.True);
            Assert.That(siblingCue, Is.SameAs(hitCue));
            Assert.That(none, Is.False);
            Assert.That(noneCue, Is.Null);
            Assert.That(invalid, Is.False);
            Assert.That(invalidCue, Is.Null);
        }

        [Test]
        public void Index_Lookup_DoesNotAllocate()
        {
            GameplayTagDefinition hit = CreateTag("Cue.Hit");
            GameplayTagDefinition fire = CreateTag("Cue.Hit.Fire", hit);
            GameplayTagDefinition fireBig = CreateTag("Cue.Hit.Fire.Big", fire);
            var index = new CueIndex();
            index.Rebuild(new[] { CreateLibrary(CreateCue(hit)) });
            var tag = new GameplayTag(fireBig);
            index.TryFind(tag, out _);

            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    index.TryFind(tag, out _);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void CueParameters_DefaultsAndWith()
        {
            var target = new GameObject("Target");
            var instigator = new GameObject("Instigator");
            _created.Add(target);
            _created.Add(instigator);
            target.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
            Quaternion rotation = Quaternion.Euler(10f, 0f, 0f);

            CueParameters at = CueParameters.At(new Vector3(4f, 5f, 6f));
            CueParameters on = CueParameters.On(target.transform);
            CueParameters full = at
                .WithRotation(rotation)
                .WithNormal(Vector3.up)
                .WithTarget(target.transform)
                .WithInstigator(instigator.transform)
                .WithMagnitude(30f, 0.25f)
                .WithUserFloat(1.5f)
                .WithUserInt(7);

            Assert.That(at.Position, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            Assert.That(at.Rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(at.HasNormal, Is.False);
            Assert.That(at.HasMagnitude, Is.False);
            Assert.That(at.Target, Is.Null);
            Assert.That(at.Instigator, Is.Null);
            Assert.That(on.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(on.Rotation, Is.EqualTo(target.transform.rotation));
            Assert.That(on.Target, Is.SameAs(target.transform));
            Assert.That(full.Position, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            Assert.That(full.Rotation, Is.EqualTo(rotation));
            Assert.That(full.HasNormal, Is.True);
            Assert.That(full.Normal, Is.EqualTo(Vector3.up));
            Assert.That(full.Target, Is.SameAs(target.transform));
            Assert.That(full.Instigator, Is.SameAs(instigator.transform));
            Assert.That(full.HasMagnitude, Is.True);
            Assert.That(full.Magnitude, Is.EqualTo(30f));
            Assert.That(full.NormalizedMagnitude, Is.EqualTo(0.25f));
            Assert.That(full.UserFloat, Is.EqualTo(1.5f));
            Assert.That(full.UserInt, Is.EqualTo(7));
            Assert.That(at.HasMagnitude, Is.False, "With… não muda o original");
        }

        [Test]
        public void Handle_DefaultIsInvalid()
        {
            CueHandle none = default;
            var live = new CueHandle(0, 1);
            var stale = new CueHandle(0, 2);

            Assert.That(none.IsValid, Is.False);
            Assert.That(live.IsValid, Is.True);
            Assert.That(live, Is.Not.EqualTo(stale), "slot reutilizado com outra geração é outro handle");
            Assert.That(live, Is.EqualTo(new CueHandle(0, 1)));
        }

        private GameplayTagDefinition CreateTag(string name, GameplayTagDefinition parent = null)
        {
            var definition = ScriptableObject.CreateInstance<GameplayTagDefinition>();
            definition.name = name;
            _created.Add(definition);
            if (parent != null)
            {
                Modify(definition, so => so.FindProperty("parent").objectReferenceValue = parent);
            }

            return definition;
        }

        private CueDefinition CreateCue(GameplayTagDefinition tag)
        {
            var cue = ScriptableObject.CreateInstance<CueDefinition>();
            cue.name = tag.name;
            _created.Add(cue);
            Modify(cue, so => so.FindProperty("tag.definition").objectReferenceValue = tag);
            return cue;
        }

        private CueLibrary CreateLibrary(params CueDefinition[] cues)
        {
            var library = ScriptableObject.CreateInstance<CueLibrary>();
            _created.Add(library);
            Modify(library, so =>
            {
                SerializedProperty list = so.FindProperty("cues");
                list.arraySize = cues.Length;
                for (int i = 0; i < cues.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = cues[i];
                }
            });
            return library;
        }

        private static void Modify(Object target, System.Action<SerializedObject> edit)
        {
            using (var serialized = new SerializedObject(target))
            {
                edit(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
