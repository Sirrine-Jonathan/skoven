using System.Collections.Generic;
using UnityEngine;
using Skoven.Network;

namespace Skoven.Agent
{
    /// <summary>
    /// Text-to-speech player — converts AI speech messages to audio.
    ///
    /// Prototype implementation uses Unity's built-in text-to-speech
    /// (Android TTS via AndroidJavaClass). For Quest production, replace
    /// with a proper TTS solution such as:
    ///   - Meta Voice SDK TTS
    ///   - ElevenLabs API (audio streamed from companion app)
    ///   - Azure Cognitive Services TTS
    ///
    /// High-priority messages interrupt the current speech.
    /// </summary>
    public class TtsPlayer : MonoBehaviour
    {
        [Header("Settings")]
        public float speechVolume = 1.0f;
        public bool enabled = true;

        private readonly Queue<(string text, bool highPriority)> _queue = new();
        private bool _speaking;

        private AndroidJavaObject _tts;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            InitAndroidTts();
#endif
        }

        private void Update()
        {
            if (!_speaking && _queue.Count > 0)
            {
                var (text, _) = _queue.Dequeue();
                Speak(text);
            }
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _tts?.Call("shutdown");
#endif
        }

        // ── Public API ─────────────────────────────────────────────────────

        public void OnSpeech(SpeechMessage msg)
        {
            if (!enabled) return;

            if (msg.priority == "high")
            {
                // Clear queue and interrupt current speech
                _queue.Clear();
                Stop();
                Speak(msg.text);
            }
            else
            {
                _queue.Enqueue((msg.text, false));
            }
        }

        public void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _tts?.Call("stop");
#endif
            _speaking = false;
        }

        // ── TTS implementation ─────────────────────────────────────────────

        private void Speak(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _speaking = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            _tts?.Call("speak", text, 0 /* QUEUE_FLUSH */, null, null);
            // Reset _speaking after estimated duration (rough heuristic)
            Invoke(nameof(OnSpeechComplete), EstimateDuration(text));
#else
            // Editor fallback: log to console
            Debug.Log($"[TTS] {text}");
            _speaking = false;
#endif
        }

        private void OnSpeechComplete() => _speaking = false;

        private static float EstimateDuration(string text) =>
            Mathf.Max(1.5f, text.Split(' ').Length * 0.35f);

#if UNITY_ANDROID && !UNITY_EDITOR
        private void InitAndroidTts()
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, null);
        }
#endif
    }
}
