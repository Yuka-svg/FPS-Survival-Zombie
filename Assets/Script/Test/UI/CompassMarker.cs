using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Custom, engine-free compass marker. Place on any world object to show a blip on
/// the custom CompassWidget. Self-registers to a static list the widget reads each
/// frame. Replaces the Cowsins CompassElement registry for the unified HUD.
/// </summary>
[DisallowMultipleComponent]
public class CompassMarker : MonoBehaviour
{
    [Tooltip("Blip icon shown on the compass strip.")]
    public Sprite icon;

    public static readonly List<CompassMarker> Active = new List<CompassMarker>();

    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    private void OnDisable() { Active.Remove(this); }

    public Vector2 PlanarPosition => new Vector2(transform.position.x, transform.position.z);
}
