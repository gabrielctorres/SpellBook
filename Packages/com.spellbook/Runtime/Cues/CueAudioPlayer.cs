using SpellBook.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace SpellBook.Cues
{
    /// <summary>
    /// Toca o som de um <see cref="SoundCueItem"/> no <see cref="AudioSource"/> do objeto. Vai na raiz do prefab de
    /// áudio do <see cref="CueRouter"/>; o roteador devolve o objeto ao spawner quando o clipe acaba.
    /// </summary>
    [HelpURL(DocUrls.Cues + "#cueaudioplayer")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class CueAudioPlayer : MonoBehaviour
    {
        private AudioSource _source;

        /// <summary>A fonte de áudio do objeto.</summary>
        public AudioSource Source
        {
            get
            {
                // Em EditMode a Unity não chama o Awake: a primeira leitura guarda a fonte
                if (_source == null) _source = GetComponent<AudioSource>();
                return _source;
            }
        }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
        }

        /// <summary>Configura a fonte e toca o <paramref name="clip"/> do início.</summary>
        public void Play(AudioClip clip, float volume, float pitch, float spatialBlend, AudioMixerGroup mixer, bool loop)
        {
            AudioSource source = Source;
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = spatialBlend;
            source.outputAudioMixerGroup = mixer;
            source.loop = loop;
            source.Play();
        }
    }
}
