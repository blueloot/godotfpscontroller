using Godot;
using System;

public class PlayerInput : Node
{
    Mouse Mouse;

    public override void _Ready()
    {
        Mouse = GetNode<Mouse>("/root/Mouse");
    }

    public bool GetEscapeKeyPressed()
    {
        return Input.IsActionJustPressed("ui_cancel");
    }

    public bool Allowed()
    {
        return Mouse.Hidden;
    }

    public bool GetJump()
    {
        if (Allowed())
        {
            return Input.IsActionPressed("jump");
        }
        return false;
    }

    public bool GetSprint()
    {
        if (Allowed())
        {
            return Input.IsActionPressed("sprint");
        }
        return false;
    }

    public bool GetCrouch()
    {
        if (Allowed())
        {
            return Input.IsActionPressed("crouch");
        }
        return false;
    }

    public Vector2 GetMovement()
    {
        if (Allowed())
        {
            if (Input.IsActionPressed("forward") || Input.IsActionPressed("left")
            || Input.IsActionPressed("backward") || Input.IsActionPressed("right"))
            {
                Vector2 inputVector;
                inputVector.x = Input.GetActionStrength("right") - Input.GetActionStrength("left");
                inputVector.y = Input.GetActionStrength("forward") - Input.GetActionStrength("backward");
                return inputVector.Normalized();
            }
        }
        return Vector2.Zero;
    }
}
