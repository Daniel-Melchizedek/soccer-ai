// The match companion: spoken questions (tap to start and stop, or hold to talk) and typed questions,
// answered by the Foundry agent through the gateway. Holding a button is a barrier for many motor-
// impaired fans and impossible with eye-gaze dwell, so tapping is the default: tap, speak, and it stops
// by itself when you finish (or tap again). Commentary is silent while the fan speaks, so it can't
// reach the microphone, and turned down while the answer plays.
import { endCompanion, showCompanion } from './captions.js';
import { loadPrefs } from './prefs.js';
import { createCatchup, renderCatchup } from './catchup.js';

const DUCKED = 0.2;
const RAMP_S = 0.15;
const MAX_QUESTION_MS = 20_000;
const THINKING_TICK_MS = 700;
const THINKING_LIMIT_MS = 30_000;
const INTERACTION_LIMIT_MS = 120_000;

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
  let playbackId = null;
  let turn = 0;
  let active = null;
  let disposed = false;
  let blocked = false;
  let interactionTimer = null;
  const catchup = createCatchup();
  const handlers = new Map();
  const thinking = createThinkingSound(ctx, mix.answers);
  const talk = el('talk');
  const readouts = [...document.querySelectorAll('[data-readout]')];

  const setDuck = (level) => mix.duck.gain.setTargetAtTime(level, ctx.currentTime, RAMP_S / 3);

  const renderLabels = () => labelTalk(mode, recording);
  started = true;

  const on = (name, handler) => {
    handlers.set(name, handler);
    connection.on(name, handler);
  };
  const controls = () => {
    for (const id of ['question', 'ask-send', 'who-scored']) el(id).disabled = disposed || blocked || Boolean(active);
    for (const button of readouts) button.disabled = disposed || blocked || Boolean(active);
    talk.disabled = disposed || blocked || Boolean(active && !recording);
  };
  const release = () => {
    clearTimeout(interactionTimer);
    clearTimeout(maxTimer);
    recording = false;
    talk.setAttribute('aria-pressed', 'false');
    renderLabels();
    thinking.stop();
    current = null;
    playbackId = null;
    active = null;
    setDuck(1);
    endCompanion();
    controls();
  };
  const fail = (message) => {
    if (!active || disposed) return;
    blocked = active.sent;
    const result = catchup.finish('error');
    answers.port.postMessage({ type: 'flush' });
    mic?.node.port.postMessage({ type: 'stop' });
    if (blocked) connection.send('askStop').catch(() => {});
    release();
    el('answer').setAttribute('aria-live', 'polite');
    el('answer').textContent = message;
    renderCatchup(el('catchup'), result);
    setStatus(blocked ? 'Question interrupted. Stop and start listening to ask again. Commentary continues.' : idleStatus());
  };
  const beginInteraction = (isTyped) => {
    if (disposed || blocked || active) return null;
    typed = isTyped;
    active = { turn: ++turn, sent: false };
    catchup.begin(el('lang').value);
    renderCatchup(el('catchup'), null);
    answers.port.postMessage({ type: 'flush' });
    interactionTimer = setTimeout(() => fail('The question or answer timed out.'), INTERACTION_LIMIT_MS);
    controls();
    return active;
  };
  const cancel = () => {
    blocked = true;
    catchup.cancel();
    renderCatchup(el('catchup'), null);
    el('heard').textContent = '';
    el('answer').textContent = '';
    el('answer').setAttribute('aria-live', 'off');
    if (active?.sent) connection.send('askStop').catch(() => {});
    mic?.node.port.postMessage({ type: 'stop' });
    answers.port.postMessage({ type: 'flush' });
    release();
  };

  answers.port.onmessage = ({ data }) => {
    if (!disposed && active && data.type === 'ended' && data.id === playbackId && active.ended && !recording) {
      const result = catchup.finish();
      release();
      renderCatchup(el('catchup'), result);
      setStatus(idleStatus());
    }
  };

  on('utterance', (message) => { if (!disposed) catchup.receive(message); });
  on('heard', (text) => { if (active && !disposed) el('heard').textContent = `You asked: ${text}`; });
  on('answerStart', (id) => {
    if (disposed || !active?.sent || recording || current !== null) return;
    if ((id < 0) !== Boolean(active.readout)) return;
    thinking.stop();
    current = id;
    playbackId = `answer:${active.turn}:${id}`;
    el('answer').textContent = '';
    el('answer').setAttribute('aria-live', typed ? 'polite' : 'off');
    answers.port.postMessage({ type: 'begin', id: playbackId, skipAfter: Infinity });
    setDuck(DUCKED);
    setStatus('Answering…');
  });
  on('answerText', (id, delta) => {
    if (disposed || !active || id !== current || active.ended) return;
    el('answer').textContent += delta;
    showCompanion(el('answer').textContent);
  });
  on('answerAudio', (id, _seq, data) => {
    if (!disposed && active && id === current && !active.ended) answers.port.postMessage({ type: 'chunk', id: playbackId, samples: toFloat32(data) });
  });
  on('answerEnd', (id, status) => {
    if (disposed || !active || id !== current || active.ended) return;
    if (status && status !== 'completed') {
      fail(`Answer ${status}.`);
      return;
    }
    active.ended = true;
    answers.port.postMessage({ type: 'end', id: playbackId });
  });
  on('answerError', fail);

  async function begin() {
    const interaction = beginInteraction(false);
    if (!interaction) return;

    recording = true;
    controls();
    thinking.stop();
    talk.setAttribute('aria-pressed', 'true');
    renderLabels();
    answers.port.postMessage({ type: 'flush' });
    setDuck(0);
    try {
      mic ??= await openMicrophone();
      if (disposed || active !== interaction) {
        mic?.stream.getTracks().forEach((track) => track.stop());
        await mic?.ctx.close();
        mic = null;
        return;
      }
      interaction.sent = true;
      await connection.send('askBegin');
      if (disposed || active !== interaction) return;
      mic.node.port.postMessage({ type: 'start' });
      if (!recording) {
        mic.node.port.postMessage({ type: 'stop' });
        return;
      }
      maxTimer = setTimeout(end, MAX_QUESTION_MS);
      setStatus('Listening to your question…');
    } catch (err) {
      if (active === interaction) fail(`Microphone unavailable: ${err.message ?? err}.`);
    }
  }

  function end() {
    if (!recording) {
      return;
    }

    recording = false;
    controls();
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
    if (disposed || blocked) {
      stream.getTracks().forEach((track) => track.stop());
      throw new Error('Question cancelled.');
    }
    const micCtx = new AudioContext();
    try {
      await micCtx.audioWorklet.addModule('js/pcm-capture.worklet.js');
    } catch (error) {
      stream.getTracks().forEach((track) => track.stop());
      await micCtx.close();
      throw error;
    }
    const node = new AudioWorkletNode(micCtx, 'pcm-capture', { numberOfOutputs: 0 });
    micCtx.createMediaStreamSource(stream).connect(node);
    node.port.onmessage = ({ data }) => {
      if (disposed || blocked || !active?.sent) return;
      if (data.type === 'chunk') {
        connection.send('askAudio', toBase64(data.samples)).catch((error) => fail(`Question failed: ${error.message ?? error}`));
      } else if (data.type === 'silence' && mode === 'tap') {
        end(); // the fan has finished speaking
      } else if (data.type === 'stopped') {
        connection.send('askEnd').catch((error) => fail(`Question failed: ${error.message ?? error}`));
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
  const askText = async (question) => {
    if (!question) return;
    const interaction = beginInteraction(true);
    if (!interaction) return;
    el('heard').textContent = `You asked: ${question}`;
    el('question').value = '';
    setDuck(DUCKED);
    setStatus('Thinking…');
    thinking.start();
    interaction.sent = true;
    try {
      await connection.send('askText', question);
    } catch (error) {
      if (active === interaction) fail(`Question failed: ${error.message ?? error}`);
    }
  };
  const onSubmit = (event) => {
    event.preventDefault();
    return askText(el('question').value.trim());
  };
  const onWhoScored = () => {
    const questions = { 'en-GB': 'Who scored?', 'es-ES': '\u00bfQui\u00e9n ha marcado?', 'ta-IN': '\u0baf\u0bbe\u0bb0\u0bcd \u0b95\u0bcb\u0bb2\u0bcd \u0b85\u0b9f\u0bbf\u0ba4\u0bcd\u0ba4\u0bbe\u0bb0\u0bcd?' };
    return askText(questions[el('lang').value] ?? questions['en-GB']);
  };
  const onReadout = async (event) => {
    const interaction = beginInteraction(true);
    if (!interaction) return;
    interaction.readout = true;
    interaction.sent = true;
    setDuck(DUCKED);
    setStatus('Thinking…');
    thinking.start();
    try {
      await connection.invoke('readout', event.currentTarget.dataset.readout);
    } catch (error) {
      if (active === interaction) fail(`Readout failed: ${error.message ?? error}`);
    }
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
  el('who-scored').addEventListener('click', onWhoScored);
  for (const button of readouts) button.addEventListener('click', onReadout);
  thinking.setGentle(loadPrefs().gentle);
  renderLabels();
  controls();

  const cleanup = async () => {
    if (disposed) return;
    cancel();
    disposed = true;
    controls();
    for (const [name, handler] of handlers) connection.off(name, handler);
    answers.port.onmessage = null;
    answers.disconnect();
    talk.removeEventListener('click', onClick);
    talk.removeEventListener('pointerdown', onPointerDown);
    talk.removeEventListener('pointerup', onPointerUp);
    talk.removeEventListener('pointercancel', onPointerUp);
    talk.removeEventListener('keydown', onKeyDown);
    talk.removeEventListener('keyup', onKeyUp);
    document.removeEventListener('keydown', onShortcut);
    window.removeEventListener('vog:prefs', onPrefs);
    el('ask-form').removeEventListener('submit', onSubmit);
    el('who-scored').removeEventListener('click', onWhoScored);
    for (const button of readouts) button.removeEventListener('click', onReadout);
    started = false;
    mic?.stream.getTracks().forEach((t) => t.stop());
    await mic?.ctx.close();
  };
  cleanup.cancel = cancel;
  return cleanup;
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
