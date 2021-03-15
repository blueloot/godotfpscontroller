using Godot;
using System;

public class PlayerWalking : Node
{
    [Export] public float MovementSpeed = 3f;

    PlayerInput PlayerInput;
    Mouse Mouse;

    public override void _Ready()
    {
        PlayerInput = GetNode<PlayerInput>("/root/PlayerInput");
        Mouse = GetNode<Mouse>("/root/Mouse");
    }

    public override void _Process(float delta)
    {

    }
}
