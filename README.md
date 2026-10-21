# Dungeon 2D

A top-down 2D dungeon crawler made with Unity. Fight your way through ten rooms, pick up perks and loot along the way, and take down the Corrupted Guardian at the end of the dungeon.

## Features

- **10-room run** made of combat, upgrade and treasure rooms, ending in a boss room
- **Ranged and melee combat**: shoot with the mouse, slash up close, dash out of danger
- **Enemy types**: melee, ranged, tank and elite ranged enemies
- **Boss fight**: the Corrupted Guardian, with multiple phases, telegraphed attacks, hazards and summoned minions
- **Perk drafts**: pick one of three perks (Common, Uncommon, Rare) that raise health, damage, attack rate, move speed, projectile speed or armor
- **Items**: health potions, speed boosts, and keys for locked caches
- **HUD**: health, score, room progress, inventory and boss health bar

## Controls

| Key | Action |
| --- | --- |
| `W` `A` `S` `D` / Arrow keys | Move |
| Mouse | Aim |
| Left mouse button | Shoot |
| Right mouse button | Melee |
| `Shift` | Dash |
| `Q` | Use health potion |
| `1` | Use speed boost |
| `E` | Interact |
| `1` `2` `3` | Pick a perk |
| `Esc` | Pause |
| `Enter` / `Space` | Start game |
| `R` | Restart after victory or defeat |

## Getting started

### Requirements

- [Unity Hub](https://unity.com/download)
- Unity **6000.3.10f1** (Unity 6.3)

### Running the project

1. Clone the repository:

   ```bash
   git clone https://github.com/mertkara256/Dungeon-2D.git
   ```

2. In Unity Hub, choose **Add > Add project from disk** and select the cloned folder.
3. Open `Assets/Scenes/SampleScene.unity`.
4. Press **Play**.

The game sets itself up at runtime: `Dungeon2DBootstrapper` creates the game object, and the dungeon, camera and UI are all built from code. Sprites are loaded from `Assets/Resources/Dungeon2D`.

In the editor, a static preview of the dungeon is drawn in the scene so the layout can be inspected without entering Play mode. It can be rebuilt from **Tools > Dungeon 2D > Rebuild Scene Preview**.

## Project structure

```
Assets/
├── Editor/                 Edit-mode scene preview builder
├── Resources/Dungeon2D/
│   ├── Art/                Character, enemy, environment and effect sprites
│   └── UI/                 Menu backgrounds, UI atlas and perk icons
├── Scenes/                 SampleScene
└── Scripts/Dungeon2D/
    ├── Dungeon2DGame*.cs   Game state, dungeon generation and spawning
    ├── Dungeon2DConfig.cs  Room layout, sizes and tuning constants
    ├── Dungeon2DCombat.cs  Projectiles, pickups, upgrade stations, chests, doors and hazards
    ├── DungeonPlayer2D.cs  Player movement, dash, attacks and items
    ├── DungeonEnemy2D.cs   Enemy behaviour
    ├── DungeonBoss2D.cs    Boss phases and attacks
    ├── Dungeon2DModels.cs  Shared types, player stats and perk catalog
    ├── Dungeon2DUI*.cs     HUD, menus and perk draft screen
    ├── Dungeon2DArt.cs     Sprite loading and slicing
    ├── Dungeon2DVisuals.cs Animation, effects, health bars and audio
    └── Dungeon2DInput.cs   Keyboard and mouse input
```

## Built with

- Unity 6.3 with the Universal Render Pipeline (2D Renderer)
- Unity Input System
