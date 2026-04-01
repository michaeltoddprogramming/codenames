# Codenames CLI UI Overhaul Design

## Summary

Complete visual overhaul of the Codenames terminal CLI game to make it aesthetically pleasing, add text-based animations, and make important messages (like "no clue given") prominent and unmissable. **Zero backend logic changes** - purely rendering and display modifications within the existing C#/Spectre.Console architecture.

## Approach

Enhance `TerminalRenderer.cs` as the central visual engine. Add an `AnimationHelper` static utility class for text animation effects. Modify each screen's rendering to use richer Spectre.Console components. All changes are confined to the `cli/` project.

## Constraints

- No changes to any server/backend code, API contracts, SSE event handling logic, or game state management
- No changes to navigation flow, screen lifecycle, or DI wiring
- All SSE event handlers (`Apply*` methods) remain untouched - only the `Draw()` methods and rendering code change
- Must work within the existing 250ms redraw cycle and synchronized output frame model

---

## 1. Card Rendering Overhaul

**Files:** `TerminalRenderer.cs`, `WordCard.cs`

### Current State
- 12-char-wide, 3-line boxes (border-word-border)
- Single border color, grey for unrevealed
- No visual distinction between voted/unvoted beyond a checkmark prefix

### New Design
- **16-char-wide, 5-line cards**: top border, empty padding line, centered word, empty padding line, bottom border
- **Revealed cards**: text color matches team, background fill for spymaster view using Spectre `Style(foreground, background)` on the inner area
- **Unrevealed cards (operative view)**: subtle dotted border chars instead of solid grey - makes them look more "hidden"
- **Selected card (cursor)**: bright white double-border (`╔═╗║╚╝`) - same chars but brighter white color instead of team color, to clearly show selection regardless of card state
- **Voted cards**: checkmark prefix stays, plus border changes to a dashed style to visually separate from unvoted
- **Spymaster view**: cards get colored background fills per category (dark red BG for red, dark blue BG for blue, dark grey BG for assassin, sandy BG for neutral) so the grid reads at a glance
- **Column width**: increase `TableColumn` width from 16 to 20 to accommodate wider cards

### Color Palette Update (WordCard.cs)
| Category | Border (Spymaster/Revealed) | Text | Background (Spymaster) |
|----------|----------------------------|------|------------------------|
| RED | `Color.Red` | `Color.White` | `Color.DarkRed` |
| BLUE | `Color.DodgerBlue1` | `Color.White` | `Color.NavyBlue` |
| ASSASSIN | `Color.Grey93` | `Color.White` | `Color.Grey23` |
| NEUTRAL | `Color.NavajoWhite3` | `Color.Black` | `Color.Wheat1` |
| Unrevealed | `Color.Grey50` | `Color.Grey70` | none |

---

## 2. Messages & Status Overhaul

**Files:** `BoardScreen.cs` (Draw method, RenderCluePanel), `TerminalRenderer.cs`

### "Waiting for Spymaster's Clue" Banner
- Replace the current dim inline text with a **centered Spectre.Console `Panel`** with double-line border
- Text: `"WAITING FOR SPYMASTER'S CLUE..."` in bold yellow
- Panel width: 50 chars, centered on screen
- **Pulsing effect**: track a `_pulsePhase` bool toggled every ~500ms (every 2 redraws at 250ms). Alternate between `[bold yellow]` and `[dim yellow]` on the banner text. This uses the existing redraw loop with no new timers.

### Clue Given Announcement
- When `CLUE_GIVEN` SSE sets `_statusMessage`, the Draw method renders it as a **prominent centered panel** in team color
- Format: `"CLUE: WATER x 3"` in bold, with team-colored border
- After ~2 seconds (8 redraws), falls back to normal status line display. Track via a `_statusMessageSetAt` timestamp and fade to normal rendering after threshold.

### "Round Skipped" / Timeout
- Full-width red `Panel` with `"ROUND SKIPPED - SPYMASTER TIMED OUT"` in bold white on red
- Same 2-second prominance window as clue announcements

### Status Area Redesign
- Add a `RenderStatusPanel(string message, string color)` method to `TerminalRenderer`
- Uses `Panel` with rounded border, team-colored or severity-colored
- Consistent position at bottom of board display
- Color coding: green = success (voted), yellow = info (waiting), red = error (failed)

### Clue Panel Redesign
- Each team's clue section rendered inside a mini `Panel` with team-colored border
- Active clue word in **bold uppercase**, number styled as a badge: `[WATER x 3]`
- Vote counts as inline indicators: `word(2)` with vote count colored by threshold
- Team arrow marker (`<`) replaced with a filled dot indicator: `[green]●[/]` for active team

---

## 3. Animations & Visual Effects

**Files:** `TerminalRenderer.cs` (new methods), `BoardScreen.cs` (Draw method)

### AnimationHelper Utility
New static class `AnimationHelper` in `Codenames.Cli.Tui` namespace with pure helper methods:
- `static string PulseMarkup(string text, string boldColor, string dimColor, bool phase)` - returns markup string alternating between bold and dim based on phase
- `static bool ShouldShowFlash(DateTimeOffset setAt, double durationSeconds)` - returns whether a flash/announcement is still in its prominent window
- `static string ConfettiLine(int width, Random rng)` - generates a line of random colored chars for celebration effects

### Pulse Animation (Waiting States)
- Add `_pulsePhase` bool field to `BoardScreen`, toggled in `DrawIfNeeded` when `redrawBucket` resets (every 250ms, so flips every other redraw = 500ms period)
- Used by: "waiting for clue" banner, timer bar when under 5 seconds
- Implementation: just alternates markup color tags, no threads or async needed

### Card Reveal Flash
- Add `_revealAnimationWords` (HashSet<string>) and `_revealAnimationStart` (DateTimeOffset?) to `BoardScreen`
- In `ApplyWordsRevealed`: populate `_revealAnimationWords` with the revealed words and set timestamp
- In `Draw()`: for 1.5 seconds after reveal, cards in `_revealAnimationWords` render with alternating white/team-color border (flash effect using `_pulsePhase`)
- After 1.5 seconds, clear `_revealAnimationWords` and render normally

### Timer Bar Upgrade
- Smooth color gradient: >50% green, 50-25% yellow, 25-10% `Color.Orange1`, <10% red
- Under 5 seconds: pulsing red effect (bold/dim alternation using `_pulsePhase`)
- Add urgency indicator: when under 10s, prefix with `"!!!"` in pulsing red

### Game Over Celebration
- Winner text as **FigletText** in team color (not just markup text)
- 2-second confetti animation loop: render 2 lines of random colored `*`, `+`, `.`, `o` characters above the winner FigletText and 2 lines below it, re-randomized each 250ms redraw. The confetti uses `AnsiConsole.MarkupLine` with random Spectre color tags per character. After 2 seconds (8 redraws), confetti stops and the screen renders statically.
- Render final scores in a Spectre.Console `Table` with team-colored rows
- "Press any key" prompt pulsing gently

### Lobby Countdown
- Each countdown number rendered as **FigletText** centered
- Color cycle: 5=blue, 4=green, 3=yellow, 2=orange, 1=red, 0="GO!" in white bold

---

## 4. Screen Polish

**Files:** `WelcomeScreen.cs`, `MainMenuScreen.cs`, `LobbyRoomScreen.cs`, `ClueManager.cs`, `GameResultScreen.cs`, `TerminalRenderer.cs`

### Welcome & Main Menu
- FigletText header color: change from `Color.Blue` to `Color.Gold1` (amber/gold tone)
- Menu items: selected item gets full-width background bar using `Style(foreground: Color.Black, background: Color.Gold1)` instead of plain white
- Unselected items: `Color.Grey70` instead of `Color.Silver` for softer look
- Add subtle separator line (`Rule`) between header and menu

### Lobby Room
- **Join code**: render in a `Panel` with `PanelBorder.Double`, large text, making it easy to read aloud: `"JOIN CODE: ABCD"` in bold white
- **Player list**: convert from plain text lines to a Spectre.Console `Table` with columns: `#`, `Player`, `Role`
- Host indicator: `"[gold1][HOST][/]"` tag instead of plain `"(host)"`
- Connection indicator: `[green]● Connected[/]` or `[red]● Reconnecting...[/]` at top of screen

### Clue Input Screen
- Render inside a `Panel` with clear title: `"Give Your Clue"`
- Format hint: `"Format: WORD NUMBER  (e.g. WATER 3)"` in grey below input
- Error messages in a red-bordered mini panel instead of plain red text

### Board Header
- Score display as a visual bar: `"RED ████░░░░░░ 6/10    BLUE ███████░░░ 8/10"` showing progress toward revealing all words
- Role indicator more prominent: render inside a colored badge panel

---

## 5. Files Modified (No New Files Except AnimationHelper)

| File | Changes |
|------|---------|
| `TerminalRenderer.cs` | New methods: `RenderStatusPanel`, `RenderScoreBar`, `RenderBanner`, wider cards, color palette updates, FigletText color change |
| `AnimationHelper.cs` | **New file** in `Codenames.Cli.Tui` - static utility class for pulse markup, flash timing, confetti generation |
| `WordCard.cs` | Updated color palette with richer colors, add `GetBackgroundColor` method |
| `BoardScreen.cs` | Draw() method overhauled for panels/banners/animations, new fields for pulse and reveal animation state, NO changes to SSE handlers or game logic |
| `GameResultScreen.cs` | FigletText winner, confetti animation loop, Table for scores |
| `LobbyRoomScreen.cs` | DrawLobby redesign with panels and table, countdown FigletText numbers |
| `WelcomeScreen.cs` | Gold header, improved menu styling |
| `MainMenuScreen.cs` | Gold header, improved menu styling |
| `ClueManager.cs` | Panel-based input screen, better error display |

## 6. What Does NOT Change

- All SSE event handler logic (`Apply*` methods in BoardScreen)
- All API client calls (`GameApiClient`, `LobbyApiClient`)
- Navigation flow and screen lifecycle
- DI registration and wiring (`AppHost.cs`)
- All server-side code (`server/` directory)
- Game state management (`LobbySession`, `AuthSession`)
- Vote submission logic, clue submission logic
- Keyboard handling flow and key bindings
- Screen transition logic (which screen goes where)
