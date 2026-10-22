// Listener page: joins a channel, plays its speech through an AudioWorklet, plays earcons, and shows
// word-timed captions. Audio never passes through the main thread's timers: the worklet owns timing.
import { startCompanion } from './ask.js';
import { resetCaptions, showCommentary } from './captions.js';
import { playEarcon } from './earcons.js';
import { createHaptics } from './haptics.js';
import { createCrowdBed } from './crowd.js';
import { createMix } from './mix.js';
import { loadPrefs } from './prefs.js';
import { createPitchMap } from './pitch.js';
import { createBallSonifier } from './sonify.js';
import './settings.js';

const SAMPLE_RATE = 24000;
const MAX_HISTORY = 50;

/** Seconds behind the booth before a line is skipped; the same limits as the server's Staleness.cs. */
const SKIP_AFTER_S = { P0: Infinity, P1: 4 };
const SKIP_AFTER_DEFAULT_S = 1.5;

const el = (id) => document.getElementById(id);
const pending = new Map(); // utterance id -> message + { receivedAt, startedAt, cues }
const haptics = createHaptics();
const pitchMap = createPitchMap({
  canvas: el('pitch-map'),
  description: el('pitch-description'),
  zoomIn: el('pitch-zoom-in'),
  zoomOut: el('pitch-zoom-out'),
  follow: el('pitch-follow'),
  homeKey: el('pitch-home-key'),
  awayKey: el('pitch-away-key'),
});
const atmosphere = el('atmosphere-meter');
const atmosphereText = el('atmosphere-text');
if (haptics.available) {
  el('haptics-setting').hidden = false;
}

let ctx = null;
let mix = null;
let sonifier = null;
let crowdBed = null;
let audioSetup = null;
let player = null;
let connection = null;
let stopCompanion = null;
let listeningStatus = '';
let teams = null;

el('start').addEventListener('click', () => (connection ? stop() : start()));
el('lang').addEventListener('change', rejoin);
el('mode').addEventListener('change', rejoin);
document.querySelectorAll('[data-readout]').forEach((button) => {
  button.addEventListener('click', () => {
    connection?.invoke('readout', button.dataset.readout).catch((err) => setStatus(`Readout failed: ${err.message ?? err}`));
  });
});
el('who-scored').addEventListener('click', () => {
  const questions = {
    'en-GB': 'Who scored?',
    'es-ES': '¿Quién ha marcado?',
    'ta-IN': 'யார் கோல் அடித்தார்?',
  };
  connection?.send('askText', questions[el('lang').value]).catch((err) => setStatus(`Question failed: ${err.message ?? err}`));
});
window.addEventListener('vog:prefs', (event) => {
  mix?.apply(event.detail);
  sonifier?.setEnabled(event.detail.sonifyBall);
  haptics.configure(event.detail);
});
el('learn-sounds').addEventListener('click', async () => {
  try {
    await prepareAudio();
    await ctx.resume();
    sonifier.demo();
  } catch (err) {
    setStatus(`Could not play the sound demo: ${err.message ?? err}`);
  }
});

async function start() {
  el('start').disabled = true;
  setStatus('Connecting…');

  try {
    await prepareAudio();
    await ctx.resume();
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
    connection.on('pitch', (message) => {
      sonifier?.update(message);
      crowdBed?.update(message.e);
      showAtmosphere(message.e);
      pitchMap.update(message);
    });
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

    // Stadium entry confirmation: blind users need immediate audio feedback that the match is live.
    // A whistle earcon + gentle crowd swell tells them the connection succeeded before any booth
    // utterance arrives. Both route through mix buses so fan volume controls apply.
    playEarcon(ctx, 'whistle', 0, mix.earcons);
    crowdBed.update(0.25);

    // Add a sound label to the history log so Deaf/HoH users see the same context.
    const stadiumEntry = document.createElement('li');
    stadiumEntry.textContent = '[CROWD RISES]';
    el('history').prepend(stadiumEntry);
  } catch (err) {
    console.error(err);
    await stop();
    setStatus(`Could not start: ${err.message ?? err}`);
  } finally {
    el('start').disabled = false;
  }
}

async function prepareAudio() {
  if (sonifier) {
    return;
  }

  if (!audioSetup) {
    audioSetup = (async () => {
      ctx = new AudioContext({ sampleRate: SAMPLE_RATE, latencyHint: 'interactive' });
      try {
        await ctx.audioWorklet.addModule('js/pcm-player.worklet.js');
        mix = createMix(ctx);
        crowdBed = createCrowdBed(ctx, mix.crowd);
        const prefs = loadPrefs();
        mix.apply(prefs);
        haptics.configure(prefs);
        sonifier = createBallSonifier(ctx, mix.ball);
        sonifier.setEnabled(prefs.sonifyBall);
      } catch (error) {
        await ctx.close();
        ctx = null;
        mix = null;
        throw error;
      }
    })();
  }

  try {
    await audioSetup;
  } finally {
    audioSetup = null;
  }
}

async function stop() {
  await stopCompanion?.();
  stopCompanion = null;
  const c = connection;
  connection = null;
  await c?.stop();
  sonifier?.dispose();
  sonifier = null;
  crowdBed?.dispose();
  crowdBed = null;
  await ctx?.close();
  ctx = null;
  mix = null;
  player = null;
  reset();
  document.querySelectorAll('[data-readout], #who-scored').forEach((button) => (button.disabled = true));
  el('start').textContent = 'Start listening';
  setStatus('Not listening.');
}

async function join() {
  // Anything queued from the previous channel is no longer wanted.
  reset();
  player?.port.postMessage({ type: 'flush' });
  const result = await connection.invoke('join', el('lang').value, el('mode').value, false);
  teams = result;
  pitchMap.setTeams(result);
  document.querySelectorAll('[data-readout], #who-scored').forEach((button) => (button.disabled = false));
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
    haptics.play(u.earcon);
    if (u.earcon === 'goal') {
      crowdBed?.pulse();
    }
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
  showCommentary(cues, u.startedAt, u.sound);
}

function addHistory(u) {
  const item = document.createElement('li');
  item.textContent = [u.clock, u.sound, u.text].filter(Boolean).join(' ');
  const history = el('history');
  history.prepend(item);
  while (history.children.length > MAX_HISTORY) {
    history.lastElementChild.remove();
  }
}

/** Kit colours for the scoreboard markers (decorative: team names carry the meaning). */
const KIT_COLOURS = { red: '#e53935', blue: '#1e63ff', white: '#ffffff', yellow: '#ffd400', green: '#1faa59', claret: '#7b1e3a', sky_blue: '#64c8ff', orange: '#ff8a2a', black: '#111111', purple: '#7e3ff2' };

// Track BallInPlay to fire a kick-off whistle when the match starts.
let lastBallInPlay = false;

function showState(s) {
  el('match-speed').textContent = s.speed > 1 ? ` | ${s.speed}x match clock` : '';

  // Kick-off whistle: fires when ball transitions from dead to in-play (e.g., kick-off).
  if (s.ballInPlay && !lastBallInPlay && ctx && mix) {
    playEarcon(ctx, 'whistle', 0, mix.earcons);
  }
  lastBallInPlay = Boolean(s.ballInPlay);

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

function showAtmosphere(value) {
  const energy = Math.max(0, Math.min(1, Number(value) || 0));
  atmosphere.setAttribute('aria-valuenow', String(Math.round(energy * 100)));
  atmosphere.firstElementChild.style.width = `${energy * 100}%`;
  const bands = {
    'en-GB': ['Quiet', 'Building', 'Roaring'],
    'es-ES': ['En calma', 'En aumento', 'Ruge'],
    'ta-IN': ['அமைதி', 'உற்சாகம் கூடுகிறது', 'ஆர்ப்பரிப்பு'],
  }[el('lang').value];
  atmosphereText.textContent = bands[energy >= 0.72 ? 2 : energy >= 0.35 ? 1 : 0];
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

// ---- Recent match history ----
// Fetches the last 10 match summaries from /history (Azure Blob Storage archive).
// The <ul> has aria-live="polite" so screen readers announce new results.
async function loadHistory() {
  const list = el('recent-matches');
  if (!list) return;
  try {
    const res = await fetch('/history');
    if (!res.ok) {
      list.innerHTML = '<li>No match history available.</li>';
      return;
    }
    const summaries = await res.json();
    list.innerHTML = '';
    if (!summaries.length) {
      list.innerHTML = '<li>No matches recorded yet.</li>';
      return;
    }
    for (const s of summaries) {
      const item = document.createElement('li');
      const date = new Date(s.utcDate).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
      // Plain text: screen-reader-friendly, no icon-only content.
      item.textContent = `${s.homeName} ${s.homeScore} – ${s.awayScore} ${s.awayName} · ${date}`;
      list.appendChild(item);
    }
  } catch {
    // Silently ignore: history is supplementary, never breaks the live experience.
  }
}

loadHistory();
