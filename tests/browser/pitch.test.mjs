import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { createContext, SourceTextModule } from 'node:vm';

const source = await readFile(new URL('../../src/Vog.Gateway/wwwroot/js/pitch.js', import.meta.url), 'utf8');

for (const kits of [
  { home: 'red', away: 'blue', fills: ['#e53935', '#1e63ff'] },
  { home: 'blue', away: 'red', fills: ['#1e63ff', '#e53935'] },
]) {
  test(`goalkeepers follow ${kits.home}/${kits.away} kits and retain H/A labels`, async () => {
    const keeperFills = [];
    const labels = [];
    let shape = null;
    const drawing = new Proxy({
      beginPath() { shape = null; },
      rect(_left, _top, width, height) { shape = { width, height }; },
      fill() {
        if (shape?.width === 22 && shape.height === 22) keeperFills.push(this.fillStyle);
      },
      fillText(label) { labels.push(label); },
    }, {
      get(target, property) { return target[property] ?? (() => {}); },
    });
    const canvas = {
      getContext: () => drawing,
      getBoundingClientRect: () => ({ width: 640, height: 420 }),
    };
    const control = () => ({ addEventListener() {}, checked: false });
    const environment = createContext({
      window: {
        devicePixelRatio: 1,
        matchMedia: () => ({ matches: false, addEventListener() {} }),
      },
      performance: { now: () => 10000 },
      ResizeObserver: class { observe() {} },
    });
    const module = new SourceTextModule(source, { context: environment });
    await module.link(() => { throw new Error('Unexpected pitch module import'); });
    await module.evaluate();
    const renderer = module.namespace.createPitchMap({
      canvas,
      description: {},
      zoomIn: control(),
      zoomOut: control(),
      follow: control(),
      homeKey: {},
      awayKey: {},
    });
    renderer.setTeams({ home: 'Home', away: 'Away', homeKit: kits.home, awayKit: kits.away });
    renderer.update({
      b: [52.5, 34],
      hr: true,
      o: 'h',
      h: [[4, 34, 1, true], [30, 20, 9, false]],
      a: [[101, 34, 1, true], [75, 48, 9, false]],
    });
    assert.deepEqual(keeperFills, kits.fills);
    assert.ok(labels.includes('H1'));
    assert.ok(labels.includes('A1'));
    assert.equal(labels.filter(label => label === '9').length, 2);
  });
}