#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using SpellBook.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Testing
{
    /// <summary>
    /// Cria canais de evento para testes sem nenhum asset em disco. Tudo o que a factory cria é destruído no
    /// <see cref="Dispose"/>, o que também tira os canais da limpeza de sessão. Só compila no Editor (SerializedObject).
    /// </summary>
    public sealed class EventsFactory : IDisposable
    {
        private readonly List<Object> _created = new List<Object>();

        /// <summary>Canal do tipo dado. Com <paramref name="logRaises"/>, cada disparo vai para o console.</summary>
        public TChannel CreateChannel<TChannel>(bool logRaises = false) where TChannel : EventChannel
        {
            var channel = ScriptableObject.CreateInstance<TChannel>();
            channel.name = typeof(TChannel).Name + "_Test";
            _created.Add(channel);

            var serialized = new SerializedObject(channel);
            serialized.FindProperty("logRaises").boolValue = logRaises;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return channel;
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
    }
}
#endif
