// The fan's sound, caption and control settings, remembered on this device only (accessibility needs
// can reveal a disability, so nothing is sent to the server). Changes are announced as a "vog:prefs"
// event so the mix, captions and companion pick them up straight away.

const KEY = 'vog.prefs.v1';

export const DEFAULTS = Object.freeze({
  volumes: { commentary: 1, answers: 1, earcons: 0.8, ball: 0.6, crowd: 0.5 },
  sonifyBall: false,
  haptics: false,
  clearSpeech: false,
  gentle: false,
  caption: { size: 'M', colours: 'white-black', background: 0.9, outline: 'none', font: 'system', position: 'panel' },
  talkMode: 'tap',
});

/** Caption text colour, box colour (RGB) and the companion's colour, in BBC speaker order. */
export const CAPTION_SCHEMES = Object.freeze({
  'white-black': { fg: '#ffffff', bg: '0, 0, 0', companion: '#5ff3ff' },
  'yellow-black': { fg: '#ffd400', bg: '0, 0, 0', companion: '#ffffff' },
  'cyan-black': { fg: '#5ff3ff', bg: '0, 0, 0', companion: '#ffd400' },
  'black-white': { fg: '#000000', bg: '255, 255, 255', companion: '#00525a' },
});

export const CAPTION_SIZES = Object.freeze({ S: '1.2rem', M: '1.5rem', L: '1.9rem', XL: '2.5rem' });

const FONTS = Object.freeze({
  system: 'inherit',
  hyperlegible: '"Atkinson Hyperlegible", system-ui, sans-serif',
});

const OUTLINES = Object.freeze({
  none: 'none',
  black: '1px 0 #000, -1px 0 #000, 0 1px #000, 0 -1px #000',
  white: '1px 0 #fff, -1px 0 #fff, 0 1px #fff, 0 -1px #fff',
});

export function loadPrefs() {
  try {
    const saved = JSON.parse(localStorage.getItem(KEY) ?? '{}');
    return {
      ...structuredClone(DEFAULTS),
      ...saved,
      volumes: { ...DEFAULTS.volumes, ...saved.volumes },
      caption: { ...DEFAULTS.caption, ...saved.caption },
    };
  } catch {
    return structuredClone(DEFAULTS);
  }
}

export function savePrefs(prefs) {
  localStorage.setItem(KEY, JSON.stringify(prefs));
  window.dispatchEvent(new CustomEvent('vog:prefs', { detail: prefs }));
}

/** Caption appearance lives in CSS custom properties, so the live captions and the preview match. */
export function applyCaptionStyle(prefs) {
  const c = prefs.caption;
  const scheme = CAPTION_SCHEMES[c.colours] ?? CAPTION_SCHEMES['white-black'];
  const root = document.documentElement.style;
  root.setProperty('--caption-fg', scheme.fg);
  root.setProperty('--caption-bg', `rgba(${scheme.bg}, ${c.background})`);
  root.setProperty('--caption-companion', scheme.companion);
  root.setProperty('--caption-size', CAPTION_SIZES[c.size] ?? CAPTION_SIZES.M);
  root.setProperty('--caption-font', FONTS[c.font] ?? FONTS.system);
  root.setProperty('--caption-outline', OUTLINES[c.outline] ?? OUTLINES.none);
  document.body.classList.toggle('captions-pinned', c.position === 'bottom');
}
