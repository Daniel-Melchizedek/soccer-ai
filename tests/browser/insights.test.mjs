import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { createContext, SourceTextModule } from 'node:vm';

const copySource = await readFile(new URL('../../src/Vog.Gateway/wwwroot/js/insights-copy.js', import.meta.url), 'utf8');
const rendererSource = await readFile(new URL('../../src/Vog.Gateway/wwwroot/js/insights.js', import.meta.url), 'utf8');
const { recapCopy } = await import(`data:text/javascript;base64,${Buffer.from(copySource).toString('base64')}`);

test('all recap locales preserve scope keys and copy contracts', () => {
  const english = recapCopy('en-GB').copy;
  for (const language of ['en-GB', 'es-ES', 'ta-IN']) {
    const { copy } = recapCopy(language);
    assert.deepEqual(Object.keys(copy), Object.keys(english));
    assert.deepEqual(Object.keys(copy.scopes), ['recent', 'match', 'halftime', 'fulltime']);
    assert.equal(copy.columns.length, 4);
    assert.match(copy.clock('45:00', copy.scopes.halftime), /45:00/);
    assert.match(copy.evidence('event-123', 2, 2700000), /event-123.*2.*2700000/);
    assert.notEqual(copy.casualNote(5, true), copy.casualNote(5, false));
  }
});

test('draft translations disclose native review and English server text', () => {
  assert.match(recapCopy('es-ES').copy.disclosure, /borrador.*hablantes nativos.*inglés/);
  assert.match(recapCopy('ta-IN').copy.disclosure, /வரைவு.*தாய்மொழி.*ஆங்கில/);
  assert.match(recapCopy('en-GB').copy.disclosure, /remain in English/);
});

test('DOM language aliases and unsupported languages resolve truthfully', () => {
  assert.equal(recapCopy('es').language, 'es-ES');
  assert.equal(recapCopy('ta').language, 'ta-IN');
  assert.equal(recapCopy('en').language, 'en-GB');
  assert.equal(recapCopy('fr-FR').language, 'en-GB');
});

class Element {
  constructor(tagName = 'div') {
    this.tagName = tagName;
    this.children = [];
    this.attributes = {};
    this.listeners = {};
    this.selectors = {};
    this.style = {};
    this.text = '';
  }
  set textContent(value) {
    this.children = [];
    this.text = value == null ? '' : String(value);
  }
  get textContent() { return this.text + this.children.map(child => child.textContent).join(''); }
  set innerHTML(value) { throw new Error(`Unsafe HTML assignment: ${value}`); }
  append(...nodes) {
    for (let node of nodes) {
      if (typeof node === 'string') {
        const text = new Element('#text');
        text.textContent = node;
        node = text;
      }
      node.parentElement = this;
      this.children.push(node);
    }
  }
  replaceChildren(...nodes) { this.textContent = ''; this.append(...nodes); }
  get nextSibling() { return this.parentElement.children[this.parentElement.children.indexOf(this) + 1]; }
  setAttribute(name, value) { this.attributes[name] = String(value); }
  getAttribute(name) { return this.attributes[name]; }
  addEventListener(name, listener) { (this.listeners[name] ??= []).push(listener); }
  async emit(name) { for (const listener of this.listeners[name] ?? []) await listener(); }
  contains(node) { return this === node || this.children.some(child => child.contains(node)); }
  querySelectorAll(selector) {
    if (selector in this.selectors) return this.selectors[selector];
    if (selector === 'details[open]') return descendants(this).filter(node => node.tagName === 'details' && node.open);
    throw new Error(`Unimplemented test selector: ${selector}`);
  }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
}

function descendants(node) {
  return node.children.flatMap(child => [child, ...descendants(child)]);
}

function fixture() {
  const document = new Element('document');
  document.documentElement = new Element('html');
  document.documentElement.lang = 'en';
  document.hidden = false;
  document.activeElement = null;
  document.createElement = tag => new Element(tag);
  const ids = Object.fromEntries([
    'match-insights', 'insights-heading', 'insights-metrics', 'insights-highlights',
    'insights-teams', 'insights-score', 'insights-limitations',
    'insights-status', 'recap-scope', 'refresh-insights', 'lang', 'insights-chart', 'insights-chart-frame'
  ].map(id => [id, new Element()]));
  document.getElementById = id => ids[id] ?? null;
  const panel = ids['match-insights'];
  panel.append(...Object.values(ids).filter(node => node !== panel && node !== ids.lang));
  const radios = ['casual', 'analyst'].map(value => {
    const label = new Element('label');
    const input = new Element('input');
    input.value = value;
    label.append(input, ` ${value}`);
    panel.append(label);
    document.selectors[`input[name="insight-view"][value="${value}"]`] = [input];
    return input;
  });
  document.selectors['input[name="insight-view"]'] = radios;
  for (const selector of ['legend', 'label[for="recap-scope"]', ':scope > p:last-child']) {
    const node = new Element();
    panel.selectors[selector] = [node];
    panel.append(node);
  }
  ids['recap-scope'].options = ['recent', 'match', 'halftime', 'fulltime'].map(value => {
    const option = new Element('option');
    option.value = value;
    return option;
  });
  ids['recap-scope'].value = 'recent';
  ids.lang.value = 'en-GB';
  return { document, ids, panel, radios };
}

function facts(count = 45) {
  return {
    status: 'available', clock: '47:12', scope: 'recent', matchId: 'match-42',
    score: { home: 2, away: 1 }, limitations: 'Counts cover only this interval.',
    teams: [{ name: 'Home <img>', shots: 4, goals: 2, corners: 1 }, { name: 'Away', shots: 2, goals: 1, corners: 0 }],
    highlights: Array.from({ length: count }, (_, index) => ({
      eventId: `event-${index}`, period: 2, matchClockMs: 2832000,
      why: index % 2 === 0 ? 'Shot creates a chance.' : null,
      fact: { clock: '47:12', team: 'Home <img>', player: 'Player', what: 'Shot <script>alert(1)</script>', where: 'Left penalty area' }
    }))
  };
}

async function boot({ language = 'en-GB', view = 'casual', result = facts(), fail = false, chartAvailable = true } = {}) {
  const dom = fixture();
  dom.ids.lang.value = language;
  const requests = [];
  const stored = new Map([['vog-insight-view', view]]);
  let autoRefresh;
  let languageChanged;
  const charts = [];
  const context = createContext({
    Chart: chartAvailable ? class {
      constructor(canvas, config) { this.canvas = canvas; Object.assign(this, config); charts.push(this); }
      update(mode) { assert.equal(mode, 'none'); }
      destroy() { this.destroyed = true; }
    } : undefined,
    document: dom.document,
    localStorage: { getItem: key => stored.get(key), setItem: (key, value) => stored.set(key, value) },
    AbortController,
    setTimeout: () => 1, clearTimeout: () => {},
    setInterval: (callback, delay) => { assert.equal(delay, 15000); autoRefresh = callback; },
    MutationObserver: class {
      constructor(callback) { languageChanged = callback; }
      observe(target, options) {
        assert.equal(target, dom.document.documentElement);
        assert.deepEqual(Array.from(options.attributeFilter), ['lang']);
      }
    },
    fetch: async (url, options) => {
      requests.push({ url, options });
      if (fail) throw new Error('Offline');
      return { ok: true, json: async () => result };
    }
  });
  const copyModule = new SourceTextModule(copySource, { context });
  const renderer = new SourceTextModule(rendererSource, { context });
  await renderer.link(specifier => {
    assert.equal(specifier, './insights-copy.js');
    return copyModule;
  });
  await renderer.evaluate();
  const settle = () => new Promise(resolve => setImmediate(resolve));
  await settle();
  return { ...dom, requests, stored, autoRefresh, languageChanged, settle, charts };
}

test('selector changes translate existing DOM without losing focus, evidence or raw facts', async () => {
  const app = await boot({ view: 'analyst' });
  const highlights = app.ids['insights-highlights'];
  assert.equal(highlights.children.length, 40);
  const first = highlights.children[0];
  const [fact, evidence] = first.children;
  evidence.open = true;
  const summary = evidence.children[0];
  app.document.activeElement = summary;
  const englishFact = fact.textContent;
  for (const language of ['es-ES', 'ta-IN', 'en-GB']) {
    app.ids.lang.value = language;
    await app.ids.lang.emit('change');
    const { copy } = recapCopy(language);
    assert.equal(app.panel.lang, language);
    assert.equal(app.ids['insights-heading'].textContent, copy.heading);
    assert.equal(app.ids['refresh-insights'].textContent, copy.refresh);
    assert.equal(app.radios[1].nextSibling.textContent, ` ${copy.analyst}`);
    assert.equal(app.radios[1].value, 'analyst');
    assert.deepEqual(app.ids['recap-scope'].options.map(option => option.value), ['recent', 'match', 'halftime', 'fulltime']);
    assert.deepEqual(app.ids['recap-scope'].options.map(option => option.textContent), Object.values(copy.scopes));
    assert.equal(app.ids['recap-scope'].value, 'recent');
    assert.equal(summary.textContent, copy.source);
    assert.equal(app.document.activeElement, summary);
    assert.equal(highlights.children[0], first);
    assert.equal(evidence.open, true);
    assert.equal(fact.textContent, englishFact);
    assert.equal(fact.lang, 'en');
    assert.match(fact.textContent, /<script>alert\(1\)<\/script>/);
    assert.equal(fact.children.length, 0);
    assert.match(evidence.textContent, /event-5.*2.*2832000.*Left penalty area/);
    assert.equal(evidence.children[1].children[1].lang, 'en');
    assert.equal(app.ids['insights-limitations'].children[0].lang, 'en');
    assert.match(app.ids['insights-limitations'].textContent, /Counts cover only this interval/);
    assert.equal(app.panel.querySelector(':scope > p:last-child').textContent, copy.disclosure);
    assert.equal(app.ids['insights-teams'].getAttribute('aria-label'), copy.counts);
    assert.deepEqual(app.ids['insights-teams'].children.map(group => group.children[0].textContent), copy.columns.slice(1));
    assert.deepEqual(Array.from(app.charts[0].data.labels), copy.columns.slice(1));
    assert.equal(app.charts.length, 1);
    assert.equal(app.requests.length, 1);
  }
});

test('Analyst chart compares scoped counts on one scale with exact accessible values', async () => {
  const result = facts(0);
  result.score = { home: 9, away: 8 };
  const app = await boot({ view: 'analyst', result });
  const chart = app.ids['insights-teams'];
  assert.equal(chart.children.length, 3);
  for (const [index, metric] of ['shots', 'goals', 'corners'].entries()) {
    const group = chart.children[index];
    assert.equal(group.children[0].textContent, recapCopy('en-GB').copy.columns[index + 1]);
    for (const [teamIndex, team] of result.teams.entries()) {
      const [name, count] = group.children[teamIndex + 1].children;
      assert.equal(name.textContent, team.name);
      assert.equal(name.children.length, 0);
      assert.equal(app.charts[0].data.datasets[teamIndex].data[index], team[metric]);
      assert.equal(count.textContent, String(team[metric]));
    }
  }
  assert.equal(descendants(chart).some(node => ['table', 'tr', 'td'].includes(node.tagName)), false);
  assert.match(app.ids['insights-score'].textContent, /9 - 8/);
  assert.equal(app.charts[0].type, 'bar');
  assert.equal(app.charts[0].options.indexAxis, 'y');
  assert.equal(app.charts[0].options.scales.x.max, 4);
  assert.equal(app.charts[0].options.scales.x.min, 0);
  assert.equal(app.charts[0].options.interaction.mode, 'index');
  assert.equal(app.charts[0].options.interaction.intersect, false);
  assert.equal(app.charts[0].options.plugins.legend.position, 'top');
  assert.ok(app.charts[0].options.scales.y.ticks.font.size >= 14);
  assert.notEqual(app.charts[0].data.datasets[0].backgroundColor, app.charts[0].data.datasets[1].backgroundColor);
});

test('team names retain red and blue colours even when team order reverses', async () => {
  for (const names of [['Reds', 'Blues'], ['Blues', 'Reds']]) {
    const result = facts(0);
    result.teams.forEach((team, index) => { team.name = names[index]; });
    const app = await boot({ view: 'analyst', result });
    const expected = names.map(name => name === 'Reds' ? '#c62828' : '#1e63ff');
    assert.deepEqual(Array.from(app.charts[0].data.datasets, dataset => dataset.backgroundColor), expected);
    for (const group of app.ids['insights-teams'].children) {
      assert.deepEqual(group.children.slice(1).map(row => row.style.borderLeftColor), expected);
    }
  }
});

test('Analyst chart keeps zero-count intervals visible and clears unavailable results', async () => {
  const result = facts(0);
  result.teams.forEach(team => { team.shots = 0; team.goals = 0; team.corners = 0; });
  const app = await boot({ view: 'analyst', result });
  assert.equal(app.ids['insights-metrics'].hidden, false);
  assert.equal(app.charts[0].options.scales.x.max, 1);
  assert.ok(app.charts[0].data.datasets.every(dataset => dataset.data.every(value => value === 0)));
  result.status = 'unavailable';
  await app.ids['refresh-insights'].emit('click');
  assert.equal(app.ids['insights-metrics'].hidden, true);
  assert.equal(app.ids['insights-teams'].children.length, 0);
  assert.equal(app.charts[0].destroyed, true);
});

test('exact counts remain available when Chart.js cannot load', async () => {
  const app = await boot({ view: 'analyst', chartAvailable: false });
  assert.equal(app.ids['insights-chart-frame'].hidden, true);
  assert.equal(app.ids['insights-teams'].children.length, 3);
  assert.match(app.ids['insights-teams'].textContent, /Home <img>4/);
});

test('DOM root language changes, casual selection, view persistence and refresh guard remain intact', async () => {
  const app = await boot();
  assert.equal(app.ids['insights-highlights'].children.length, 5);
  assert.equal(app.ids['insights-metrics'].hidden, true);
  assert.match(app.ids['insights-highlights'].children[0].textContent, /Shot creates a chance/);
  app.document.documentElement.lang = 'ta';
  app.languageChanged();
  assert.equal(app.panel.lang, 'ta-IN');
  assert.equal(app.ids['insights-limitations'].textContent, recapCopy('ta').copy.casualNote(5, true));
  await app.radios[1].emit('change');
  assert.equal(app.stored.get('vog-insight-view'), 'analyst');
  assert.equal(app.ids['insights-metrics'].hidden, false);
  const evidence = app.ids['insights-highlights'].children[0].children[1];
  evidence.open = true;
  app.autoRefresh();
  assert.equal(app.requests.length, 1);
  evidence.open = false;
  app.document.activeElement = app.ids['recap-scope'];
  app.autoRefresh();
  assert.equal(app.requests.length, 1);
  app.document.activeElement = null;
  app.document.hidden = true;
  app.autoRefresh();
  assert.equal(app.requests.length, 1);
  app.document.hidden = false;
  app.autoRefresh();
  await app.settle();
  assert.equal(app.requests.length, 2);
  for (const { url, options } of app.requests) {
    assert.equal(url, '/insights?scope=recent&lang=en-GB');
    assert.equal(options.cache, 'no-store');
    assert.ok(options.signal instanceof AbortSignal);
  }
  assert.equal(app.requests[0].options.signal.aborted, true);
});

test('scope requests retain exact values and interval data stays unchanged', async () => {
  const result = facts(0);
  const app = await boot({ language: 'es-ES', result });
  const { copy } = recapCopy('es-ES');
  for (const scope of ['recent', 'match', 'halftime', 'fulltime']) {
    result.scope = scope;
    app.ids['recap-scope'].value = scope;
    await app.ids['recap-scope'].emit('change');
    assert.equal(app.requests.at(-1).url, `/insights?scope=${scope}&lang=en-GB`);
    assert.equal(app.ids['insights-status'].textContent, copy.clock('47:12', copy.scopes[scope]));
    assert.equal(app.ids['insights-limitations'].textContent, copy.casualNote(0, scope === 'recent'));
    assert.equal(app.ids['insights-highlights'].textContent, copy.empty);
    assert.equal(app.ids['insights-score'].textContent, 'Home <img> 2 - 1 Away');
  }
});

test('unavailable and network failure copy localizes without translating server reasons', async () => {
  const app = await boot({ language: 'es-ES', result: { status: 'unavailable', reason: 'Half-time has not occurred.' } });
  const status = app.ids['insights-status'];
  assert.equal(status.children[0].textContent, recapCopy('es-ES').copy.unavailable);
  assert.equal(status.children[1].lang, 'en');
  assert.equal(status.children[1].textContent, ' Half-time has not occurred.');
  app.ids.lang.value = 'ta-IN';
  await app.ids.lang.emit('change');
  assert.equal(status.children[0].textContent, recapCopy('ta-IN').copy.unavailable);
  assert.equal(status.children[1].textContent, ' Half-time has not occurred.');
  const offline = await boot({ language: 'ta-IN', fail: true });
  assert.equal(offline.ids['insights-status'].textContent, recapCopy('ta-IN').copy.unavailable);
});