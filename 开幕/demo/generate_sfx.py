from __future__ import annotations

import math
import random
import struct
import wave
from pathlib import Path


SAMPLE_RATE = 22_050
MAX_AMPLITUDE = 32_767


def clamp(value: float) -> int:
    return int(max(-1.0, min(1.0, value)) * MAX_AMPLITUDE)


def write_wave(path: Path, samples: list[float]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(b"".join(struct.pack("<h", clamp(sample)) for sample in samples))


def make_typewriter_click() -> list[float]:
    rng = random.Random(1943)
    length = int(SAMPLE_RATE * 0.032)
    samples: list[float] = []
    for index in range(length):
        time = index / SAMPLE_RATE
        envelope = math.exp(-time * 105.0)
        metal = math.sin(2.0 * math.pi * 1_850.0 * time)
        body = math.sin(2.0 * math.pi * 610.0 * time)
        noise = rng.uniform(-1.0, 1.0)
        samples.append(envelope * (0.33 * metal + 0.20 * body + 0.30 * noise))
    return samples


def make_mechanical_button() -> list[float]:
    rng = random.Random(812)
    length = int(SAMPLE_RATE * 0.18)
    samples: list[float] = []
    for index in range(length):
        time = index / SAMPLE_RATE
        value = 0.0

        # Heavy switch travel.
        if time < 0.075:
            envelope = math.exp(-time * 42.0)
            value += envelope * (
                0.34 * math.sin(2.0 * math.pi * 125.0 * time)
                + 0.22 * math.sin(2.0 * math.pi * 940.0 * time)
                + 0.22 * rng.uniform(-1.0, 1.0)
            )

        # A smaller return clack gives the button a physical two-stage action.
        release_time = time - 0.092
        if release_time >= 0.0:
            envelope = math.exp(-release_time * 70.0)
            value += envelope * (
                0.27 * math.sin(2.0 * math.pi * 1_260.0 * release_time)
                + 0.16 * rng.uniform(-1.0, 1.0)
            )

        samples.append(value)
    return samples


def ensure_sound_assets(asset_dir: Path) -> None:
    targets = {
        asset_dir / "typewriter.wav": make_typewriter_click,
        asset_dir / "mechanical_button.wav": make_mechanical_button,
    }
    for path, generator in targets.items():
        if not path.is_file():
            write_wave(path, generator())


def main() -> None:
    ensure_sound_assets(Path(__file__).resolve().parent / "assets")
    print("已生成打字机与机械按钮音效。")


if __name__ == "__main__":
    main()
