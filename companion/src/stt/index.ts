/**
 * Speech-to-Text module
 *
 * Receives raw PCM audio chunks (16 kHz, mono, 16-bit LE) accumulated from
 * the Quest and returns a transcript string.
 *
 * Backend selection via SKOVEN_STT_BACKEND env var:
 *   openai_whisper  — sends audio to OpenAI Whisper API (default)
 *   passthrough     — expects text directly; used for desktop testing
 */

import fs from "fs";
import os from "os";
import path from "path";
import { execFile } from "child_process";
import { promisify } from "util";
import OpenAI from "openai";

const execFileAsync = promisify(execFile);

export type STTBackend = "openai_whisper" | "local_whisper" | "passthrough" | "auto";

export class SpeechToText {
  private readonly backend: STTBackend;
  private readonly openai: OpenAI | null;
  private readonly localWhisperBin: string | null;
  private readonly localWhisperModel: string | null;

  constructor() {
    const configured = (process.env["SKOVEN_STT_BACKEND"] ?? "auto") as STTBackend;

    // Check OpenAI availability
    this.openai =
      process.env["OPENAI_API_KEY"]
        ? new OpenAI({ apiKey: process.env["OPENAI_API_KEY"] })
        : null;

    // Check local whisper-cli availability
    const customBin = process.env["WHISPER_CPP_BIN"];
    const standardBin = "/opt/homebrew/bin/whisper-cli";
    if (customBin && fs.existsSync(customBin)) {
      this.localWhisperBin = customBin;
    } else if (fs.existsSync(standardBin)) {
      this.localWhisperBin = standardBin;
    } else {
      this.localWhisperBin = null;
    }

    const defaultModel = path.join(
      os.homedir(),
      ".cache",
      "whisper.cpp",
      "ggml-base.en.bin"
    );
    const customModel = process.env["WHISPER_CPP_MODEL"];
    if (customModel && fs.existsSync(customModel)) {
      this.localWhisperModel = customModel;
    } else if (fs.existsSync(defaultModel)) {
      this.localWhisperModel = defaultModel;
    } else {
      this.localWhisperModel = null;
    }

    if (configured === "auto") {
      if (this.openai) {
        this.backend = "openai_whisper";
        console.log("[STT] Auto-selected backend: openai_whisper");
      } else if (this.localWhisperBin && this.localWhisperModel) {
        this.backend = "local_whisper";
        console.log(`[STT] Auto-selected backend: local_whisper (${this.localWhisperBin})`);
      } else {
        this.backend = "openai_whisper"; // will fail with descriptive error if called
        console.warn("[STT] Neither OPENAI_API_KEY nor whisper-cli model found.");
      }
    } else {
      this.backend = configured;
    }
  }

  /**
   * Transcribe a complete audio buffer.
   *
   * @param pcmBuffer Raw PCM audio: 16 kHz, mono, 16-bit little-endian
   * @returns Recognized transcript, or empty string if nothing was heard
   */
  async transcribe(pcmBuffer: Buffer): Promise<string> {
    if (this.backend === "passthrough") {
      // In passthrough mode the Quest sends QuestTranscript directly
      return "";
    }

    if (this.backend === "openai_whisper") {
      if (!this.openai && this.localWhisperBin && this.localWhisperModel) {
        console.log("[STT] OPENAI_API_KEY missing, falling back to local_whisper");
        return this.transcribeWithLocalWhisper(pcmBuffer);
      }
      return this.transcribeWithWhisper(pcmBuffer);
    }

    if (this.backend === "local_whisper") {
      return this.transcribeWithLocalWhisper(pcmBuffer);
    }

    throw new Error(`Unknown STT backend: ${this.backend}`);
  }

  private async transcribeWithWhisper(pcmBuffer: Buffer): Promise<string> {
    if (!this.openai) {
      throw new Error(
        "OpenAI client not initialized. Check OPENAI_API_KEY in .env or switch to SKOVEN_STT_BACKEND=local_whisper"
      );
    }

    // Whisper API requires a file upload. Write PCM as WAV to a temp file.
    const wavBuffer = pcmToWav(pcmBuffer, 16000, 1, 16);
    const tmpPath = path.join(os.tmpdir(), `skoven-stt-${Date.now()}.wav`);

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
      if (fs.existsSync(tmpPath)) {
        fs.unlinkSync(tmpPath);
      }
    }
  }

  private async transcribeWithLocalWhisper(pcmBuffer: Buffer): Promise<string> {
    const bin = this.localWhisperBin ?? "/opt/homebrew/bin/whisper-cli";
    const model =
      this.localWhisperModel ??
      path.join(os.homedir(), ".cache", "whisper.cpp", "ggml-base.en.bin");

    if (!fs.existsSync(bin)) {
      throw new Error(`whisper-cli binary not found at ${bin}. Run 'brew install whisper-cpp'.`);
    }
    if (!fs.existsSync(model)) {
      throw new Error(
        `whisper GGML model not found at ${model}. Download ggml-base.en.bin to ~/.cache/whisper.cpp/`
      );
    }

    const wavBuffer = pcmToWav(pcmBuffer, 16000, 1, 16);
    const tmpPath = path.join(os.tmpdir(), `skoven-stt-${Date.now()}.wav`);

    try {
      fs.writeFileSync(tmpPath, wavBuffer);
      // Run whisper-cli with no timestamps (-nt) and no progress prints (-np)
      const { stdout } = await execFileAsync(bin, [
        "-m",
        model,
        "-nt",
        "-np",
        "-f",
        tmpPath,
      ]);

      const result = stdout.trim();
      console.log(`[STT] Local whisper transcribed: "${result}"`);
      return result;
    } catch (err) {
      console.error("[STT] Local whisper transcription failed:", err);
      return "";
    } finally {
      if (fs.existsSync(tmpPath)) {
        fs.unlinkSync(tmpPath);
      }
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
