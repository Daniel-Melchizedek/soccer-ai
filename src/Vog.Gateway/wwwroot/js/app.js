// Listener page: joins a channel, plays its speech through an AudioWorklet, plays earcons, and shows
// word-timed captions. Audio never passes through the main thread's timers: the worklet owns timing.
import { playEarcon } from './earcons.js';

const SAMPLE_RATE = 24000;
const MAX_HISTORY = 50;
const CAPTION_HOLD_MS = 3000; // a caption stays this long after its words, then clears

/** Seconds behind the booth before a line is skipped; the same limits as the server's Staleness.cs. */
const SKIP_AFTER_S = { P0: Infinity, P1: 4 };
const SKIP_AFTER_DEFAULT_S = 1.5;

const el = (id) => document.getElementById(id);
const pending = new Map(); // utterance id -> message + { receivedAt, startedAt, cues }

let ctx = null;
let player = null;
let connection = null;
let speech = false;
let teams = null;
let captionTimers = [];

el('start').addEventListener('click', () => (connection ? stop() : start()));
el('lang').addEventListener('change', rejoin);
el('mode').addEventListener('change', rejoin);

async function start() {
  el('start').disabled = true;
  setStatus('Connecting…');

  try {
    // Created on the click: browsers only allow audio after a user gesture.
    ctx = new AudioContext({ sampleRate: SAMPLE_RATE, latencyHint: 'interactive' });
    await ctx.audioWorklet.addModule('js/pcm-player.worklet.js');
    player = new AudioWorkletNode(ctx, 'pcm-player', { numberOfInputs: 0, outputChannelCount: [1] });
    player.connect(ctx.destination);
    player.port.onmessage = (event) => onPlayer(event.data);

    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/commentary')
      .withAutomaticReconnect()
      .build();

    connection.on('utterance', onUtterance);
    connection.on('audio', (id, _seq, data) => player?.port.postMessage({ type: 'chunk', id, samples: toFloat32(data) }));
    connection.on('audioEnd', (id) => player?.port.postMessage({ type: 'end', id }));
    connection.on('cut', (id, afterMs) => player?.port.postMessage({ type: 'cut', id, afterSamples: Math.round((afterMs * SAMPLE_RATE) / 1000) }));
    connection.on('captions', onCaptions);
    connection.on('skipped', (id) => {
      pending.delete(id);
      player?.port.postMessage({ type: 'skip', id });
    });
    connection.on('state', showState);
    connection.onreconnecting(() => setStatus('Connection lost, reconnecting…'));
    connection.onreconnected(() => join());

    await connection.start();
    await join();
    el('start').textContent = 'Stop listening';
  } catch (err) {
    console.error(err);
    await stop();
    setStatus(`Could not start: ${err.message ?? err}`);
  } finally {
    el('start').disabled = false;
  }
}

async function stop() {
  const c = connection;
  connection = null;
  await c?.stop();
  await ctx?.close();
  ctx = null;
  player = null;
  reset();
  el('start').textContent = 'Start listening';
  setStatus('Not listening.');
}

async function join() {
  // Anything queued from the previous channel is no longer wanted.
  reset();
  player?.port.postMessage({ type: 'flush' });
  const result = await connection.invoke('join', el('lang').value, el('mode').value);
  speech = result.speech;
  teams = result;
  const channel = `${el('lang').selectedOptions[0].text}, ${el('mode').selectedOptions[0].text}`;
  setStatus(`Listening: ${channel}. ${result.home} in ${result.homeKit} v ${result.away} in ${result.awayKit}.${speech ? '' : ' Captions only: speech is not configured on the server.'}`);
}

async function rejoin() {
  if (connection) {
    await join();
  }
}

function reset() {
  pending.clear();
  captionTimers.forEach(clearTimeout);
  captionTimers = [];
}

function onUtterance(u) {
  const entry = { ...u, receivedAt: performance.now(), startedAt: null, cues: null };
  if (!speech) {
    // Captions only: show each line as the booth airs it.
    entry.startedAt = entry.receivedAt;
    playEarcon(ctx, u.earcon, u.pan);
    showCaptions(entry);
    addHistory(u);
    return;
  }

  pending.set(u.id, entry);
  player.port.postMessage({ type: 'begin', id: u.id, skipAfter: SKIP_AFTER_S[u.priority] ?? SKIP_AFTER_DEFAULT_S });
}

function onPlayer(message) {
  const u = pending.get(message.id);
  if (!u) {
    return;
  }

  if (message.type === 'started') {
    u.startedAt = performance.now();
    const delay = u.startedAt - u.receivedAt;
    connection?.invoke('played', message.id, delay).catch(() => {});
    el('latency').textContent = `Last line started ${Math.round(delay)} ms after the server sent it.`;
    playEarcon(ctx, u.earcon, u.pan);
    showCaptions(u);
    addHistory(u);
  } else if (message.type === 'ended') {
    pending.delete(message.id);
  } else if (message.type === 'skipped') {
    pending.delete(message.id);
    connection?.invoke('skipped', message.id).catch(() => {});
  }
}

// Word-timed cues arrive once synthesis finishes, usually while the line is already playing.
function onCaptions(id, cues) {
  const u = pending.get(id);
  if (u) {
    u.cues = cues;
    if (u.startedAt !== null) {
      showCaptions(u);
    }
  }
}

/** Shows the cue for the current point in the line, then schedules the rest. */
function showCaptions(u) {
  captionTimers.forEach(clearTimeout);
  captionTimers = [];
  const cues = u.cues?.length ? u.cues : [{ startMs: 0, endMs: u.scheduledMs, lines: [u.text] }];
  const elapsed = performance.now() - u.startedAt;
  const current = cues.findLast((cue) => cue.startMs <= elapsed) ?? cues[0];
  const show = (text) => (el('caption').textContent = text);
  show(current.lines.join('\n'));
  for (const cue of cues.filter((c) => c.startMs > elapsed)) {
    captionTimers.push(setTimeout(() => show(cue.lines.join('\n')), cue.startMs - elapsed));
  }

  const endMs = Math.max(...cues.map((c) => c.endMs ?? 0));
  captionTimers.push(setTimeout(() => show(''), Math.max(0, endMs - elapsed) + CAPTION_HOLD_MS));
}

function addHistory(u) {
  const item = document.createElement('li');
  item.textContent = `${u.clock} ${u.text}`;
  const history = el('history');
  history.prepend(item);
  while (history.children.length > MAX_HISTORY) {
    history.lastElementChild.remove();
  }
}

function showState(s) {
  el('score').textContent = teams ? `${teams.home} ${s.homeScore}, ${teams.away} ${s.awayScore}. ${s.clock}` : `${s.homeScore}–${s.awayScore}. ${s.clock}`;
}

function setStatus(text) {
  el('status').textContent = text;
}

/** SignalR's JSON protocol carries byte[] as base64: little-endian 16-bit PCM → floats. */
function toFloat32(base64) {
  const bytes = Uint8Array.from(atob(base64), (c) => c.codePointAt(0));
  const view = new DataView(bytes.buffer);
  const samples = new Float32Array(bytes.length >> 1);
  for (let i = 0; i < samples.length; i++) {
    samples[i] = view.getInt16(i * 2, true) / 32768;
  }

  return samples;
}
