using UnityEngine;
using W = TapOrDrag.Synth.Wave;

namespace TapOrDrag
{
    public class AudioManager : MonoBehaviour
    {
        const string MuteKey = "TapOrDrag.Muted";
        const float MusicVolume = 0.45f;

        AudioSource music;
        AudioSource[] voices;
        int nextVoice;
        float musicTarget = MusicVolume;
        AudioClip flap, pass, combo, dash, gateBreak, stomp, shieldPop, skillReady, feverStart, feverEnd, perfect, closeCall, smash,
            coin, switchFlip, biome, mission, purchase, portalUp, portalDown, switchToggle, iceCrack, iceShatter, meteorWarn, meteorLaunch, shoot, enemyHit, explode, bigExplode, powerUp, bossAlarm, miss, hit, zap, fall, thud, gameOver, newBest, click, start;

        public bool Muted { get; private set; }

        public void Build()
        {
            flap = Synth.Clip("Flap", Synth.Tone(0.09f, 380f, 820f, W.Square, 0.2f, decay: 1.5f, curve: 0.6f));
            pass = Synth.Clip("Pass", Synth.Seq(
                (0f, Synth.Tone(0.06f, 988f, 988f, W.Square, 0.18f)),
                (0.06f, Synth.Tone(0.18f, 1319f, 1319f, W.Square, 0.18f, decay: 1.2f))));
            combo = Synth.Clip("Combo", Synth.Arp(0.055f, new[] { 1047f, 1319f, 1568f, 2093f }, W.Square, 0.14f, 0.25f));
            dash = Synth.Clip("Dash", Synth.Seq(
                (0f, Synth.Tone(0.3f, 2500f, 400f, W.Noise, 0.28f, decay: 0.9f, curve: 0.7f)),
                (0f, Synth.Tone(0.2f, 250f, 1000f, W.Square, 0.1f, 0.125f, curve: 0.5f))));
            gateBreak = Synth.Clip("GateBreak", Synth.Seq(
                (0f, Synth.Tone(0.28f, 2400f, 160f, W.Square, 0.13f, curve: 0.4f, vibratoHz: 40f, vibratoDepth: 0.1f)),
                (0f, Synth.Tone(0.32f, 7000f, 1500f, W.Noise, 0.2f)),
                (0.04f, Synth.Arp(0.05f, new[] { 1568f, 2093f, 2637f }, W.Square, 0.08f, 0.25f))));
            stomp = Synth.Clip("Stomp", Synth.Seq(
                (0f, Synth.Tone(0.09f, 180f, 620f, W.Square, 0.26f, curve: 0.5f)),
                (0f, Synth.Tone(0.1f, 2500f, 600f, W.Noise, 0.2f)),
                (0.07f, Synth.Tone(0.07f, 988f, 988f, W.Square, 0.15f, 0.25f)),
                (0.13f, Synth.Tone(0.28f, 1976f, 1976f, W.Square, 0.14f, 0.25f, 1.2f))));
            shieldPop = Synth.Clip("ShieldPop", Synth.Seq(
                (0f, Synth.Tone(0.16f, 4000f, 800f, W.Noise, 0.3f, decay: 1.2f)),
                (0f, Synth.Tone(0.22f, 500f, 1500f, W.Square, 0.14f, 0.25f, curve: 0.5f, vibratoHz: 30f, vibratoDepth: 0.05f))));
            feverStart = Synth.Clip("FeverStart", Synth.Seq(
                (0f, Synth.Arp(0.045f, new[] { 523f, 659f, 784f, 1047f, 1319f, 1568f, 2093f }, W.Square, 0.14f, 0.25f)),
                (0f, Synth.Tone(0.35f, 800f, 6000f, W.Noise, 0.12f, decay: 0.5f))));
            feverEnd = Synth.Clip("FeverEnd", Synth.Tone(0.35f, 1200f, 300f, W.Square, 0.12f, 0.25f, 0.8f));
            perfect = Synth.Clip("Perfect", Synth.Seq(
                (0f, Synth.Tone(0.05f, 2093f, 2093f, W.Square, 0.14f, 0.25f)),
                (0.05f, Synth.Tone(0.3f, 3136f, 3136f, W.Sine, 0.22f, decay: 1.5f))));
            closeCall = Synth.Clip("Close", Synth.Seq(
                (0f, Synth.Tone(0.05f, 1760f, 1760f, W.Square, 0.1f, 0.125f)),
                (0.05f, Synth.Tone(0.08f, 2349f, 2349f, W.Square, 0.1f, 0.125f))));
            smash = Synth.Clip("Smash", Synth.Seq(
                (0f, Synth.Tone(0.14f, 3000f, 500f, W.Noise, 0.32f)),
                (0f, Synth.Tone(0.12f, 220f, 70f, W.Square, 0.2f))));
            coin = Synth.Clip("Coin", Synth.Seq(
                (0f, Synth.Tone(0.035f, 1568f, 1568f, W.Square, 0.1f, 0.25f)),
                (0.035f, Synth.Tone(0.1f, 2093f, 2093f, W.Square, 0.1f, 0.25f, 1.2f))));
            switchFlip = Synth.Clip("SwitchFlip", Synth.Tone(0.14f, 400f, 1300f, W.Square, 0.13f, 0.125f, vibratoHz: 60f, vibratoDepth: 0.2f));
            biome = Synth.Clip("Biome", Synth.Seq(
                (0f, Synth.Tone(0.7f, 300f, 4000f, W.Noise, 0.1f, decay: 0.6f, curve: 0.6f)),
                (0.1f, Synth.Arp(0.09f, new[] { 392f, 523f, 659f, 784f }, W.Triangle, 0.2f, decay: 0.8f))));
            shoot = Synth.Clip("Shoot", Synth.Tone(0.05f, 1400f, 700f, W.Square, 0.06f, 0.25f, 1.5f));
            enemyHit = Synth.Clip("EnemyHit", Synth.Tone(0.03f, 2500f, 1800f, W.Noise, 0.12f));
            explode = Synth.Clip("Explode", Synth.Seq(
                (0f, Synth.Tone(0.25f, 3000f, 300f, W.Noise, 0.3f, decay: 1.3f)),
                (0f, Synth.Tone(0.2f, 220f, 60f, W.Square, 0.15f))));
            bigExplode = Synth.Clip("BigExplode", Synth.Seq(
                (0f, Synth.Tone(0.8f, 2500f, 150f, W.Noise, 0.45f, decay: 0.9f)),
                (0f, Synth.Tone(0.7f, 160f, 35f, W.Sine, 0.5f, decay: 0.8f)),
                (0.15f, Synth.Tone(0.5f, 1800f, 200f, W.Noise, 0.3f))));
            powerUp = Synth.Clip("PowerUp", Synth.Arp(0.05f, new[] { 523f, 784f, 1047f, 1568f, 2093f }, W.Square, 0.13f, 0.25f));
            bossAlarm = Synth.Clip("BossAlarm", Synth.Seq(
                (0f, Synth.Tone(0.35f, 440f, 880f, W.Square, 0.14f, 0.5f, 0.3f)),
                (0.4f, Synth.Tone(0.35f, 440f, 880f, W.Square, 0.14f, 0.5f, 0.3f)),
                (0.8f, Synth.Tone(0.35f, 440f, 880f, W.Square, 0.14f, 0.5f, 0.3f))));
            iceCrack = Synth.Clip("IceCrack", Synth.Tone(0.25f, 5000f, 2500f, W.Noise, 0.12f, decay: 0.4f, vibratoHz: 40f, vibratoDepth: 0.3f));
            iceShatter = Synth.Clip("IceShatter", Synth.Seq(
                (0f, Synth.Tone(0.22f, 7000f, 2000f, W.Noise, 0.3f, decay: 1.3f)),
                (0f, Synth.Arp(0.035f, new[] { 3136f, 2637f, 3520f, 2349f }, W.Sine, 0.12f))));
            meteorWarn = Synth.Clip("MeteorWarn", Synth.Seq(
                (0f, Synth.Tone(0.08f, 1760f, 1760f, W.Square, 0.12f)),
                (0.16f, Synth.Tone(0.08f, 1760f, 1760f, W.Square, 0.12f))));
            meteorLaunch = Synth.Clip("MeteorLaunch", Synth.Seq(
                (0f, Synth.Tone(0.5f, 1500f, 200f, W.Noise, 0.25f, decay: 0.8f)),
                (0f, Synth.Tone(0.4f, 300f, 60f, W.Sine, 0.3f))));
            switchToggle = Synth.Clip("SwitchToggle", Synth.Seq(
                (0f, Synth.Tone(0.05f, 660f, 660f, W.Square, 0.14f, 0.5f)),
                (0.05f, Synth.Tone(0.09f, 990f, 990f, W.Square, 0.14f, 0.5f, 1.4f)),
                (0f, Synth.Tone(0.04f, 3000f, 1500f, W.Noise, 0.1f))));
            portalUp = Synth.Clip("PortalUp", Synth.Seq(
                (0f, Synth.Tone(0.4f, 220f, 1320f, W.Sine, 0.3f, decay: 0.6f, curve: 0.7f, vibratoHz: 14f, vibratoDepth: 0.04f)),
                (0f, Synth.Tone(0.35f, 800f, 5000f, W.Noise, 0.08f, decay: 0.8f))));
            portalDown = Synth.Clip("PortalDown", Synth.Seq(
                (0f, Synth.Tone(0.4f, 1320f, 220f, W.Sine, 0.3f, decay: 0.6f, curve: 0.7f, vibratoHz: 14f, vibratoDepth: 0.04f)),
                (0f, Synth.Tone(0.35f, 5000f, 800f, W.Noise, 0.08f, decay: 0.8f))));
            mission = Synth.Clip("Mission", Synth.Arp(0.07f, new[] { 784f, 988f, 1175f, 1568f }, W.Square, 0.13f, 0.25f));
            purchase = Synth.Clip("Purchase", Synth.Seq(
                (0f, Synth.Arp(0.06f, new[] { 1047f, 1319f, 1568f, 2093f, 2637f }, W.Square, 0.12f, 0.25f)),
                (0f, Synth.Tone(0.4f, 3000f, 8000f, W.Noise, 0.06f))));
            skillReady = Synth.Clip("SkillReady", Synth.Arp(0.05f, new[] { 1319f, 1760f, 2637f }, W.Square, 0.12f, 0.25f));
            miss = Synth.Clip("Miss", Synth.Seq(
                (0f, Synth.Tone(0.25f, 190f, 110f, W.Square, 0.18f, decay: 0.6f, vibratoHz: 20f, vibratoDepth: 0.1f)),
                (0f, Synth.Tone(0.25f, 187f, 108f, W.Square, 0.12f, 0.3f, 0.6f))));
            hit = Synth.Clip("Hit", Synth.Seq(
                (0f, Synth.Tone(0.2f, 3000f, 300f, W.Noise, 0.5f, decay: 1.4f)),
                (0f, Synth.Tone(0.28f, 170f, 40f, W.Square, 0.32f))));
            zap = Synth.Clip("Zap", Synth.Tone(0.4f, 900f, 90f, W.Square, 0.22f, 0.3f, vibratoHz: 55f, vibratoDepth: 0.35f));
            fall = Synth.Clip("Fall", Synth.Tone(0.6f, 750f, 110f, W.Triangle, 0.3f, decay: 0.4f, curve: 1.2f));
            thud = Synth.Clip("Thud", Synth.Seq(
                (0f, Synth.Tone(0.14f, 130f, 45f, W.Sine, 0.6f)),
                (0f, Synth.Tone(0.09f, 1400f, 300f, W.Noise, 0.25f))));
            gameOver = Synth.Clip("GameOver", Synth.Arp(0.13f, new[] { 784f, 659f, 523f, 392f }, W.Triangle, 0.3f, decay: 0.8f));
            newBest = Synth.Clip("NewBest", Synth.Seq(
                (0f, Synth.Arp(0.09f, new[] { 523f, 659f, 784f, 1047f, 1319f }, W.Square, 0.14f, 0.25f)),
                (0.45f, Synth.Tone(0.45f, 1568f, 1568f, W.Square, 0.14f, 0.25f, 0.8f, vibratoHz: 7f, vibratoDepth: 0.01f))));
            click = Synth.Clip("Click", Synth.Tone(0.05f, 1400f, 1400f, W.Square, 0.12f, 0.25f));
            // Two short barks: a pitched square "wuh" with a noisy attack.
            var bark = Synth.Seq(
                (0f, Synth.Tone(0.03f, 3000f, 1200f, W.Noise, 0.2f)),
                (0f, Synth.Tone(0.11f, 620f, 330f, W.Square, 0.22f, 0.35f, 1.2f, curve: 0.6f, vibratoHz: 35f, vibratoDepth: 0.06f)));
            start = Synth.Clip("Start", Synth.Seq((0f, bark), (0.16f, bark)));

            music = gameObject.AddComponent<AudioSource>();
            music.clip = Synth.Clip("Music", Synth.Music());
            music.loop = true;
            music.playOnAwake = false;
            music.volume = 0f;

            voices = new AudioSource[8];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }

            Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            AudioListener.volume = Muted ? 0f : 1f;
        }

        void Update()
        {
            if (music != null)
                music.volume = Mathf.MoveTowards(music.volume, musicTarget, Time.unscaledDeltaTime * 1.2f);
        }

        void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            var v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            v.clip = clip;
            v.volume = volume;
            v.pitch = pitch;
            v.Play();
        }

        public void EnsureMusic()
        {
            if (!music.isPlaying) music.Play();
        }

        public void DuckMusic(bool duck) => musicTarget = duck ? 0.12f : MusicVolume;

        public void ToggleMute()
        {
            Muted = !Muted;
            AudioListener.volume = Muted ? 0f : 1f;
            PlayerPrefs.SetInt(MuteKey, Muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Flap() => Play(flap, 0.9f, Random.Range(0.95f, 1.08f));
        public void Pass(int multiplier) => Play(pass, 0.8f, 1f + 0.06f * Mathf.Min(multiplier - 1, 10));
        public void ComboUp(int multiplier) => Play(combo, 0.9f, 1f + 0.05f * Mathf.Min(multiplier - 2, 8));
        public void Dash() => Play(dash, 0.9f, Random.Range(0.96f, 1.04f));
        public void GateBreak() => Play(gateBreak);
        public void Stomp() => Play(stomp);
        public void ShieldPop() => Play(shieldPop);
        public void FeverStart() => Play(feverStart);
        public void FeverEnd() => Play(feverEnd, 0.8f);
        public void Perfect() => Play(perfect);
        public void CloseCall() => Play(closeCall, 0.9f);
        public void Smash() => Play(smash, 0.8f, Random.Range(0.9f, 1.15f));
        public void Coin(int chain) => Play(coin, 0.7f, 1f + 0.05f * Mathf.Min(chain, 10));
        public void SwitchFlip() => Play(switchFlip);
        public void Biome() => Play(biome, 0.8f);
        public void Mission() => Play(mission);
        public void Shoot() => Play(shoot, 0.5f, Random.Range(0.95f, 1.05f));
        public void EnemyHit() => Play(enemyHit, 0.5f, Random.Range(0.9f, 1.2f));
        public void Explode() => Play(explode, 0.8f, Random.Range(0.85f, 1.15f));
        public void BigExplode() => Play(bigExplode);
        public void PowerUp() => Play(powerUp);
        public void BossAlarm() => Play(bossAlarm, 0.8f);
        public void IceCrack() => Play(iceCrack, 0.7f);
        public void IceShatter() => Play(iceShatter, 0.8f, Random.Range(0.9f, 1.1f));
        public void MeteorWarn() => Play(meteorWarn, 0.8f);
        public void MeteorLaunch() => Play(meteorLaunch, 0.9f);
        public void SwitchToggle(int state) => Play(switchToggle, 0.9f, state == 0 ? 1f : 0.8f);
        public void Portal(bool inverted) => Play(inverted ? portalUp : portalDown);
        public void Purchase() => Play(purchase);
        public void SetMusicPitch(float pitch) { if (music != null) music.pitch = pitch; }
        public void SkillReady() => Play(skillReady, 0.8f);
        public void Miss() => Play(miss);
        public void Hit() => Play(hit);
        public void Zap() => Play(zap, 0.9f);
        public void Fall() => Play(fall, 0.8f);
        public void Thud() => Play(thud);
        public void GameOver() => Play(gameOver);
        public void NewBest() => Play(newBest);
        public void Click() => Play(click, 0.8f);
        public void RunStart() => Play(start, 0.8f);
    }
}
