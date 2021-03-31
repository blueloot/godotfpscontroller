using Godot;
using System;

public class Game : Node
{
    Mouse Mouse;

    public override void _Ready()
    {
        // hide mouse when game launch
        Mouse = GetNode<Mouse>("/root/Mouse");
        Mouse.Hide();
    }
}
