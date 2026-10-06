const LENGTH = 105;
const WIDTH = 68;

const clamp = (value, min, max) => Math.max(min, Math.min(max, value));

export function createPitchMap({ canvas, description, zoomIn, zoomOut, follow }) {
  const context = canvas.getContext('2d');
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  let message = null;
  let teams = { home: 'Home', away: 'Away', homeKit: 'red', awayKit: 'blue' };
  let zoom = 1;
  let followBall = false;
  let lastDraw = -Infinity;
  let lastDescription = -Infinity;
  const colours = { red: '#e53935', blue: '#1e63ff', white: '#ffffff', yellow: '#ffd400', green: '#1faa59', claret: '#7b1e3a', sky_blue: '#64c8ff', orange: '#ff8a2a', black: '#111111', purple: '#7e3ff2' };

  function setTeams(value) {
    teams = value;
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
    description.textContent = `Ball: ${zone}${owner ? `, ${owner} in possession` : ', no team in possession'}.`;
  }

  function draw() {
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

    const margin = 12;
    const viewWidth = LENGTH / zoom;
    const viewHeight = WIDTH / zoom;
    const ballX = clamp(Number(message.b?.[0]) || 0, 0, LENGTH);
    const ballY = clamp(Number(message.b?.[1]) || 0, 0, WIDTH);
    const left = followBall ? clamp(ballX - viewWidth / 2, 0, LENGTH - viewWidth) : (LENGTH - viewWidth) / 2;
    const bottom = followBall ? clamp(ballY - viewHeight / 2, 0, WIDTH - viewHeight) : (WIDTH - viewHeight) / 2;
    const mapWidth = bounds.width - margin * 2;
    const mapHeight = bounds.height - margin * 2;
    const point = (x, y) => ({
      x: margin + ((x - left) / viewWidth) * mapWidth,
      y: margin + ((bottom + viewHeight - y) / viewHeight) * mapHeight,
    });

    context.strokeStyle = '#d9eee5';
    context.lineWidth = 2;
    context.strokeRect(...[point(left, bottom + viewHeight).x, point(left, bottom + viewHeight).y, mapWidth, mapHeight]);
    const halfway = point(LENGTH / 2, bottom + viewHeight);
    context.beginPath();
    context.moveTo(halfway.x, margin);
    context.lineTo(halfway.x, margin + mapHeight);
    context.stroke();
    const centre = point(LENGTH / 2, WIDTH / 2);
    context.beginPath();
    context.arc(centre.x, centre.y, Math.min(mapWidth / viewWidth, mapHeight / viewHeight) * 9.15, 0, Math.PI * 2);
    context.stroke();

    drawPlayers(message.h, teams.homeKit, true, point);
    drawPlayers(message.a, teams.awayKit, false, point);

    const ball = point(ballX, ballY);
    context.beginPath();
    context.arc(ball.x, ball.y, 7, 0, Math.PI * 2);
    context.fillStyle = '#ffffff';
    context.fill();
    context.strokeStyle = '#101010';
    context.lineWidth = 2;
    context.stroke();
  }

  function drawPlayers(players, kit, isHome, point) {
    const fill = colours[kit] ?? '#f0f0f0';
    for (const [x, y, number] of players ?? []) {
      const position = point(x, y);
      context.beginPath();
      if (isHome) {
        context.arc(position.x, position.y, 9, 0, Math.PI * 2);
      } else {
        context.moveTo(position.x, position.y - 10);
        context.lineTo(position.x + 9, position.y + 7);
        context.lineTo(position.x - 9, position.y + 7);
        context.closePath();
      }

      context.fillStyle = fill;
      context.fill();
      context.strokeStyle = '#ffffff';
      context.lineWidth = 1.5;
      context.stroke();
      context.fillStyle = '#ffffff';
      context.font = 'bold 9px system-ui';
      context.textAlign = 'center';
      context.textBaseline = 'middle';
      context.fillText(String(number), position.x, position.y);
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