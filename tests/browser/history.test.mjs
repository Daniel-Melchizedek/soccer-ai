import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { createContext, runInContext } from 'node:vm';

const source = await readFile(new URL('../../src/Vog.Gateway/wwwroot/js/app.js', import.meta.url), 'utf8');
const historySource = source.slice(source.indexOf('async function loadHistory()'));

async function load(fetch) {
  const list = {
    children: [{ textContent: 'Loading...' }],
    set innerHTML(value) { throw new Error(`Unsafe HTML: ${value}`); },
    replaceChildren(...items) { this.children = items; },
    appendChild(item) { this.children.push(item); }
  };
  const context = createContext({
    fetch,
    el: () => list,
    document: { createElement: () => ({ textContent: '' }) }
  });
  await runInContext(historySource, context);
  return list;
}

test('history request failures replace the loading state', async () => {
  const list = await load(async () => { throw new Error('Network unavailable'); });
  assert.equal(list.children[0].textContent, 'No match history available.');
});

test('history JSON parsing failures replace the loading state', async () => {
  const list = await load(async () => ({
    ok: true,
    json: async () => { throw new SyntaxError('Invalid JSON'); }
  }));
  assert.equal(list.children[0].textContent, 'No match history available.');
});

test('unsuccessful history responses show unavailable, not empty history', async () => {
  const list = await load(async () => ({ ok: false }));
  assert.equal(list.children[0].textContent, 'No match history available.');
});

test('successful empty history shows no recorded matches', async () => {
  const list = await load(async () => ({ ok: true, json: async () => [] }));
  assert.equal(list.children[0].textContent, 'No matches recorded yet.');
});

test('successful history retains plain-text archived results', async () => {
  const list = await load(async () => ({ ok: true, json: async () => [{
    homeName: '<Home>', awayName: 'Away', homeScore: 2, awayScore: 1,
    utcDate: '2026-10-26T09:00:00Z'
  }] }));
  assert.equal(list.children.length, 1);
  assert.match(list.children[0].textContent, /^<Home> 2 .* 1 Away/);
});