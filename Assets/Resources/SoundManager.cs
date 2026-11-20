using Assets.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Assets.Resources
{
    public class SoundManager : MonoBehaviour
    {
        private static SoundManager _instance;
        public List<SoundByte> SoundBytes = new();
        [SerializeField]
        public Dictionary<string, SoundByte> SoundClipLookup = new();
        public int AmountOfAudioSources = 60;

        public static SoundManager instance
        {
            get
            {
                if (!_instance)
                    _instance = Instantiate(UnityEngine.Resources.Load<SoundManager>(nameof(SoundManager)));
                return _instance;
            }
        }

        private AudioSource[] audioss;
        private int audiossIndex;

        public Transform player
        {
            get
            {
                if (_player == null)
                {
                    _player = Camera.main.transform.root;
                }

                return _player;
            }
        }

        private Transform _player;

        // Start is called before the first frame update
        void Awake()
        {
            transform.SetAmountOfChildren(AmountOfAudioSources, true);
            audioss = GetComponentsInChildren<AudioSource>();
            SoundClipLookup = SoundBytes.ToDictionary(x => x.name);
        }

#if UNITY_EDITOR
        public bool Run = false;
        public void OnValidate()
        {
            if (Run)
            {
                Run = false;
                // Search for all prefabs within the "Assets/Prefabs" directory
                string[] guids = AssetDatabase.FindAssets("t:SoundByte glob:\"Assets/Resources/Sounds/SoundBytes/*.asset\"");

                Debug.Log($"Found {guids.Length} prefabs in Assets/Prefabs folder.");
                SoundBytes.Clear();
                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var itemData = AssetDatabase.LoadAssetAtPath(assetPath, typeof(SoundByte)) as SoundByte;
                    SoundBytes.Add(itemData);
                }
            }
        }
#endif

        public SoundByte GetSoundByte(string name)
        {
            if (!SoundClipLookup.TryGetValue(name, out var soundClip))
            {
                Debug.LogError($"Couldn't find SoundClip {name}");
                return null;
            }
            return soundClip;
        }

        private AudioSource GetNextAudioSource()
        {
            var index = audiossIndex % audioss.Length;
            var audioSource = audioss[index];
            audiossIndex++;
            return audioSource;
        }

        public void PlaySoundClipAtLocation(Vector3 position, string name, float pitchOverride = 0, AudioSource audios = null)
        {
            if(audios == null)
                audios = GetNextAudioSource();
            audios.transform.position = position;
            PlaySoundClip(name, audios, pitchOverride);
        }

        public void PlaySoundClip(string name, AudioSource audios = null, float pitchOverride = 0)
        {
            if(audios == null)
            {
                audios = GetNextAudioSource();
            }

            var instanceID = audios.GetEntityId();
            var soundClip = GetSoundByte(name);
            if (audios.isPlaying && audios.priority < soundClip.Priority) return;
            
            var pitch = (pitchOverride > 0 ? pitchOverride :  soundClip.PitchData.Pitch) * (1 + UnityEngine.Random.value * soundClip.PitchData.PitchDeviation - soundClip.PitchData.PitchDeviation * 0.5f);
            audios.pitch = pitch;
            audios.priority = soundClip.Priority;
            audios.volume = soundClip.Volume;
            var clip = SoundBytes.Count == 1 ? soundClip.clips.First() : soundClip.clips.UnseededRandomElement();
            audios.clip = clip;
            audios.Play();
        }

        public AudioClip GetAudioClip(string name)
        {
            var soundClip = GetSoundByte(name);
            return SoundBytes.Count == 1 ? soundClip.clips.First() : soundClip.clips.UnseededRandomElement();
        }
    }
}

