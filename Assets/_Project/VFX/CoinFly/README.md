# Coin Collect FX (Shared)

Use the shared API from any popup or screen:

```csharp
CoinFlyFX.PlayFromUI(rewardIcon, goldAmount);
CoinFlyFX.PlayFromWorld(worldPosition, worldCamera, goldAmount);
```

Both calls resolve the existing `CoinFlyFX.prefab`, find a safe root canvas,
find the active `GoldHandler` target, play the collect animation, and add the
reward to `PlayerData`. Overloads accept an explicit target when needed. Optional
parameters support visual coin count, a custom fly root, arrival callbacks,
visual-only playback, a custom template, and custom motion options.

`PopupInGame` also uses this shared API for merge rewards. Its Inspector values
remain the project defaults, so existing merge timing and visuals are reused by
Shop and other popups without copying animation code.

## Ingame merge binding

`PopupInGame` owns the scene binding for this effect:

- **Target:** `Image_coin` inside the ingame `coin_tray`.
- **FX root:** the PopupInGame container, above the board and outside board masks.
- **Gameplay call:** `Level.AnimateMergeCards` starts the effect when the merge stack reaches its final merge point.

Reusable APIs on `PopupInGame` are `PlayFromWorld(Vector3, Camera, int, int)` and `PlayFromUI(RectTransform, int, int)`. `MergeCoinArrived` and `MergeCoinSequenceFinished` are available for presentation-only callbacks.

## Reusable assets

- `CoinFlyFXSettings.asset` contains the ordered `coin_animation (fx)_0` through `_10` frames at 24 FPS.
- `CoinSpin.anim` and `CoinSpin.controller` animate only `Image.sprite` and loop.
- `CoinView.prefab` contains the UI Image and an Animator in Unscaled Time mode.
- `CoinFlyFX.prefab` stores the reusable settings and CoinView references. It has no scene target reference.

## Editing

Tune movement, burst, trail, pulse, and visual-count limits on `PopupInGame` under **Merge Gold Reward**. Coin reward values and visual coin counts per card value are in `MergeGoldRewardConfig.asset`.

The wallet is credited immediately at merge time. `PopupInGame` keeps the HUD number at its previous value, then reveals each integer portion when a visual coin reaches `coin_tray`.

## Preview and validation

In Play Mode, select `PopupInGame` and use its component menu **Preview Merge Coin Collect FX**. This preview does not change the wallet.

Use **Tools > Game FX > Validate Coin Collect FX** to verify the eleven ordered frames and assets. **Tools > Game FX > Setup Coin Collect FX** creates only missing assets and binds the PopupInGame template only if its reference is empty.
