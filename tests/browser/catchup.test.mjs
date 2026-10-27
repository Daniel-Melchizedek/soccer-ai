import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { createContext, SourceTextModule } from 'node:vm';

const source = await readFile(new URL('../../src/Vog.Gateway/wwwroot/js/catchup.js', import.meta.url), 'utf8');
const { createCatchup, renderCatchup } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);
const utterance = (id, kind = 'Goal', text = `Received ${id}`) => ({ id, kind, text, clock: '12:34', priority: 'P2' });

test('captures only the real question interval, including answer playback', () => {
  let time = 10;
  const capture = createCatchup({ now: () => time });
  capture.receive(utterance('before'));
  capture.begin('es-ES');
  capture.receive(utterance('during-question'));
  time = 2000;
  capture.receive(utterance('during-playback'));
  time = 3010;
  const result = capture.finish();
  capture.receive(utterance('after'));
  assert.deepEqual(result.items.map(item => item.id), ['during-question', 'during-playback']);
  assert.equal(result.endedAt - result.startedAt, 3000);
  assert.equal(result.items[0].language, 'es-ES');
  assert.equal(capture.finish(), null);
});

test('filters routine backlog, deduplicates ids and identical moments, prioritizes goals and cards', () => {
  const capture = createCatchup();
  capture.begin('en-GB');
  for (let index = 0; index < 20; index++) capture.receive(utterance(`routine-${index}`, 'Pass'));
  for (let index = 0; index < 8; index++) capture.receive(utterance(`shot-${index}`, 'Shot'));
  capture.receive(utterance('goal'));
  capture.receive(utterance('goal'));
  capture.receive(utterance('duplicate-goal', 'Goal', 'Received goal'));
  capture.receive(utterance('card', 'YellowCard'));
  const result = capture.finish();
  assert.equal(result.items.length, 6);
  assert.deepEqual(result.items.slice(-2).map(item => item.id), ['goal', 'card']);
  assert.equal(result.truncated, true);
});

test('distinguishes empty, routine-only, errors, cancellation and a fresh interval', () => {
  const capture = createCatchup();
  capture.begin('en-GB');
  assert.equal(capture.finish().received, 0);
  capture.begin('en-GB');
  capture.receive(utterance('pass', 'Pass'));
  const result = capture.finish('error');
  assert.equal(result.outcome, 'error');
  assert.equal(result.received, 1);
  assert.equal(result.items.length, 0);
  capture.begin('en-GB');
  capture.receive(utterance('discard'));
  capture.cancel();
  assert.equal(capture.finish(), null);
  capture.begin('ta-IN');
  assert.equal(capture.finish().items.length, 0);
});

test('bounds retained text and deduplication state under sustained input', () => {
  const capture = createCatchup();
  capture.begin('en-GB');
  for (let index = 0; index < 2000; index++) capture.receive(utterance(`${index}`, 'Goal', `${index}${'x'.repeat(1000)}`));
  const result = capture.finish();
  assert.equal(result.items.length, 6);
  assert.ok(result.items.every(item => item.text.length <= 600));
  assert.equal(result.truncated, true);
});

class Element {
  constructor(document) { this.ownerDocument = document; this.children = []; this.listeners = new Map(); this.attributes = {}; this.value = ''; this.text = ''; }
  set textContent(value) { this.text = value; this.children = []; }
  get textContent() { return this.text + this.children.map(child => child.textContent).join(''); }
  set innerHTML(value) { throw new Error(`Unsafe HTML: ${value}`); }
  setAttribute(name, value) { this.attributes[name] = value; }
  getAttribute(name) { return this.attributes[name]; }
  append(...children) { this.children.push(...children); }
  replaceChildren(...children) { this.text = ''; this.children = children; }
  querySelector() { return this.label ??= new Element(this.ownerDocument); }
  addEventListener(name, handler) { if (!this.listeners.has(name)) this.listeners.set(name, new Set()); this.listeners.get(name).add(handler); }
  removeEventListener(name, handler) { this.listeners.get(name)?.delete(handler); }
  async emit(name, event = {}) { for (const handler of this.listeners.get(name) ?? []) await handler({ preventDefault() {}, currentTarget: this, ...event }); }
}

async function companionFixture({ microphone, talkMode = 'tap' } = {}) {
  const document = new Element();
  document.createElement = () => new Element(document);
  const ids = Object.fromEntries(['talk', 'ask-help', 'heard', 'answer', 'question', 'ask-send', 'ask-form', 'who-scored', 'lang', 'catchup'].map(id => [id, new Element(document)]));
  ids.lang.value = 'en-GB';
  ids.talk.setPointerCapture = () => {};
  document.getElementById = id => ids[id];
  const readouts = ['score', 'ball'].map(kind => Object.assign(new Element(document), { dataset: { readout: kind } }));
  document.querySelectorAll = () => readouts;
  const window = new Element(document);
  const timers = new Map();
  let nextTimer = 0;
  const nodes = [];
  class AudioNode {
    constructor(_context, name) {
      this.name = name;
      this.messages = [];
      this.port = { postMessage: message => this.messages.push(message) };
      this.gain = { setValueAtTime() {}, linearRampToValueAtTime() {}, exponentialRampToValueAtTime() {} };
      nodes.push(this);
    }
    connect(target) { return target; }
    disconnect() { this.disconnected = true; }
    start() {}
    stop() {}
  }
  class AudioContext {
    constructor() { this.currentTime = 0; this.audioWorklet = { addModule: async () => {} }; }
    createMediaStreamSource() { return new AudioNode(); }
    async close() { this.closed = true; }
  }
  const sent = [];
  const connection = new Element();
  connection.on = connection.addEventListener;
  connection.off = connection.removeEventListener;
  connection.send = async (...args) => { sent.push(args); };
  connection.invoke = async (...args) => { sent.push(args); };
  connection.receive = (name, ...args) => { for (const handler of connection.listeners.get(name) ?? []) handler(...args); };
  const context = createContext({ document, window, performance: { now: () => 1000 }, AudioWorkletNode: AudioNode, AudioContext, OscillatorNode: AudioNode, GainNode: AudioNode,
    navigator: { mediaDevices: { getUserMedia: microphone ?? (async () => ({ getTracks: () => [] })) } },
    setTimeout: (handler, delay) => { timers.set(++nextTimer, { handler, delay }); return nextTimer; }, clearTimeout: id => timers.delete(id),
    setInterval: () => ++nextTimer, clearInterval() {}, atob, btoa });
  const dependencies = {
    './catchup.js': source,
    './captions.js': 'export function endCompanion() {} export function showCompanion() {}',
    './prefs.js': `export function loadPrefs() { return { talkMode: ${JSON.stringify(talkMode)}, gentle: false }; }`,
  };
  const module = new SourceTextModule(await readFile(new URL('../../src/Vog.Gateway/wwwroot/js/ask.js', import.meta.url), 'utf8'), { context });
  await module.link(specifier => new SourceTextModule(dependencies[specifier], { context }));
  await module.evaluate();
  let status = '';
  const ducks = [];
  const cleanup = await module.namespace.startCompanion({ ctx: new AudioContext(), connection, mix: { answers: {}, duck: { gain: { setTargetAtTime: value => ducks.push(value) } } }, setStatus: value => { status = value; }, idleStatus: () => 'Listening' });
  return { ids, readouts, document, window, connection, sent, cleanup, timers, nodes, ducks, status: () => status, player: nodes[0],
    async ask() { ids.question.value = 'What happened?'; await ids['ask-form'].emit('submit'); },
    complete(id = 1) {
      connection.receive('answerStart', id);
      connection.receive('answerText', id, 'Actual answer');
      connection.receive('answerEnd', id, 'completed');
    },
    played() { const begin = nodes[0].messages.findLast(message => message.type === 'begin'); nodes[0].port.onmessage?.({ data: { type: 'ended', id: begin?.id } }); },
  };
}

test('typed lifecycle captures through playback end, never answerEnd alone, and blocks overlapping questions', async () => {
  const fixture = await companionFixture();
  await fixture.ask();
  fixture.connection.receive('utterance', utterance('question-goal'));
  await fixture.ask();
  assert.equal(fixture.sent.filter(([name]) => name === 'askText').length, 1);
  fixture.complete();
  assert.equal(fixture.ids.catchup.textContent, '');
  fixture.connection.receive('utterance', utterance('playback-card', 'Card'));
  fixture.played();
  assert.match(fixture.ids.catchup.textContent, /question-goal.*playback-card/);
  assert.equal(fixture.status(), 'Listening');
  assert.equal(fixture.ducks.at(-1), 1);
  assert.equal(fixture.ids.question.disabled, false);
  await fixture.cleanup();
});

test('who-scored shortcut starts the same capture and preserves source language', async () => {
  const fixture = await companionFixture();
  fixture.ids.lang.value = 'es-ES';
  await fixture.ids['who-scored'].emit('click');
  assert.equal(fixture.sent[0][0], 'askText');
  assert.equal(fixture.sent[0][1], '\u00bfQui\u00e9n ha marcado?');
  fixture.connection.receive('utterance', utterance('goal', 'Goal', '<img src=x onerror=alert(1)>'));
  fixture.complete();
  fixture.played();
  assert.match(fixture.ids.catchup.textContent, /Source language: es-ES/);
  const entry = fixture.ids.catchup.children[1].children[0];
  assert.equal(entry.lang, 'es-ES');
  assert.match(entry.textContent, /<img/);
  await fixture.cleanup();
});

test('talk captures before microphone permission resolves and ends only after answer playback', async () => {
  let grant;
  const fixture = await companionFixture({ microphone: () => new Promise(resolve => { grant = resolve; }) });
  await fixture.ids.talk.emit('click');
  fixture.connection.receive('utterance', utterance('permission-goal'));
  grant({ getTracks: () => [] });
  await new Promise(resolve => setImmediate(resolve));
  await fixture.ids.talk.emit('click');
  const capture = fixture.nodes.find(node => node.name === 'pcm-capture');
  capture.port.onmessage({ data: { type: 'stopped' } });
  assert.deepEqual(fixture.sent.map(([name]) => name), ['askBegin', 'askEnd']);
  fixture.complete();
  fixture.played();
  assert.match(fixture.ids.catchup.textContent, /permission-goal/);
  await fixture.cleanup();
});

test('successive holds keep the pressed button enabled until release', async () => {
  const fixture = await companionFixture({ talkMode: 'hold' });
  const disabledChanges = [];
  Object.defineProperty(fixture.ids.talk, 'disabled', {
    get() { return disabledChanges.at(-1) ?? false; },
    set(value) { disabledChanges.push(value); },
  });
  for (const pointerId of [1, 2]) {
    disabledChanges.length = 0;
    await fixture.ids.talk.emit('pointerdown', { pointerId });
    await new Promise(resolve => setImmediate(resolve));
    assert.ok(disabledChanges.every(value => value === false), 'Starting a hold must not disable its pointer-capture target');
    assert.equal(fixture.ids.talk.getAttribute('aria-pressed'), 'true');
    await fixture.ids.talk.emit('pointerup', { pointerId });
    const capture = fixture.nodes.find(node => node.name === 'pcm-capture');
    capture.port.onmessage({ data: { type: 'stopped' } });
    fixture.complete(pointerId);
    fixture.played();
    assert.equal(fixture.ids.talk.disabled, false);
    assert.equal(fixture.status(), 'Listening');
  }
  assert.equal(fixture.nodes.filter(node => node.name === 'pcm-capture').length, 1);
  assert.deepEqual(fixture.sent.map(([name]) => name), ['askBegin', 'askEnd', 'askBegin', 'askEnd']);
  await fixture.cleanup();
});

test('a second hold ends on lost pointer capture or focus and removes recovery handlers on cleanup', async () => {
  for (const [target, event] of [['talk', 'lostpointercapture'], ['talk', 'blur'], ['window', 'blur']]) {
    const fixture = await companionFixture({ talkMode: 'hold' });
    const finishTarget = target === 'window' ? fixture.window : fixture.ids.talk;
    for (const pointerId of [1, 2]) {
      await fixture.ids.talk.emit('pointerdown', { pointerId });
      await new Promise(resolve => setImmediate(resolve));
      if (pointerId === 1) await fixture.ids.talk.emit('pointerup', { pointerId });
      else await finishTarget.emit(event, { pointerId });
      assert.equal(fixture.ids.talk.getAttribute('aria-pressed'), 'false');
      const capture = fixture.nodes.find(node => node.name === 'pcm-capture');
      assert.equal(capture.messages.at(-1).type, 'stop');
      const stopCount = capture.messages.filter(message => message.type === 'stop').length;
      await fixture.ids.talk.emit('pointerup', { pointerId });
      assert.equal(capture.messages.filter(message => message.type === 'stop').length, stopCount);
      capture.port.onmessage({ data: { type: 'stopped' } });
      fixture.complete(pointerId);
      fixture.played();
      assert.equal(fixture.ids.talk.disabled, false);
    }
    assert.deepEqual(fixture.sent.map(([name]) => name), ['askBegin', 'askEnd', 'askBegin', 'askEnd']);
    await fixture.cleanup();
    assert.equal(finishTarget.listeners.get(event)?.size, 0);
  }
});

test('releasing hold before microphone permission resolves cancels without waiting for an answer', async () => {
  let grant;
  let stopped = false;
  const fixture = await companionFixture({ talkMode: 'hold', microphone: () => new Promise(resolve => { grant = resolve; }) });
  await fixture.ids.talk.emit('pointerdown', { pointerId: 1 });
  await fixture.ids.talk.emit('pointerup', { pointerId: 1 });
  assert.equal(fixture.ids.talk.getAttribute('aria-pressed'), 'false');
  assert.equal(fixture.ids.talk.disabled, false);
  assert.equal(fixture.status(), 'Listening');
  assert.equal(fixture.ducks.at(-1), 1);
  grant({ getTracks: () => [{ stop() { stopped = true; } }] });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(stopped, true);
  assert.deepEqual(fixture.sent, []);
  assert.equal(fixture.timers.size, 0);
  await fixture.cleanup();
});

test('cancellation rejects all late answer events and cleanup removes connection and DOM handlers', async () => {
  const fixture = await companionFixture();
  await fixture.ask();
  fixture.complete();
  const previous = fixture.player.port.onmessage;
  const playback = fixture.player.messages.find(message => message.type === 'begin').id;
  fixture.cleanup.cancel();
  fixture.connection.receive('utterance', utterance('stale'));
  fixture.complete(2);
  previous({ data: { type: 'ended', id: playback } });
  assert.equal(fixture.ids.catchup.textContent, '');
  assert.equal(fixture.ids.answer.textContent, '');
  assert.equal(fixture.ids.heard.textContent, '');
  assert.equal(fixture.ids.question.disabled, true);
  assert.equal(fixture.sent.at(-1)[0], 'askStop');
  await fixture.cleanup();
  for (const handlers of fixture.connection.listeners.values()) assert.equal(handlers.size, 0);
  assert.equal(fixture.ids['who-scored'].listeners.get('click').size, 0);
  assert.equal(fixture.player.disconnected, true);
});

test('send errors, answer errors and deadline produce partial catch-up and quarantine uncorrelated responses', async () => {
  for (const failure of ['send', 'answer', 'timeout', 'cancelled']) {
    const fixture = await companionFixture();
    if (failure === 'send') fixture.connection.send = async () => { throw new Error('offline'); };
    await fixture.ask();
    if (failure !== 'send') {
      fixture.connection.receive('utterance', utterance('partial-goal'));
      if (failure === 'answer') fixture.connection.receive('answerError', 'Unavailable');
      if (failure === 'timeout') [...fixture.timers.values()].find(timer => timer.delay === 120000).handler();
      if (failure === 'cancelled') {
        fixture.connection.receive('answerStart', 1);
        fixture.connection.receive('answerEnd', 1, 'cancelled');
      }
    }
    assert.match(fixture.ids.catchup.textContent, /partial interval/);
    assert.equal(fixture.ids.question.disabled, true);
    const text = fixture.ids.catchup.textContent;
    fixture.complete(55);
    fixture.played();
    assert.equal(fixture.ids.catchup.textContent, text);
    await fixture.cleanup();
  }
});

test('pending microphone acquisition cannot send a question or leak tracks after cleanup', async () => {
  let grant;
  let stopped = false;
  const fixture = await companionFixture({ microphone: () => new Promise(resolve => { grant = resolve; }) });
  await fixture.ids.talk.emit('click');
  await fixture.cleanup();
  grant({ getTracks: () => [{ stop() { stopped = true; } }] });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(stopped, true);
  assert.equal(fixture.sent.length, 0);
  assert.equal(fixture.ids.catchup.textContent, '');
});

test('existing readouts still play and cannot claim a companion question', async () => {
  const fixture = await companionFixture();
  await fixture.readouts[0].emit('click');
  assert.deepEqual(fixture.sent[0], ['readout', 'score']);
  fixture.connection.receive('answerStart', 10);
  assert.equal(fixture.player.messages.some(message => message.type === 'begin'), false);
  fixture.complete(-1);
  fixture.played();
  assert.match(fixture.ids.catchup.textContent, /No commentary received/);
  await fixture.ask();
  fixture.connection.receive('answerStart', -2);
  fixture.connection.receive('answerText', -2, 'Wrong readout');
  fixture.complete(11);
  fixture.played();
  assert.equal(fixture.ids.answer.textContent, 'Actual answer');
  await fixture.cleanup();
  for (const button of fixture.readouts) assert.equal(button.listeners.get('click').size, 0);
});

test('bounded dedupe history still admits a later goal ahead of shots', () => {
  const capture = createCatchup();
  capture.begin('en-GB');
  for (let index = 0; index < 1000; index++) capture.receive(utterance(`shot-${index}`, 'Shot'));
  capture.receive(utterance('late-goal'));
  const result = capture.finish();
  assert.ok(result.items.some(item => item.id === 'late-goal'));
  assert.equal(result.truncated, true);
});

async function appFixture() {
  const document = new Element();
  const ids = new Map();
  document.getElementById = id => {
    if (!ids.has(id)) ids.set(id, Object.assign(new Element(document), { selectedOptions: [{ text: id }], firstElementChild: { style: {} } }));
    return ids.get(id);
  };
  document.createElement = () => new Element(document);
  document.querySelectorAll = selector => selector === '[data-channel-lang]' ? [] : [document.getElementById('who-scored')];
  document.getElementById('lang').value = 'en-GB';
  document.getElementById('mode').value = 'Commentary';
  document.getElementById('history').prepend = () => {};
  const companions = [];
  const connections = [];
  const players = [];
  const startCompanion = async () => {
    const companion = { cancels: 0, cleanups: 0 };
    companions.push(companion);
    const cleanup = async () => { companion.cleanups++; };
    cleanup.cancel = () => { companion.cancels++; };
    return cleanup;
  };
  class Connection {
    constructor() { this.handlers = new Map(); this.calls = []; connections.push(this); }
    on(name, handler) { this.handlers.set(name, handler); }
    onreconnecting(handler) { this.reconnecting = handler; }
    onreconnected(handler) { this.reconnected = handler; }
    onclose(handler) { this.closed = handler; }
    async start() {}
    async stop() { this.stopped = true; this.closed?.(); }
    async invoke(...args) {
      this.calls.push(args);
      return this.joinResult ? await this.joinResult : { home: 'Home', away: 'Away', homeKit: 'red', awayKit: 'blue' };
    }
  }
  class Builder {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    build() { return new Connection(); }
  }
  class AudioContext {
    constructor() { this.audioWorklet = { addModule: async () => {} }; }
    async resume() {}
    async close() {}
  }
  class Player {
    constructor() { this.messages = []; this.port = { postMessage: message => this.messages.push(message) }; players.push(this); }
    connect() {}
  }
  const context = createContext({ document, window: new Element(), startCompanion, signalR: { HubConnectionBuilder: Builder }, AudioContext, AudioWorkletNode: Player,
    performance: { now: () => 1000 }, fetch: async () => ({ ok: true, json: async () => [] }), console, atob });
  const stubs = {
    './ask.js': 'export const startCompanion = globalThis.startCompanion;',
    './captions.js': 'export function resetCaptions() {} export function showCommentary() {}',
    './earcons.js': 'export function playEarcon() {}',
    './haptics.js': 'export function createHaptics() { return { configure() {}, play() {} }; }',
    './crowd.js': 'export function createCrowdBed() { return { update() {}, dispose() {} }; }',
    './mix.js': 'export function createMix() { return { apply() {}, duck: {}, earcons: {} }; }',
    './prefs.js': 'export function loadPrefs() { return {}; }',
    './pitch.js': 'export function createPitchMap() { return { setTeams() {}, update() {} }; }',
    './sonify.js': 'export function createBallSonifier() { return { setEnabled() {}, dispose() {} }; }',
    './settings.js': '',
  };
  const module = new SourceTextModule(await readFile(new URL('../../src/Vog.Gateway/wwwroot/js/app.js', import.meta.url), 'utf8'), { context });
  await module.link(specifier => new SourceTextModule(stubs[specifier], { context }));
  await module.evaluate();
  return { ids, connections, companions, players, click: () => document.getElementById('start').emit('click'), change: () => document.getElementById('lang').emit('change') };
}

test('app channel changes dispose the old companion and use a fresh transport; stale messages cannot enter it', async () => {
  const fixture = await appFixture();
  await fixture.click();
  assert.equal(fixture.companions.length, 1);
  const oldConnection = fixture.connections[0];
  fixture.ids.get('lang').value = 'es-ES';
  await fixture.change();
  assert.equal(oldConnection.stopped, true);
  assert.equal(fixture.companions[0].cancels, 1);
  assert.equal(fixture.companions[0].cleanups, 1);
  assert.equal(fixture.connections.length, 2);
  assert.equal(fixture.connections[1].calls[0][1], 'es-ES');
  const currentPlayer = fixture.players.at(-1);
  const count = currentPlayer.messages.length;
  oldConnection.handlers.get('utterance')(utterance('old'));
  oldConnection.handlers.get('audioEnd')('old');
  assert.equal(currentPlayer.messages.length, count);
  await fixture.click();
  assert.equal(fixture.companions[1].cleanups, 1);
});

test('reconnect cancels immediately and replaces companion only after joining', async () => {
  const fixture = await appFixture();
  await fixture.click();
  const connection = fixture.connections[0];
  connection.reconnecting();
  assert.equal(fixture.companions[0].cancels, 1);
  assert.equal(fixture.ids.get('who-scored').disabled, true);
  await connection.reconnected();
  assert.equal(fixture.companions[0].cleanups, 1);
  assert.equal(fixture.companions.length, 2);
  assert.equal(connection.calls.filter(([name]) => name === 'join').length, 2);
  await fixture.click();
});

test('stop during a pending reconnect join cannot resurrect companion or controls', async () => {
  const fixture = await appFixture();
  await fixture.click();
  const connection = fixture.connections[0];
  connection.reconnecting();
  let finishJoin;
  connection.joinResult = new Promise(resolve => { finishJoin = resolve; });
  const reconnect = connection.reconnected();
  await new Promise(resolve => setImmediate(resolve));
  await fixture.click();
  finishJoin({ home: 'Stale', away: 'Old', homeKit: 'red', awayKit: 'blue' });
  await reconnect;
  assert.equal(fixture.companions.length, 1);
  assert.equal(fixture.ids.get('status').textContent, 'Not listening.');
  assert.equal(fixture.ids.get('who-scored').disabled, true);
});

test('microphone denial ends the interval without blocking a subsequent typed question', async () => {
  const fixture = await companionFixture({ microphone: async () => { throw new Error('Permission denied'); } });
  await fixture.ids.talk.emit('click');
  await new Promise(resolve => setImmediate(resolve));
  assert.match(fixture.ids.answer.textContent, /Permission denied/);
  assert.match(fixture.ids.catchup.textContent, /partial interval/);
  assert.equal(fixture.ids.question.disabled, false);
  await fixture.ask();
  fixture.complete();
  fixture.played();
  assert.match(fixture.ids.catchup.textContent, /No commentary received/);
  await fixture.cleanup();
});

test('hold-to-talk captures from key down and releases on key up', async () => {
  const fixture = await companionFixture();
  await fixture.window.emit('vog:prefs', { detail: { talkMode: 'hold', gentle: false } });
  await fixture.ids.talk.emit('keydown', { key: ' ', repeat: false });
  fixture.connection.receive('utterance', utterance('hold-goal'));
  await new Promise(resolve => setImmediate(resolve));
  await fixture.ids.talk.emit('keyup', { key: ' ' });
  fixture.nodes.find(node => node.name === 'pcm-capture').port.onmessage({ data: { type: 'stopped' } });
  fixture.complete();
  fixture.played();
  assert.match(fixture.ids.catchup.textContent, /hold-goal/);
  assert.deepEqual(fixture.sent.map(([name]) => name), ['askBegin', 'askEnd']);
  await fixture.cleanup();
});