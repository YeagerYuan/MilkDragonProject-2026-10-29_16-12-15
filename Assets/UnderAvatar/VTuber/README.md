# White-haired VTuber

Open `VTuberPreview.unity` and press Play to preview the transparent 2D avatar.

To use it in your game, drag `WhiteHairVTuber.prefab` underneath your stream overlay Canvas. Resize its RectTransform; keep Width and Height equal to preserve the artwork proportions. The preview scene has a dark camera background for visibility; that background is not part of the prefab.

Select the prefab instance and adjust the VTuber Loop component:
- Loop Seconds: default 12 seconds.
- Motion Strength: breathing, head tilt, and hair movement; 0 holds the body still.
- Talking Amount: mouth animation; 0 keeps the mouth closed.
- Animate: pause/resume the loop.
- Use Unscaled Time: keep the avatar moving when the game time scale changes.

Motion is evaluated continuously each rendered frame. Blinking and speaking smoothly blend painted expression poses. This is a lightweight 2D idle/talking puppet, not a fully rigged Live2D model; it does not provide webcam tracking or microphone lip-sync.

The generated expression atlas is transparent and supplied locally with its material and shader. No external packages or online services are needed to play it. Do not change the shader or atlas grid layout when replacing the artwork.

The package does not automatically modify your existing stream HUD. To recreate the assets if needed, use Tools > Under Avatar > Create VTuber Prefab (outside Play Mode).
