using System;
using System.Collections.Generic;
using SpellBook.Time;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Testing
{
    /// <summary>
    /// Cria fontes de tempo para testes sem nenhum asset em disco. Tudo o que a factory cria é destruído no
    /// <see cref="Dispose"/>, o que também tira as fontes do registro de reset de sessão.
    /// </summary>
    public sealed class TimeFactory : IDisposable
    {
        private readonly List<Object> _created = new List<Object>();

        /// <summary>Fonte parada em 0 que só avança por <see cref="ManualTimeSource.Advance"/>.</summary>
        public ManualTimeSource CreateManual()
        {
            return Track(ScriptableObject.CreateInstance<ManualTimeSource>(), "ManualTime_Test");
        }

        /// <summary>Grupo pausável, começando retomado.</summary>
        public PausableTimeSource CreatePausable()
        {
            return Track(ScriptableObject.CreateInstance<PausableTimeSource>(), "PausableTime_Test");
        }

        /// <summary>Fonte não escalada da Unity.</summary>
        public UnscaledTimeSource CreateUnscaled()
        {
            return Track(ScriptableObject.CreateInstance<UnscaledTimeSource>(), "UnscaledTime_Test");
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

        private T Track<T>(T asset, string name) where T : Object
        {
            asset.name = name;
            _created.Add(asset);
            return asset;
        }
    }
}
