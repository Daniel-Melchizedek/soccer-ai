import { recapCopy } from './insights-copy.js';

const element = id => document.getElementById(id);
let current;
let request;
let { language, copy } = recapCopy(element('lang')?.value || document.documentElement.lang);
const translations = new Map();
let view = 'casual';
try { view = localStorage.getItem('vog-insight-view') === 'analyst' ? 'analyst' : 'casual'; } catch {}
document.querySelector(`input[name="insight-view"][value="${view}"]`).checked = true;

function translated(node, value, attribute) {
  const update = () => {
    if (attribute) node.setAttribute(attribute, value(copy));
    else node.textContent = value(copy);
  };
  translations.set(node, update);
  update();
}

function english(text) {
  const node = document.createElement('span');
  node.lang = 'en';
  node.textContent = text;
  return node;
}

function localize(languageCode) {
  ({ language, copy } = recapCopy(languageCode));
  const panel = element('match-insights');
  panel.lang = language;
  element('insights-heading').textContent = copy.heading;
  panel.querySelector('legend').textContent = copy.view;
  document.querySelectorAll('input[name="insight-view"]').forEach(input => {
    input.nextSibling.textContent = ` ${copy[input.value]}`;
  });
  panel.querySelector('label[for="recap-scope"]').textContent = copy.recap;
  for (const option of element('recap-scope').options) option.textContent = copy.scopes[option.value];
  element('refresh-insights').textContent = copy.refresh;
  panel.querySelector('table').setAttribute('aria-label', copy.counts);
  panel.querySelectorAll('thead th').forEach((cell, index) => { cell.textContent = copy.columns[index]; });
  element('shot-comparison').setAttribute('aria-label', copy.comparison);
  panel.querySelector(':scope > p:last-child').textContent = copy.disclosure;
  for (const update of translations.values()) update();
}

function render() {
  translations.clear();
  const analyst = view === 'analyst';
  const available = current?.status === 'available';
  element('insights-metrics').hidden = !available || !analyst;
  element('insights-highlights').replaceChildren();
  element('insights-teams').replaceChildren();
  element('shot-comparison').replaceChildren();
  element('insights-score').textContent = '';
  element('insights-limitations').textContent = '';
  if (!available) {
    const status = element('insights-status');
    status.replaceChildren();
    const message = document.createElement('span');
    translated(message, text => text.unavailable);
    status.append(message);
    const reason = current?.reason ?? current?.status;
    if (reason) status.append(english(` ${reason}`));
    return;
  }
  translated(element('insights-status'), text => text.clock(current.clock, text.scopes[current.scope]));
  element('insights-score').lang = 'en';
  element('insights-score').textContent = `${current.teams[0].name} ${current.score.home} - ${current.score.away} ${current.teams[1].name}`;
  const decisive = current.highlights.filter(item => item.why);
  const selected = analyst ? current.highlights.slice(-40) : (decisive.length ? decisive : current.highlights).slice(-5);
  if (!selected.length) {
    const empty = document.createElement('li');
    translated(empty, text => text.empty);
    element('insights-highlights').append(empty);
  }
  for (const highlight of selected) {
    const row = document.createElement('li');
    const fact = highlight.fact;
    const text = document.createElement('p');
    text.lang = 'en';
    text.textContent = `${fact.clock}: ${[fact.team, fact.player, fact.what].filter(Boolean).join(', ')}. ${highlight.why ?? ''}`;
    row.append(text);
    if (analyst) {
      const evidence = document.createElement('details');
      const summary = document.createElement('summary');
      translated(summary, text => text.source);
      const source = document.createElement('p');
      const metadata = document.createElement('span');
      translated(metadata, text => text.evidence(highlight.eventId, highlight.period, highlight.matchClockMs));
      source.append(metadata);
      if (fact.where) source.append(english(`; ${fact.where}`));
      source.append('.');
      evidence.append(summary, source);
      row.append(evidence);
    }
    element('insights-highlights').append(row);
  }
  for (const team of current.teams) {
    const row = document.createElement('tr');
    for (const [index, value] of [team.name, team.shots, team.goals, team.corners].entries()) {
      const cell = document.createElement(index === 0 ? 'th' : 'td');
      if (index === 0) {
        cell.scope = 'row';
        cell.lang = 'en';
      }
      cell.textContent = value;
      row.append(cell);
    }
    element('insights-teams').append(row);
    const label = document.createElement('label');
    const shots = document.createElement('span');
    translated(shots, text => `${text.shots(team.shots)} `);
    label.append(english(`${team.name}: `), shots);
    const meter = document.createElement('meter');
    meter.min = 0;
    meter.max = Math.max(1, ...current.teams.map(item => item.shots));
    meter.value = team.shots;
    translated(meter, text => text.shotsLabel(team.name), 'aria-label');
    label.append(meter);
    element('shot-comparison').append(label);
  }
  const limitations = element('insights-limitations');
  if (analyst) limitations.append(english(`${current.limitations} `));
  const note = document.createElement('span');
  translated(note, text => analyst
    ? text.analystNote(selected.length, current.highlights.length, current.matchId)
    : text.casualNote(selected.length, current.scope === 'recent'));
  limitations.append(note);
}

async function refresh() {
  request?.abort();
  const controller = new AbortController();
  request = controller;
  const timeout = setTimeout(() => controller.abort(), 12000);
  translated(element('insights-status'), text => text.updating);
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
function autoRefresh() {
  if (!document.hidden && !element('match-insights').contains(document.activeElement)
      && !element('insights-highlights').querySelector('details[open]')) refresh();
}
document.addEventListener('visibilitychange', autoRefresh);
setInterval(autoRefresh, 15000);
element('lang')?.addEventListener('change', () => localize(element('lang').value));
new MutationObserver(() => localize(document.documentElement.lang))
  .observe(document.documentElement, { attributes: true, attributeFilter: ['lang'] });
localize(language);
refresh();