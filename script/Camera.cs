using Godot;
using System;

public class Camera : Godot.Camera
{

    [Export] private NodePath Player;
    [Export] private float FollowDelay = 10.0f;

    [Export] private float RotationSensitivity = 0.05f;
    [Export] private bool RotationReverseX = false;
    [Export] private float RotationMaxPitch = 70f;

    private int MouseModeAxisX() { return (RotationReverseX)?1:-1; }

    private Spatial Head;
    private KinematicBody Body;
    private Mouse Mouse;

    public override void _Ready()
    {
        Body = GetNode<KinematicBody>(Player);
        Head = GetNode<Spatial>(Player+"/Head");
        Mouse = GetNode<Mouse>("/root/Mouse");

        GlobalTransform = Head.GlobalTransform;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion && Mouse.Hidden())
        {
            var mousemotion = @event as InputEventMouseMotion;
            Body.RotateY(Mathf.Deg2Rad(-mousemotion.Relative.x * RotationSensitivity));
            Head.RotateX(Mathf.Deg2Rad(mousemotion.Relative.y * RotationSensitivity * MouseModeAxisX()));

            var clamped = Head.RotationDegrees;
            clamped.x = Mathf.Clamp(clamped.x, -RotationMaxPitch, RotationMaxPitch);
            Head.RotationDegrees = clamped;
        }
    }

    public override void _Process(float delta)
    {
        var delay = GlobalTransform;
        var old = delay;
        delay = Head.GlobalTransform;
        delay.origin.y = old.origin.y;
        delay.origin.y = Mathf.Lerp( delay.origin.y, Head.GlobalTransform.origin.y, FollowDelay * delta );
        GlobalTransform = delay;
    }

}
