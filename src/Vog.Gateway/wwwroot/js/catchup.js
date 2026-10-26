const MAX_ITEMS = 6;
const MAX_SEEN = 256;
const MAX_TEXT = 600;

function importance(message) {
  if (message.kind === 'Goal' || message.earcon === 'goal') return 0;
  if (/card/i.test(message.kind ?? '') || /card/i.test(message.earcon ?? '')) return 1;
  if (message.priority === 'P0') return 2;
  if (['Shot', 'ShotOutcome', 'PeriodChange'].includes(message.kind)) return 3;
  return null;
}

export function createCatchup({ now = () => performance.now() } = {}) {
  let interval = null;
  return {
    begin(language) {
      interval = { language, startedAt: now(), received: 0, seen: new Set(), items: [], truncated: false };
    },
    receive(message) {
      if (!interval || typeof message.text !== 'string' || !message.text.trim()) return;
      interval.received++;
      const rank = importance(message);
      if (rank === null) return;
      const text = message.text.slice(0, MAX_TEXT);
      const id = String(message.id ?? '').slice(0, 160);
      const clock = String(message.clock ?? '').slice(0, 32);
      const key = JSON.stringify([clock, text]);
      if (interval.seen.has(id) || interval.seen.has(key) || interval.items.some(item => item.id === id || item.key === key)) return;
      if (interval.seen.size >= MAX_SEEN) {
        interval.truncated = true;
        interval.seen.delete(interval.seen.values().next().value);
        interval.seen.delete(interval.seen.values().next().value);
      }
      interval.seen.add(id);
      interval.seen.add(key);
      interval.truncated ||= text.length !== message.text.length;
      interval.items.push({ id, key, text, clock, language: interval.language, rank, order: interval.received });
      interval.items.sort((left, right) => left.rank - right.rank || left.order - right.order);
      if (interval.items.length > MAX_ITEMS) {
        interval.items.pop();
        interval.truncated = true;
      }
    },
    finish(outcome = 'answered') {
      if (!interval) return null;
      const result = { language: interval.language, startedAt: interval.startedAt, endedAt: now(), received: interval.received, truncated: interval.truncated, outcome, items: interval.items.sort((left, right) => left.order - right.order) };
      interval = null;
      return result;
    },
    cancel() { interval = null; },
  };
}

export function renderCatchup(output, result) {
  output.replaceChildren();
  if (!result) return;
  const document = output.ownerDocument;
  const summary = document.createElement('p');
  const duration = Math.max(0, Math.round((result.endedAt - result.startedAt) / 1000));
  const outcome = result.outcome === 'answered' ? 'After your answer.' : 'Question interrupted or failed; partial interval.';
  const coverage = result.received === 0 ? 'No commentary received during this interval.'
    : result.items.length === 0 ? 'No major moments in the received commentary.' : 'Selected moments from received commentary.';
  summary.textContent = `${outcome} ${duration}s captured. ${coverage}${result.truncated ? ' Truncated: some moments or text omitted.' : ''} Source language: ${result.language}.`;
  const list = document.createElement('ol');
  for (const item of result.items) {
    const entry = document.createElement('li');
    entry.lang = item.language;
    entry.textContent = [item.clock, item.text].filter(Boolean).join(' ');
    list.append(entry);
  }
  output.replaceChildren(summary, list);
}