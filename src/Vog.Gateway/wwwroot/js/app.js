// Listener page: joins a channel, plays its speech through an AudioWorklet, plays earcons, and shows
// word-timed captions. Audio never passes through the main thread's timers: the worklet owns timing.
import { startCompanion } from './ask.js';
import { resetCaptions, showCommentary } from './captions.js';
import { playEarcon } from './earcons.js';
import { createMix } from './mix.js';
import { loadPrefs } from './prefs.js';
import './settings.js';

const SAMPLE_RATE = 24000;
const MAX_HISTORY = 50;

/** Seconds behind the booth before a line is skipped; the same limits as the server's Staleness.cs. */
const SKIP_AFTER_S = { P0: Infinity, P1: 4 };
const SKIP_AFTER_DEFAULT_S = 1.5;

const el = (id) => document.getElementById(id);
const pending = new Map(); // utterance id -> message + { receivedAt, startedAt, cues }

let ctx = null;
let mix = null;
let player = null;
let connection = null;
let stopCompanion = null;
let listeningStatus = '';
let teams = null;

el('start').addEventListener('click', () => (connection ? stop() : start()));
el('lang').addEventListener('change', rejoin);
el('mode').addEventListener('change', rejoin);
window.addEventListener('vog:prefs', (event) => mix?.apply(event.detail));

async function start() {
  el('start').disabled = true;
  setStatus('Connecting…');

  try {
    // Created on the click: browsers only allow audio after a user gesture.
    ctx = new AudioContext({ sampleRate: SAMPLE_RATE, latencyHint: 'interactive' });
    await ctx.audioWorklet.addModule('js/pcm-player.worklet.js');
    mix = createMix(ctx);
    mix.apply(loadPrefs());
    player = new AudioWorkletNode(ctx, 'pcm-player', { numberOfInputs: 0, outputChannelCount: [1] });
    player.connect(mix.duck);
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
    stopCompanion = await startCompanion({ ctx, connection, mix, setStatus, idleStatus: () => listeningStatus });
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
  await stopCompanion?.();
  stopCompanion = null;
  const c = connection;
  connection = null;
  await c?.stop();
  await ctx?.close();
  ctx = null;
  mix = null;
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
  teams = result;
  // Captions, the log and answers are in the channel's language: screen readers and fonts need to know.
  document.querySelectorAll('[data-channel-lang]').forEach((e) => (e.lang = el('lang').value));
  const channel = `${el('lang').selectedOptions[0].text}, ${el('mode').selectedOptions[0].text}`;
  listeningStatus = `Listening: ${channel}. ${result.home} in ${result.homeKit} v ${result.away} in ${result.awayKit}.`;
  setStatus(listeningStatus);
}

async function rejoin() {
  if (connection) {
    await join();
  }
}

function reset() {
  pending.clear();
  resetCaptions();
}

function onUtterance(u) {
  pending.set(u.id, { ...u, receivedAt: performance.now(), startedAt: null, cues: null });
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
    playEarcon(ctx, u.earcon, u.pan, mix.earcons);
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

function showCaptions(u) {
  const cues = u.cues?.length ? u.cues : [{ startMs: 0, endMs: u.scheduledMs, lines: [u.text] }];
  showCommentary(cues, u.startedAt);
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

/** Kit colours for the scoreboard markers (decorative: team names carry the meaning). */
const KIT_COLOURS = { red: '#e53935', blue: '#1e63ff', white: '#ffffff', yellow: '#ffd400', green: '#1faa59', claret: '#7b1e3a', sky_blue: '#64c8ff', orange: '#ff8a2a', black: '#111111', purple: '#7e3ff2' };

function showState(s) {
  const part = (text, className) => Object.assign(document.createElement('span'), { textContent: text, className });
  const team = (name, kit) => {
    const span = part(name ?? '', 'team');
    const marker = part('', 'kit');
    marker.setAttribute('aria-hidden', 'true');
    marker.style.background = KIT_COLOURS[kit] ?? 'transparent';
    span.prepend(marker);
    return span;
  };

  el('score').replaceChildren(
    team(teams?.home ?? 'Home', teams?.homeKit),
    part(`${s.homeScore} – ${s.awayScore}`, 'goals'),
    team(teams?.away ?? 'Away', teams?.awayKit),
    part(s.clock, 'clock'));
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
