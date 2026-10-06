// The caption box. Commentary captions follow the spoken words (cue times from the server); the
// companion's answers appear here too, in the companion's colour, so Deaf and hard-of-hearing fans
// read everything in one place.

const HOLD_MS = 3000; // a caption stays this long after its words, then clears

const box = () => document.getElementById('caption');
let timers = [];
let companionShowing = false;

function clearTimers() {
  timers.forEach(clearTimeout);
  timers = [];
}

function show(text, speaker) {
  const el = box();
  el.textContent = text;
  el.classList.toggle('companion', speaker === 'companion');
}

/** Shows the cue for the current point in a commentary line, then schedules the rest. */
export function showCommentary(cues, startedAt, sound = null) {
  if (companionShowing) {
    return; // the fan is reading an answer; commentary captions resume afterwards
  }

  clearTimers();
  const elapsed = performance.now() - startedAt;
  const current = cues.findLast((cue) => cue.startMs <= elapsed) ?? cues[0];
  const text = (cue) => [sound, cue.lines.join('\n')].filter(Boolean).join('\n');
  show(text(current), 'commentary');
  for (const cue of cues.filter((c) => c.startMs > elapsed)) {
    timers.push(setTimeout(() => show(text(cue), 'commentary'), cue.startMs - elapsed));
  }

  const endMs = Math.max(...cues.map((c) => c.endMs ?? 0));
  timers.push(setTimeout(() => show('', 'commentary'), Math.max(0, endMs - elapsed) + HOLD_MS));
}

/** The companion's answer as it arrives. */
export function showCompanion(text) {
  clearTimers();
  companionShowing = true;
  show(text, 'companion');
}

/** The answer finished: leave it up briefly, then hand the box back to the commentary. */
export function endCompanion() {
  timers.push(
    setTimeout(() => {
      companionShowing = false;
      show('', 'commentary');
    }, HOLD_MS));
}

export function resetCaptions() {
  clearTimers();
  companionShowing = false;
  show('', 'commentary');
}
