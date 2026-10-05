// Captures the microphone for a push-to-talk question as 24 kHz 16-bit PCM in 100 ms chunks, which is
// what Voice Live takes. Runs in its own AudioContext at the device's rate (some browsers won't connect
// a microphone to a context at a different rate) and resamples here: each output sample averages the
// input samples in its period, a box filter that keeps hiss from aliasing down into speech.

// It also notices when the fan has finished speaking ("silence"), so tap-to-ask stops by itself: speech
// is energy well above the room's noise floor, and a question has ended after 1.2 s back at the floor.

const TARGET_RATE = 24000;
const CHUNK_SAMPLES = 2400; // 100 ms
const WINDOW_SAMPLES = 480; // 20 ms
const MIN_SPEECH_WINDOWS = 3; // 60 ms of speech before a pause can end the question
const END_SILENCE_WINDOWS = 60; // 1.2 s
const MIN_THRESHOLD = 0.015;

class PcmCapture extends AudioWorkletProcessor {
  constructor() {
    super();
    this.recording = false;
    this.ratio = sampleRate / TARGET_RATE;
    this.reset();
    this.port.onmessage = (event) => {
      if (event.data.type === 'start') {
        this.reset();
        this.recording = true;
      } else if (event.data.type === 'stop') {
        this.recording = false;
        if (this.filled > 0) {
          this.port.postMessage({ type: 'chunk', samples: this.chunk.slice(0, this.filled) });
        }

        this.port.postMessage({ type: 'stopped' });
        this.reset();
      }
    };
  }

  reset() {
    this.chunk = new Int16Array(CHUNK_SAMPLES);
    this.filled = 0;
    this.sum = 0;
    this.count = 0;
    this.phase = 0;
    this.energy = 0;
    this.windowFilled = 0;
    this.floor = 0.005;
    this.speechWindows = 0;
    this.silentWindows = 0;
    this.silenceSent = false;
  }

  /** One 20 ms window of output: track the noise floor and say "silence" once a question has ended. */
  detect(rms) {
    if (rms < this.floor) {
      this.floor = rms;
    } else {
      this.floor += (rms - this.floor) * 0.002;
    }

    const speaking = rms > Math.max(MIN_THRESHOLD, this.floor * 3);
    if (speaking) {
      this.speechWindows++;
      this.silentWindows = 0;
    } else if (this.speechWindows >= MIN_SPEECH_WINDOWS) {
      this.silentWindows++;
    }

    if (!this.silenceSent && this.speechWindows >= MIN_SPEECH_WINDOWS && this.silentWindows >= END_SILENCE_WINDOWS) {
      this.silenceSent = true;
      this.port.postMessage({ type: 'silence' });
    }
  }

  process(inputs) {
    const input = inputs[0]?.[0];
    if (!this.recording || !input) {
      return true;
    }

    for (let i = 0; i < input.length; i++) {
      this.sum += input[i];
      this.count++;
      this.phase += 1;
      if (this.phase >= this.ratio) {
        this.phase -= this.ratio;
        const sample = Math.max(-1, Math.min(1, this.sum / this.count));
        this.chunk[this.filled++] = sample < 0 ? sample * 0x8000 : sample * 0x7fff;
        this.sum = 0;
        this.count = 0;
        this.energy += sample * sample;
        if (++this.windowFilled === WINDOW_SAMPLES) {
          this.detect(Math.sqrt(this.energy / WINDOW_SAMPLES));
          this.energy = 0;
          this.windowFilled = 0;
        }
        if (this.filled === CHUNK_SAMPLES) {
          this.port.postMessage({ type: 'chunk', samples: this.chunk });
          this.chunk = new Int16Array(CHUNK_SAMPLES);
          this.filled = 0;
        }
      }
    }

    return true;
  }
}

registerProcessor('pcm-capture', PcmCapture);
