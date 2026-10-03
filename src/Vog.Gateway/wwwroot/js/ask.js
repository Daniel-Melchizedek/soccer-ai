// The match companion: hold-to-ask (push-to-talk) and typed questions, answered by the Foundry agent
// through the gateway. Commentary is muted while the fan speaks, so it can't reach the microphone, and
// turned down while the answer plays (BUILD_PLAN.md D4: push-to-talk, because browsers don't cancel
// the page's own audio from the microphone).

const DUCKED = 0.2;
const RAMP_S = 0.15;

const el = (id) => document.getElementById(id);

/**
 * Wires the companion controls to a joined connection.
 * @param {{ ctx: AudioContext, connection: any, duck: GainNode, setStatus: (text: string) => void, idleStatus: () => string }} deps
 * @returns {() => Promise<void>} a function that releases the microphone and controls
 */
export async function startCompanion({ ctx, connection, duck, setStatus, idleStatus }) {
  // The player worklet module is already loaded into ctx by app.js.
  const answers = new AudioWorkletNode(ctx, 'pcm-player', { numberOfInputs: 0, outputChannelCount: [1] });
  answers.connect(ctx.destination);

  let mic = null; // { ctx, stream, node } once the fan has first pressed the button
  let recording = false;
  let typed = false;
  let current = null;

  const setDuck = (level) => duck.gain.setTargetAtTime(level, ctx.currentTime, RAMP_S / 3);

  answers.port.onmessage = ({ data }) => {
    if (data.type === 'ended' && data.id === current && !recording) {
      setDuck(1);
      setStatus(idleStatus());
    }
  };

  connection.on('heard', (text) => (el('heard').textContent = `You asked: ${text}`));
  connection.on('answerStart', (id) => {
    current = id;
    el('answer').textContent = '';
    el('answer').setAttribute('aria-live', typed ? 'polite' : 'off');
    answers.port.postMessage({ type: 'begin', id, skipAfter: Infinity });
    setDuck(DUCKED);
    setStatus('Answering…');
  });
  connection.on('answerText', (_id, delta) => (el('answer').textContent += delta));
  connection.on('answerAudio', (id, _seq, data) => answers.port.postMessage({ type: 'chunk', id, samples: toFloat32(data) }));
  connection.on('answerEnd', (id) => answers.port.postMessage({ type: 'end', id }));
  connection.on('answerError', (message) => {
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
    el('talk').setAttribute('aria-pressed', 'true');
    answers.port.postMessage({ type: 'flush' });
    setDuck(0);
    try {
      mic ??= await openMicrophone();
      await connection.send('askBegin');
      mic.node.port.postMessage({ type: 'start' });
      setStatus('Listening to your question…');
    } catch (err) {
      recording = false;
      el('talk').setAttribute('aria-pressed', 'false');
      setDuck(1);
      setStatus(`Microphone unavailable: ${err.message ?? err}. You can type your question instead.`);
    }
  }

  function end() {
    if (!recording) {
      return;
    }

    recording = false;
    el('talk').setAttribute('aria-pressed', 'false');
    mic?.node.port.postMessage({ type: 'stop' });
    setDuck(DUCKED);
    setStatus('Thinking…');
  }

  async function openMicrophone() {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true, autoGainControl: true } });
    const micCtx = new AudioContext();
    await micCtx.audioWorklet.addModule('js/pcm-capture.worklet.js');
    const node = new AudioWorkletNode(micCtx, 'pcm-capture', { numberOfOutputs: 0 });
    micCtx.createMediaStreamSource(stream).connect(node);
    node.port.onmessage = ({ data }) => {
      if (data.type === 'chunk') {
        connection.send('askAudio', toBase64(data.samples)).catch(() => {});
      } else if (data.type === 'stopped') {
        connection.send('askEnd').catch(() => {});
      }
    };
    return { ctx: micCtx, stream, node };
  }

  const talk = el('talk');
  const onKeyDown = (e) => {
    if ((e.key === ' ' || e.key === 'Enter') && !e.repeat) {
      e.preventDefault();
      void begin();
    }
  };
  const onKeyUp = (e) => {
    if (e.key === ' ' || e.key === 'Enter') {
      e.preventDefault();
      end();
    }
  };
  const onPointerDown = (e) => {
    talk.setPointerCapture(e.pointerId);
    void begin();
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
    await connection.send('askText', question);
  };

  talk.addEventListener('keydown', onKeyDown);
  talk.addEventListener('keyup', onKeyUp);
  talk.addEventListener('pointerdown', onPointerDown);
  talk.addEventListener('pointerup', end);
  talk.addEventListener('pointercancel', end);
  el('ask-form').addEventListener('submit', onSubmit);
  for (const id of ['talk', 'question', 'ask-send']) {
    el(id).disabled = false;
  }

  return async () => {
    for (const id of ['talk', 'question', 'ask-send']) {
      el(id).disabled = true;
    }

    talk.removeEventListener('keydown', onKeyDown);
    talk.removeEventListener('keyup', onKeyUp);
    talk.removeEventListener('pointerdown', onPointerDown);
    talk.removeEventListener('pointerup', end);
    talk.removeEventListener('pointercancel', end);
    el('ask-form').removeEventListener('submit', onSubmit);
    mic?.stream.getTracks().forEach((t) => t.stop());
    await mic?.ctx.close();
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
