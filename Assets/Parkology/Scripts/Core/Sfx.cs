using System.Collections.Generic;
using UnityEngine;

namespace Parkology
{
    /// <summary>Sound effects (pooled voices, multiple takes per cue) and the looping music bed.</summary>
    public sealed class Sfx : MonoBehaviour
    {
        static Sfx instance;
        static readonly string[] cues =
        {
            "tap", "snap", "hop", "drop", "go", "blocked", "complete", "horn", "reveal", "hammer",
            "slot", "magnet", "coin", "whoosh", "toot", "chug", "win", "fail", "warning"
        };

        readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<string, int> lastTake = new Dictionary<string, int>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        AudioSource[] voices;
        AudioSource music;
        int next;
        float musicTarget;
        const float MusicVolume = 0.32f;

        public static void Create(Transform parent)
        {
            if (instance) return;
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            instance = go.AddComponent<Sfx>();
            instance.voices = new AudioSource[14];
            for (int i = 0; i < instance.voices.Length; i++)
            {
                var v = go.AddComponent<AudioSource>();
                v.playOnAwake = false;
                v.spatialBlend = 0f;
                instance.voices[i] = v;
            }
            instance.music = go.AddComponent<AudioSource>();
            instance.music.loop = true;
            instance.music.playOnAwake = false;
            instance.music.volume = 0f;
        }

        /// <summary>Loads one cue per call so the splash can report real progress.</summary>
        public static IEnumerable<float> LoadAll()
        {
            for (int i = 0; i < cues.Length; i++)
            {
                var takes = new List<AudioClip>();
                for (int k = 1; ; k++)
                {
                    var clip = Resources.Load<AudioClip>("Audio/" + (k == 1 ? cues[i] : cues[i] + "-" + k));
                    if (clip == null && k == 1) clip = Resources.Load<AudioClip>("Audio/" + cues[i] + "-1");
                    if (clip == null) break;
                    clip.LoadAudioData();
                    takes.Add(clip);
                }
                instance.clips[cues[i]] = takes.ToArray();
                yield return (i + 1f) / (cues.Length + 1f);
            }
            instance.music.clip = Resources.Load<AudioClip>("Audio/music");
            yield return 1f;
        }

        /// <summary>Plays a cue. For "snap", take selects the stack position (1..3) so the pitch climbs.</summary>
        public static void Play(string cue, float volume = 1f, float pitch = 1f, int take = 0, float minGap = 0.03f)
        {
            if (instance == null || !Save.Sound) return;
            if (!instance.clips.TryGetValue(cue, out var takes) || takes.Length == 0) return;
            float now = Time.unscaledTime;
            if (instance.lastPlayed.TryGetValue(cue, out float last) && now - last < minGap) return;
            instance.lastPlayed[cue] = now;
            AudioClip clip;
            if (take > 0) clip = takes[Mathf.Clamp(take - 1, 0, takes.Length - 1)];
            else if (takes.Length == 1) clip = takes[0];
            else
            {
                instance.lastTake.TryGetValue(cue, out int prev);
                int pick = Random.Range(0, takes.Length - 1);
                if (pick >= prev) pick++;
                instance.lastTake[cue] = pick;
                clip = takes[pick];
            }
            var v = instance.voices[instance.next++ % instance.voices.Length];
            v.pitch = pitch * Random.Range(0.97f, 1.03f);
            v.PlayOneShot(clip, volume);
        }

        public static void ApplyMusic()
        {
            if (instance == null || instance.music.clip == null) return;
            instance.musicTarget = Save.Music ? MusicVolume : 0f;
            if (Save.Music && !instance.music.isPlaying) instance.music.Play();
        }

        /// <summary>Ducks the music briefly under a jingle.</summary>
        public static void Duck(float seconds)
        {
            if (instance == null) return;
            instance.duckUntil = Time.unscaledTime + seconds;
        }

        float duckUntil;

        void Update()
        {
            if (music == null || music.clip == null) return;
            float target = Time.unscaledTime < duckUntil ? musicTarget * 0.3f : musicTarget;
            music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime * 0.6f);
            if (music.volume <= 0.001f && musicTarget <= 0f && music.isPlaying) music.Pause();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) ApplyMusic();
        }
    }
}
