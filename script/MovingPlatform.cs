using Godot;
using System;

public class MovingPlatform : Spatial
{

    // using animation player at the moment for movement

    KinematicBody Platform;

    public override void _Ready()
    {
        Platform = GetNode<KinematicBody>("Body");
        
        Platform.AddToGroup("platform");
    }
}
