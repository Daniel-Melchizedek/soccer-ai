import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { createContext, SourceTextModule } from 'node:vm';

const source = await readFile(new URL('../../src/Vog.Gateway/wwwroot/js/football-history.js', import.meta.url), 'utf8');

async function boot({ fail = false, rows, chartsAvailable = true } = {}) {
  const elements = new Map();
  const charts = [];
  const requests = [];
  const createElement = () => ({
    children: [], listeners: {}, hidden: false, textContent: '',
    append(...nodes) { this.children.push(...nodes); },
    replaceChildren() { this.children = []; },
    addEventListener(event, listener) { this.listeners[event] = listener; }
  });
  const get = id => {
    if (!elements.has(id)) elements.set(id, createElement());
    return elements.get(id);
  };
  const state = { fail, rows };
  const context = createContext({
    document: { getElementById: get, createElement }, AbortSignal,
    Chart: chartsAvailable ? class {
      constructor(canvas, config) { Object.assign(this, config); charts.push(this); }
      destroy() { this.destroyed = true; }
    } : undefined,
    fetch: async url => {
      requests.push(url);
      return { ok: !state.fail, status: 503, json: async () => state.rows ?? [
        { decade: '2020s', avgGoals: 2.5, homeWinPct: 51 },
        { decade: '2010s', avgGoals: 2.3, homeWinPct: 49 }
      ] };
    }
  });
  const module = new SourceTextModule(source, { context });
  await module.link(() => { throw new Error('Unexpected import'); });
  await module.evaluate();
  await new Promise(resolve => setImmediate(resolve));
  return { get, charts, requests, state };
}

test('history uses Chart.js with sorted data, exact tables and an honest percentage scale', async () => {
  const app = await boot();
  assert.deepEqual(app.requests, ['/data/goals-per-decade.json', '/data/home-advantage.json']);
  assert.equal(app.charts.length, 2);
  assert.equal(app.charts[0].type, 'bar');
  assert.equal(app.charts[1].type, 'line');
  assert.deepEqual(Array.from(app.charts[0].data.labels), ['2010s', '2020s']);
  assert.deepEqual(Array.from(app.charts[0].data.datasets[0].data), [2.3, 2.5]);
  assert.equal(app.charts[1].options.scales.y.max, 100);
  assert.equal(app.get('goals-rows').children[0].children[1].textContent, '2.30');
  assert.equal(app.get('home-rows').children[0].children[1].textContent, '49.0%');
  assert.equal(app.get('goals-data').hidden, false);
});

test('failed history requests can retry without duplicating rows or charts', async () => {
  const app = await boot({ fail: true });
  assert.equal(app.get('goals-retry').hidden, false);
  assert.equal(app.get('goals-data').hidden, true);
  app.state.fail = false;
  await app.get('goals-retry').listeners.click();
  assert.equal(app.get('goals-retry').hidden, true);
  assert.equal(app.get('goals-rows').children.length, 2);
  await app.get('goals-retry').listeners.click();
  assert.equal(app.charts[0].destroyed, true);
  assert.equal(app.get('goals-rows').children.length, 2);
});

test('invalid and empty aggregates never render misleading charts', async () => {
  for (const rows of [[], {}, [null], [{ decade: '<img>', avgGoals: 1 }], [{ decade: '2020s', avgGoals: -1, homeWinPct: 101 }]]) {
    const app = await boot({ rows });
    assert.equal(app.charts.length, 0);
    assert.equal(app.get('goals-retry').hidden, false);
  }
});

test('history tables remain available when Chart.js fails to load', async () => {
  const app = await boot({ chartsAvailable: false });
  assert.equal(app.get('goals-data').hidden, false);
  assert.equal(app.get('goals-data').open, true);
  assert.equal(app.get('goals-rows').children.length, 2);
  assert.match(app.get('goals-status').textContent, /Chart unavailable/);
});