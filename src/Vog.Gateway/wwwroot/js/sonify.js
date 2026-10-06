const PITCH_LENGTH = 105;
const PITCH_WIDTH = 68;
const RAMP_S = 0.12;
const BASE_LEVEL = 0.12;

const clamp = (value, min, max) => Math.max(min, Math.min(max, value));

export function createBallSonifier(ctx, bus) {
  let enabled = false;
  let demoActive = false;
  let lastPitch = null;
  let nodes = null;
  let demoTimer = null;
  let stopTimer = null;

  function ensureNodes() {
    if (nodes) {
      clearTimeout(stopTimer);
      stopTimer = null;
      return nodes;
    }

    const oscillator = new OscillatorNode(ctx, { type: 'sine', frequency: 330 });
    const filter = new BiquadFilterNode(ctx, { type: 'lowpass', frequency: 1400, Q: 0.5 });
    const panner = new StereoPannerNode(ctx);
    const gain = new GainNode(ctx, { gain: 0 });
    oscillator.connect(filter).connect(panner).connect(gain).connect(bus);
    oscillator.start();
    nodes = { oscillator, filter, panner, gain };
    return nodes;
  }

  function stopNodes() {
    if (!nodes || enabled || demoActive) {
      return;
    }

    const current = nodes;
    nodes = null;
    try {
      current.oscillator.stop(ctx.currentTime + 0.02);
    } catch {
    }
    current.oscillator.disconnect();
    current.filter.disconnect();
    current.panner.disconnect();
    current.gain.disconnect();
  }

  function render(message, preview = false) {
    if (!message || !Array.isArray(message.b) || message.b.length < 2) {
      return;
    }

    const [rawX, rawY, rawZ = 0] = message.b.map(Number);
    if (![rawX, rawY, rawZ].every(Number.isFinite)) {
      return;
    }

    const x = clamp(rawX, 0, PITCH_LENGTH);
    const y = clamp(rawY, 0, PITCH_WIDTH);
    const z = clamp(rawZ, 0, 15);
    const owner = message.o === 'h' || message.o === 'a' ? message.o : null;
    const homeAttacksRight = Boolean(message.hr);
    const attacksRight = owner === 'h' ? homeAttacksRight : owner === 'a' ? !homeAttacksRight : null;
    const progress = attacksRight === null ? 0.5 : (attacksRight ? x : PITCH_LENGTH - x) / PITCH_LENGTH;
    const frequency = 220 * (2 ** clamp(progress, 0, 1)) + (z / 15) * 18;
    const distanceLevel = 1 - (y / PITCH_WIDTH) * 0.55;
    const possessionLevel = owner === null ? 0.22 : 1;
    const level = preview ? 0.16 : enabled ? BASE_LEVEL * distanceLevel * possessionLevel * (z > 1 ? 0.82 : 1) : 0;
    const now = ctx.currentTime;
    const sound = ensureNodes();

    sound.oscillator.type = owner === 'a' ? 'triangle' : 'sine';
    sound.oscillator.frequency.setTargetAtTime(frequency, now, RAMP_S);
    sound.filter.frequency.setTargetAtTime(1100 + (z / 15) * 900, now, RAMP_S);
    sound.panner.pan.setTargetAtTime((x / PITCH_LENGTH) * 2 - 1, now, RAMP_S);
    sound.gain.gain.setTargetAtTime(level, now, RAMP_S);
  }

  function update(message) {
    lastPitch = message;
    if (enabled && !demoActive) {
      render(message);
    }
  }

  function setEnabled(value) {
    enabled = Boolean(value);
    if (enabled) {
      render(lastPitch);
    } else if (nodes && !demoActive) {
      nodes.gain.gain.setTargetAtTime(0, ctx.currentTime, RAMP_S);
      stopTimer = setTimeout(stopNodes, 500);
    }
  }

  function demo() {
    if (demoActive) {
      return;
    }

    demoActive = true;
    const started = performance.now();
    ensureNodes();
    const tick = () => {
      const progress = clamp((performance.now() - started) / 3200, 0, 1);
      render({ b: [PITCH_LENGTH * progress, 15, 0], o: 'h', hr: true }, true);
      if (progress >= 1) {
        clearInterval(demoTimer);
        demoTimer = null;
        demoActive = false;
        if (enabled) {
          render(lastPitch);
        } else if (nodes) {
          nodes.gain.gain.setTargetAtTime(0, ctx.currentTime, RAMP_S);
          stopTimer = setTimeout(stopNodes, 500);
        }
      }
    };

    tick();
    demoTimer = setInterval(tick, 50);
  }

  function dispose() {
    enabled = false;
    demoActive = false;
    clearInterval(demoTimer);
    clearTimeout(stopTimer);
    stopNodes();
  }

  return { update, setEnabled, demo, dispose };
}