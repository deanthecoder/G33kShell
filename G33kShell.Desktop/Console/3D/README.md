# G33kShell 3D Console Renderer

This folder contains a lightweight 3D rendering system for console applications. It provides a simple way to create, transform, and render 3D objects using character-based rendering.

## Quick Overview

### Core Classes

| Class                     | Description |
|---------------------------|-------------|
| `Scene3D`                 | Manages rendering a collection of `SceneObject` instances. Handles projection and transformation. |
| `SceneObject`             | Represents a generic 3D object with vertices, faces, transformations (position, rotation, scale). |
| `CubeObject`              | A predefined `SceneObject` representing a cube. |
| `SceneBackground`         | Base class for defining a scene's background. |
| `ChequeredSceneBackground`| A background that renders a dynamic chequered pattern. |
---

## How to Use

### 1. Creating a Scene
To create a 3D scene, you need a screen, a background, and at least one `SceneObject`.

```csharp
var screen = new ScreenData(width: 80, height: 25);
var background = new ChequeredSceneBackground(foregroundColor, backgroundColor, 0.08);
var scene = new Scene3D(screen, background);
```

### 2. Adding a Cube
You can add a cube to the scene with different ASCII-like character faces.

```csharp
var cube = new CubeObject(0.5f, new Attr[]
{
    new Attr('.', foregroundColor, backgroundColor),
    new Attr(';', foregroundColor, backgroundColor),
    new Attr('i', foregroundColor, backgroundColor),
    new Attr('S', foregroundColor, backgroundColor),
    new Attr('8', foregroundColor, backgroundColor),
    new Attr('l', foregroundColor, backgroundColor)
});

scene.AddObject(cube);
```

### 3. Animating the Scene
Update the cube's position and rotation over time.

```csharp
var time = GetElapsedTime();  // Example function for time tracking.
cube.Rotation = new Vector3(1.8f, 0.3f, 0.7f) * time;
cube.Position = new Vector3(0, 0, 8.0f * (0.5f + 0.5f * MathF.Sin(time)));

scene.Render(time);
```