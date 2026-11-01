using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Tags.Tests
{
    /// <summary>Cria definições de tag em memória e as destrói no <see cref="Dispose"/>.</summary>
    internal sealed class TagTestFactory : IDisposable
    {
        private readonly List<Object> _created = new List<Object>();

        public GameplayTagDefinition Create(string name, GameplayTagDefinition parent = null)
        {
            var definition = ScriptableObject.CreateInstance<GameplayTagDefinition>();
            definition.name = name;
            _created.Add(definition);

            if (parent != null)
            {
                using (var serialized = new SerializedObject(definition))
                {
                    serialized.FindProperty("parent").objectReferenceValue = parent;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            return definition;
        }

        public void Dispose()
        {
            foreach (Object obj in _created)
            {
                Object.DestroyImmediate(obj);
            }

            _created.Clear();
        }
    }
}
