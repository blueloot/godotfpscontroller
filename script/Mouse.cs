using Godot;
using System;

public class Mouse : Node
{

    public void HideShow()
    {
        if (!Input.IsActionJustPressed("ui_cancel")) { return; }

        if (Visible())
        {
            Hide();
            return;
        }

        Show();
    }

    public void Hide()
    {
        Input.SetMouseMode(Input.MouseMode.Captured);
    }

    public void Show()
    {
        Input.SetMouseMode(Input.MouseMode.Visible);
    }

    public bool Visible()
    {
        return Input.GetMouseMode() == Input.MouseMode.Visible;
    }

    public bool Hidden()
    {
        return Input.GetMouseMode() == Input.MouseMode.Captured;
    }

}
