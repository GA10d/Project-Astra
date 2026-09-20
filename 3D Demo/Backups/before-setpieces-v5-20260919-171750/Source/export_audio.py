#!/usr/bin/env python3
"""Export Astra's original mechanical Foley as editable, mono PCM WAV files.

No samples, music, third-party libraries, or network access are used. Synthesis is
deterministic and follows CabinAudio.cs's sound vocabulary. The runtime generates
its own clips; these WAVs are standalone source assets for DAWs and later imports.

Usage:
    python export_audio.py
    python export_audio.py --output ./Audio --sample-rate 48000 --gain 1.0

Hum.wav is a seamless six-second loop. All other clips have short edge fades.
The default output is Source/Audio, regardless of the working directory.
"""

from __future__ import annotations

import argparse
from array import array
import math
from pathlib import Path
import random
import sys
import wave


DURATIONS = {
    "Click": 0.11,
    "Switch": 0.20,
    "Lever": 0.64,
    "Hatch": 0.85,
    "Valve": 0.90,
    "Printer": 1.20,
    "Radio": 0.82,
    "Turn": 0.32,
    "Hum": 6.0,
}
TAU = math.tau


def clamp(value: float, low: float = 0.0, high: float = 1.0) -> float:
    return max(low, min(high, value))


def smooth(value: float) -> float:
    value = clamp(value)
    return value * value * (3.0 - 2.0 * value)


def impact(t: float, frequency: float, decay: float, noise: float) -> float:
    if t < 0.0:
        return 0.0
    return (math.sin(TAU * frequency * t) * 0.63 + noise * 0.37) * math.exp(-t * decay)


def synthesize(name: str, sample_rate: int, gain: float) -> list[float]:
    duration = DURATIONS[name]
    rng = random.Random(1943 + tuple(DURATIONS).index(name) * 1747)
    values = []
    filtered = 0.0
    # Match the filter's cutoff to the runtime even at a higher export rate.
    filter_alpha = 1.0 - (1.0 - 0.075) ** (22050.0 / sample_rate)
    for index in range(round(duration * sample_rate)):
        t = index / sample_rate
        u = t / duration
        noise = rng.uniform(-1.0, 1.0)
        filtered += (noise - filtered) * filter_alpha
        envelope = math.sin(math.pi * u)

        if name == "Click":
            value = (impact(t, 1050, 65, noise) * 0.34
                     + impact(t - 0.026, 580, 95, noise) * 0.12)
        elif name == "Switch":
            value = (impact(t, 1350, 78, noise) * 0.28
                     + impact(t - 0.065, 490, 52, noise) * 0.24)
        elif name == "Lever":
            value = (filtered * envelope * 0.25
                     + math.sin(TAU * (180 * t + 55 * t * t)) * envelope * 0.035
                     + impact(t - 0.06, 410, 38, noise) * 0.17
                     + impact(t - 0.47, 260, 28, noise) * 0.34)
        elif name == "Hatch":
            value = (filtered * envelope * 0.18
                     + impact(t - 0.11, 120, 12, noise) * 0.33
                     + impact(t - 0.53, 74, 11, noise) * 0.48
                     + math.sin(TAU * 188 * t) * math.exp(-t * 9) * 0.055)
        elif name == "Valve":
            value = ((filtered * 0.30
                      + math.sin(TAU * (230 * t + 17 * math.sin(t * 4))) * 0.026)
                     * envelope + impact(t - 0.67, 330, 32, noise) * 0.18)
        elif name == "Printer":
            gate = 0.35 + 0.65 * smooth(math.sin(t * 15) * 0.5 + 0.5)
            value = ((math.sin(TAU * (125 * t + 16 * math.sin(t * 3))) * 0.06
                      + math.sin(TAU * 392 * t) * 0.018 + filtered * 0.10)
                     * gate * envelope + impact(t - 0.03, 560, 52, noise) * 0.15)
        elif name == "Radio":
            burst = (math.sin(t * 17) * 0.5 + 0.5) ** 3
            value = (noise * 0.065 * burst + filtered * 0.17
                     + math.sin(TAU * (780 * t + 35 * t * t)) * 0.035
                     * math.sin(t * 32)) * envelope
        elif name == "Turn":
            value = (filtered * 0.20 + math.sin(TAU * 95 * t) * 0.008) * envelope
        elif name == "Hum":
            # Every frequency is an integer multiple of 1/6 Hz. No randomized
            # noise or fade is inserted, so both waveform and slope loop cleanly.
            wind = math.sin(TAU * 7 * t / 6) * math.sin(TAU * 19 * t / 6)
            value = (math.sin(TAU * 48 * t) * 0.10
                     + math.sin(TAU * 96 * t) * 0.023
                     + math.sin(TAU * 144 * t) * 0.012
                     + math.sin(TAU * 317 * t) * 0.006 * (0.6 + wind * 0.4))
        else:
            raise ValueError(name)

        if name != "Hum":
            value *= clamp(t * 900) * clamp((duration - t) * 55)
        values.append(clamp(value * gain, -0.95, 0.95))
    return values


def write_wav(path: Path, samples: list[float], sample_rate: int) -> None:
    pcm = array("h", (round(value * 32767) for value in samples))
    if sys.byteorder != "little":
        pcm.byteswap()
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(sample_rate)
        output.writeframes(pcm.tobytes())


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path,
                        default=Path(__file__).resolve().parent / "Audio")
    parser.add_argument("--sample-rate", type=int, default=48000,
                        choices=(22050, 44100, 48000, 96000))
    parser.add_argument("--gain", type=float, default=1.0)
    args = parser.parse_args()
    if not 0.05 <= args.gain <= 2.0:
        parser.error("--gain must be between 0.05 and 2.0")
    args.output.mkdir(parents=True, exist_ok=True)
    print("Astra original Foley: PCM16 mono, {} Hz".format(args.sample_rate))
    for name in DURATIONS:
        samples = synthesize(name, args.sample_rate, args.gain)
        output = args.output / (name + ".wav")
        write_wav(output, samples, args.sample_rate)
        peak = max(abs(value) for value in samples)
        rms = math.sqrt(sum(value * value for value in samples) / len(samples))
        assert 0.0001 < peak <= 0.95, "Silent or clipped export: " + name
        with wave.open(str(output), "rb") as check:
            assert check.getnframes() == len(samples)
            assert check.getnchannels() == 1 and check.getsampwidth() == 2
            assert check.getframerate() == args.sample_rate
        print("  {:7s} {:4.2f}s  peak {:6.2f} dBFS  RMS {:6.2f} dBFS{}".format(
            name, len(samples) / args.sample_rate,
            20 * math.log10(peak), 20 * math.log10(max(rms, 1e-9)),
            "  [seamless loop]" if name == "Hum" else ""))
    print("Exported and validated {} WAV files.".format(len(DURATIONS)))


if __name__ == "__main__":
    main()
