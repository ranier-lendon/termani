# Termani 🎬

> Convert any video into ASCII art and play it — with audio — directly in your terminal.

```
  ████████╗███████╗██████╗ ███╗   ███╗ █████╗ ███╗   ██╗██╗
  ╚══██╔══╝██╔════╝██╔══██╗████╗ ████║██╔══██╗████╗  ██║██║
     ██║   █████╗  ██████╔╝██╔████╔██║███████║██╔██╗ ██║██║
     ██║   ██╔══╝  ██╔══██╗██║╚██╔╝██║██╔══██║██║╚██╗██║██║
     ██║   ███████╗██║  ██║██║ ╚═╝ ██║██║  ██║██║ ╚████║██║
     ╚═╝   ╚══════╝╚═╝  ╚═╝╚═╝     ╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝╚═╝
```

---

## 📋 Requirements

| Requirement | Notes |
|-------------|-------|
| [FFmpeg](https://ffmpeg.org/download.html) | Must be on your system `PATH` |

> **.NET is NOT required** — the `.exe` already includes everything it needs.

### Install FFmpeg (Windows)

**Option A — winget (recommended):**
```powershell
winget install Gyan.FFmpeg
```

**Option B — Chocolatey:**
```powershell
choco install ffmpeg
```

**Option C — Manual:**
1. Download from https://ffmpeg.org/download.html
2. Extract and copy `ffmpeg.exe` to a folder like `C:\ffmpeg\bin\`
3. Add that folder to your system `PATH`

After installing, restart your terminal and verify:
```powershell
ffmpeg -version
```

---

## 🚀 Getting Started

### 1. Get the files

Copy the `publish` folder somewhere on your PC. It contains:
```
publish/
  TerminalAnimation.exe   ← the main program
  Run.bat                 ← easy launcher (double-click this!)
```

### 2. Double-click `Run.bat`

A terminal window will open with a menu. **Maximize it first** for best results.

### 3. Convert a video

In the launcher, type:
```
 > convert "C:\path\to\video.mp4"
```

This auto-detects your terminal size and saves the animation in a folder with the same name as the video.

### 4. Play it

```
 > play "video"
```

That's it! Press `Q` to stop playback and return to the menu.

---

## 📖 Commands

### `convert` — Convert a video to ASCII frames

```
TerminalAnimation.exe convert <video> [output] [options]
```

**Examples:**

```
# In Run.bat launcher:
 > convert "myvideo.mp4"

# Or directly in PowerShell:
.\TerminalAnimation.exe convert "myvideo.mp4"

# Custom output folder name
.\TerminalAnimation.exe convert "myvideo.mp4" my_animation

# Manual size (e.g. for a 120-column terminal)
.\TerminalAnimation.exe convert "myvideo.mp4" output --width 480 --cell 4 8

# Maximum detail
.\TerminalAnimation.exe convert "myvideo.mp4" output --extended-ramp
```

> **💡 Tip:** Always **maximize your terminal window first**, then run the convert command.
> The auto-detect reads your current terminal size at the moment of conversion.

---

### `play` — Play an ASCII animation

```
TerminalAnimation.exe play <path> [--loop]
```

**Examples:**

```
# In Run.bat launcher:
 > play "myvideo"
 > play "myvideo" --loop

# Or directly in PowerShell:
.\TerminalAnimation.exe play "myvideo"
.\TerminalAnimation.exe play "myvideo" --loop
```

**Keyboard controls during playback:**

| Key | Action |
|-----|--------|
| `Q` / `Escape` | Quit playback |
| `Space` | Pause / Resume (audio pauses too) |
| `◄` Left Arrow | Rewind 2 frames *(while paused)* |
| `►` Right Arrow | Advance 1 frame *(while paused)* |
| `↑` Up Arrow | Volume up (+5%) |
| `↓` Down Arrow | Volume down (-5%) |

---

### `info` — Show animation metadata

```
TerminalAnimation.exe info <path>
```

Prints details about a converted animation without playing it.

```
# In Run.bat launcher:
 > info "myvideo"

# Or directly:
.\TerminalAnimation.exe info "myvideo"
```

---

## 🔊 Audio

Audio is extracted **automatically** during conversion if the video has an audio track.
No extra steps needed.

- During playback, audio plays in sync with the frames
- Volume is adjustable with `↑` / `↓` during playback
- Pausing with `Space` pauses both video and audio

---

## 🛠️ Troubleshooting

### `ffmpeg` not found
Make sure FFmpeg is installed and on your PATH:
```powershell
ffmpeg -version
```

### Animation is cut off / too tall
Maximize your terminal window then re-convert:
```
# Delete old output folder first, then:
 > convert "video.mp4"
```

### Audio not playing
1. Check Windows volume / make sure the app is not muted
2. Re-convert — the audio fields may be missing from an old conversion

### Playback is choppy
- Close other heavy applications
- Use a smaller size: `--width 320 --cell 4 8`
- Use `--extended-ramp` only on short clips

### Cursor error / crash on play
This happens when the animation is larger than the terminal.
Fix: re-convert with auto-detect (no `--width` flag) after maximizing the window.

---

## 📜 License

This project is licensed under the [MIT License](LICENSE).
