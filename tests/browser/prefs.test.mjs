import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { createContext, SourceTextModule } from 'node:vm';

const source = await readFile(new URL('../../src/Vog.Gateway/wwwroot/js/prefs.js', import.meta.url), 'utf8');

async function boot(saved) {
  const properties = new Map();
  const classes = new Map();
  const context = createContext({
    structuredClone,
    localStorage: { getItem: () => saved },
    document: {
      documentElement: { style: { setProperty: (name, value) => properties.set(name, value) } },
      body: { classList: { toggle: (name, value) => classes.set(name, value) } }
    }
  });
  const module = new SourceTextModule(source, { context });
  await module.link(() => { throw new Error('Unexpected import'); });
  await module.evaluate();
  return { prefs: module.namespace, properties, classes };
}

test('captions float by default, including missing or invalid stored preferences', async () => {
  for (const saved of [null, '{}', '{"caption":{"size":"XL"}}', 'invalid']) {
    const app = await boot(saved);
    const settings = app.prefs.loadPrefs();
    assert.equal(settings.caption.position, 'bottom');
    app.prefs.applyCaptionStyle(settings);
    assert.equal(app.classes.get('captions-pinned'), true);
  }
});

test('explicit panel preference and caption appearance remain respected', async () => {
  const app = await boot(JSON.stringify({ caption: { position: 'panel', size: 'XL', colours: 'black-white', background: 0.8 } }));
  app.prefs.applyCaptionStyle(app.prefs.loadPrefs());
  assert.equal(app.classes.get('captions-pinned'), false);
  assert.equal(app.properties.get('--caption-size'), '2.5rem');
  assert.equal(app.properties.get('--caption-fg'), '#000000');
  assert.equal(app.properties.get('--caption-bg'), 'rgba(255, 255, 255, 0.8)');
});