using System;
using System.Collections.Generic;
using SpellBook.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Testing
{
    /// <summary>
    /// Cria spawners e prefabs para testes sem nenhum asset em disco. O "prefab" é um objeto da cena usado como modelo.
    /// Tudo o que a factory cria, ou recebe pelo <see cref="Track{T}"/>, é destruído no <see cref="Dispose"/>.
    /// </summary>
    public sealed class SpawnFactory : IDisposable
    {
        private readonly List<Object> _created = new List<Object>();

        public InstantiateSpawnerAsset CreateInstantiateSpawner()
        {
            return Track(ScriptableObject.CreateInstance<InstantiateSpawnerAsset>(), "Spawner_Test");
        }

        public GameObject CreatePrefab(string name = "Prefab_Test")
        {
            return Track(new GameObject(), name);
        }

        /// <summary>Prefab com um <see cref="ParticleSystem"/> que não toca sozinho e não repete.</summary>
        public GameObject CreateParticlePrefab(string name = "ParticlePrefab_Test")
        {
            GameObject prefab = CreatePrefab(name);
            var particles = prefab.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            return prefab;
        }

        /// <summary>Registra um objeto criado pelo teste (por exemplo, uma instância de <c>Spawn</c>) para o <see cref="Dispose"/>.</summary>
        public T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        public void Dispose()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] == null) continue;
                if (Application.isPlaying) Object.Destroy(_created[i]);
                else Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        private T Track<T>(T obj, string name) where T : Object
        {
            obj.name = name;
            return Track(obj);
        }
    }
}
