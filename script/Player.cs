using Godot;
using System;

public class Player : KinematicBody
{
    // public float RotationSensitivity = 0.5f;
    // public float RotationMaxPitch = 75f;


    PlayerInput PlayerInput;
    Mouse Mouse;

    KinematicBody Body;
    Spatial Head;

    public override void _Ready()
    {
        PlayerInput = GetNode<PlayerInput>("/root/PlayerInput");
        Mouse = GetNode<Mouse>("/root/Mouse");

        Body = GetNode<KinematicBody>(GetPath());
        Head = GetNode<Spatial>("Head");
    }

    public override void _Process(float delta)
    {

    }
}
