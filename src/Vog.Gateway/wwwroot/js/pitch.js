const LENGTH = 105;
const WIDTH = 68;

const clamp = (value, min, max) => Math.max(min, Math.min(max, value));

export function createPitchMap({ canvas, description, zoomIn, zoomOut, follow, homeKey, awayKey }) {
  const context = canvas.getContext('2d');
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  let message = null;
  let teams = { home: 'Home', away: 'Away', homeKit: 'red', awayKit: 'blue' };
  let zoom = 1;
  let followBall = false;
  let lastDraw = -Infinity;
  let lastDescription = -Infinity;
  let lastSummary = '';
  const colours = { red: '#e53935', blue: '#1e63ff', white: '#ffffff', yellow: '#ffd400', green: '#1faa59', claret: '#7b1e3a', sky_blue: '#64c8ff', orange: '#ff8a2a', black: '#111111', purple: '#7e3ff2' };

  function setTeams(value) {
    teams = value;
    homeKey.textContent = teams.home;
    awayKey.textContent = teams.away;
    draw();
  }

  function update(value) {
    message = value;
    updateDescription();
    if (reducedMotion.matches && performance.now() - lastDraw < 500) {
      return;
    }

    draw();
  }

  function updateDescription() {
    const now = performance.now();
    if (!message || now - lastDescription < 5000) {
      return;
    }

    lastDescription = now;
    const x = clamp(Number(message.b?.[0]) || 0, 0, LENGTH);
    const owner = message.o === 'h' ? teams.home : message.o === 'a' ? teams.away : null;
    const attackingRight = message.o === 'h' ? Boolean(message.hr) : message.o === 'a' ? !message.hr : null;
    const depth = attackingRight === null ? x / LENGTH : (attackingRight ? x : LENGTH - x) / LENGTH;
    const zone = depth >= 2 / 3 ? 'attacking third' : depth >= 1 / 3 ? 'middle third' : 'defensive third';
    const summary = `Ball: ${zone}${owner ? `, ${owner} in possession` : ', no team in possession'}.`;
    if (summary !== lastSummary) {
      description.textContent = summary;
      lastSummary = summary;
    }
  }

  function draw() {
    zoomOut.disabled = zoom === 1;
    zoomIn.disabled = zoom === 3;
    const bounds = canvas.getBoundingClientRect();
    if (!bounds.width || !bounds.height) {
      return;
    }

    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.round(bounds.width * dpr);
    canvas.height = Math.round(bounds.height * dpr);
    context.setTransform(dpr, 0, 0, dpr, 0, 0);
    context.clearRect(0, 0, bounds.width, bounds.height);
    context.fillStyle = '#08251f';
    context.fillRect(0, 0, bounds.width, bounds.height);
    lastDraw = performance.now();

    if (!message) {
      return;
    }

    const margin = 22;
    const viewWidth = LENGTH / zoom;
    const viewHeight = WIDTH / zoom;
    const ballX = clamp(Number(message.b?.[0]) || 0, 0, LENGTH);
    const ballY = clamp(Number(message.b?.[1]) || 0, 0, WIDTH);
    const left = followBall ? clamp(ballX - viewWidth / 2, 0, LENGTH - viewWidth) : (LENGTH - viewWidth) / 2;
    const bottom = followBall ? clamp(ballY - viewHeight / 2, 0, WIDTH - viewHeight) : (WIDTH - viewHeight) / 2;
    const scale = Math.min((bounds.width - margin * 2) / viewWidth, (bounds.height - margin * 2) / viewHeight);
    if (scale <= 0) {
      return;
    }
    const mapWidth = viewWidth * scale;
    const mapHeight = viewHeight * scale;
    const plotLeft = (bounds.width - mapWidth) / 2;
    const plotTop = (bounds.height - mapHeight) / 2;
    const point = (x, y) => ({
      x: plotLeft + (x - left) * scale,
      y: plotTop + (bottom + viewHeight - y) * scale,
    });

    context.save();
    context.beginPath();
    context.rect(plotLeft - 16, plotTop - 16, mapWidth + 32, mapHeight + 32);
    context.clip();
    context.strokeStyle = '#d9eee5';
    context.lineWidth = 2;
    const field = point(0, WIDTH);
    context.strokeRect(field.x, field.y, LENGTH * scale, WIDTH * scale);
    drawGoalEnd(0, point, context);
    drawGoalEnd(LENGTH, point, context);
    const halfway = point(LENGTH / 2, WIDTH);
    context.beginPath();
    context.moveTo(halfway.x, halfway.y);
    context.lineTo(halfway.x, point(LENGTH / 2, 0).y);
    context.stroke();
    const centre = point(LENGTH / 2, WIDTH / 2);
    context.beginPath();
    context.arc(centre.x, centre.y, scale * 9.15, 0, Math.PI * 2);
    context.stroke();

    const visible = (x, y) => x >= left && x <= left + viewWidth && y >= bottom && y <= bottom + viewHeight;
    drawPlayers(message.h, teams.homeKit, true, point, visible);
    drawPlayers(message.a, teams.awayKit, false, point, visible);

    const ball = point(ballX, ballY);
    if (visible(ballX, ballY)) {
      for (const [radius, colour] of [[14, '#101010'], [12, '#ffffff'], [10, '#101010'], [8, '#ffd400']]) {
        context.beginPath();
        context.arc(ball.x, ball.y, radius, 0, Math.PI * 2);
        context.fillStyle = colour;
        context.fill();
      }
    }
    context.restore();
  }

  function drawGoalEnd(goalX, point, context) {
    const goalTop = (WIDTH - 7.32) / 2;
    const goalBottom = (WIDTH + 7.32) / 2;
    const penaltyTop = (WIDTH - 40.32) / 2;
    const penaltyBottom = (WIDTH + 40.32) / 2;
    const goalAreaTop = (WIDTH - 18.32) / 2;
    const goalAreaBottom = (WIDTH + 18.32) / 2;
    const inward = goalX === 0 ? 1 : -1;
    const rectangle = (depth, top, bottom) => {
      const a = point(goalX, top);
      const b = point(goalX + inward * depth, bottom);
      context.strokeRect(Math.min(a.x, b.x), Math.min(a.y, b.y), Math.abs(b.x - a.x), Math.abs(b.y - a.y));
    };

    rectangle(16.5, penaltyTop, penaltyBottom);
    rectangle(5.5, goalAreaTop, goalAreaBottom);

    // A shallow, cross-hatched net makes goals clear at a glance without obscuring play.
    const netDepth = 2.2;
    const frontTop = point(goalX, goalTop);
    const frontBottom = point(goalX, goalBottom);
    const backTop = point(goalX + inward * netDepth, goalTop);
    const backBottom = point(goalX + inward * netDepth, goalBottom);
    context.save();
    context.fillStyle = 'rgba(255, 255, 255, 0.12)';
    context.beginPath();
    context.moveTo(frontTop.x, frontTop.y);
    context.lineTo(backTop.x, backTop.y);
    context.lineTo(backBottom.x, backBottom.y);
    context.lineTo(frontBottom.x, frontBottom.y);
    context.closePath();
    context.fill();
    context.strokeStyle = '#ffffff';
    context.lineWidth = 2.5;
    context.stroke();
    context.setLineDash([3, 3]);
    context.lineWidth = 1;
    for (let t = 0.2; t < 1; t += 0.2) {
      const x1 = frontTop.x + (backTop.x - frontTop.x) * t;
      const y1 = frontTop.y + (backTop.y - frontTop.y) * t;
      const x2 = frontBottom.x + (backBottom.x - frontBottom.x) * t;
      const y2 = frontBottom.y + (backBottom.y - frontBottom.y) * t;
      context.beginPath();
      context.moveTo(x1, y1);
      context.lineTo(x2, y2);
      context.stroke();
    }
    context.restore();
  }

  function drawPlayers(players, kit, isHome, point, visible) {
    const fill = colours[kit] ?? '#f0f0f0';
    for (const [x, y, number, goalkeeper] of players ?? []) {
      if (!visible(x, y)) {
        continue;
      }
      const position = point(x, y);
      context.beginPath();
      if (goalkeeper) {
        context.rect(position.x - 8, position.y - 8, 16, 16);
      } else if (isHome) {
        context.arc(position.x, position.y, 9, 0, Math.PI * 2);
      } else {
        context.moveTo(position.x, position.y - 10);
        context.lineTo(position.x + 9, position.y + 7);
        context.lineTo(position.x - 9, position.y + 7);
        context.closePath();
      }

      // Goalkeepers use gold (#ffd400) regardless of kit colour, matching the legend.
      // Shape and shirt number remain the primary accessible signals; colour closes the
      // legend-vs-canvas gap for sighted and low-vision users.
      context.fillStyle = goalkeeper ? '#ffd400' : fill;
      context.fill();
      // A diagonal hatch makes the two teams distinguishable without relying on colour.
      // For goalkeepers (gold fill) use dark stripes to maintain contrast.
      context.save();
      context.clip();
      context.strokeStyle = goalkeeper ? 'rgba(0, 0, 0, 0.55)' : (isHome ? 'rgba(0, 0, 0, 0.6)' : 'rgba(255, 255, 255, 0.8)');
      context.lineWidth = 1.25;
      for (let offset = -12; offset <= 12; offset += 5) {
        context.beginPath();
        context.moveTo(position.x - 12, position.y + offset);
        context.lineTo(position.x + 12, position.y + offset - 12);
        context.stroke();
      }
      context.restore();
      context.strokeStyle = '#ffffff';
      context.lineWidth = 2;
      context.stroke();
      context.fillStyle = '#000000';
      context.font = 'bold 10px system-ui';
      context.textAlign = 'center';
      context.textBaseline = 'middle';
      context.lineWidth = 2.5;
      context.strokeStyle = '#ffffff';
      const ball = point(clamp(Number(message.b?.[0]) || 0, 0, LENGTH), clamp(Number(message.b?.[1]) || 0, 0, WIDTH));
      const overlapsBall = Math.hypot(position.x - ball.x, position.y - ball.y) < 24;
      const numberY = overlapsBall ? position.y + (position.y < canvas.height / (window.devicePixelRatio || 1) / 2 ? 25 : -25) : position.y;
      context.strokeText(String(number), position.x, numberY);
      context.fillText(String(number), position.x, numberY);
    }
  }

  const resizeObserver = new ResizeObserver(draw);
  resizeObserver.observe(canvas);
  zoomIn.addEventListener('click', () => {
    zoom = Math.min(3, zoom + 0.5);
    draw();
  });
  zoomOut.addEventListener('click', () => {
    zoom = Math.max(1, zoom - 0.5);
    draw();
  });
  follow.addEventListener('change', () => {
    followBall = follow.checked;
    draw();
  });
  reducedMotion.addEventListener('change', draw);

  return { update, setTeams };
}