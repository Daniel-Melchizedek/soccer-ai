// Short procedural sounds that tell a listener what happened before any words do, panned to where it
// happened ("you're in the main stand": play on your left is panned left). Generated, not recorded, so
// there is nothing to license. Kept well below speech level.

const EARCON_GAIN = 0.12;

/** Each earcon: notes of [frequency Hz, start s, duration s], with an oscillator type. */
const EARCONS = {
  'possession.home': { type: 'sine', notes: [[660, 0, 0.08], [880, 0.09, 0.1]] },
  'possession.away': { type: 'sine', notes: [[880, 0, 0.08], [660, 0.09, 0.1]] },
  shot: { type: 'triangle', sweep: [400, 1200, 0.15] },
  saved: { type: 'sine', notes: [[180, 0, 0.12]] },
  goal: { type: 'triangle', notes: [[523, 0, 0.12], [659, 0.12, 0.12], [784, 0.24, 0.12], [1047, 0.36, 0.4]] },
  whistle: { type: 'square', notes: [[2000, 0, 0.08], [2500, 0.09, 0.08]], gain: 0.04 },
  'card.yellow': { type: 'sine', notes: [[1200, 0, 0.2]] },
  'card.red': { type: 'sine', notes: [[300, 0, 0.15], [300, 0.2, 0.15]] },
  out_of_play: { type: 'sine', notes: [[1200, 0, 0.015]] },
};

export function playEarcon(ctx, id, pan, bus = ctx?.destination) {
  const spec = EARCONS[id];
  if (!ctx || !spec) {
    return;
  }

  const panner = new StereoPannerNode(ctx, { pan: Math.max(-1, Math.min(1, pan ?? 0)) });
  panner.connect(bus);
  const now = ctx.currentTime;
  const level = spec.gain ?? EARCON_GAIN;

  const tone = (frequency, start, duration, toFrequency) => {
    const osc = new OscillatorNode(ctx, { type: spec.type, frequency });
    const gain = new GainNode(ctx, { gain: 0 });
    if (toFrequency) {
      osc.frequency.exponentialRampToValueAtTime(toFrequency, now + start + duration);
    }

    gain.gain.setValueAtTime(0, now + start);
    gain.gain.linearRampToValueAtTime(level, now + start + 0.005);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + start + duration);
    osc.connect(gain).connect(panner);
    osc.start(now + start);
    osc.stop(now + start + duration + 0.02);
  };

  if (spec.sweep) {
    const [from, to, duration] = spec.sweep;
    tone(from, 0, duration, to);
  }

  for (const [frequency, start, duration] of spec.notes ?? []) {
    tone(frequency, start, duration);
  }
}
