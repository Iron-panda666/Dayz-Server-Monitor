export class KeyGesture {
  constructor(shortPress, longPress, timers = globalThis) { this.shortPress = shortPress; this.longPress = longPress; this.timers = timers; this.timer = null; this.down = false; this.long = false; }
  press() {
    if (this.down) return;
    this.down = true; this.long = false;
    this.timer = this.timers.setTimeout(() => { this.timer = null; if (this.down) { this.long = true; this.longPress(); } }, 1000);
  }
  release() {
    if (!this.down) return;
    if (this.timer !== null) this.timers.clearTimeout(this.timer);
    this.timer = null; this.down = false;
    if (!this.long) this.shortPress();
  }
  cancel() { if (this.timer !== null) this.timers.clearTimeout(this.timer); this.timer = null; this.down = false; }
}
