const LENGTH = 105;
const WIDTH = 68;

export const PITCH_STYLES = Object.freeze({
  classic: { background: '#08251f', line: '#d9eee5', outline: '#ffffff', shadow: '#101010', ball: '#ffd400' },
  light: { background: '#f4f7f5', line: '#24382d', outline: '#101010', shadow: '#ffffff', ball: '#ffd400' },
  soft: { background: '#182322', line: '#b7c9c1', outline: '#cdd9d4', shadow: '#101010', ball: '#ffd400' },
  contrast: { background: '#000000', line: '#ffffff', outline: '#ffffff', shadow: '#000000', ball: '#ffffff', player: '#ffffff' },
  yellow: { background: '#000000', line: '#ffd400', outline: '#ffd400', shadow: '#000000', ball: '#ffd400', player: '#ffd400' },
});
const SYSTEM_STYLE = { background: 'Canvas', line: 'CanvasText', outline: 'CanvasText', shadow: 'Canvas', ball: 'CanvasText', player: 'Canvas', text: 'CanvasText' };

const clamp = (value, min, max) => Math.max(min, Math.min(max, value));

function contrastingText(fill) {
  const channels = fill.slice(1).match(/../g).map(channel => {
    const value = Number.parseInt(channel, 16) / 255;
    return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  });
  const luminance = channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
  return (luminance + 0.05) / 0.05 >= 1.05 / (luminance + 0.05) ? '#000000' : '#ffffff';
}

export function createPitchMap({ canvas, description, zoomIn, zoomOut, zoomLevel, markerSize, pitchStyle, follow, homeKey, awayKey, homeKeeperKey, awayKeeperKey }) {
  const context = canvas.getContext('2d');
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  const forcedColours = window.matchMedia('(forced-colors: active)');
  let message = null;
  let teams = { home: 'Home', away: 'Away', homeKit: 'red', awayKit: 'blue' };
  let zoom = 1;
  let followBall = false;
  let styleName = 'classic';
  let lastDraw = -Infinity;
  let lastDescription = -Infinity;
  let lastSummary = '';
  const colours = { red: '#e53935', blue: '#1e63ff', white: '#ffffff', yellow: '#ffd400', green: '#1faa59', claret: '#7b1e3a', sky_blue: '#64c8ff', orange: '#ff8a2a', black: '#111111', purple: '#7e3ff2' };

  const palette = () => forcedColours.matches ? SYSTEM_STYLE : PITCH_STYLES[styleName];

  function updateLegend() {
    const style = palette();
    const homeFill = style.player ?? colours[teams.homeKit] ?? '#f0f0f0';
    const awayFill = style.player ?? colours[teams.awayKit] ?? '#f0f0f0';
    homeKey.parentElement?.style.setProperty('--kit-colour', homeFill);
    awayKey.parentElement?.style.setProperty('--kit-colour', awayFill);
    homeKeeperKey?.style.setProperty('--keeper-colour', homeFill);
    awayKeeperKey?.style.setProperty('--keeper-colour', awayFill);
    homeKeeperKey?.style.setProperty('--keeper-text', style.text ?? contrastingText(homeFill));
    awayKeeperKey?.style.setProperty('--keeper-text', style.text ?? contrastingText(awayFill));
    for (const [name, value] of Object.entries(style)) {
      canvas.parentElement?.style.setProperty(`--pitch-${name}`, value);
    }
  }

  function setStyle(value) {
    styleName = Object.hasOwn(PITCH_STYLES, value) ? value : 'classic';
    if (pitchStyle) pitchStyle.value = styleName;
    updateLegend();
    draw();
  }

  function setTeams(value) {
    teams = value;
    homeKey.textContent = teams.home;
    awayKey.textContent = teams.away;
    updateLegend();
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
    const style = palette();
    zoomOut.disabled = zoom === 1;
    zoomIn.disabled = zoom === 3;
    const zoomText = `${zoom * 100}%`;
    if (zoomLevel && zoomLevel.textContent !== zoomText) zoomLevel.textContent = zoomText;
    const markerScale = [1, 1.4, 1.8].includes(Number(markerSize?.value)) ? Number(markerSize.value) : 1;
    const bounds = canvas.getBoundingClientRect();
    if (!bounds.width || !bounds.height) {
      return;
    }

    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.round(bounds.width * dpr);
    canvas.height = Math.round(bounds.height * dpr);
    context.setTransform(dpr, 0, 0, dpr, 0, 0);
    context.clearRect(0, 0, bounds.width, bounds.height);
    context.fillStyle = style.background;
    context.fillRect(0, 0, bounds.width, bounds.height);
    lastDraw = performance.now();

    if (!message) {
      return;
    }

    const margin = Math.ceil(22 * markerScale);
    const viewWidth = LENGTH / zoom;
    const viewHeight = WIDTH / zoom;
    const ballX = clamp(Number(message.b?.[0]) || 0, 0, LENGTH);
    const ballY = clamp(Number(message.b?.[1]) || 0, 0, WIDTH);
    const left = followBall ? clamp(ballX - viewWidth / 2, 0, LENGTH - viewWidth) : (LENGTH - viewWidth) / 2;
    const bottom = followBall ? clamp(ballY - viewHeight / 2, 0, WIDTH - viewHeight) : (WIDTH - viewHeight) / 2;
    const portrait = bounds.height > bounds.width;
    const scale = Math.min((bounds.width - margin * 2) / (portrait ? viewHeight : viewWidth), (bounds.height - margin * 2) / (portrait ? viewWidth : viewHeight));
    if (scale <= 0) {
      return;
    }
    const mapWidth = (portrait ? viewHeight : viewWidth) * scale;
    const mapHeight = (portrait ? viewWidth : viewHeight) * scale;
    const plotLeft = (bounds.width - mapWidth) / 2;
    const plotTop = (bounds.height - mapHeight) / 2;
    const point = (x, y) => portrait ? ({
      x: plotLeft + (bottom + viewHeight - y) * scale,
      y: plotTop + (x - left) * scale,
    }) : ({
      x: plotLeft + (x - left) * scale,
      y: plotTop + (bottom + viewHeight - y) * scale,
    });

    context.save();
    context.beginPath();
    context.rect(plotLeft - margin + 4, plotTop - margin + 4, mapWidth + 2 * margin - 8, mapHeight + 2 * margin - 8);
    context.clip();
    context.strokeStyle = style.line;
    context.lineWidth = 3;
    const field = point(0, WIDTH);
    const fieldEnd = point(LENGTH, 0);
    context.strokeRect(field.x, field.y, fieldEnd.x - field.x, fieldEnd.y - field.y);
    drawGoalEnd(0, point, context);
    drawGoalEnd(LENGTH, point, context);
    const halfway = point(LENGTH / 2, WIDTH);
    const halfwayEnd = point(LENGTH / 2, 0);
    context.beginPath();
    context.moveTo(halfway.x, halfway.y);
    context.lineTo(halfwayEnd.x, halfwayEnd.y);
    context.stroke();
    const centre = point(LENGTH / 2, WIDTH / 2);
    context.beginPath();
    context.arc(centre.x, centre.y, scale * 9.15, 0, Math.PI * 2);
    context.stroke();

    const visible = (x, y) => x >= left && x <= left + viewWidth && y >= bottom && y <= bottom + viewHeight;
    drawPlayers(message.h, teams.homeKit, true, point, visible, markerScale);
    drawPlayers(message.a, teams.awayKit, false, point, visible, markerScale);

    const ball = point(ballX, ballY);
    if (visible(ballX, ballY)) {
      const rings = [[18, style.shadow], [15, style.outline], [12, style.shadow], [8, style.ball]];
      for (const [radius, colour] of rings) {
        context.beginPath();
        context.arc(ball.x, ball.y, radius * markerScale, 0, Math.PI * 2);
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
    context.fillStyle = palette().background;
    context.beginPath();
    context.moveTo(frontTop.x, frontTop.y);
    context.lineTo(backTop.x, backTop.y);
    context.lineTo(backBottom.x, backBottom.y);
    context.lineTo(frontBottom.x, frontBottom.y);
    context.closePath();
    context.fill();
    context.strokeStyle = palette().line;
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

  function drawPlayers(players, kit, isHome, point, visible, markerScale) {
    const style = palette();
    const fill = style.player ?? colours[kit] ?? '#f0f0f0';
    const numberColour = style.text ?? contrastingText(fill);
    for (const [x, y, number, goalkeeper] of players ?? []) {
      if (!visible(x, y)) {
        continue;
      }
      const position = point(x, y);
      context.beginPath();
      if (goalkeeper) {
        context.rect(position.x - 15 * markerScale, position.y - 15 * markerScale, 30 * markerScale, 30 * markerScale);
      } else if (isHome) {
        context.arc(position.x, position.y, 15 * markerScale, 0, Math.PI * 2);
      } else {
        context.moveTo(position.x, position.y - 19 * markerScale);
        context.lineTo(position.x + 18 * markerScale, position.y + 15 * markerScale);
        context.lineTo(position.x - 18 * markerScale, position.y + 15 * markerScale);
        context.closePath();
      }

      context.fillStyle = fill;
      context.fill();
      context.strokeStyle = style.shadow;
      context.lineWidth = 6;
      context.stroke();
      context.strokeStyle = style.outline;
      context.lineWidth = 3;
      context.stroke();
      context.font = `bold ${Math.round(14 * markerScale)}px "Atkinson Hyperlegible", sans-serif`;
      context.textAlign = 'center';
      context.textBaseline = 'middle';
      const ball = point(clamp(Number(message.b?.[0]) || 0, 0, LENGTH), clamp(Number(message.b?.[1]) || 0, 0, WIDTH));
      const overlapsBall = Math.hypot(position.x - ball.x, position.y - ball.y) < 36 * markerScale;
      const numberY = overlapsBall ? position.y + (position.y < canvas.height / (window.devicePixelRatio || 1) / 2 ? 36 : -36) * markerScale : position.y + (isHome || goalkeeper ? 0 : 3 * markerScale);
      const label = goalkeeper ? `${isHome ? 'H' : 'A'}${number}` : String(number);
      if (overlapsBall) {
        context.fillStyle = style.background;
        context.fillRect(position.x - 15 * markerScale, numberY - 10 * markerScale, 30 * markerScale, 20 * markerScale);
      }
      context.fillStyle = overlapsBall ? style.line : numberColour;
      context.fillText(label, position.x, numberY);
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
  markerSize?.addEventListener('change', draw);
  pitchStyle?.addEventListener('change', () => setStyle(pitchStyle.value));
  reducedMotion.addEventListener('change', draw);
  forcedColours.addEventListener('change', () => { updateLegend(); draw(); });
  setStyle(pitchStyle?.value);

  return { update, setTeams, setStyle };
}