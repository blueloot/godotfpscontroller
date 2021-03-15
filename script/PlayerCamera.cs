using Godot;
using System;

public class PlayerCamera : Camera
{
    [Export] private NodePath PlayerNode = "";
    [Export] private float RotationSensitivity = 0.1f;
    [Export] private bool RotationReverseX = false;
    [Export] private float RotationMaxPitch = 70f;

    private int MouseModeAxisX { get {  return (RotationReverseX)?1:-1; } }

    private Mouse Mouse;
    private PlayerInput PlayerInput;

    private Spatial Head;
    private KinematicBody Body;


    public override void _Ready()
    {
        PlayerInput = GetNode<PlayerInput>("/root/PlayerInput");
        Mouse = GetNode<Mouse>("/root/Mouse");

        Body = GetNode<KinematicBody>(PlayerNode);
        Head = GetParent<Spatial>();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion && Mouse.Hidden)
        {
            var mousemotion = @event as InputEventMouseMotion;
            Body.RotateY(Mathf.Deg2Rad(-mousemotion.Relative.x * RotationSensitivity));
            Head.RotateX(Mathf.Deg2Rad(mousemotion.Relative.y * RotationSensitivity * MouseModeAxisX));

            var clamped = Head.RotationDegrees;
            clamped.x = Mathf.Clamp(clamped.x, -RotationMaxPitch, RotationMaxPitch);
            Head.RotationDegrees = clamped;
        }
    }

}
