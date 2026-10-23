const element = id => document.getElementById(id);
let current;
let request;
let view = 'casual';
try { view = localStorage.getItem('vog-insight-view') === 'analyst' ? 'analyst' : 'casual'; } catch {}
document.querySelector(`input[name="insight-view"][value="${view}"]`).checked = true;

function render() {
  const analyst = view === 'analyst';
  const available = current?.status === 'available';
  element('insights-metrics').hidden = !available || !analyst;
  element('insights-highlights').replaceChildren();
  element('insights-teams').replaceChildren();
  element('shot-comparison').replaceChildren();
  element('insights-score').textContent = '';
  element('insights-limitations').textContent = '';
  if (!available) {
    element('insights-status').textContent = current?.reason ?? current?.status ?? 'Match facts unavailable. Retry with Refresh recap.';
    return;
  }
  const scopeNames = { recent: 'Last two minutes', match: 'Match so far', halftime: 'Half-time', fulltime: 'Full-time' };
  element('insights-status').textContent = `Match clock ${current.clock}. ${scopeNames[current.scope]}.`;
  element('insights-score').textContent = `${current.teams[0].name} ${current.score.home} - ${current.score.away} ${current.teams[1].name}`;
  const decisive = current.highlights.filter(item => item.why);
  const selected = analyst ? current.highlights.slice(-40) : (decisive.length ? decisive : current.highlights).slice(-5);
  if (!selected.length) {
    const empty = document.createElement('li');
    empty.textContent = 'No goals, shots, cards, substitutions or period endings in this interval.';
    element('insights-highlights').append(empty);
  }
  for (const highlight of selected) {
    const row = document.createElement('li');
    const fact = highlight.fact;
    const text = document.createElement('p');
    text.textContent = `${fact.clock}: ${[fact.team, fact.player, fact.what].filter(Boolean).join(', ')}. ${highlight.why ?? ''}`;
    row.append(text);
    if (analyst) {
      const evidence = document.createElement('details');
      const summary = document.createElement('summary');
      summary.textContent = 'Source event';
      const source = document.createElement('p');
      source.textContent = `${highlight.eventId}; half ${highlight.period}; clock ${highlight.matchClockMs} ms${fact.where ? `; ${fact.where}` : ''}.`;
      evidence.append(summary, source);
      row.append(evidence);
    }
    element('insights-highlights').append(row);
  }
  for (const team of current.teams) {
    const row = document.createElement('tr');
    for (const [index, value] of [team.name, team.shots, team.goals, team.corners].entries()) {
      const cell = document.createElement(index === 0 ? 'th' : 'td');
      if (index === 0) cell.scope = 'row';
      cell.textContent = value;
      row.append(cell);
    }
    element('insights-teams').append(row);
    const label = document.createElement('label');
    label.textContent = `${team.name}: ${team.shots} shots `;
    const meter = document.createElement('meter');
    meter.min = 0;
    meter.max = Math.max(1, ...current.teams.map(item => item.shots));
    meter.value = team.shots;
    meter.setAttribute('aria-label', `${team.name} shots`);
    label.append(meter);
    element('shot-comparison').append(label);
  }
  element('insights-limitations').textContent = analyst
    ? `${current.limitations} Showing ${selected.length} of ${current.highlights.length} highlights. Match ID: ${current.matchId}.`
    : `Score at the end of this interval. ${current.scope === 'recent' ? 'The interval stays within the current half.' : ''} Showing ${selected.length} selected moments.`;
}

async function refresh() {
  request?.abort();
  const controller = new AbortController();
  request = controller;
  const timeout = setTimeout(() => controller.abort(), 12000);
  element('insights-status').textContent = 'Updating match facts...';
  try {
    const response = await fetch(`/insights?scope=${encodeURIComponent(element('recap-scope').value)}&lang=en-GB`, { cache: 'no-store', signal: controller.signal });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const result = await response.json();
    if (request !== controller) return;
    current = result;
    render();
  } catch {
    if (request !== controller) return;
    current = null;
    render();
  } finally {
    clearTimeout(timeout);
  }
}

document.querySelectorAll('input[name="insight-view"]').forEach(input => input.addEventListener('change', () => {
  view = input.value;
  try { localStorage.setItem('vog-insight-view', view); } catch {}
  render();
}));
element('recap-scope').addEventListener('change', refresh);
element('refresh-insights').addEventListener('click', refresh);
document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh(); });
setInterval(() => {
  if (!document.hidden && !element('match-insights').contains(document.activeElement)
      && !element('insights-highlights').querySelector('details[open]')) refresh();
}, 15000);
refresh();