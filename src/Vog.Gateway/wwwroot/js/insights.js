import { recapCopy } from './insights-copy.js';

const element = id => document.getElementById(id);
let current;
let request;
let chart;
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

function teamColour(team, index) {
  const name = team.name.trim().toLowerCase();
  if (name === 'reds') return '#c62828';
  if (name === 'blues') return '#1e63ff';
  return index === 0 ? '#c62828' : '#1e63ff';
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
  element('insights-teams').setAttribute('aria-label', copy.counts);
  panel.querySelector(':scope > p:last-child').textContent = copy.disclosure;
  for (const update of translations.values()) update();
  if (chart) {
    chart.data.labels = copy.columns.slice(1);
    chart.update('none');
  }
}

function render() {
  chart?.destroy();
  chart = null;
  translations.clear();
  const analyst = view === 'analyst';
  const available = current?.status === 'available';
  element('insights-metrics').hidden = !available || !analyst;
  element('insights-highlights').replaceChildren();
  element('insights-teams').replaceChildren();
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
  const metrics = ['shots', 'goals', 'corners'];
  const maximum = Math.max(1, ...current.teams.flatMap(team => metrics.map(metric => team[metric])));
  if (analyst && globalThis.Chart) {
    chart = new globalThis.Chart(element('insights-chart'), {
      type: 'bar',
      data: {
        labels: copy.columns.slice(1),
        datasets: current.teams.map((team, index) => ({
          label: team.name,
          data: metrics.map(metric => team[metric]),
          backgroundColor: teamColour(team, index),
          borderRadius: 3,
          maxBarThickness: 22,
          categoryPercentage: 0.7
        }))
      },
      options: {
        indexAxis: 'y', responsive: true, maintainAspectRatio: false, animation: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: {
            position: 'top', align: 'start', onClick: () => {},
            labels: { color: '#202626', boxWidth: 14, padding: 20, font: { size: 14 } }
          },
          tooltip: { bodyFont: { size: 14 }, titleFont: { size: 14 }, padding: 12 }
        },
        scales: {
          x: {
            min: 0, max: maximum,
            ticks: { precision: 0, color: '#485454', font: { size: 14 } },
            grid: { color: '#dce3e3' }, border: { display: false }
          },
          y: {
            ticks: { color: '#202626', font: { size: 15, weight: 'bold' }, padding: 12 },
            grid: { display: false }, border: { display: false }
          }
        }
      }
    });
  }
  element('insights-chart-frame').hidden = !chart;
  for (const [metricIndex, metric] of metrics.entries()) {
    const group = document.createElement('div');
    group.className = 'insight-count-group';
    const heading = document.createElement('h3');
    translated(heading, text => text.columns[metricIndex + 1]);
    group.append(heading);
    for (const [teamIndex, team] of current.teams.entries()) {
      const row = document.createElement('div');
      row.className = `insight-count-row ${teamIndex === 0 ? 'home' : 'away'}`;
      row.style.borderLeftColor = teamColour(team, teamIndex);
      const count = document.createElement('strong');
      count.textContent = team[metric];
      row.append(english(team.name), count);
      group.append(row);
    }
    element('insights-teams').append(group);
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