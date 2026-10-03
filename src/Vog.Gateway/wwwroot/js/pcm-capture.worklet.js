// Captures the microphone for a push-to-talk question as 24 kHz 16-bit PCM in 100 ms chunks, which is
// what Voice Live takes. Runs in its own AudioContext at the device's rate (some browsers won't connect
// a microphone to a context at a different rate) and resamples here: each output sample averages the
// input samples in its period, a box filter that keeps hiss from aliasing down into speech.

const TARGET_RATE = 24000;
const CHUNK_SAMPLES = 2400; // 100 ms

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
