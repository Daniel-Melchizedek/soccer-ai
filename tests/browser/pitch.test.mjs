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
    const legendColours = {};
    const keeperKey = team => ({ style: { setProperty(name, value) {
      assert.equal(name, '--keeper-colour');
      legendColours[team] = value;
    } } });
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
      homeKeeperKey: keeperKey('home'),
      awayKeeperKey: keeperKey('away'),
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
    assert.deepEqual([legendColours.home, legendColours.away], kits.fills);
    assert.ok(labels.includes('H1'));
    assert.ok(labels.includes('A1'));
    assert.equal(labels.filter(label => label === '9').length, 2);
  });
}

test('follow ball pans zoomed views, clamps at pitch edges and leaves full-pitch views unchanged', async () => {
  let ball = null;
  let field = null;
  const drawing = new Proxy({
    clearRect() { ball = null; field = null; },
    strokeRect(left, top, width, height) { field ??= { left, top, width, height }; },
    arc(x, y, radius) { if (radius === 8) ball = { x, y }; },
  }, { get(target, property) { return target[property] ?? (() => {}); } });
  const control = () => ({
    checked: false,
    listeners: {},
    addEventListener(name, listener) { this.listeners[name] = listener; },
    emit(name) { this.listeners[name](); },
  });
  const zoomIn = control();
  const zoomOut = control();
  const follow = control();
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
    canvas: { getContext: () => drawing, getBoundingClientRect: () => ({ width: 640, height: 420 }) },
    description: {}, zoomIn, zoomOut, follow, homeKey: {}, awayKey: {},
  });
  const update = (x, y) => renderer.update({ b: [x, y], hr: true, o: 'h', h: [], a: [] });

  update(40, 25);
  const fullPitch = { ...field };
  const fullPitchBall = { ...ball };
  follow.checked = true;
  follow.emit('change');
  assert.deepEqual(field, fullPitch);
  assert.deepEqual(ball, fullPitchBall);

  zoomIn.emit('click');
  zoomIn.emit('click');
  assert.deepEqual(ball, { x: 320, y: 210 });
  const firstView = { ...field };
  update(65, 40);
  assert.deepEqual(ball, { x: 320, y: 210 });
  assert.notDeepEqual(field, firstView);

  follow.checked = false;
  follow.emit('change');
  const fixedView = { ...field };
  update(40, 25);
  assert.deepEqual(field, fixedView);
  assert.notDeepEqual(ball, { x: 320, y: 210 });
  update(0, 0);
  assert.equal(ball, null);

  follow.checked = true;
  follow.emit('change');
  for (const [x, y] of [[0, 0], [105, 68]]) {
    update(x, y);
    assert.ok(ball.x >= 22 && ball.x <= 618);
    assert.ok(ball.y >= 22 && ball.y <= 398);
  }
  zoomOut.emit('click');
  zoomOut.emit('click');
  assert.deepEqual(field, fullPitch);
  assert.equal(zoomOut.disabled, true);
});