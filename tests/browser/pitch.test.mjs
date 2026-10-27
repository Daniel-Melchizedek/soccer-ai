import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { createContext, SourceTextModule } from 'node:vm';

const source = await readFile(new URL('../../src/Vog.Gateway/wwwroot/js/pitch.js', import.meta.url), 'utf8');

function luminance(hex) {
  const channels = hex.slice(1).match(/../g).map(channel => {
    const value = parseInt(channel, 16) / 255;
    return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  });
  return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
}

function contrast(first, second) {
  const values = [luminance(first), luminance(second)];
  return (Math.max(...values) + 0.05) / (Math.min(...values) + 0.05);
}

test('pitch palettes preserve graphical contrast and high-contrast text targets', async () => {
  const module = new SourceTextModule(source);
  await module.link(() => { throw new Error('Unexpected pitch module import'); });
  await module.evaluate();
  for (const [name, style] of Object.entries(module.namespace.PITCH_STYLES)) {
    assert.ok(contrast(style.line, style.background) >= 4.5, `${name}: legend and detached labels`);
    assert.ok(contrast(style.outline, style.background) >= 3, `${name}: player outlines`);
    assert.ok(contrast(style.line, style.background) >= 3, `${name}: field markings`);
    if (style.player) assert.ok(contrast('#000000', style.player) >= 7, `${name}: player numbers`);
  }
});

for (const kits of [
  { home: 'red', away: 'blue', fills: ['#e53935', '#1e63ff'] },
  { home: 'blue', away: 'red', fills: ['#1e63ff', '#e53935'] },
  { home: 'white', away: 'yellow', fills: ['#ffffff', '#ffd400'] },
  { home: 'green', away: 'claret', fills: ['#1faa59', '#7b1e3a'] },
  { home: 'sky_blue', away: 'orange', fills: ['#64c8ff', '#ff8a2a'] },
  { home: 'black', away: 'purple', fills: ['#111111', '#7e3ff2'] },
]) {
  test(`goalkeepers follow ${kits.home}/${kits.away} kits and retain H/A labels`, async () => {
    const keeperFills = [];
    const labels = [];
    const textStyles = [];
    const legendColours = {};
    const keeperKey = team => ({ style: { setProperty(name, value) {
      assert.ok(['--keeper-colour', '--keeper-text'].includes(name));
      if (name === '--keeper-colour') legendColours[team] = value;
    } } });
    let shape = null;
    const drawing = new Proxy({
      beginPath() { shape = null; },
      rect(_left, _top, width, height) { shape = { width, height }; },
      fill() {
        if (shape?.width === 30 && shape.height === 30) keeperFills.push(this.fillStyle);
      },
      fillText(label) {
        labels.push(label);
        textStyles.push({ fill: this.fillStyle, font: this.font });
      },
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
    for (const [index, style] of textStyles.entries()) {
      const background = luminance(kits.fills[index < 2 ? 0 : 1]);
      const foreground = luminance(style.fill);
      assert.ok((Math.max(background, foreground) + 0.05) / (Math.min(background, foreground) + 0.05) >= 4.5);
      assert.equal(style.font, 'bold 14px "Atkinson Hyperlegible", sans-serif');
    }
    for (const [name, style] of Object.entries(module.namespace.PITCH_STYLES)) {
      keeperFills.length = 0;
      labels.length = 0;
      textStyles.length = 0;
      renderer.setStyle(name);
      const expected = kits.fills.map(fill => style.player ?? fill);
      assert.deepEqual(keeperFills, expected);
      assert.deepEqual([legendColours.home, legendColours.away], expected);
      assert.deepEqual(labels, ['H1', '9', 'A1', '9']);
      for (const [index, text] of textStyles.entries()) {
        assert.ok(contrast(text.fill, expected[index < 2 ? 0 : 1]) >= (style.player ? 7 : 4.5));
      }
    }
  });
}

test('follow ball pans zoomed views, clamps at pitch edges and leaves full-pitch views unchanged', async () => {
  let ball = null;
  let field = null;
  const bounds = { width: 640, height: 420 };
  const markerWidths = [];
  const fonts = [];
  const paints = [];
  const radii = [];
  const drawing = new Proxy({
    clearRect() { ball = null; field = null; },
    strokeRect(left, top, width, height) { field ??= { left, top, width, height }; },
    arc(x, y, radius) { radii.push(radius); if (radius === 8) ball = { x, y }; },
    rect(_left, _top, width, height) { if (width === height) markerWidths.push(width); },
    fillText() { fonts.push(this.font); paints.push(this.fillStyle); },
    fill() { paints.push(this.fillStyle); },
    fillRect() { paints.push(this.fillStyle); },
    stroke() { paints.push(this.strokeStyle); },
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
  const zoomValues = [];
  const zoomLevel = {
    get textContent() { return zoomValues.at(-1); },
    set textContent(value) { zoomValues.push(value); },
  };
  const markerSize = control();
  markerSize.value = '1';
  const pitchStyle = control();
  pitchStyle.value = 'classic';
  const properties = new Map();
  const forcedColours = control();
  forcedColours.matches = false;
  const environment = createContext({
    window: {
      devicePixelRatio: 1,
      matchMedia: query => query === '(forced-colors: active)' ? forcedColours : { matches: false, addEventListener() {} },
    },
    performance: { now: () => 10000 },
    ResizeObserver: class { observe() {} },
  });
  const module = new SourceTextModule(source, { context: environment });
  await module.link(() => { throw new Error('Unexpected pitch module import'); });
  await module.evaluate();
  const renderer = module.namespace.createPitchMap({
    canvas: { getContext: () => drawing, getBoundingClientRect: () => bounds, parentElement: { style: { setProperty: (key, value) => properties.set(key, value) } } },
    description: {}, zoomIn, zoomOut, zoomLevel, markerSize, pitchStyle, follow, homeKey: {}, awayKey: {},
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
  assert.equal(zoomLevel.textContent, '200%');
  assert.deepEqual(ball, { x: 320, y: 210 });
  const firstView = { ...field };
  update(65, 40);
  assert.deepEqual(ball, { x: 320, y: 210 });
  assert.deepEqual(zoomValues, ['100%', '150%', '200%']);
  assert.notDeepEqual(field, firstView);
  const themedField = { ...field };
  for (const [name, style] of Object.entries(module.namespace.PITCH_STYLES)) {
    pitchStyle.value = name;
    pitchStyle.emit('change');
    assert.equal(properties.get('--pitch-background'), style.background);
    assert.equal(properties.get('--pitch-ball'), style.ball);
    assert.deepEqual(field, themedField);
    assert.deepEqual(ball, { x: 320, y: 210 });
    assert.equal(follow.checked, true);
    assert.deepEqual(zoomValues, ['100%', '150%', '200%']);
  }
  for (const invalid of ['unknown', '__proto__', null]) {
    renderer.setStyle(invalid);
    assert.equal(pitchStyle.value, 'classic');
    assert.equal(properties.get('--pitch-background'), '#08251f');
  }

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
  assert.equal(zoomLevel.textContent, '100%');

  renderer.update({ b: [52.5, 34], hr: true, o: 'h', h: [[20, 34, 1, true]], a: [] });
  for (const scale of [1, 1.4, 1.8]) {
    markerSize.value = String(scale);
    markerSize.emit('change');
    assert.equal(markerWidths.at(-1), 30 * scale);
    assert.equal(fonts.at(-1), `bold ${Math.round(14 * scale)}px "Atkinson Hyperlegible", sans-serif`);
    assert.equal(radii.at(-1), 8 * scale);
  }
  markerSize.value = 'invalid';
  markerSize.emit('change');
  assert.equal(markerWidths.at(-1), 30);

  paints.length = 0;
  forcedColours.matches = true;
  forcedColours.emit('change');
  assert.deepEqual([...new Set(paints)].sort(), ['Canvas', 'CanvasText']);
  for (const name of Object.keys(module.namespace.PITCH_STYLES)) {
    paints.length = 0;
    renderer.setStyle(name);
    assert.deepEqual([...new Set(paints)].sort(), ['Canvas', 'CanvasText']);
    assert.equal(properties.get('--pitch-background'), 'Canvas');
  }
  forcedColours.matches = false;
  forcedColours.emit('change');
  assert.equal(pitchStyle.value, 'yellow');
  assert.equal(properties.get('--pitch-line'), '#ffd400');

  bounds.width = 320;
  bounds.height = 480;
  markerSize.value = '1';
  update(40, 25);
  assert.ok(field.height > field.width);
  zoomIn.emit('click');
  zoomIn.emit('click');
  assert.deepEqual(ball, { x: 160, y: 240 });
  for (const [x, y] of [[0, 0], [105, 68]]) {
    update(x, y);
    assert.ok(ball.x >= 22 && ball.x <= 298);
    assert.ok(ball.y >= 22 && ball.y <= 458);
  }
});