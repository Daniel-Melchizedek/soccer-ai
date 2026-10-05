// The match companion: spoken questions (tap to start and stop, or hold to talk) and typed questions,
// answered by the Foundry agent through the gateway. Holding a button is a barrier for many motor-
// impaired fans and impossible with eye-gaze dwell, so tapping is the default: tap, speak, and it stops
// by itself when you finish (or tap again). Commentary is silent while the fan speaks, so it can't
// reach the microphone, and turned down while the answer plays.
import { endCompanion, showCompanion } from './captions.js';
import { loadPrefs } from './prefs.js';

const DUCKED = 0.2;
const RAMP_S = 0.15;
const MAX_QUESTION_MS = 20_000;
const THINKING_TICK_MS = 700;
const THINKING_LIMIT_MS = 30_000;

const el = (id) => document.getElementById(id);

const LABELS = {
  tap: { idle: 'Tap to ask', recording: 'Stop asking', help: 'Tap the button (or press Space or Enter on it), ask, for example: "What\'s the score?", "Where\'s the ball?", "Who scored?". It stops by itself when you finish speaking, or tap again.' },
  hold: { idle: 'Hold to ask', recording: 'Listening… let go to finish', help: 'Hold the button (or hold Space or Enter on it) and ask, for example: "What\'s the score?", "Where\'s the ball?", "Who scored?". Let go when you\'ve finished.' },
};

let started = false;

function labelTalk(mode, recording) {
  const labels = LABELS[mode] ?? LABELS.tap;
  el('talk').querySelector('.talk-label').textContent = recording ? labels.recording : labels.idle;
  el('ask-help').textContent = labels.help;
}

// The button and its help match the chosen talk mode even before listening starts.
labelTalk(loadPrefs().talkMode, false);
window.addEventListener('vog:prefs', (event) => {
  if (!started) {
    labelTalk(event.detail.talkMode, false);
  }
});

/**
 * Wires the companion controls to a joined connection.
 * @param {{ ctx: AudioContext, connection: any, mix: ReturnType<import('./mix.js').createMix>, setStatus: (text: string) => void, idleStatus: () => string }} deps
 * @returns {() => Promise<void>} a function that releases the microphone and controls
 */
export async function startCompanion({ ctx, connection, mix, setStatus, idleStatus }) {
  // The player worklet module is already loaded into ctx by app.js.
  const answers = new AudioWorkletNode(ctx, 'pcm-player', { numberOfInputs: 0, outputChannelCount: [1] });
  answers.connect(mix.answers);

  let mode = loadPrefs().talkMode;
  let mic = null; // { ctx, stream, node } once the fan has first asked by voice
  let recording = false;
  let maxTimer = null;
  let typed = false;
  let current = null;
  const thinking = createThinkingSound(ctx, mix.answers);
  const talk = el('talk');

  const setDuck = (level) => mix.duck.gain.setTargetAtTime(level, ctx.currentTime, RAMP_S / 3);

  const renderLabels = () => labelTalk(mode, recording);
  started = true;

  answers.port.onmessage = ({ data }) => {
    if (data.type === 'ended' && data.id === current && !recording) {
      setDuck(1);
      endCompanion();
      setStatus(idleStatus());
    }
  };

  connection.on('heard', (text) => (el('heard').textContent = `You asked: ${text}`));
  connection.on('answerStart', (id) => {
    thinking.stop();
    current = id;
    el('answer').textContent = '';
    el('answer').setAttribute('aria-live', typed ? 'polite' : 'off');
    answers.port.postMessage({ type: 'begin', id, skipAfter: Infinity });
    setDuck(DUCKED);
    setStatus('Answering…');
  });
  connection.on('answerText', (_id, delta) => {
    el('answer').textContent += delta;
    showCompanion(el('answer').textContent);
  });
  connection.on('answerAudio', (id, _seq, data) => answers.port.postMessage({ type: 'chunk', id, samples: toFloat32(data) }));
  connection.on('answerEnd', (id) => answers.port.postMessage({ type: 'end', id }));
  connection.on('answerError', (message) => {
    thinking.stop();
    el('answer').setAttribute('aria-live', 'polite');
    el('answer').textContent = message;
    setDuck(1);
    setStatus(idleStatus());
  });

  async function begin() {
    if (recording) {
      return;
    }

    recording = true;
    typed = false;
    thinking.stop();
    talk.setAttribute('aria-pressed', 'true');
    renderLabels();
    answers.port.postMessage({ type: 'flush' });
    setDuck(0);
    try {
      mic ??= await openMicrophone();
      await connection.send('askBegin');
      mic.node.port.postMessage({ type: 'start' });
      maxTimer = setTimeout(end, MAX_QUESTION_MS);
      setStatus('Listening to your question…');
    } catch (err) {
      recording = false;
      talk.setAttribute('aria-pressed', 'false');
      renderLabels();
      setDuck(1);
      setStatus(`Microphone unavailable: ${err.message ?? err}. You can type your question instead.`);
    }
  }

  function end() {
    if (!recording) {
      return;
    }

    recording = false;
    clearTimeout(maxTimer);
    talk.setAttribute('aria-pressed', 'false');
    renderLabels();
    mic?.node.port.postMessage({ type: 'stop' });
    setDuck(DUCKED);
    setStatus('Thinking…');
    thinking.start();
  }

  const toggle = () => (recording ? end() : void begin());

  async function openMicrophone() {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true, autoGainControl: true } });
    const micCtx = new AudioContext();
    await micCtx.audioWorklet.addModule('js/pcm-capture.worklet.js');
    const node = new AudioWorkletNode(micCtx, 'pcm-capture', { numberOfOutputs: 0 });
    micCtx.createMediaStreamSource(stream).connect(node);
    node.port.onmessage = ({ data }) => {
      if (data.type === 'chunk') {
        connection.send('askAudio', toBase64(data.samples)).catch(() => {});
      } else if (data.type === 'silence' && mode === 'tap') {
        end(); // the fan has finished speaking
      } else if (data.type === 'stopped') {
        connection.send('askEnd').catch(() => {});
      }
    };
    return { ctx: micCtx, stream, node };
  }

  // Tap mode: a normal button (click covers mouse, touch, Space, Enter, switches and dwell clicks).
  const onClick = () => {
    if (mode === 'tap') {
      toggle();
    }
  };
  // Hold mode: press to start, release to finish.
  const onPointerDown = (e) => {
    if (mode === 'hold') {
      talk.setPointerCapture(e.pointerId);
      void begin();
    }
  };
  const onPointerUp = () => {
    if (mode === 'hold') {
      end();
    }
  };
  const onKeyDown = (e) => {
    if (mode === 'hold' && (e.key === ' ' || e.key === 'Enter') && !e.repeat) {
      e.preventDefault();
      void begin();
    }
  };
  const onKeyUp = (e) => {
    if (mode === 'hold' && (e.key === ' ' || e.key === 'Enter')) {
      e.preventDefault();
      end();
    }
  };
  // Alt+Shift+A from anywhere: start or stop asking, whatever the mode.
  const onShortcut = (e) => {
    if (e.altKey && e.shiftKey && e.code === 'KeyA' && !e.repeat) {
      e.preventDefault();
      toggle();
    }
  };
  const onSubmit = async (e) => {
    e.preventDefault();
    const question = el('question').value.trim();
    if (!question) {
      return;
    }

    typed = true;
    answers.port.postMessage({ type: 'flush' });
    el('heard').textContent = `You asked: ${question}`;
    el('question').value = '';
    setStatus('Thinking…');
    thinking.start();
    await connection.send('askText', question);
  };
  const onPrefs = (event) => {
    if (event.detail.talkMode !== mode) {
      end();
      mode = event.detail.talkMode;
      renderLabels();
    }

    thinking.setGentle(event.detail.gentle);
  };

  talk.addEventListener('click', onClick);
  talk.addEventListener('pointerdown', onPointerDown);
  talk.addEventListener('pointerup', onPointerUp);
  talk.addEventListener('pointercancel', onPointerUp);
  talk.addEventListener('keydown', onKeyDown);
  talk.addEventListener('keyup', onKeyUp);
  document.addEventListener('keydown', onShortcut);
  window.addEventListener('vog:prefs', onPrefs);
  el('ask-form').addEventListener('submit', onSubmit);
  thinking.setGentle(loadPrefs().gentle);
  renderLabels();
  for (const id of ['talk', 'question', 'ask-send']) {
    el(id).disabled = false;
  }

  return async () => {
    for (const id of ['talk', 'question', 'ask-send']) {
      el(id).disabled = true;
    }

    end();
    thinking.stop();
    talk.removeEventListener('click', onClick);
    talk.removeEventListener('pointerdown', onPointerDown);
    talk.removeEventListener('pointerup', onPointerUp);
    talk.removeEventListener('pointercancel', onPointerUp);
    talk.removeEventListener('keydown', onKeyDown);
    talk.removeEventListener('keyup', onKeyUp);
    document.removeEventListener('keydown', onShortcut);
    window.removeEventListener('vog:prefs', onPrefs);
    el('ask-form').removeEventListener('submit', onSubmit);
    started = false;
    mic?.stream.getTracks().forEach((t) => t.stop());
    await mic?.ctx.close();
  };
}

/**
 * A soft tick while the companion works, so a fan who can't see the screen isn't left in silence
 * wondering whether the question was heard. Stops when the answer starts, or after 30 s.
 */
function createThinkingSound(ctx, bus) {
  let interval = null;
  let limit = null;
  let level = 0.08;

  const tick = () => {
    const now = ctx.currentTime;
    const osc = new OscillatorNode(ctx, { type: 'sine', frequency: 880 });
    const gain = new GainNode(ctx, { gain: 0 });
    gain.gain.setValueAtTime(0, now);
    gain.gain.linearRampToValueAtTime(level, now + 0.005);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.06);
    osc.connect(gain).connect(bus);
    osc.start(now);
    osc.stop(now + 0.08);
  };

  const stop = () => {
    clearInterval(interval);
    clearTimeout(limit);
    interval = null;
  };

  return {
    start() {
      stop();
      tick();
      interval = setInterval(tick, THINKING_TICK_MS);
      limit = setTimeout(stop, THINKING_LIMIT_MS);
    },
    stop,
    setGentle(gentle) {
      level = gentle ? 0.04 : 0.08;
    },
  };
}

/** 16-bit PCM → base64 for the hub (SignalR's JSON protocol has no binary type). */
function toBase64(samples) {
  const bytes = new Uint8Array(samples.buffer, samples.byteOffset, samples.byteLength);
  let text = '';
  for (let i = 0; i < bytes.length; i += 0x8000) {
    text += String.fromCodePoint(...bytes.subarray(i, i + 0x8000));
  }

  return btoa(text);
}

/** base64 little-endian 16-bit PCM → floats for the player. */
function toFloat32(base64) {
  const bytes = Uint8Array.from(atob(base64), (c) => c.codePointAt(0));
  const view = new DataView(bytes.buffer);
  const samples = new Float32Array(bytes.length >> 1);
  for (let i = 0; i < samples.length; i++) {
    samples[i] = view.getInt16(i * 2, true) / 32768;
  }

  return samples;
}
