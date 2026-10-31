using System;
using System.Collections;
using UnityEngine;
using Flowstate.Network;

namespace Flowstate.Voice
{
    /// <summary>
    /// Captures microphone audio on push-to-talk (right grip) and streams
    /// it to the companion app as base64-encoded PCM chunks.
    ///
    /// Audio format: 16 kHz, mono, 16-bit little-endian PCM.
    /// Each chunk is ~100ms (1600 samples). isFinal=true on PTT release.
    /// </summary>
    public class VoiceCapture : MonoBehaviour
    {
        [Header("Dependencies")]
        public WebSocketClient wsClient;

        [Header("Settings")]
        [Tooltip("Sample rate sent to the companion app STT backend.")]
        public int sampleRate = 16000;

        [Tooltip("Chunk size in samples (~100ms at 16kHz).")]
        public int chunkSamples = 1600;

        [Tooltip("Noise gate: minimum RMS to consider as speech.")]
        [Range(0f, 0.1f)]
        public float noisGateThreshold = 0.01f;

        public bool IsRecording => _isRecording;

        private AudioClip _mic;
        private string _deviceName;
        private bool _isRecording;
        private int _lastSamplePos;
        private Coroutine _streamCoroutine;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Start()
        {
            // Select the first available mic device
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[Flowstate] No microphone devices found.");
                return;
            }
            _deviceName = Microphone.devices[0];
            Debug.Log($"[Flowstate] Using microphone: {_deviceName}");
        }

        private void OnDestroy()
        {
            StopCapture();
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// Call on push-to-talk button press (e.g., right grip).
        /// </summary>
        public void StartCapture()
        {
            if (_isRecording || string.IsNullOrEmpty(_deviceName)) return;

            // Record into a looping 10-second clip; we stream chunks in real time
            _mic = Microphone.Start(_deviceName, loop: true, lengthSec: 10, frequency: sampleRate);
            _isRecording = true;
            _lastSamplePos = 0;

            // Wait for mic to warm up
            _streamCoroutine = StartCoroutine(StreamChunks());

            Debug.Log("[Flowstate] Voice capture started.");
        }

        /// <summary>
        /// Call on push-to-talk button release.
        /// </summary>
        public void StopCapture()
        {
            if (!_isRecording) return;

            if (_streamCoroutine != null)
                StopCoroutine(_streamCoroutine);
            _streamCoroutine = null;

            // Flush remaining audio as the final chunk
            FlushRemaining(isFinal: true);

            Microphone.End(_deviceName);
            _isRecording = false;

            Debug.Log("[Flowstate] Voice capture stopped.");
        }

        // ── Streaming ──────────────────────────────────────────────────────

        private IEnumerator StreamChunks()
        {
            // Wait for mic to start
            while (Microphone.GetPosition(_deviceName) <= 0)
                yield return null;

            while (_isRecording)
            {
                int currentPos = Microphone.GetPosition(_deviceName);
                int available = currentPos >= _lastSamplePos
                    ? currentPos - _lastSamplePos
                    : (_mic.samples - _lastSamplePos) + currentPos;

                if (available >= chunkSamples)
                {
                    SendChunk(isFinal: false);
                }

                yield return new WaitForSeconds(0.05f); // poll at 20Hz
            }
        }

        private void SendChunk(bool isFinal)
        {
            int currentPos = Microphone.GetPosition(_deviceName);
            int available = currentPos >= _lastSamplePos
                ? currentPos - _lastSamplePos
                : (_mic.samples - _lastSamplePos) + currentPos;

            if (available <= 0) return;

            int samplesToRead = Mathf.Min(available, chunkSamples * 4);
            var samples = new float[samplesToRead];
            _mic.GetData(samples, _lastSamplePos % _mic.samples);
            _lastSamplePos = (_lastSamplePos + samplesToRead) % _mic.samples;

            // Noise gate
            float rms = 0f;
            foreach (var s in samples) rms += s * s;
            rms = Mathf.Sqrt(rms / samples.Length);
            if (rms < noisGateThreshold && !isFinal) return;

            // Convert float32 → int16 PCM
            var pcm = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short s16 = (short)Mathf.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
                pcm[i * 2] = (byte)(s16 & 0xFF);
                pcm[i * 2 + 1] = (byte)((s16 >> 8) & 0xFF);
            }

            string b64 = Convert.ToBase64String(pcm);
            string json = $"{{\"type\":\"audio_chunk\",\"data\":\"{b64}\",\"isFinal\":{(isFinal ? "true" : "false")}}}";
            wsClient.Send(json);
        }

        private void FlushRemaining(bool isFinal)
        {
            if (_mic == null) return;
            SendChunk(isFinal);
        }
    }
}
