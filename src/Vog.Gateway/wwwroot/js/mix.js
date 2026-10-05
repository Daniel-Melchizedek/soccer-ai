// One mix for everything the page plays, so each part has its own level and the fan sets the balance.
// The balance between speech and background is hearing-impaired viewers' most common complaint, and
// "Clear speech" adds what helps hearing-aid users: compressed, louder speech over quieter extras.
//
//   commentary player → duck → commentary ─┐
//   answers player ────────────→ answers ──┼→ speech (compressor → makeup) ─┐
//   earcons, ball sound, crowd ────────────────────────────────────────────┼→ out → speakers
//
// Everything reaches `out`, which is also the reference for hands-free echo cancellation.

const RAMP_S = 0.05;

const NEUTRAL = { threshold: 0, knee: 0, ratio: 1, attack: 0.003, release: 0.25 };
const CLEAR = { threshold: -26, knee: 6, ratio: 4, attack: 0.003, release: 0.25 };
const CLEAR_MAKEUP = 1.6;
const CLEAR_EXTRAS = 0.5; // ball sound and crowd under clear speech
const GENTLE_SOUNDS = 0.5;

export function createMix(ctx) {
  const out = new GainNode(ctx);
  out.connect(ctx.destination);

  const makeup = new GainNode(ctx);
  makeup.connect(out);
  const compressor = new DynamicsCompressorNode(ctx, NEUTRAL);
  compressor.connect(makeup);

  const bus = (to) => {
    const gain = new GainNode(ctx);
    gain.connect(to);
    return gain;
  };

  const commentary = bus(compressor);
  // The companion turns this down while a question is asked and answered; the fan's level is separate.
  const duck = bus(commentary);
  const answers = bus(compressor);
  const earcons = bus(out);
  const ball = bus(out);
  const crowd = bus(out);

  const set = (param, value) => param.setTargetAtTime(value, ctx.currentTime, RAMP_S);

  /** Applies the fan's levels and presets (see prefs.js). */
  function apply(prefs) {
    const v = prefs.volumes;
    const extras = prefs.clearSpeech ? CLEAR_EXTRAS : 1;
    set(commentary.gain, v.commentary);
    set(answers.gain, v.answers);
    set(earcons.gain, v.earcons * (prefs.gentle ? GENTLE_SOUNDS : 1));
    set(ball.gain, v.ball * extras);
    set(crowd.gain, v.crowd * extras * (prefs.gentle ? GENTLE_SOUNDS : 1));
    const c = prefs.clearSpeech ? CLEAR : NEUTRAL;
    for (const [name, value] of Object.entries(c)) {
      set(compressor[name], value);
    }

    set(makeup.gain, prefs.clearSpeech ? CLEAR_MAKEUP : 1);
  }

  return { out, duck, answers, earcons, ball, crowd, apply };
}
