/**
 * Speech-to-Text module
 *
 * Receives raw PCM audio chunks (16 kHz, mono, 16-bit LE) accumulated from
 * the Quest and returns a transcript string.
 *
 * Backend selection via FLOWSTATE_STT_BACKEND env var:
 *   openai_whisper  — sends audio to OpenAI Whisper API (default)
 *   passthrough     — expects text directly; used for desktop testing
 */

import fs from "fs";
import os from "os";
import path from "path";
import OpenAI from "openai";

export type STTBackend = "openai_whisper" | "passthrough";

export class SpeechToText {
  private readonly backend: STTBackend;
  private readonly openai: OpenAI | null;

  constructor() {
    this.backend = (process.env["FLOWSTATE_STT_BACKEND"] ??
      "openai_whisper") as STTBackend;

    this.openai =
      this.backend === "openai_whisper" && process.env["OPENAI_API_KEY"]
        ? new OpenAI({ apiKey: process.env["OPENAI_API_KEY"] })
        : null;
  }

  /**
   * Transcribe a complete audio buffer.
   *
   * @param pcmBuffer Raw PCM audio: 16 kHz, mono, 16-bit little-endian
   * @returns Recognized transcript, or empty string if nothing was heard
   */
  async transcribe(pcmBuffer: Buffer): Promise<string> {
    if (this.backend === "passthrough") {
      // In passthrough mode the Quest sends QuestTranscript directly;
      // this path should never be called, but guard defensively.
      return "";
    }

    if (this.backend === "openai_whisper") {
      return this.transcribeWithWhisper(pcmBuffer);
    }

    throw new Error(`Unknown STT backend: ${this.backend}`);
  }

  private async transcribeWithWhisper(pcmBuffer: Buffer): Promise<string> {
    if (!this.openai) {
      throw new Error(
        "OpenAI client not initialized. Check OPENAI_API_KEY in .env"
      );
    }

    // Whisper API requires a file upload. Write PCM as WAV to a temp file.
    const wavBuffer = pcmToWav(pcmBuffer, 16000, 1, 16);
    const tmpPath = path.join(os.tmpdir(), `flowstate-stt-${Date.now()}.wav`);

    try {
      fs.writeFileSync(tmpPath, wavBuffer);

      const response = await this.openai.audio.transcriptions.create({
        file: fs.createReadStream(tmpPath) as unknown as File,
        model: "whisper-1",
        response_format: "text",
        language: "en",
      });

      return (response as unknown as string).trim();
    } finally {
      fs.unlinkSync(tmpPath);
    }
  }
}

/**
 * Wrap raw PCM data in a minimal WAV container.
 * Required by the Whisper API which needs an audio file, not raw PCM.
 */
function pcmToWav(
  pcm: Buffer,
  sampleRate: number,
  channels: number,
  bitDepth: number
): Buffer {
  const byteRate = (sampleRate * channels * bitDepth) / 8;
  const blockAlign = (channels * bitDepth) / 8;
  const dataSize = pcm.length;
  const headerSize = 44;
  const buf = Buffer.alloc(headerSize + dataSize);

  buf.write("RIFF", 0);
  buf.writeUInt32LE(36 + dataSize, 4);
  buf.write("WAVE", 8);
  buf.write("fmt ", 12);
  buf.writeUInt32LE(16, 16); // PCM chunk size
  buf.writeUInt16LE(1, 20); // PCM format
  buf.writeUInt16LE(channels, 22);
  buf.writeUInt32LE(sampleRate, 24);
  buf.writeUInt32LE(byteRate, 28);
  buf.writeUInt16LE(blockAlign, 32);
  buf.writeUInt16LE(bitDepth, 34);
  buf.write("data", 36);
  buf.writeUInt32LE(dataSize, 40);
  pcm.copy(buf, headerSize);

  return buf;
}
