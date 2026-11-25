# Main menu and options

**Runtime:** Unity game. `game_html/` has no menus.

## Main menu

`Assets/_Project/Scenes/MainMenu/MainMenu.unity` is the title screen. It holds:

- a camera;
- a `Lockdown.UI.MainMenu` component, which draws everything with IMGUI in the museum HUD style (walnut panels, brass frames and the Cinzel font).

The screen shows the title **NO WAY OUT**, then three buttons:

| Button | What it does |
|---|---|
| **Start game** | Loads `Museum_Main` with the [intro cutscene](IntroCutscene.md). |
| **Options** | Opens the options panel. Esc (or B on a gamepad) closes it. |
| **Exit** | Quits the game. In the editor it stops Play mode. It's hidden in the Web build, because browsers ignore quitting. |

Opening the main menu always resets the time scale and audio pause, and unlocks the cursor, so arriving from a paused game never leaves it frozen.

## Pause menu

In the museum, **Esc** (or **Start** on a gamepad) opens the pause menu. Pausing:

- freezes game time (`Time.timeScale = 0`);
- pauses audio (`AudioListener.pause`);
- releases the cursor;
- sets `FirstPersonController.InputBlocked`, so the controller ignores Esc and click-to-capture and the menu owns both.

| Button | What it does |
|---|---|
| **Continue** | Resumes. Esc (or B) also resumes. |
| **Restart** | Reloads the museum, without the intro. |
| **Options** | Opens the shared options panel. Esc (or B) goes back. |
| **Main menu** | Loads the title screen. |

A few rules:

- The menu never opens over the caught, died or escaped screens.
- If gameplay code re-captures the cursor while paused, the game resumes.
- Leaving the scene while paused always restores time and audio.

`PlayerVitals` adds `PauseMenu` to the player at runtime, so `Museum_Main.unity` needs no extra setup.

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
