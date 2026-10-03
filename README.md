# Current Pochu!

A power-cut adventure for 1 to 4 friends, by Segar Games. Top-down 2D pixel art, made in
Unity 6.6 with Netcode for GameObjects. The story and full design are in
[GAME_DESIGN.md](GAME_DESIGN.md).

## Playing

Download an installer from the [Releases](https://github.com/Chandirasegaran/current-poche/releases) page.

- **Play solo** needs no internet.
- **Host online** gives you a join code; friends type it in on the title screen.
- Move with WASD or the arrow keys. E talks, picks up, uses and throws things (Space, Enter
  or a click also continue dialogue). Q whistles to Battery the dog: stay, or come. Tab
  hides the task list. Esc opens the menu. F11 switches between fullscreen and a window.
- **Settings** (title screen or Esc menu): music and sound volume, fullscreen, and every
  key can be remapped.

## The story, chapter by chapter

Each chapter opens a new part of the map when the one before is finished.

| Chapter | Where | What you do |
|---|---|---|
| 1. Bazaar Blackout | The town of Minnalpatti | Lead minminis to 14 dead streetlights; earn 4 fuses by finding Paati's glasses, bringing home Selvam's goat, running ice to the wedding hall and collecting 6 cricket balls; restart the transformer |
| 2. The Pump-set | Paddy fields, east | Cross the bund maze, open three sluice valves at once, recover the fan belt from the scarecrow, power the pump |
| 3. Last Show at Raja Talkies | Cinema yard, north | Find three film reels, power the projector, turn three mirrors so the beam reaches the screen; freeze the ghosts with your torch |
| 4. Goods Yard | Railway yard, west | Dodge three shunting engines, find four signal lanterns, set the point levers from the notice board, power the signal cabin |
| 5. Kaatthaadi Hills | Windmill ridge, north-west | Climb three ledges against gusts of wind and release the brake on three windmills |
| 6. The Powerhouse | The dam | Start the generator with eight minminis and send Minnal home |

Three optional **side-jobs** can be done at any time, and each makes every torch reach
further: fetch Thatha's radio from the godown (its gate opens while something heavy is on
the stone slab), carry or throw three milk crates from the bus stop to the tea stall, and
light the five oil lamps around the temple tank.

Bandicoots roam the dark and scatter the minminis you are leading; shine your torch at them.

Progress is saved on the host's computer whenever something is achieved, and loaded when a
game starts. **ERASE SAVE** on the title screen starts the story over.

## Working on it

- `Assets/Scripts/` is the game code. Start with `PlayerController.cs`, then `Quests.cs`.
- `Assets/Editor/TownBuilder.cs` builds the whole world. Run it from the Unity menu
  **Current Pochu > Rebuild Town**. Hand edits to the scene are overwritten when it runs.
- `python3 Tools/gen_art.py` regenerates all pixel art; `python3 Tools/gen_audio.py`
  regenerates all sound and music.
- `Packaging/make-installers.sh <version>` turns the Unity builds in `Builds/` into a
  Windows setup .exe and .msi, a .deb, an .rpm, an AppImage and portable zips. It needs
  only podman.
- Command-line switches for a built game: `-solo`, `-host`, `-join <code>`,
  `-profile <name>` (use different profiles to run two copies on one computer).
