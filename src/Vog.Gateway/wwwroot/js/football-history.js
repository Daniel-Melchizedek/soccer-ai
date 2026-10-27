const element = id => document.getElementById(id);
const plots = [
  { id: 'goals', file: 'goals-per-decade.json', field: 'avgGoals', label: 'Average goals per match', type: 'bar', color: '#2fe0d4', digits: 2 },
  { id: 'home', file: 'home-advantage.json', field: 'homeWinPct', label: 'Home-team wins (%)', type: 'line', color: '#ff8a2a', digits: 1 }
];

async function load(plot) {
  const status = element(`${plot.id}-status`);
  const retry = element(`${plot.id}-retry`);
  const figure = element(`${plot.id}-figure`);
  const data = element(`${plot.id}-data`);
  const body = element(`${plot.id}-rows`);
  retry.hidden = true;
  status.textContent = 'Loading historical results...';
  try {
    const response = await fetch(`/data/${plot.file}`, { signal: AbortSignal.timeout(12000) });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const rows = await response.json();
    if (!Array.isArray(rows) || !rows.length || rows.some(row => !row || !/^\d{4}s$/.test(row.decade)
      || !Number.isFinite(row[plot.field]) || row[plot.field] < 0 || (plot.id === 'home' && row[plot.field] > 100))
      || new Set(rows.map(row => row.decade)).size !== rows.length) throw new Error('Invalid aggregate');
    rows.sort((first, second) => first.decade.localeCompare(second.decade));
    body.replaceChildren();
    for (const row of rows) {
      const line = document.createElement('tr');
      const decade = document.createElement('th');
      decade.scope = 'row';
      decade.textContent = row.decade;
      const value = document.createElement('td');
      value.textContent = row[plot.field].toFixed(plot.digits) + (plot.id === 'home' ? '%' : '');
      line.append(decade, value);
      body.append(line);
    }
    data.hidden = false;
    if (!globalThis.Chart) {
      status.textContent = 'Chart unavailable. Historical values are available in the data table.';
      data.open = true;
      return;
    }
    figure.hidden = false;
    plot.chart?.destroy();
    plot.chart = new globalThis.Chart(element(`${plot.id}-chart`), {
      type: plot.type,
      data: {
        labels: rows.map(row => row.decade),
        datasets: [{ label: plot.label, data: rows.map(row => row[plot.field]),
          backgroundColor: plot.color, borderColor: plot.color, borderWidth: 2, pointRadius: 3, tension: 0 }]
      },
      options: {
        responsive: true, maintainAspectRatio: false, animation: false,
        plugins: { legend: { display: false } },
        scales: {
          x: { ticks: { color: '#d4c6f0', maxRotation: 0 }, grid: { display: false } },
          y: { beginAtZero: true, ...(plot.id === 'home' ? { max: 100 } : {}),
            ticks: { color: '#d4c6f0' }, grid: { color: '#4a2d7a' } }
        }
      }
    });
    status.textContent = `${rows.length} decades: ${rows[0].decade} to ${rows.at(-1).decade}.`;
  } catch {
    plot.chart?.destroy();
    plot.chart = null;
    figure.hidden = true;
    data.hidden = true;
    body.replaceChildren();
    status.textContent = 'Historical data is unavailable. Try again.';
    retry.hidden = false;
  }
}

for (const plot of plots) {
  element(`${plot.id}-retry`).addEventListener('click', () => load(plot));
  load(plot);
}