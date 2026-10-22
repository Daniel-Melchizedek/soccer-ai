const MAX_GAIN = 0.12;

export function createCrowdBed(ctx, bus) {
  const frames = ctx.sampleRate * 2;
  const buffer = ctx.createBuffer(1, frames, ctx.sampleRate);
  const samples = buffer.getChannelData(0);
  for (let i = 0; i < frames; i++) {
    samples[i] = Math.random() * 2 - 1;
  }

  const source = new AudioBufferSourceNode(ctx, { buffer, loop: true });
  const highpass = new BiquadFilterNode(ctx, { type: 'highpass', frequency: 120, Q: 0.4 });
  const lowpass = new BiquadFilterNode(ctx, { type: 'lowpass', frequency: 520, Q: 0.5 });
  const gain = new GainNode(ctx, { gain: 0 });
  source.connect(highpass).connect(lowpass).connect(gain).connect(bus);
  source.start();

  let energy = 0;
  let pulseEnergy = 0;
  let pulseTimer = null;

  function apply() {
    gain.gain.setTargetAtTime(Math.max(energy, pulseEnergy) * MAX_GAIN, ctx.currentTime, 0.35);
  }

  function update(value) {
    energy = Math.max(0, Math.min(1, Number(value) || 0));
    apply();
  }

  function pulse(value = 1) {
    pulseEnergy = Math.max(pulseEnergy, Math.max(0, Math.min(1, value)));
    apply();
    clearTimeout(pulseTimer);
    // Extended to 3000 ms for a more emphatic goal celebration crowd roar.
    pulseTimer = setTimeout(() => {
      pulseEnergy = 0;
      apply();
    }, 3000);
  }

  function dispose() {
    clearTimeout(pulseTimer);
    gain.gain.setTargetAtTime(0, ctx.currentTime, 0.1);
    source.stop(ctx.currentTime + 0.35);
  }

  return { update, pulse, dispose };
}