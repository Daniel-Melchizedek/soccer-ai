// The "Sound, captions and controls" panel. Every control carries data-pref="path.to.setting"; values
// apply and save as soon as they change, and the caption preview shows the result (the FCC asks for
// caption settings that are easy to find, previewable and remembered).
import { DEFAULTS, applyCaptionStyle, loadPrefs, savePrefs } from './prefs.js';

/** Settings stored as 0–1 but shown as percentages. */
const PERCENT = /^volumes\.|^caption\.background$/;

let prefs = loadPrefs();

const read = (path) => path.split('.').reduce((o, k) => o?.[k], prefs);

function write(path, value) {
  const keys = path.split('.');
  const last = keys.pop();
  keys.reduce((o, k) => o[k], prefs)[last] = value;
}

function render() {
  for (const control of document.querySelectorAll('[data-pref]')) {
    const value = read(control.dataset.pref);
    if (control.type === 'checkbox') {
      control.checked = Boolean(value);
    } else if (control.type === 'radio') {
      control.checked = control.value === value;
    } else if (PERCENT.test(control.dataset.pref)) {
      control.value = Math.round(value * 100);
    } else {
      control.value = value;
    }

    const output = document.querySelector(`output[for="${control.id}"]`);
    if (output) {
      output.textContent = `${control.value}%`;
    }
  }

  applyCaptionStyle(prefs);
}

function onChange(event) {
  const control = event.target.closest('[data-pref]');
  if (!control || (control.type === 'radio' && !control.checked)) {
    return;
  }

  const path = control.dataset.pref;
  const value = control.type === 'checkbox'
    ? control.checked
    : PERCENT.test(path) ? Number(control.value) / 100 : control.value;
  write(path, value);
  savePrefs(prefs);
  render();
}

const panel = document.getElementById('settings');
panel.addEventListener('input', onChange);
panel.addEventListener('change', onChange);
document.getElementById('pitch-style')?.addEventListener('change', onChange);
document.getElementById('reset-settings').addEventListener('click', () => {
  prefs = structuredClone(DEFAULTS);
  savePrefs(prefs);
  render();
});

render();
