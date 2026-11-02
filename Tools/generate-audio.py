"""Parkology sound set, rendered offline as deterministic mono PCM WAV files.

Run from the project root:  python3 Tools/generate-audio.py

Everything is synthesised from scratch (no samples), so the files are original.
The palette is "toy plastic": bricks click with short bright transients, trucks
toot like a toy horn, and rewards use a C-major marimba/glockenspiel.
"""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 44100
OUT = Path('Assets/Resources/Audio')
TAU = 2 * math.pi


# ---------------------------------------------------------------- primitives

def buf(seconds):
    return [0.0] * int(seconds * RATE)


def mix(dst, src, at=0.0, gain=1.0):
    start = int(at * RATE)
    need = start + len(src)
    if need > len(dst):
        dst.extend([0.0] * (need - len(dst)))
    for i, v in enumerate(src):
        dst[start + i] += v * gain
    return dst


def env_adsr(n, a, d, s, r, sustain_level):
    out = []
    a_n, d_n, r_n = int(a * RATE), int(d * RATE), int(r * RATE)
    s_n = max(0, n - a_n - d_n - r_n)
    for i in range(n):
        if i < a_n:
            out.append(i / max(1, a_n))
        elif i < a_n + d_n:
            t = (i - a_n) / max(1, d_n)
            out.append(1 - (1 - sustain_level) * t)
        elif i < a_n + d_n + s_n:
            out.append(sustain_level)
        else:
            t = (i - a_n - d_n - s_n) / max(1, r_n)
            out.append(sustain_level * max(0.0, 1 - t))
    return out


def tone(freq, seconds, partials=((1, 1.0),), decay=8.0, attack=0.002, glide=0.0, vibrato=0.0, rng=None):
    """Additive tone with exponential decay. glide multiplies frequency over time."""
    n = int(seconds * RATE)
    out = [0.0] * n
    rng = rng or random.Random(1)
    for ratio, amp in partials:
        phase = rng.uniform(0, TAU)
        k = math.exp(-decay * (1 + 0.35 * (ratio - 1)) / RATE)
        e = amp
        for i in range(n):
            t = i / RATE
            f = freq * ratio * (1 + glide * t) * (1 + vibrato * math.sin(TAU * 6 * t))
            phase += TAU * f / RATE
            out[i] += math.sin(phase) * e
            e *= k
    a = max(1, int(attack * RATE))
    for i in range(min(a, n)):
        out[i] *= i / a
    return out


class Biquad:
    def __init__(self, kind, freq, q=0.707):
        w = TAU * freq / RATE
        alpha = math.sin(w) / (2 * q)
        c = math.cos(w)
        if kind == 'lowpass':
            b = [(1 - c) / 2, 1 - c, (1 - c) / 2]
        elif kind == 'highpass':
            b = [(1 + c) / 2, -(1 + c), (1 + c) / 2]
        else:
            b = [alpha, 0, -alpha]
        a0 = 1 + alpha
        self.b = [x / a0 for x in b]
        self.a = [(-2 * c) / a0, (1 - alpha) / a0]
        self.x1 = self.x2 = self.y1 = self.y2 = 0.0

    def __call__(self, x):
        y = self.b[0] * x + self.b[1] * self.x1 + self.b[2] * self.x2 - self.a[0] * self.y1 - self.a[1] * self.y2
        self.x2, self.x1, self.y2, self.y1 = self.x1, x, self.y1, y
        return y


def noise(seconds, rng, kind='bandpass', freq=3000, q=1.0, decay=40.0, attack=0.0005):
    f = Biquad(kind, freq, q)
    n = int(seconds * RATE)
    k = math.exp(-decay / RATE)
    e = 1.0
    out = []
    a = max(1, int(attack * RATE))
    for i in range(n):
        v = f(rng.uniform(-1, 1)) * e
        if i < a:
            v *= i / a
        out.append(v)
        e *= k
    return out


def sweep_noise(seconds, rng, f0, f1, q=2.0, curve=None):
    """Band-passed noise whose centre glides from f0 to f1 (whooshes)."""
    n = int(seconds * RATE)
    out = []
    x1 = x2 = y1 = y2 = 0.0
    for i in range(n):
        t = i / n
        f = f0 * (f1 / f0) ** t
        w = TAU * f / RATE
        alpha = math.sin(w) / (2 * q)
        c = math.cos(w)
        a0 = 1 + alpha
        b0, b2 = alpha / a0, -alpha / a0
        a1, a2 = -2 * c / a0, (1 - alpha) / a0
        x = rng.uniform(-1, 1)
        y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, x, y1, y
        env = math.sin(math.pi * t) ** (curve or 1.5)
        out.append(y * env)
    return out


def lowpass(samples, freq, q=0.707):
    f = Biquad('lowpass', freq, q)
    return [f(v) for v in samples]


def highpass(samples, freq, q=0.707):
    f = Biquad('highpass', freq, q)
    return [f(v) for v in samples]


def fade_out(samples, seconds=0.01):
    n = min(len(samples), int(seconds * RATE))
    for i in range(n):
        samples[-1 - i] *= i / n
    return samples


def normalize(samples, peak=0.85):
    m = max(1e-9, max(abs(v) for v in samples))
    return [v * peak / m for v in samples]


def soft_clip(samples, drive=1.4):
    return [math.tanh(v * drive) / math.tanh(drive) for v in samples]


def write(name, samples, peak=0.85):
    OUT.mkdir(parents=True, exist_ok=True)
    samples = normalize(fade_out(list(samples)), peak)
    with wave.open(str(OUT / f'{name}.wav'), 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, v)) * 32767)) for v in samples))
    print('wrote', name, f'{len(samples) / RATE:.2f}s')


def note(n):
    """MIDI note number to Hz."""
    return 440.0 * 2 ** ((n - 69) / 12)


MARIMBA = ((1, 1.0), (3.9, 0.35), (9.2, 0.08))
GLOCK = ((1, 1.0), (2.76, 0.4), (5.4, 0.18), (8.9, 0.06))
PLUCK = ((1, 1.0), (2, 0.45), (3, 0.2), (4, 0.1))


# ---------------------------------------------------------------- effects

def plastic_click(rng, pitch=1.0, weight=1.0):
    """Lego stud click: two tiny bright transients and a short hollow body."""
    out = buf(0.16)
    mix(out, noise(0.03, rng, 'bandpass', 4200 * pitch, 2.2, 260), 0.0, 0.9 * weight)
    mix(out, noise(0.025, rng, 'bandpass', 6100 * pitch, 3.0, 320), 0.012, 0.55)
    mix(out, tone(1250 * pitch, 0.09, ((1, 1.0), (2.3, 0.4)), decay=60, rng=rng), 0.0, 0.45)
    mix(out, tone(320 * pitch, 0.12, ((1, 1.0), (1.5, 0.3)), decay=38, rng=rng), 0.004, 0.55 * weight)
    return out


def ui_tap(rng):
    out = buf(0.09)
    mix(out, tone(880, 0.07, ((1, 1.0), (2, 0.25)), decay=55, glide=1.6, rng=rng), 0, 0.8)
    mix(out, noise(0.015, rng, 'bandpass', 3000, 1.5, 300), 0, 0.3)
    return out


def hop(rng, pitch=1.0):
    out = buf(0.26)
    mix(out, tone(420 * pitch, 0.22, ((1, 1.0), (2, 0.18)), decay=11, glide=2.4, attack=0.004, rng=rng), 0, 0.55)
    mix(out, sweep_noise(0.2, rng, 900, 4200, 1.4), 0, 0.22)
    return out


def drop(rng, pitch=1.0):
    out = buf(0.14)
    mix(out, tone(210 * pitch, 0.12, ((1, 1.0), (2.7, 0.3)), decay=34, rng=rng), 0, 0.8)
    mix(out, noise(0.03, rng, 'bandpass', 2300 * pitch, 1.6, 140), 0, 0.5)
    return out


def engine_go(rng):
    """Toy truck rev: buzzy low saw rising, with tyre swish."""
    seconds = 0.42
    n = int(seconds * RATE)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / RATE
        f = 70 + 120 * (t / seconds) ** 0.7
        phase += f / RATE
        saw = 2 * (phase % 1.0) - 1
        wob = 0.6 + 0.4 * math.sin(TAU * 22 * t)
        e = min(1, t / 0.02) * (1 - (t / seconds) ** 2)
        out.append(saw * wob * e)
    out = lowpass(out, 900, 0.9)
    mix(out, sweep_noise(0.35, rng, 600, 2600, 1.2), 0.05, 0.35)
    return out


def blocked(rng):
    out = buf(0.32)
    for k, at in enumerate((0.0, 0.13)):
        mix(out, tone(150 - 25 * k, 0.16, ((1, 1.0), (2, 0.5), (3, 0.25)), decay=22, rng=rng), at, 0.8)
        mix(out, noise(0.04, rng, 'lowpass', 600, 0.8, 80), at, 0.4)
    return soft_clip(out, 1.8)


def stack_complete(rng):
    out = buf(0.9)
    for k, n in enumerate((72, 76, 79, 84)):
        mix(out, tone(note(n), 0.6, GLOCK, decay=6, rng=rng), k * 0.065, 0.5)
    mix(out, sweep_noise(0.5, rng, 3000, 9000, 3.0), 0.1, 0.12)
    return out


def toy_horn(rng):
    """Beep-beep of a little truck leaving."""
    out = buf(0.5)
    for at in (0.0, 0.2):
        n = int(0.14 * RATE)
        part = []
        phase = 0.0
        for i in range(n):
            t = i / RATE
            phase += 520 / RATE
            sq = 1.0 if (phase % 1.0) < 0.42 else -1.0
            e = min(1, t / 0.01) * min(1, (0.14 - t) / 0.03)
            part.append(sq * e)
        part = lowpass(part, 1900, 1.2)
        mix(out, part, at, 0.6)
        mix(out, tone(780, 0.14, ((1, 1.0),), decay=6, rng=rng), at, 0.2)
    return out


def reveal(rng):
    out = buf(0.7)
    for k in range(7):
        n = (79, 83, 86, 88, 91, 95, 98)[k]
        mix(out, tone(note(n), 0.3, GLOCK, decay=14, rng=rng), k * 0.04, 0.32)
    mix(out, sweep_noise(0.5, rng, 5000, 11000, 4), 0, 0.15)
    return out


def hammer(rng):
    out = buf(0.75)
    mix(out, tone(95, 0.3, ((1, 1.0), (1.6, 0.4)), decay=14, glide=-0.5, rng=rng), 0, 1.0)
    mix(out, noise(0.08, rng, 'lowpass', 1200, 0.7, 45), 0, 0.9)
    for k in range(9):
        at = 0.05 + k * 0.035 + rng.uniform(0, 0.03)
        mix(out, plastic_click(rng, rng.uniform(0.8, 1.5), 0.5), at, 0.35 * (1 - k / 11))
    return soft_clip(out, 1.6)


def power_up(rng):
    out = buf(0.75)
    for k, n in enumerate((60, 64, 67, 72, 76, 79)):
        mix(out, tone(note(n), 0.25, PLUCK, decay=16, rng=rng), k * 0.06, 0.45)
    mix(out, tone(note(84), 0.4, GLOCK, decay=7, rng=rng), 0.36, 0.5)
    return out


def magnet(rng):
    out = buf(0.6)
    mix(out, tone(300, 0.5, ((1, 1.0), (2, 0.4), (3, 0.2)), decay=4, glide=1.8, vibrato=0.06, attack=0.01, rng=rng), 0, 0.5)
    mix(out, sweep_noise(0.45, rng, 400, 3000, 2.5), 0.05, 0.3)
    return out


def coin(rng):
    out = buf(0.45)
    mix(out, tone(note(88), 0.12, GLOCK, decay=20, rng=rng), 0, 0.6)
    mix(out, tone(note(93), 0.35, GLOCK, decay=9, rng=rng), 0.075, 0.6)
    return out


def whoosh(rng):
    return sweep_noise(0.45, rng, 300, 3800, 1.3, curve=1.2)


def toot(rng):
    """Steam-toy train whistle for the splash."""
    out = buf(0.9)
    for (n, at, dur) in ((79, 0.0, 0.28), (84, 0.32, 0.5)):
        seg = buf(dur)
        for f_ratio, amp in ((1, 1.0), (1.26, 0.6), (1.5, 0.5)):
            mix(seg, tone(note(n) * f_ratio, dur, ((1, 1.0), (2, 0.15)), decay=1.5, attack=0.03, vibrato=0.004, rng=rng), 0, amp)
        e = env_adsr(len(seg), 0.03, 0.05, 0.85, 0.08, 0.85)
        seg = [v * e[i] for i, v in enumerate(seg)]
        mix(seg, sweep_noise(dur, rng, 2500, 3500, 1.0), 0, 0.12)
        mix(out, seg, at, 0.5)
    return out


def chug(rng):
    out = buf(0.24)
    mix(out, noise(0.12, rng, 'bandpass', 900, 0.9, 30), 0, 0.7)
    mix(out, noise(0.08, rng, 'bandpass', 1800, 1.2, 50), 0.11, 0.4)
    return out


def win_fanfare(rng):
    out = buf(2.2)
    melody = [(72, 0.0), (76, 0.12), (79, 0.24), (84, 0.36), (79, 0.6), (84, 0.72)]
    for n, at in melody:
        mix(out, tone(note(n), 0.45, MARIMBA, decay=7, rng=rng), at, 0.5)
        mix(out, tone(note(n + 12), 0.3, GLOCK, decay=10, rng=rng), at, 0.18)
    for n in (60, 64, 67, 72):
        mix(out, tone(note(n), 1.3, PLUCK, decay=2.6, attack=0.01, rng=rng), 0.72, 0.22)
    mix(out, sweep_noise(1.0, rng, 4000, 10000, 3), 0.7, 0.08)
    return out


def fail_tune(rng):
    out = buf(1.6)
    for k, n in enumerate((67, 66, 65)):
        mix(out, tone(note(n), 0.32, MARIMBA, decay=6, vibrato=0.01, rng=rng), k * 0.24, 0.5)
    mix(out, tone(note(64), 0.9, MARIMBA, decay=3, vibrato=0.025, rng=rng), 0.72, 0.6)
    mix(out, tone(note(40), 0.9, ((1, 1.0), (2, 0.3)), decay=3, rng=rng), 0.72, 0.35)
    return out


def warning(rng):
    out = buf(0.3)
    mix(out, tone(note(81), 0.1, PLUCK, decay=25, rng=rng), 0, 0.5)
    mix(out, tone(note(81), 0.12, PLUCK, decay=25, rng=rng), 0.12, 0.5)
    return out


# ---------------------------------------------------------------- music

def music_loop(rng):
    """Bouncy 8-bar loop in C major (I - vi - IV - V), 116 bpm, seamless."""
    bpm = 116
    beat = 60 / bpm
    bars = 8
    total = bars * 4 * beat
    out = buf(total + 2.0)
    prog = [(48, (60, 64, 67)), (45, (57, 60, 64)), (41, (57, 60, 65)), (43, (59, 62, 67))] * 2
    lead = [79, 76, 77, 79, 81, 79, 76, 72, 74, 76, 77, 74, 76, 72, 71, 74,
            79, 76, 77, 79, 84, 83, 81, 79, 77, 76, 74, 72, 74, 71, 72, None]
    for bar, (bass, chord) in enumerate(prog):
        t0 = bar * 4 * beat
        # bass: root on 1 and 3, fifth on the and of 2
        for b, n in ((0, bass), (1.5, bass + 7), (2, bass), (3.5, bass + 12)):
            mix(out, tone(note(n), beat * 0.9, ((1, 1.0), (2, 0.35), (3, 0.12)), decay=5, attack=0.006, rng=rng), t0 + b * beat, 0.42)
        # off-beat chord stabs (marimba)
        for b in (0.5, 1.5, 2.5, 3.5):
            for n in chord:
                mix(out, tone(note(n), 0.25, MARIMBA, decay=16, rng=rng), t0 + b * beat, 0.11)
        # shaker on eighths
        for e in range(8):
            mix(out, noise(0.05, rng, 'highpass', 7000, 0.7, 90), t0 + e * beat / 2, 0.05 if e % 2 else 0.08)
        # soft kick on 1 and 3
        for b in (0, 2):
            mix(out, tone(70, 0.18, ((1, 1.0),), decay=20, glide=-0.6, rng=rng), t0 + b * beat, 0.35)
        # lead melody: 4 notes per bar (quarter notes), glockenspiel-ish
        for q in range(4):
            n = lead[(bar * 4 + q) % len(lead)]
            if n is not None:
                mix(out, tone(note(n), beat * 0.95, GLOCK, decay=7, rng=rng), t0 + q * beat, 0.16)
    # wrap the tail into the beginning so the loop is seamless
    n_total = int(total * RATE)
    tail = out[n_total:]
    out = out[:n_total]
    for i, v in enumerate(tail):
        if i < len(out):
            out[i] += v
    return lowpass(out, 9000)


def main():
    rng = random.Random(20261102)
    write('tap', ui_tap(rng), 0.7)
    for k, p in enumerate((1.0, 1.12, 1.26)):
        write(f'snap-{k + 1}', plastic_click(rng, p), 0.9)
    for k, p in enumerate((1.0, 1.08, 0.94)):
        write('hop' if k == 0 else f'hop-{k + 1}', hop(rng, p), 0.6)
    for k, p in enumerate((1.0, 1.1, 0.9)):
        write('drop' if k == 0 else f'drop-{k + 1}', drop(rng, p), 0.55)
    write('go', engine_go(rng), 0.7)
    write('blocked', blocked(rng), 0.75)
    write('complete', stack_complete(rng), 0.75)
    write('horn', toy_horn(rng), 0.55)
    write('reveal', reveal(rng), 0.65)
    write('hammer', hammer(rng), 0.9)
    write('slot', power_up(rng), 0.75)
    write('magnet', magnet(rng), 0.65)
    write('coin', coin(rng), 0.6)
    write('whoosh', whoosh(rng), 0.55)
    write('toot', toot(rng), 0.6)
    write('chug', chug(rng), 0.5)
    write('win', win_fanfare(rng), 0.8)
    write('fail', fail_tune(rng), 0.75)
    write('warning', warning(rng), 0.55)
    write('music', music_loop(rng), 0.55)


if __name__ == '__main__':
    main()
