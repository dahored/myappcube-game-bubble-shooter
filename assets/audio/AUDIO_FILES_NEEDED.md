# Audio Files Needed — Coralia

All files must be OGG Vorbis format. Drop them in the corresponding folder and Godot will auto-import.

## Music  (`assets/audio/music/`)

| File | Usage | Loop | Notes |
|------|-------|------|-------|
| `gameplay.ogg` | Background loop during gameplay | Yes | Cozy, underwater feel. Suggested: calm piano + ambient pads |

**Free sources:** Freesound.org, OpenGameArt.org, Incompetech (Kevin MacLeod), itch.io free music

## SFX (`assets/audio/sfx/`)

| File | Usage | Notes |
|------|-------|-------|
| `pop.ogg` | Bubble match pop | Short, satisfying. Pitch varies ±8% automatically |
| `drop.ogg` | Floating bubbles drop after match | Soft whoosh/plop |
| `shoot.ogg` | Bubble launched from cannon | Light "fwoosh" |
| `victory.ogg` | Level won | Short fanfare (1-2 sec) |
| `defeat.ogg` | Level lost / out of shots | Short sad tone |
| `button.ogg` | UI button tap | Light click/tap |

**Note:** The audio system handles missing files gracefully — the game runs fine without them, it just logs a warning. Add files incrementally as you source them.

**Recommended search terms on Freesound:**
- pop.ogg → "bubble pop", "soap bubble"
- shoot.ogg → "launch", "pew", "soft shoot"
- victory.ogg → "level complete", "success jingle"
- defeat.ogg → "fail", "game over gentle"
- button.ogg → "ui click", "menu select"
