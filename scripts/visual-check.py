#!/usr/bin/env python3
"""
Visual regression check for the NativeStyles sample app.

Builds and installs the sample on an iOS simulator and/or an Android emulator, puts the device in a deterministic state
(9:41 status bar, light or dark theme), opens every page through the sample's nativestyles:// automation links, takes a
screenshot, downscales it by an integer factor (to about 400 px wide) and compares it with the committed baseline in
tests/visual/baselines/<platform>/<page>-<theme>.png using a tolerant pixel diff.

    scripts/visual-check.py --ios <UDID> --android <SERIAL>            compare, exit 1 on differences
    scripts/visual-check.py --ios <UDID> --update                     (re)generate the iOS baselines
    scripts/visual-check.py --android emulator-5554 --pages buttons,inputs --themes dark --no-build

Output (report.html, report.md, current/diff images, app logs) goes to --output (default: artifacts/visual-check,
git-ignored). Exit codes: 0 = everything matches, 1 = visual differences or missing baselines, 2 = setup/device errors.

Standard library only (no Pillow / ImageMagick): screenshots are taken in uncompressed formats (BMP on iOS, raw
RGBA on Android) and PNG is read and written with zlib, so the same code produces identical pixels on macOS and Linux.
"""
from __future__ import annotations

import argparse
import html
import json
import os
import platform
import shutil
import struct
import subprocess
import sys
import tempfile
import time
import zlib
from dataclasses import dataclass, field
from itertools import accumulate
from operator import add, or_
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
SAMPLE = REPO / "samples" / "NativeStyles.Sample" / "NativeStyles.Sample.csproj"
BASELINES = REPO / "tests" / "visual" / "baselines"
MASKS = REPO / "tests" / "visual" / "masks.json"
APP_ID = "it.mahiz.mauinativestyle"
# Keep in sync with samples/NativeStyles.Sample/Automation/AutomationLinks.cs
PAGES = ["buttons", "inputs", "selection", "feedback", "lists", "views", "typography", "flyout"]
THEMES = ["light", "dark"]
TARGET_WIDTH = 400


# ---------------------------------------------------------------------------------------------------------------------
# Images: RGBA8 buffers, PNG codec (zlib), integer box downscale, tolerant diff
# ---------------------------------------------------------------------------------------------------------------------

@dataclass
class Image:
	width: int
	height: int
	rgba: bytes


_masks: dict[int, tuple[int, int, int]] = {}


def _swar_masks(n: int) -> tuple[int, int, int]:
	if n not in _masks:
		_masks[n] = (int.from_bytes(b"\x7f" * n, "little"), int.from_bytes(b"\x80" * n, "little"), (1 << (8 * n)) - 1)
	return _masks[n]


def _add_bytes(a: bytes, b: bytes) -> bytes:
	"""Byte-wise (a + b) mod 256 on whole rows at once (SWAR on Python big integers)."""
	low, high, _ = _swar_masks(len(a))
	x, y = int.from_bytes(a, "little"), int.from_bytes(b, "little")
	return (((x & low) + (y & low)) ^ ((x ^ y) & high)).to_bytes(len(a), "little")


def _sub_bytes(a: bytes, b: bytes) -> bytes:
	"""Byte-wise (a - b) mod 256 on whole rows at once."""
	low, high, full = _swar_masks(len(a))
	x, y = int.from_bytes(a, "little"), int.from_bytes(b, "little")
	return ((((x | high) - (y & low)) ^ ((x ^ ~y) & high)) & full).to_bytes(len(a), "little")


_AND_255 = (255).__and__


def _unfilter(raw: bytes, width: int, height: int, bpp: int) -> bytearray:
	stride = width * bpp
	out = bytearray(stride * height)
	prior = bytes(stride)
	pos = 0
	for y in range(height):
		kind = raw[pos]
		line = raw[pos + 1:pos + 1 + stride]
		pos += 1 + stride
		if kind == 0:
			row = bytes(line)
		elif kind == 1:  # Sub: per-channel running sum
			buf = bytearray(stride)
			for c in range(bpp):
				buf[c::bpp] = bytes(map(_AND_255, accumulate(line[c::bpp])))
			row = bytes(buf)
		elif kind == 2:  # Up
			row = _add_bytes(line, prior)
		elif kind == 3:  # Average
			buf = bytearray(line)
			for i in range(stride):
				left = buf[i - bpp] if i >= bpp else 0
				buf[i] = (buf[i] + ((left + prior[i]) >> 1)) & 255
			row = bytes(buf)
		elif kind == 4:  # Paeth
			buf = bytearray(line)
			for i in range(stride):
				b = prior[i]
				if i >= bpp:
					a, c = buf[i - bpp], prior[i - bpp]
				else:
					a = c = 0
				pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
				buf[i] = (buf[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
			row = bytes(buf)
		else:
			raise ValueError(f"invalid PNG filter {kind}")
		out[y * stride:(y + 1) * stride] = row
		prior = row
	return out


def read_png(path: Path) -> Image:
	data = path.read_bytes()
	if data[:8] != b"\x89PNG\r\n\x1a\n":
		raise ValueError(f"{path} is not a PNG file")
	pos, idat, header, palette, trns = 8, [], None, b"", b""
	while pos < len(data):
		length, kind = struct.unpack(">I4s", data[pos:pos + 8])
		chunk = data[pos + 8:pos + 8 + length]
		pos += 12 + length
		if kind == b"IHDR":
			header = struct.unpack(">IIBBBBB", chunk)
		elif kind == b"PLTE":
			palette = chunk
		elif kind == b"tRNS":
			trns = chunk
		elif kind == b"IDAT":
			idat.append(chunk)
		elif kind == b"IEND":
			break
	if header is None:
		raise ValueError(f"{path}: missing IHDR")
	width, height, depth, color, _, _, interlace = header
	if interlace or depth not in (8, 16) or (color == 3 and depth != 8):
		raise ValueError(f"{path}: unsupported PNG (depth {depth}, color type {color}, interlace {interlace})")
	channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[color]
	pixels = _unfilter(zlib.decompress(b"".join(idat)), width, height, channels * depth // 8)
	if depth == 16:
		pixels = pixels[0::2]  # keep the most significant byte
	n = width * height
	rgba = bytearray(b"\xff" * (n * 4))
	if color == 6:
		rgba = pixels
	elif color == 2:
		for c in range(3):
			rgba[c::4] = pixels[c::3]
	elif color in (0, 4):
		for c in range(3):
			rgba[c::4] = pixels[0::channels]
		if color == 4:
			rgba[3::4] = pixels[1::2]
	else:
		lut = [palette[i * 3:i * 3 + 3] + (trns[i:i + 1] or b"\xff") for i in range(len(palette) // 3)]
		rgba = bytearray(b"".join(lut[p] for p in pixels))
	return Image(width, height, bytes(rgba))


_COST = [min(v, 256 - v) for v in range(256)]


def write_png(path: Path, image: Image) -> None:
	"""RGB PNG (screens are opaque), None/Sub/Up filter chosen per row by the usual minimum-sum heuristic."""
	width, height = image.width, image.height
	rgb = bytearray(width * height * 3)
	for c in range(3):
		rgb[c::3] = image.rgba[c::4]
	stride = width * 3
	prior = bytes(stride)
	lines = []
	for y in range(height):
		row = bytes(rgb[y * stride:(y + 1) * stride])
		candidates = [
			(0, row),
			(1, row[:3] + _sub_bytes(row[3:], row[:-3])),
			(2, _sub_bytes(row, prior)),
		]
		kind, filtered = min(candidates, key=lambda item: sum(map(_COST.__getitem__, item[1])))
		lines.append(bytes([kind]) + filtered)
		prior = row

	def chunk(kind: bytes, payload: bytes) -> bytes:
		return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload))

	path.parent.mkdir(parents=True, exist_ok=True)
	path.write_bytes(b"\x89PNG\r\n\x1a\n"
		+ chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
		+ chunk(b"IDAT", zlib.compress(b"".join(lines), 9))
		+ chunk(b"IEND", b""))


def read_bmp(data: bytes) -> Image:
	"""32-bit BI_BITFIELDS BMP as written by `simctl io screenshot --type=bmp` (BGRA, usually top-down)."""
	if data[:2] != b"BM":
		raise ValueError("not a BMP screenshot")
	offset = struct.unpack("<I", data[10:14])[0]
	width, height, _, bits, compression = struct.unpack("<iiHHI", data[18:34])
	masks = struct.unpack("<III", data[54:66]) if compression == 3 else (0xFF0000, 0xFF00, 0xFF)
	if bits != 32 or masks != (0xFF0000, 0xFF00, 0xFF):
		raise ValueError(f"unsupported BMP layout ({bits} bpp, masks {masks})")
	rows, stride = abs(height), width * 4
	pixels = data[offset:offset + rows * stride]
	if height > 0:  # bottom-up
		pixels = b"".join(pixels[y * stride:(y + 1) * stride] for y in reversed(range(rows)))
	rgba = bytearray(len(pixels))
	rgba[0::4], rgba[1::4], rgba[2::4] = pixels[2::4], pixels[1::4], pixels[0::4]
	rgba[3::4] = b"\xff" * (width * rows)
	return Image(width, rows, bytes(rgba))


def read_screencap(data: bytes) -> Image:
	"""Raw `adb exec-out screencap` output: width, height, format [, color space] header, then RGBA_8888 pixels."""
	width, height, fmt = struct.unpack("<III", data[:12])
	header = len(data) - width * height * 4
	if header not in (12, 16) or fmt not in (1, 2):  # RGBA_8888, RGBX_8888
		raise ValueError(f"unsupported screencap (format {fmt}, header {header} bytes)")
	rgba = bytearray(data[header:])
	rgba[3::4] = b"\xff" * (width * height)
	return Image(width, height, bytes(rgba))


def downscale(image: Image, target_width: int = TARGET_WIDTH) -> Image:
	"""Box filter by the integer factor closest to width / target_width: exact, fast and identical everywhere."""
	f = max(1, round(image.width / target_width))
	if f == 1:
		return image
	width, height = image.width // f, image.height // f
	stride, used, div = image.width * 4, width * f * 4, f * f
	table = bytes((v + div // 2) // div for v in range(255 * div + 1))
	src, out = image.rgba, bytearray(width * height * 4)
	for oy in range(height):
		base = oy * f * stride
		acc = list(src[base:base + used])
		for k in range(1, f):
			acc = list(map(add, acc, src[base + k * stride:base + k * stride + used]))
		row = bytearray(width * 4)
		for c in range(4):
			values = acc[c::4]
			sums = values[0::f]
			for j in range(1, f):
				sums = list(map(add, sums, values[j::f]))
			row[c::4] = bytes(map(table.__getitem__, sums))
		out[oy * width * 4:(oy + 1) * width * 4] = row
	return Image(width, height, bytes(out))


@dataclass
class Diff:
	changed: int
	total: int
	box: tuple[int, int, int, int] | None  # x, y, width, height of the changed area
	image: Image | None

	@property
	def ratio(self) -> float:
		return self.changed / self.total if self.total else 0.0


def compare(baseline: Image, current: Image, threshold: int, masks: list[list[int]]) -> Diff:
	"""A pixel counts as changed when any RGB channel differs by more than `threshold` (0-255)."""
	total = baseline.width * baseline.height
	if baseline.rgba == current.rgba:
		return Diff(0, total, None, None)
	a, b = baseline.rgba, current.rgba
	flags: list = [False] * total
	for c in range(3):
		flags = list(map(or_, flags, map(lambda x, y: x - y > threshold or y - x > threshold, a[c::4], b[c::4])))
	width = baseline.width
	for x, y, w, h in masks:
		for row in range(max(0, y), min(baseline.height, y + h)):
			start = row * width
			flags[start + max(0, x):start + min(width, x + w)] = [False] * (min(width, x + w) - max(0, x))
	changed = sum(flags)
	if not changed:
		return Diff(0, total, None, None)
	indices = [i for i, flag in enumerate(flags) if flag]
	ys = (indices[0] // width, indices[-1] // width)
	xs = [i % width for i in indices]
	box = (min(xs), ys[0], max(xs) - min(xs) + 1, ys[1] - ys[0] + 1)
	# Diff image: the baseline as a faded gray ghost, changed pixels in magenta
	out = bytearray(total * 4)
	gray = bytes(map(lambda r, g, bl: 255 - (255 - (r * 3 + g * 6 + bl) // 10) // 4, a[0::4], a[1::4], a[2::4]))
	out[0::4] = out[1::4] = out[2::4] = gray
	out[3::4] = b"\xff" * total
	for i in indices:
		out[i * 4:i * 4 + 3] = b"\xff\x00\xff"
	return Diff(changed, total, box, Image(baseline.width, baseline.height, bytes(out)))


# ---------------------------------------------------------------------------------------------------------------------
# Devices
# ---------------------------------------------------------------------------------------------------------------------

def run(cmd: list[str], check: bool = True, capture: bool = True, env: dict | None = None, timeout: float | None = None) -> subprocess.CompletedProcess:
	result = subprocess.run(cmd, capture_output=capture, env=env, timeout=timeout)
	if check and result.returncode != 0:
		stderr = result.stderr.decode(errors="replace").strip() if capture else ""
		raise RuntimeError(f"command failed ({result.returncode}): {' '.join(cmd)}\n{stderr}")
	return result


def link(page: str, theme: str, nonce: str) -> str:
	return f"nativestyles://page/{page}?theme={theme}&run={nonce}"


class IOSSimulator:
	name = "ios"

	def __init__(self, udid: str, output: Path):
		self.udid = udid
		self.logs = output / "logs"
		self._appearance: str | None = None

	def simctl(self, *args: str, check: bool = True, timeout: float | None = 60) -> subprocess.CompletedProcess:
		return run(["xcrun", "simctl", *args], check=check, timeout=timeout)

	def describe(self) -> dict:
		devices = json.loads(self.simctl("list", "devices", "-j").stdout)["devices"]
		for runtime, entries in devices.items():
			for entry in entries:
				if entry["udid"] == self.udid:
					return {"device": entry.get("deviceTypeIdentifier", "").rsplit(".", 1)[-1], "runtime": runtime.rsplit(".", 1)[-1], "state": entry["state"]}
		raise RuntimeError(f"iOS simulator {self.udid} not found")

	def build_and_install(self, configuration: str) -> None:
		rid = "iossimulator-arm64" if platform.machine() == "arm64" else "iossimulator-x64"
		print(f"[ios] building the sample ({configuration}, {rid})", flush=True)
		run(["dotnet", "build", str(SAMPLE), "-f", "net10.0-ios", "-c", configuration, f"-p:RuntimeIdentifier={rid}", "-nologo", "-v:q"], capture=False)
		apps = sorted((SAMPLE.parent / "bin" / configuration / "net10.0-ios" / rid).glob("*.app"), key=lambda p: p.stat().st_mtime)
		if not apps:
			raise RuntimeError("no .app bundle found after the iOS build")
		print(f"[ios] installing {apps[-1].name}", flush=True)
		self.simctl("terminate", self.udid, APP_ID, check=False)
		self.simctl("install", self.udid, str(apps[-1]), timeout=300)

	def prepare(self) -> None:
		if self.describe()["state"] != "Booted":
			raise RuntimeError(f"iOS simulator {self.udid} is not booted")
		self._appearance = self.simctl("ui", self.udid, "appearance").stdout.decode().strip() or None
		self.simctl("status_bar", self.udid, "override", "--time", "9:41", "--dataNetwork", "wifi", "--wifiMode", "active",
			"--wifiBars", "3", "--cellularMode", "active", "--cellularBars", "4", "--operatorName", "",
			"--batteryState", "discharging", "--batteryLevel", "100")

	def set_theme(self, theme: str) -> None:
		# The app theme is set by the link; the system appearance is aligned so system UI matches as well
		self.simctl("ui", self.udid, "appearance", theme)

	def log_show(self, predicate: str, last: str = "2m") -> str:
		return self.simctl("spawn", self.udid, "log", "show", "--last", last, "--style", "compact", "--predicate", predicate,
			check=False).stdout.decode(errors="replace")

	def open(self, page: str, theme: str, nonce: str, timeout: float) -> None:
		# SpringBoard asks for confirmation before opening a custom URL scheme (simctl openurl included), so the link is
		# passed as a launch argument instead. Relaunching per page also gives every capture a clean state.
		url = link(page, theme, nonce)
		self.simctl("launch", "--terminate-running-process", self.udid, APP_ID, "-NativeStylesLink", url)
		# Console output of a .NET iOS app goes to the unified log
		done = f"[automation] done {url}"
		try:
			wait_for(lambda: done in self.log_show(f'eventMessage CONTAINS "{done}"', "1m"), timeout, f"iOS app to open {page}")
		except TimeoutError:
			self.logs.mkdir(parents=True, exist_ok=True)
			(self.logs / f"ios-{page}-{theme}.log").write_text(self.log_show('process == "NativeStyles.Sample"'))
			raise

	def capture(self) -> Image:
		with tempfile.TemporaryDirectory() as tmp:
			path = Path(tmp) / "screen.bmp"
			self.simctl("io", self.udid, "screenshot", "--type=bmp", str(path))
			return read_bmp(path.read_bytes())

	def restore(self) -> None:
		self.simctl("terminate", self.udid, APP_ID, check=False)
		self.simctl("status_bar", self.udid, "clear", check=False)
		if self._appearance in ("light", "dark"):
			self.simctl("ui", self.udid, "appearance", self._appearance, check=False)


class AndroidEmulator:
	name = "android"

	def __init__(self, serial: str, output: Path):
		self.serial = serial
		self.logs = output / "logs"
		self._demo_allowed: str | None = None

	def adb(self, *args: str, check: bool = True, timeout: float | None = 60) -> subprocess.CompletedProcess:
		return run(["adb", "-s", self.serial, *args], check=check, timeout=timeout)

	def shell(self, command: str, check: bool = True) -> str:
		return self.adb("shell", command, check=check).stdout.decode(errors="replace").strip()

	def describe(self) -> dict:
		return {
			"device": self.shell("getprop ro.product.model"),
			"avd": self.shell("getprop ro.boot.qemu.avd_name", check=False),
			"android": self.shell("getprop ro.build.version.release"),
			"api": self.shell("getprop ro.build.version.sdk"),
			"size": self.shell("wm size").split(":")[-1].strip(),
			"density": self.shell("wm density").split(":")[-1].strip(),
		}

	def build_and_install(self, configuration: str) -> None:
		print(f"[android] building and installing the sample ({configuration})", flush=True)
		env = dict(os.environ, ANDROID_SERIAL=self.serial)
		run(["dotnet", "build", str(SAMPLE), "-f", "net10.0-android", "-c", configuration, "-t:Install",
			f"-p:AdbTarget=-s {self.serial}", "-nologo", "-v:q"], capture=False, env=env)

	def prepare(self) -> None:
		if self.adb("get-state", check=False).stdout.decode().strip() != "device":
			raise RuntimeError(f"Android device {self.serial} is not online")
		self.shell("input keyevent KEYCODE_WAKEUP")
		self.shell("wm dismiss-keyguard", check=False)
		self._demo_allowed = self.shell("settings get global sysui_demo_allowed")
		self.shell("settings put global sysui_demo_allowed 1")
		for command in [
			"enter",
			"clock -e hhmm 0941",
			"battery -e level 100 -e plugged false -e powersave false",
			"network -e wifi show -e level 4 -e fully true",
			"network -e mobile hide",
			"notifications -e visible false",
		]:
			self.shell(f"am broadcast -a com.android.systemui.demo -e command {command}")
		# Start from a clean process: the first link cold-starts the app
		self.shell(f"am force-stop {APP_ID}")

	def set_theme(self, theme: str) -> None:
		# The app theme (Application.UserAppTheme) is set by the link; the shared emulator's night mode is left alone
		pass

	def open(self, page: str, theme: str, nonce: str, timeout: float) -> None:
		url = link(page, theme, nonce)
		self.shell(f"am start -W -a android.intent.action.VIEW -d '{url}' {APP_ID}")

		def app_log() -> str:
			# Console output of a .NET Android app goes to logcat under the DOTNET tag
			return self.adb("logcat", "-d", "-s", "DOTNET:I", check=False).stdout.decode(errors="replace")

		try:
			wait_for(lambda: f"[automation] done {url}" in app_log(), timeout, f"Android app to open {page}")
		except TimeoutError:
			self.logs.mkdir(parents=True, exist_ok=True)
			(self.logs / f"android-{page}-{theme}.log").write_text(app_log())
			raise
		top = self.shell("dumpsys activity activities | grep -E 'topResumedActivity|mResumedActivity'", check=False)
		if APP_ID not in top:
			raise RuntimeError(f"the sample is not the foreground activity: {top}")

	def capture(self) -> Image:
		return read_screencap(self.adb("exec-out", "screencap").stdout)

	def restore(self) -> None:
		self.shell(f"am force-stop {APP_ID}", check=False)
		self.shell("am broadcast -a com.android.systemui.demo -e command exit", check=False)
		if self._demo_allowed not in (None, "", "null"):
			self.shell(f"settings put global sysui_demo_allowed {self._demo_allowed}", check=False)
		else:
			self.shell("settings delete global sysui_demo_allowed", check=False)


def wait_for(probe, timeout: float, what: str) -> None:
	deadline = time.monotonic() + timeout
	while time.monotonic() < deadline:
		if probe():
			return
		time.sleep(0.25)
	raise TimeoutError(f"timed out after {timeout:.0f}s waiting for the {what}")


# ---------------------------------------------------------------------------------------------------------------------
# Run
# ---------------------------------------------------------------------------------------------------------------------

@dataclass
class Result:
	platform: str
	page: str
	theme: str
	status: str  # ok | changed | new | size | updated | error
	ratio: float = 0.0
	detail: str = ""
	files: dict[str, str] = field(default_factory=dict)


def check_device(device, args, output: Path, masks: dict) -> list[Result]:
	results: list[Result] = []
	baseline_dir = BASELINES / device.name
	info = device.describe()
	recorded = baseline_dir / "device.json"
	if not args.update and recorded.exists():
		expected = json.loads(recorded.read_text())
		different = {k: (expected.get(k), v) for k, v in info.items() if k in expected and k != "state" and expected.get(k) != v}
		if different:
			print(f"[{device.name}] warning: baselines were recorded on a different device: {different}", flush=True)

	if not args.no_build:
		device.build_and_install(args.configuration)
	device.prepare()
	nonce = str(int(time.time()))
	try:
		for theme in args.themes:
			device.set_theme(theme)
			for page in args.pages:
				key = f"{page}-{theme}"
				result = Result(device.name, page, theme, "error")
				results.append(result)
				current = None
				for attempt in (1, 2):  # a busy host occasionally drops a launch or a screenshot: retry once
					try:
						device.open(page, theme, nonce, args.timeout)
						time.sleep(args.settle)
						current = downscale(device.capture())
						break
					except Exception as error:  # keep going: one failing page should not hide the others
						result.detail = str(error).splitlines()[0]
						print(f"[{device.name}] {key}: attempt {attempt} failed: {result.detail}", flush=True)
				if current is None:
					continue
				result.detail = ""

				current_file = output / device.name / f"{key}.png"
				write_png(current_file, current)
				result.files["current"] = str(current_file.relative_to(output))
				baseline_file = baseline_dir / f"{key}.png"

				if args.update:
					baseline_file.parent.mkdir(parents=True, exist_ok=True)
					shutil.copyfile(current_file, baseline_file)
					result.status = "updated"
				elif not baseline_file.exists():
					result.status, result.detail = "new", "no baseline (run with --update)"
				else:
					baseline = read_png(baseline_file)
					copy = output / device.name / f"{key}.baseline.png"
					shutil.copyfile(baseline_file, copy)
					result.files["baseline"] = str(copy.relative_to(output))
					if (baseline.width, baseline.height) != (current.width, current.height):
						result.status = "size"
						result.detail = f"size {current.width}x{current.height}, baseline {baseline.width}x{baseline.height}"
					else:
						diff = compare(baseline, current, args.threshold, masks.get(device.name, {}).get(page, []))
						result.ratio = diff.ratio
						result.status = "ok" if diff.ratio * 100 <= args.max_diff else "changed"
						if diff.image is not None:
							diff_file = output / device.name / f"{key}.diff.png"
							write_png(diff_file, diff.image)
							result.files["diff"] = str(diff_file.relative_to(output))
							x, y, w, h = diff.box
							result.detail = f"{diff.changed} px changed in {w}x{h} at ({x},{y})"
				print(f"[{device.name}] {key}: {result.status.upper()} {result.ratio * 100:.3f}% {result.detail}".rstrip(), flush=True)
	finally:
		device.restore()

	if args.update:
		recorded.write_text(json.dumps({k: v for k, v in info.items() if k != "state"}, indent=2) + "\n")
	return results


def write_reports(results: list[Result], output: Path, args) -> None:
	labels = {"ok": "OK", "changed": "CHANGED", "new": "NO BASELINE", "size": "SIZE CHANGED", "updated": "UPDATED", "error": "ERROR"}
	lines = ["# Visual check", "", f"Threshold {args.threshold}/255 per channel, max {args.max_diff}% changed pixels.", "",
		"| Platform | Page | Theme | Result | Changed | Detail |", "|---|---|---|---|---|---|"]
	for r in results:
		lines.append(f"| {r.platform} | {r.page} | {r.theme} | {labels[r.status]} | {r.ratio * 100:.3f}% | {r.detail} |")
	(output / "report.md").write_text("\n".join(lines) + "\n")

	def cell(r: Result, kind: str) -> str:
		if kind not in r.files:
			return "<td></td>"
		src = html.escape(r.files[kind])
		return f'<td><a href="{src}"><img src="{src}" alt="{kind}"></a></td>'

	rows = []
	for r in results:
		cells = "".join(cell(r, kind) for kind in ("baseline", "current", "diff"))
		rows.append(f"<tr class=\"{r.status}\"><td><b>{r.platform}</b> {r.page}<br>{r.theme}</td>"
			f"<td class=\"status\">{labels[r.status]}<br>{r.ratio * 100:.3f}%<br><small>{html.escape(r.detail)}</small></td>{cells}</tr>")
	(output / "report.html").write_text(f"""<!doctype html>
<html><head><meta charset="utf-8"><title>Visual check</title><style>
body {{ font: 14px -apple-system, Roboto, sans-serif; margin: 16px; }}
table {{ border-collapse: collapse; }} td {{ border-top: 1px solid #ccc; padding: 8px; vertical-align: top; }}
img {{ width: 200px; border: 1px solid #ddd; }} .status {{ width: 150px; }}
.ok .status {{ color: #2e7d32; }} .changed .status, .size .status, .error .status, .new .status {{ color: #c62828; font-weight: 600; }}
</style></head><body><h1>Visual check</h1>
<p>Threshold {args.threshold}/255 per channel, max {args.max_diff}% changed pixels. Columns: baseline, current, diff (changes in magenta).</p>
<table>{''.join(rows)}</table></body></html>
""")


def main() -> int:
	parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0], formatter_class=argparse.RawDescriptionHelpFormatter,
		epilog="Pages: " + ", ".join(PAGES))
	parser.add_argument("--ios", metavar="UDID", help="iOS simulator UDID (must be booted)")
	parser.add_argument("--android", metavar="SERIAL", help="Android device serial, e.g. emulator-5554")
	parser.add_argument("--update", action="store_true", help="write the captures as the new baselines")
	parser.add_argument("--pages", default=",".join(PAGES), help="comma-separated pages (default: all)")
	parser.add_argument("--themes", default=",".join(THEMES), help="comma-separated themes (default: light,dark)")
	parser.add_argument("--no-build", action="store_true", help="use the sample already installed on the device")
	parser.add_argument("--configuration", default="Debug", help="build configuration (default: Debug)")
	parser.add_argument("--output", type=Path, default=REPO / "artifacts" / "visual-check", help="report directory")
	parser.add_argument("--threshold", type=int, default=24, help="per-channel difference ignored as noise, 0-255 (default: 24)")
	parser.add_argument("--max-diff", type=float, default=0.05, help="percentage of changed pixels tolerated (default: 0.05)")
	parser.add_argument("--settle", type=float, default=2.5, help="seconds to wait after a page opened (default: 2.5)")
	parser.add_argument("--timeout", type=float, default=60, help="seconds to wait for a page to open (default: 60)")
	args = parser.parse_args()

	args.pages = [p.strip() for p in args.pages.split(",") if p.strip()]
	args.themes = [t.strip() for t in args.themes.split(",") if t.strip()]
	unknown = [p for p in args.pages if p not in PAGES] + [t for t in args.themes if t not in THEMES]
	if unknown or not (args.ios or args.android):
		parser.error(f"unknown pages/themes: {unknown}" if unknown else "pass --ios and/or --android")

	output = args.output.resolve()
	if output.exists():
		shutil.rmtree(output)
	output.mkdir(parents=True)
	masks = json.loads(MASKS.read_text()) if MASKS.exists() else {}

	devices = ([IOSSimulator(args.ios, output)] if args.ios else []) + ([AndroidEmulator(args.android, output)] if args.android else [])
	results: list[Result] = []
	setup_failed = False
	for device in devices:
		try:
			results += check_device(device, args, output, masks)
		except Exception as error:
			setup_failed = True
			print(f"[{device.name}] setup failed: {error}", file=sys.stderr, flush=True)

	write_reports(results, output, args)
	counts = {s: sum(r.status == s for r in results) for s in ("ok", "changed", "new", "size", "updated", "error")}
	print("\n" + ", ".join(f"{v} {k}" for k, v in counts.items() if v) + f"\nReport: {output / 'report.html'}")
	if setup_failed or counts["error"]:
		return 2
	return 1 if counts["changed"] or counts["new"] or counts["size"] else 0


if __name__ == "__main__":
	sys.exit(main())
