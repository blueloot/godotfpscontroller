using Godot;
using System;

public class Player : KinematicBody
{
    PlayerInput PlayerInput;
    Mouse Mouse;

    public override void _Ready()
    {
        PlayerInput = GetNode<PlayerInput>("/root/PlayerInput");
        Mouse = GetNode<Mouse>("/root/Mouse");

        Mouse.Hide();
    }

    public override void _Process(float delta)
    {

    }
}
