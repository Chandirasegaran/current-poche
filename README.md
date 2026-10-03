# Current Pochu!

A power-cut adventure for 1 to 4 friends, by Segar Games. Top-down 2D pixel art, made in
Unity 6.6 with Netcode for GameObjects. The story and full design are in
[GAME_DESIGN.md](GAME_DESIGN.md).

## Playing

- **Play solo** needs no internet.
- **Host online** gives you a join code; friends type it in on the title screen.
- Move with WASD or the arrow keys. E talks, picks up and uses things (Space, Enter or a
  click also continue dialogue). Tab hides the task list. Esc opens the menu.

## What is in the game so far

| Chapter | Where | What you do |
|---|---|---|
| 1. Bazaar Blackout | The town of Minnalpatti | Lead minminis to 14 dead streetlights; earn 4 fuses by finding Paati's glasses, bringing home Selvam's goat, running ice to the wedding hall and collecting 6 cricket balls; restart the transformer |
| 2. The Pump-set | The paddy fields to the east | Cross the bund maze, open three sluice valves at once, recover the fan belt from the scarecrow, and power the pump |

Bandicoots roam the dark and scatter the minminis you are leading; shine your torch at them.

## Working on it

- `Assets/Scripts/` is the game code. Start with `PlayerController.cs`, then `Quests.cs`.
- `Assets/Editor/TownBuilder.cs` builds the whole world. Run it from the Unity menu
  **Current Pochu > Rebuild Town**. Hand edits to the scene are overwritten when it runs.
- `python3 Tools/gen_art.py` regenerates all pixel art; `python3 Tools/gen_audio.py`
  regenerates all sound and music.
- Command-line switches for a built game: `-solo`, `-host`, `-join <code>`,
  `-profile <name>` (use different profiles to run two copies on one computer).
