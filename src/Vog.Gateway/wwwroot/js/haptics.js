const PATTERNS = Object.freeze({
  goal: [180, 90, 180, 90, 420],
  shot: [70, 70, 70],
  saved: [70, 70, 70],
  'card.yellow': [70, 70, 70],
  'card.red': [150, 80, 150],
  whistle: [55, 70, 55],
  'possession.home': [35],
  'possession.away': [35],
});

export function createHaptics() {
  const available = typeof navigator !== 'undefined' && typeof navigator.vibrate === 'function';
  let enabled = false;
  let gentle = false;

  function configure(prefs) {
    enabled = available && Boolean(prefs.haptics);
    gentle = Boolean(prefs.gentle);
  }

  function play(earcon) {
    if (!enabled) {
      return;
    }

    const pattern = PATTERNS[earcon];
    if (!pattern) {
      return;
    }

    navigator.vibrate(gentle ? pattern.map((duration) => Math.max(20, Math.round(duration * 0.5))) : pattern);
  }

  return { available, configure, play };
}