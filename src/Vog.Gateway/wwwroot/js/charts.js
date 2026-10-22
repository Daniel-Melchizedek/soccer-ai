// Historical international football charts, sourced from Kaggle (CC0).
// Data is fetched from /data/*.json which the gateway proxies from Azure Blob Storage.
//
// Accessibility:
//   - Each <canvas> has role="img" and aria-label (plain-text summary of key finding).
//   - A hidden <table> beside each chart provides the same data for screen readers.
//   - Animation is disabled when prefers-reduced-motion: reduce is set.
//   - Colours use the existing app palette (--teal, --accent); no colour-only encoding.
//
// Attribution: Data by Mart Jürisoo, Kaggle CC0.
// Inspiration: github.com/bbenz/football-results-dashboard (MIT).

const TEAL = '#2fe0d4';
const ACCENT = '#ff8a2a';
const GRID = '#4a2d7a';
const LABEL = '#d4c6f0';
const WHITE = '#ffffff';

const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
const animDuration = reducedMotion ? 0 : 700;

function populateTable(tbodyId, rows, col1Fn, col2Fn) {
  const tbody = document.getElementById(tbodyId);
  if (!tbody) return;
  for (const row of rows) {
    const tr = tbody.insertRow();
    tr.insertCell().textContent = col1Fn(row);
    tr.insertCell().textContent = col2Fn(row);
  }
}

function chartDefaults() {
  return {
    animation: { duration: animDuration },
    responsive: true,
    plugins: {
      legend: { labels: { color: WHITE } },
    },
    scales: {
      x: { ticks: { color: LABEL }, grid: { color: GRID } },
      y: { ticks: { color: LABEL }, grid: { color: GRID } },
    },
  };
}

async function loadCharts() {
  const panel = document.getElementById('football-history');
  if (!panel) return;

  // Lazy-load: only fetch data when the user opens the panel (saves bandwidth).
  let loaded = false;
  panel.addEventListener('toggle', async () => {
    if (!panel.open || loaded) return;
    loaded = true;

    const noteEl = panel.querySelector('.history-note');

    try {
      const [goalsRes, homeRes] = await Promise.all([
        fetch('/data/goals-per-decade.json'),
        fetch('/data/home-advantage.json'),
      ]);

      if (!goalsRes.ok || !homeRes.ok) {
        if (noteEl) noteEl.textContent = 'Historical data is not available right now.';
        return;
      }

      const goals = await goalsRes.json();
      const home = await homeRes.json();

      // Update aria-labels with real data (key finding in plain language).
      const maxGoals = goals.reduce((a, b) => a.avgGoals > b.avgGoals ? a : b, goals[0]);
      const goalsCanvas = document.getElementById('chart-goals');
      if (goalsCanvas && maxGoals) {
        goalsCanvas.setAttribute('aria-label',
          `Bar chart: average goals per match in international football by decade. ` +
          `The ${maxGoals.decade} had the highest average at ${maxGoals.avgGoals} goals per match.`);
      }

      const recentHome = home[home.length - 1];
      const homeCanvas = document.getElementById('chart-home');
      if (homeCanvas && recentHome) {
        homeCanvas.setAttribute('aria-label',
          `Line chart: home team win percentage in international football by decade. ` +
          `Most recently the ${recentHome.decade} showed ${recentHome.homeWinPct}% home wins.`);
      }

      // Populate hidden data tables for screen-reader users.
      populateTable('goals-table-body', goals,
        r => r.decade,
        r => r.avgGoals.toFixed(2));
      populateTable('home-table-body', home,
        r => r.decade,
        r => r.homeWinPct.toFixed(1) + '%');

      // Goals per decade — bar chart.
      if (goalsCanvas && window.Chart) {
        new window.Chart(goalsCanvas, {
          type: 'bar',
          data: {
            labels: goals.map(d => d.decade),
            datasets: [{
              label: 'Avg goals per match',
              data: goals.map(d => d.avgGoals),
              backgroundColor: TEAL + 'cc',
              borderColor: TEAL,
              borderWidth: 1,
            }],
          },
          options: {
            ...chartDefaults(),
            scales: {
              ...chartDefaults().scales,
              y: { ...chartDefaults().scales.y, beginAtZero: true },
            },
          },
        });
      }

      // Home win % by decade — line chart.
      if (homeCanvas && window.Chart) {
        new window.Chart(homeCanvas, {
          type: 'line',
          data: {
            labels: home.map(d => d.decade),
            datasets: [{
              label: 'Home win %',
              data: home.map(d => d.homeWinPct),
              borderColor: ACCENT,
              backgroundColor: ACCENT + '33',
              pointBackgroundColor: ACCENT,
              tension: 0.35,
              fill: true,
            }],
          },
          options: {
            ...chartDefaults(),
            scales: {
              ...chartDefaults().scales,
              y: { ...chartDefaults().scales.y, min: 30, max: 80 },
            },
          },
        });
      }

    } catch (err) {
      console.error('Chart load failed:', err);
      if (noteEl) noteEl.textContent = 'Could not load historical data.';
    }
  });
}

loadCharts();
