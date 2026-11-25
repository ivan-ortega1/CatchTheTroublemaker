using UnityEngine;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// Small singleton for UI and coin sound effects. GameManager creates it at
    /// runtime and passes the clips wired in the Inspector, so it works in any scene.
    /// </summary>
    public class CttSfx : MonoBehaviour
    {
        public static CttSfx Instance { get; private set; }

        private AudioSource source;
        private AudioClip hover;
        private AudioClip press;
        private AudioClip coin;

        private void Awake()
        {
            Instance = this;
            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
        }

        public void Configure(AudioClip hoverClip, AudioClip pressClip, AudioClip coinClip)
        {
            hover = hoverClip;
            press = pressClip;
            coin = coinClip;
        }

        public void PlayHover() => Play(hover);
        public void PlayPress() => Play(press);
        public void PlayCoin() => Play(coin);

        private void Play(AudioClip clip)
        {
            if (clip == null || source == null) return;
            source.PlayOneShot(clip);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
