using UnityEngine;

namespace WesternLemegeton
{
    // Music has its own looping channel; all channels still obey the existing master volume.
    public sealed class BossTrialAudio : MonoBehaviour
    {
        internal enum Cue { Charge, Cut, Miracle, Ground, Armor, Wave }
        [Range(0,1)] public float MusicVolume = .16f;
        [Range(0,1)] public float EffectVolume = .23f;
        AudioSource music, intro, effects;
        readonly AudioClip[] cues = new AudioClip[6];

        void Awake()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            intro = gameObject.AddComponent<AudioSource>();
            intro.playOnAwake = false;
            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            for (int i=0; i<cues.Length; i++)
                cues[i] = Resources.Load<AudioClip>("Paladin/Audio/" + (Cue)i);
        }
        public void StartMusic(AudioClip clip = null, bool judicator = false)
        {
            StopMusic();
            intro.clip = null;
            music.volume = intro.volume = MusicVolume;
            if (judicator && !clip)
            {
                intro.clip = Resources.Load<AudioClip>("Paladin/Audio/ReversalIntro");
                music.clip = Resources.Load<AudioClip>("Paladin/Audio/ReversalLoop");
                double start = AudioSettings.dspTime + .12;
                intro.PlayScheduled(start);
                music.PlayScheduled(start + (double)intro.clip.samples / intro.clip.frequency);
                return;
            }
            music.clip = clip ? clip : Resources.Load<AudioClip>("Paladin/Audio/SanctuaryHymn");
            music.Play();
        }
        internal void SetMusicFade(float amount) => intro.volume = music.volume = MusicVolume * Mathf.Clamp01(amount);
        public void StopMusic() { intro.Stop(); music.Stop(); effects.Stop(); }
        internal void Play(Cue cue) => effects.PlayOneShot(cues[(int)cue],EffectVolume);
    }
}

