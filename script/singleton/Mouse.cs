using Godot;
using System;

public class Mouse : Node
{
    public bool Visible { get { return Input.GetMouseMode() == Input.MouseMode.Visible;  } }
    public bool Hidden  { get { return Input.GetMouseMode() == Input.MouseMode.Captured; } }

    private PlayerInput PlayerInput;

    public override void _Ready()
    {
        PlayerInput = GetNode<PlayerInput>("/root/PlayerInput");
    }

    public override void _Process(float delta)
    {
        if (PlayerInput.GetEscapeKeyPressed())
        {
            if (Visible) Hide(); else Show();
        }
    }

    public void Hide() { Input.SetMouseMode(Input.MouseMode.Captured); }

    public void Show() { Input.SetMouseMode(Input.MouseMode.Visible); }
}
