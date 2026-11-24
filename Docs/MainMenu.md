# Main menu and options

**Runtime:** Unity game. `game_html/` has no menus.

## Main menu

`Assets/_Project/Scenes/MainMenu/MainMenu.unity` is the title screen. It holds:

- a camera;
- a `Lockdown.UI.MainMenu` component, which draws everything with IMGUI in the museum HUD style (walnut panels, brass frames and the Cinzel font).

The screen shows the title **NO WAY OUT**, then three buttons:

| Button | What it does |
|---|---|
| **Start game** | Loads `Museum_Main`. |
| **Options** | Opens the options panel. Esc (or B on a gamepad) closes it. |
| **Exit** | Quits the game. In the editor it stops Play mode. It's hidden in the Web build, because browsers ignore quitting. |

Opening the main menu always resets the time scale and audio pause, and unlocks the cursor, so arriving from a paused game never leaves it frozen.

## Options

`OptionsPanel` is shared by the main menu and the pause menu.

- **Master volume** sets `AudioListener.volume`, so it scales every sound.
- The setting is stored in PlayerPrefs under `Lockdown.MasterVolume`.
- It's applied before the first scene loads, through `GameSettings`.
- **Back** writes it to disk.

## Scenes

`SceneFlow` names both scenes and loads them.

- **In the editor** it uses `EditorSceneManager.LoadSceneInPlayMode`, so you can still press Play in `Museum_Main` directly.
- **In a build** it uses `SceneManager.LoadScene`.

Implemented in `Assets/_Project/Scripts/Runtime/UI/`.
