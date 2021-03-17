using Godot;
using System;

public class PlayerRotate : Node
{
    [Export] private NodePath PlayerBody = "..";
    [Export] private float RotationSensitivity = 0.1f;
    [Export] private bool RotationReverseX = false;
    [Export] private float RotationMaxPitch = 70f;

    private int MouseModeAxisX { get {  return (RotationReverseX)?1:-1; } }

    private PlayerInput PlayerInput;
    private Spatial Head;
    private KinematicBody Body;


    public override void _Ready()
    {
        PlayerInput = GetNode<PlayerInput>("/root/PlayerInput");

        Body = GetNode<KinematicBody>(PlayerBody);
        Head = GetNode<Spatial>(PlayerBody+"/Head");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion && PlayerInput.Allowed())
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
