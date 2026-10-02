// Plays the channel's speech: 24 kHz mono PCM segments, one utterance after another, never overlapping.
// A segment starts once 80 ms is buffered (or it's complete), and a cut fades out over 30 ms.
// A segment that could only start too long after the booth aired it is skipped, so a backlog
// (a slow request, speech running longer than planned) drains instead of building up.

const PREBUFFER_SAMPLES = 1920; // 80 ms at 24 kHz
const FADE_SAMPLES = 720; // 30 ms

class PcmPlayer extends AudioWorkletProcessor {
  constructor() {
    super();
    this.queue = [];
    this.current = null;
    this.port.onmessage = (event) => this.onMessage(event.data);
  }

  onMessage(message) {
    switch (message.type) {
      case 'begin':
        // skipAfter: seconds behind the booth before the line is stale (Infinity: never skipped).
        this.queue.push({ id: message.id, at: currentTime, skipAfter: message.skipAfter, chunks: [], buffered: 0, chunk: 0, offset: 0, played: 0, ended: false, cutAt: Infinity });
        break;
      case 'chunk': {
        const segment = this.find(message.id);
        if (segment) {
          segment.chunks.push(message.samples);
          segment.buffered += message.samples.length;
        }
        break;
      }
      case 'end': {
        const segment = this.find(message.id);
        if (segment) {
          segment.ended = true;
        }
        break;
      }
      case 'cut': {
        const segment = this.find(message.id);
        if (segment) {
          // Already past the cut point: fade out from here rather than stopping dead.
          segment.cutAt = Math.max(segment.played + FADE_SAMPLES, message.afterSamples);
        }
        break;
      }
      case 'skip': {
        // The server skipped it: nothing will be sent for it.
        this.queue = this.queue.filter((segment) => segment.id !== message.id);
        break;
      }
      case 'flush':
        this.queue = [];
        this.current = null;
        break;
    }
  }

  find(id) {
    if (this.current && this.current.id === id) {
      return this.current;
    }

    return this.queue.find((segment) => segment.id === id);
  }

  process(_inputs, outputs) {
    const out = outputs[0][0];
    let i = 0;

    while (i < out.length) {
      if (!this.current) {
        while (this.queue.length > 0 && currentTime - this.queue[0].at > this.queue[0].skipAfter) {
          this.port.postMessage({ type: 'skipped', id: this.queue.shift().id });
        }

        const next = this.queue[0];
        if (!next || (!next.ended && next.buffered < PREBUFFER_SAMPLES)) {
          break;
        }

        this.current = this.queue.shift();
        this.port.postMessage({ type: 'started', id: this.current.id });
      }

      const s = this.current;
      const finished = s.played >= s.cutAt || (s.ended && s.chunk >= s.chunks.length);
      if (finished) {
        this.port.postMessage({ type: 'ended', id: s.id, played: s.played });
        this.current = null;
        continue;
      }

      if (s.chunk >= s.chunks.length) {
        break; // waiting for the next chunk from the network
      }

      const chunk = s.chunks[s.chunk];
      let sample = chunk[s.offset++];
      if (s.offset >= chunk.length) {
        s.chunk++;
        s.offset = 0;
      }

      const untilCut = s.cutAt - s.played;
      if (untilCut < FADE_SAMPLES) {
        sample *= Math.max(0, untilCut / FADE_SAMPLES);
      }

      out[i++] = sample;
      s.played++;
    }

    for (; i < out.length; i++) {
      out[i] = 0;
    }

    return true;
  }
}

registerProcessor('pcm-player', PcmPlayer);
