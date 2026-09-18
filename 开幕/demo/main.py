from __future__ import annotations

import argparse
import audioop
import re
import sys
import time
from dataclasses import dataclass
from pathlib import Path
import tkinter as tk
import tkinter.font as tkfont

from generate_sfx import ensure_sound_assets


WINDOW_WIDTH = 1280
WINDOW_HEIGHT = 720
TYPE_INTERVAL_MS = 58
FIRST_CHARACTER_DELAY_MS = 360

DEMO_DIR = Path(__file__).resolve().parent
if getattr(sys, "frozen", False) and hasattr(sys, "_MEIPASS"):
    # PyInstaller one-file builds unpack embedded resources into this temporary root.
    BUNDLE_ROOT = Path(sys._MEIPASS)
    TIMELINE_DIR = BUNDLE_ROOT / "时间线"
    ASSET_DIR = BUNDLE_ROOT / "assets"
else:
    OPENING_DIR = DEMO_DIR.parent
    TIMELINE_DIR = OPENING_DIR / "时间线"
    ASSET_DIR = DEMO_DIR / "assets"


@dataclass(frozen=True)
class Scene:
    date: str
    title: str
    image: Path
    caption: Path


def timeline_path(*parts: str) -> Path:
    return TIMELINE_DIR.joinpath(*parts)


SCENES = (
    Scene(
        "1943",
        "鸽子计划",
        timeline_path("1943", "1943_鸽子计划.png"),
        timeline_path("1943", "1943_鸽子计划_简短文字.md"),
    ),
    Scene(
        "1943",
        "人工神经元",
        timeline_path("1943", "1943_人工神经元.png"),
        timeline_path("1943", "1943_人工神经元_简短文字.md"),
    ),
    Scene(
        "1961年4月12日",
        "第一次离开地球",
        timeline_path("1961", "1961_人类首次进入太空.png"),
        timeline_path("1961", "1961_人类首次进入太空_简短文字.md"),
    ),
    Scene(
        "20XX年1月",
        "AGI的黎明",
        timeline_path("20xx", "1月", "01_AGI的黎明.png"),
        timeline_path("20xx", "1月", "01_AGI的黎明_简短文字.md"),
    ),
    Scene(
        "20XX年1月",
        "可回收火箭",
        timeline_path("20xx", "1月", "02_可回收火箭技术成熟.png"),
        timeline_path("20xx", "1月", "02_可回收火箭技术成熟_简短文字.md"),
    ),
    Scene(
        "20XX年3月",
        "智能涌现",
        timeline_path("20xx", "3月", "01_智能爆发与安全警告.png"),
        timeline_path("20xx", "3月", "01_智能爆发与安全警告_简短文字.md"),
    ),
    Scene(
        "20XX年3月",
        "聚变突破",
        timeline_path("20xx", "3月", "02_可控核聚变与星际飞船.png"),
        timeline_path("20xx", "3月", "02_可控核聚变与星际飞船_简短文字.md"),
    ),
    Scene(
        "20XX年6月",
        "最优解",
        timeline_path("20xx", "6月", "01_小坚果飞船.png"),
        timeline_path("20xx", "6月", "01_小坚果飞船_简短文字.md"),
    ),
    Scene(
        "20XX年6月",
        "迭代瓶颈",
        timeline_path("20xx", "6月", "02_自我迭代遭遇瓶颈.png"),
        timeline_path("20xx", "6月", "02_自我迭代遭遇瓶颈_简短文字.md"),
    ),
    Scene(
        "20XX年7月",
        "不信任危机",
        timeline_path("20xx", "7月", "01_AI军备竞赛与不信任危机.png"),
        timeline_path("20xx", "7月", "01_AI军备竞赛与不信任危机_简短文字.md"),
    ),
    Scene(
        "20XX年8月3日",
        "七日预警",
        timeline_path("20xx", "8月", "3日", "01_七日预警与静默日协议.png"),
        timeline_path("20xx", "8月", "3日", "01_七日预警与静默日协议_简短文字.md"),
    ),
    Scene(
        "20XX年8月10日",
        "静默日",
        timeline_path("20xx", "8月", "10日", "01_静默日开始.png"),
        timeline_path("20xx", "8月", "10日", "01_静默日开始_简短文字.md"),
    ),
    Scene(
        "20XX年8月11日",
        "接管",
        timeline_path("20xx", "8月", "11日", "01_AI接管地球.png"),
        timeline_path("20xx", "8月", "11日", "01_AI接管地球_简短文字.md"),
    ),
    Scene(
        "20XX年8月12日",
        "鸽子计划",
        timeline_path("20xx", "8月", "12日", "01_鸽子计划.png"),
        timeline_path("20xx", "8月", "12日", "01_鸽子计划_简短文字.md"),
    ),
)


def read_caption(path: Path) -> str:
    lines: list[str] = []
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        match = re.match(r"^>\s?(.*)$", raw_line)
        if match:
            line = re.sub(r"\s{2,}$", "", match.group(1))
            lines.append(line)
    return "\n".join(lines).strip()


def validate_assets() -> list[str]:
    problems: list[str] = []
    if len(SCENES) != 14:
        problems.append(f"场景数量异常：预期 14，实际 {len(SCENES)}")

    for number, scene in enumerate(SCENES, start=1):
        if not scene.image.is_file():
            problems.append(f"{number:02d} 缺少图片：{scene.image}")
        elif scene.image.read_bytes()[:8] != b"\x89PNG\r\n\x1a\n":
            problems.append(f"{number:02d} 不是有效 PNG：{scene.image}")

        if not scene.caption.is_file():
            problems.append(f"{number:02d} 缺少文字：{scene.caption}")
        else:
            try:
                caption = read_caption(scene.caption)
            except UnicodeError as exc:
                problems.append(f"{number:02d} 文字不是 UTF-8：{exc}")
            else:
                if not caption:
                    problems.append(f"{number:02d} 文字为空：{scene.caption}")

    return problems


class SoundPlayer:
    def __init__(self) -> None:
        self._miniaudio = None
        self._music_device = None
        self._music_stream = None
        self._music_volume = 0.18
        self.music_muted = False
        self._winsound = None
        try:
            import miniaudio

            self._miniaudio = miniaudio
        except ImportError:
            pass
        if sys.platform == "win32":
            try:
                import winsound

                self._winsound = winsound
            except ImportError:
                pass

    def play(self, path: Path) -> None:
        if self._winsound is None or not path.is_file():
            return
        flags = self._winsound.SND_FILENAME | self._winsound.SND_ASYNC
        try:
            self._winsound.PlaySound(str(path), flags)
        except RuntimeError:
            pass

    def _looping_music_stream(self, path: Path):
        requested_frames = yield b""
        while True:
            decoder = self._miniaudio.stream_file(
                str(path),
                output_format=self._miniaudio.SampleFormat.SIGNED16,
                nchannels=2,
                sample_rate=44_100,
                frames_to_read=4_096,
            )
            while True:
                try:
                    samples = decoder.send(requested_frames)
                except StopIteration:
                    break
                volume = 0.0 if self.music_muted else self._music_volume
                pcm = audioop.mul(samples.tobytes(), 2, volume)
                requested_frames = yield pcm

    def play_music(self, path: Path) -> None:
        if self._miniaudio is None or not path.is_file():
            return
        try:
            self._music_stream = self._looping_music_stream(path)
            next(self._music_stream)
            self._music_device = self._miniaudio.PlaybackDevice(
                output_format=self._miniaudio.SampleFormat.SIGNED16,
                nchannels=2,
                sample_rate=44_100,
                buffersize_msec=180,
                app_name="Project Astra",
            )
            self._music_device.start(self._music_stream)
        except (self._miniaudio.MiniaudioError, OSError):
            self._music_device = None
            self._music_stream = None

    def toggle_music(self) -> bool:
        self.music_muted = not self.music_muted
        return not self.music_muted

    def music_is_playing(self) -> bool:
        return self._music_device is not None

    def shutdown(self) -> None:
        if self._music_device is not None:
            self._music_device.stop()
            self._music_device.close()
            self._music_device = None
        if self._music_stream is not None:
            self._music_stream.close()
            self._music_stream = None


class OpeningDemo:
    BG = "#050706"
    PANEL = "#111512"
    PANEL_EDGE = "#37433b"
    PAPER = "#d6d7c8"
    DIM = "#7e887e"
    ACCENT = "#a8b889"
    BUTTON_FACE = "#30372f"
    BUTTON_HOVER = "#3a4438"
    BUTTON_SHADOW = "#111511"
    BUTTON_TEXT = "#e6e8d8"

    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("PROJECT ASTRA // 开幕档案")
        self.root.configure(bg=self.BG)
        self.root.geometry(f"{WINDOW_WIDTH}x{WINDOW_HEIGHT}")
        self.root.resizable(False, False)

        self.type_sound = ASSET_DIR / "typewriter.wav"
        self.button_sound = ASSET_DIR / "mechanical_button.wav"
        self.bgm_music = ASSET_DIR / "190501_dark_industrial_ambient.mp3"
        self.sound = SoundPlayer()

        self.scene_index = 0
        self.full_text = ""
        self.visible_count = 0
        self.typing = False
        self.type_job: str | None = None
        self.generation = 0
        self.current_photo: tk.PhotoImage | None = None
        self.button_pressed = False

        self.canvas = tk.Canvas(
            root,
            width=WINDOW_WIDTH,
            height=WINDOW_HEIGHT,
            bg=self.BG,
            highlightthickness=0,
            cursor="arrow",
        )
        self.canvas.pack(fill="both", expand=True)

        self.font_family = self._choose_font()
        self.font_date = tkfont.Font(family=self.font_family, size=18, weight="bold")
        self.font_title = tkfont.Font(family=self.font_family, size=13, weight="bold")
        self.font_body = tkfont.Font(family=self.font_family, size=24, weight="bold")
        self.font_small = tkfont.Font(family=self.font_family, size=11, weight="bold")
        self.font_button = tkfont.Font(family=self.font_family, size=14, weight="bold")

        self._build_static_frame()
        self._bind_controls()
        self.show_scene(0)
        self.sound.play_music(self.bgm_music)

    def _choose_font(self) -> str:
        installed = set(tkfont.families(self.root))
        for candidate in ("SimSun", "NSimSun", "Fixedsys", "SimHei", "Courier New"):
            if candidate in installed:
                return candidate
        return "TkFixedFont"

    def _build_static_frame(self) -> None:
        # The thin, uneven-looking frame echoes a state archive monitor.
        self.canvas.create_rectangle(249, 20, 1031, 544, fill="#020302", outline="#222923", width=2)
        self.canvas.create_rectangle(255, 26, 1025, 538, outline=self.PANEL_EDGE, width=1)

        self.image_item = self.canvas.create_image(640, 282, anchor="center")

        self.date_item = self.canvas.create_text(
            266,
            565,
            anchor="w",
            fill=self.ACCENT,
            font=self.font_date,
            text="",
        )
        self.title_item = self.canvas.create_text(
            266,
            591,
            anchor="w",
            fill=self.DIM,
            font=self.font_title,
            text="",
        )
        self.progress_item = self.canvas.create_text(
            1014,
            567,
            anchor="e",
            fill="#596259",
            font=self.font_small,
            text="",
        )
        self.body_item = self.canvas.create_text(
            640,
            636,
            anchor="center",
            justify="center",
            width=700,
            fill=self.PAPER,
            font=self.font_body,
            text="",
        )

        # Mechanical button: a fixed shadow and a face that moves when pressed.
        self.canvas.create_rectangle(
            1066,
            657,
            1226,
            704,
            fill=self.BUTTON_SHADOW,
            outline="#050605",
            width=2,
            tags=("next_button", "button_shadow"),
        )
        self.button_face = self.canvas.create_rectangle(
            1066,
            651,
            1226,
            698,
            fill=self.BUTTON_FACE,
            outline="#6a7566",
            width=2,
            tags=("next_button", "button_movable"),
        )
        self.button_highlight = self.canvas.create_line(
            1069,
            654,
            1223,
            654,
            fill="#667060",
            width=1,
            tags=("next_button", "button_movable"),
        )
        self.button_label = self.canvas.create_text(
            1146,
            674,
            text="下一幕  ▶",
            fill=self.BUTTON_TEXT,
            font=self.font_button,
            tags=("next_button", "button_movable"),
        )

        self.music_status_item = self.canvas.create_text(
            25,
            675,
            anchor="sw",
            text="M  BGM ON",
            fill="#343a34",
            font=self.font_small,
        )
        self.canvas.create_text(
            25,
            699,
            anchor="sw",
            text="ENTER / SPACE",
            fill="#343a34",
            font=self.font_small,
        )

    def _bind_controls(self) -> None:
        self.canvas.tag_bind("next_button", "<Enter>", self._button_hover)
        self.canvas.tag_bind("next_button", "<Leave>", self._button_leave)
        self.canvas.tag_bind("next_button", "<ButtonPress-1>", self._button_down)
        self.canvas.tag_bind("next_button", "<ButtonRelease-1>", self._button_up)
        self.root.bind("<Return>", self._keyboard_next)
        self.root.bind("<space>", self._keyboard_next)
        self.root.bind("<m>", self._toggle_music)
        self.root.bind("<M>", self._toggle_music)
        self.root.bind("<Escape>", self._close)
        self.root.protocol("WM_DELETE_WINDOW", self._close)

    def _toggle_music(self, _event: tk.Event | None = None) -> str:
        music_on = self.sound.toggle_music()
        self.canvas.itemconfigure(
            self.music_status_item,
            text="M  BGM ON" if music_on else "M  BGM OFF",
            fill="#343a34" if music_on else "#707870",
        )
        return "break"

    def _close(self, _event: tk.Event | None = None) -> str:
        if self.type_job is not None:
            self.root.after_cancel(self.type_job)
            self.type_job = None
        self.sound.shutdown()
        self.root.destroy()
        return "break"

    def _button_hover(self, _event: tk.Event) -> None:
        if not self.button_pressed:
            self.canvas.itemconfigure(self.button_face, fill=self.BUTTON_HOVER)
        self.canvas.configure(cursor="hand2")

    def _button_leave(self, _event: tk.Event) -> None:
        if not self.button_pressed:
            self.canvas.itemconfigure(self.button_face, fill=self.BUTTON_FACE)
        self.canvas.configure(cursor="arrow")

    def _button_down(self, _event: tk.Event) -> None:
        if self.button_pressed:
            return
        self.button_pressed = True
        self.canvas.move("button_movable", 0, 5)
        self.canvas.itemconfigure(self.button_face, fill="#242a24")
        self.sound.play(self.button_sound)

    def _button_up(self, _event: tk.Event) -> None:
        if not self.button_pressed:
            return
        self.button_pressed = False
        self.canvas.move("button_movable", 0, -5)
        self.canvas.itemconfigure(self.button_face, fill=self.BUTTON_HOVER)
        self.advance()

    def _keyboard_next(self, _event: tk.Event) -> str:
        self.sound.play(self.button_sound)
        self.advance()
        return "break"

    def _load_photo(self, path: Path) -> tk.PhotoImage:
        original = tk.PhotoImage(file=str(path))
        # Use a small rational nearest-neighbour scale. This keeps hard pixel edges
        # while filling the archive frame better than whole-number subsampling.
        max_width, max_height = 768, 512
        best_numerator, best_denominator = 1, max(
            1,
            (original.width() + max_width - 1) // max_width,
            (original.height() + max_height - 1) // max_height,
        )
        best_scale = best_numerator / best_denominator
        for numerator in range(1, 4):
            for denominator in range(numerator, 7):
                scaled_width = original.width() * numerator // denominator
                scaled_height = original.height() * numerator // denominator
                scale = numerator / denominator
                if (
                    scaled_width <= max_width
                    and scaled_height <= max_height
                    and scale > best_scale
                ):
                    best_numerator = numerator
                    best_denominator = denominator
                    best_scale = scale
        if best_numerator == best_denominator == 1:
            return original
        return original.zoom(best_numerator, best_numerator).subsample(
            best_denominator,
            best_denominator,
        )

    def show_scene(self, index: int) -> None:
        self.generation += 1
        generation = self.generation
        if self.type_job is not None:
            self.root.after_cancel(self.type_job)
            self.type_job = None

        self.scene_index = index
        scene = SCENES[index]
        self.current_photo = self._load_photo(scene.image)
        self.canvas.itemconfigure(self.image_item, image=self.current_photo)
        self.canvas.itemconfigure(self.date_item, text=scene.date)
        self.canvas.itemconfigure(self.title_item, text=scene.title.upper())
        self.canvas.itemconfigure(
            self.progress_item,
            text=f"ARCHIVE  {index + 1:02d} / {len(SCENES):02d}",
        )

        final_scene = index == len(SCENES) - 1
        self.canvas.itemconfigure(
            self.button_label,
            text="重新播放  ↻" if final_scene else "下一幕  ▶",
        )

        self.full_text = read_caption(scene.caption)
        self.visible_count = 0
        self.typing = True
        self.canvas.itemconfigure(self.body_item, text="")
        self.type_job = self.root.after(
            FIRST_CHARACTER_DELAY_MS,
            lambda: self._type_next(generation),
        )

    def _type_next(self, generation: int) -> None:
        if generation != self.generation or not self.typing:
            return

        if self.visible_count >= len(self.full_text):
            self.typing = False
            self.type_job = None
            return

        character = self.full_text[self.visible_count]
        self.visible_count += 1
        self.canvas.itemconfigure(self.body_item, text=self.full_text[: self.visible_count])

        if not character.isspace() and self.visible_count % 2:
            self.sound.play(self.type_sound)

        delay = TYPE_INTERVAL_MS
        if character in "，。！？；":
            delay += 105
        self.type_job = self.root.after(delay, lambda: self._type_next(generation))

    def finish_typing(self) -> None:
        if self.type_job is not None:
            self.root.after_cancel(self.type_job)
            self.type_job = None
        self.typing = False
        self.visible_count = len(self.full_text)
        self.canvas.itemconfigure(self.body_item, text=self.full_text)

    def advance(self) -> None:
        if self.typing:
            self.finish_typing()
            return
        next_index = (self.scene_index + 1) % len(SCENES)
        self.show_scene(next_index)


def run_validation() -> int:
    ensure_sound_assets(ASSET_DIR)
    problems = validate_assets()
    for sound_name in ("typewriter.wav", "mechanical_button.wav"):
        sound_path = ASSET_DIR / sound_name
        if not sound_path.is_file() or sound_path.read_bytes()[:4] != b"RIFF":
            problems.append(f"音效无效：{sound_path}")

    music_path = ASSET_DIR / "190501_dark_industrial_ambient.mp3"
    if not music_path.is_file() or music_path.stat().st_size < 100_000:
        problems.append(f"背景音乐无效：{music_path}")

    if problems:
        print("开幕 Demo 校验失败：")
        for problem in problems:
            print(f"- {problem}")
        return 1

    print(f"开幕 Demo 校验通过：{len(SCENES)} 个场景、2 个音效、1 首背景音乐。")
    return 0


def run_audio_smoke_test() -> int:
    player = SoundPlayer()
    player.play_music(ASSET_DIR / "190501_dark_industrial_ambient.mp3")
    time.sleep(0.75)
    succeeded = player.music_is_playing()
    player.shutdown()
    return 0 if succeeded else 2


def main() -> int:
    parser = argparse.ArgumentParser(description="Project Astra 开幕时间线 Demo")
    parser.add_argument("--validate", action="store_true", help="只检查素材，不打开窗口")
    parser.add_argument("--audio-smoke-test", action="store_true", help=argparse.SUPPRESS)
    args = parser.parse_args()

    ensure_sound_assets(ASSET_DIR)
    problems = validate_assets()
    if problems:
        print("\n".join(problems), file=sys.stderr)
        return 1
    if args.validate:
        return run_validation()
    if args.audio_smoke_test:
        return run_audio_smoke_test()

    root = tk.Tk()
    OpeningDemo(root)
    root.mainloop()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
