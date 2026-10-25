/** A short two-note chime for new orders, made with Web Audio so no sound file is needed. */
export function playChime(): void {
  try {
    const context = new AudioContext();
    const start = context.currentTime;
    for (const [i, frequency] of [880, 1320].entries()) {
      const oscillator = context.createOscillator();
      const gain = context.createGain();
      oscillator.type = 'sine';
      oscillator.frequency.value = frequency;
      const at = start + i * 0.18;
      gain.gain.setValueAtTime(0.0001, at);
      gain.gain.exponentialRampToValueAtTime(0.4, at + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, at + 0.5);
      oscillator.connect(gain).connect(context.destination);
      oscillator.start(at);
      oscillator.stop(at + 0.55);
    }
    setTimeout(() => void context.close(), 1200);
    navigator.vibrate?.([200, 100, 200]);
  } catch {
    // Audio can be unavailable (no interaction yet, or blocked); the banner still shows.
  }
}
