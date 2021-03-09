using Godot;
using System;

public class Player : KinematicBody
{
    // Movement
    [Export] private float Gravity = -42f;
    [Export] private float MoveSpeedMax = 9f;
    [Export] private float AccelerationGround = 12f;
    [Export] private float AccelerationAir = 3f;
    private Vector3 Velocity;
    private Vector3 Direction;

    // Rotation
    [Export] private float RotationSensitivity = 0.05f;
    [Export] private bool RotationReverseX = false;
    [Export] private float RotationMaxPitch = 70f;
    private int MouseModeAxisX { get { return (RotationReverseX)?1:-1; } }

    // Nodes
    private CapsuleShape Body;
    private CapsuleMesh Mesh;
    private Spatial Head;



    // Godot : READY
    public override void _Ready()
    {
        Body = GetNode<CollisionShape>("Body").Shape as CapsuleShape;
        Mesh = GetNode<MeshInstance>("Mesh").Mesh as CapsuleMesh;
        Head = GetNode<Spatial>("Head");

        MouseHide();
    }



    // Godot : INPUT
    public override void _Input(InputEvent @event)
    {
        InputSetRotation(@event);
    }



    // Godot : PHYSICS
    public override void _PhysicsProcess(float delta)
    {
        MouseHideShow();

        MovementGetDirection();

        RotationClampCamera();
        // to do: add delay on camera Y position if previous y is lower than new to make walking up steps smoother

        // get target velocity
        var speedMultiplier = 1.0f;
        var targetVel = Direction * (MoveSpeedMax * speedMultiplier);

        // set acceleration
        var acceleration = (IsOnFloor()) ? AccelerationGround : AccelerationAir;
        
        // set velocity
        Velocity.y += Gravity * delta;
        Velocity = Velocity.LinearInterpolate( targetVel, acceleration * delta);

        // apply collision
        MoveAndSlideWithSnap(Velocity, Vector3.Down, Vector3.Up, false, 4, 0.785398f, false);
    }



    // Rotation

    private void RotationClampCamera()
    {
        var clamped = Head.RotationDegrees;
        clamped.x = Mathf.Clamp(clamped.x, -RotationMaxPitch, RotationMaxPitch);
        Head.RotationDegrees = clamped;
    }



    // Movement

    private void MovementGetDirection()
    {
        var movement = MovementGetInputVector();
        var transform = GlobalTransform;
        Direction = (IsOnFloor()) ? Vector3.Zero : Direction;
        Direction += -transform.basis.z * movement.y;
        Direction +=  transform.basis.x * movement.x;
        Direction = Direction.Normalized();
    }

    private Vector2 MovementGetInputVector()
    {
        if (InputAllowed())
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



    // Input

    private void InputSetRotation(InputEvent @event)
    {
        if (@event is InputEventMouseMotion && MouseHidden())
        {
            var movement = @event as InputEventMouseMotion;
            RotateY(Mathf.Deg2Rad(-movement.Relative.x * RotationSensitivity));
            Head.RotateX(Mathf.Deg2Rad(movement.Relative.y * RotationSensitivity * MouseModeAxisX));
        }
    }

    private bool InputAllowed()
    {
        return MouseHidden();
    }



    // Mouse

    private void MouseHideShow()
    {
        if (!Input.IsActionJustPressed("ui_cancel")) { return; }

        if (MouseVisible())
        {
            MouseHide();
            return;
        }

        MouseShow();
    }

    private void MouseHide()
    {
        Input.SetMouseMode(Input.MouseMode.Captured);
    }

    private void MouseShow()
    {
        Input.SetMouseMode(Input.MouseMode.Visible);
    }

    private bool MouseVisible()
    {
        return Input.GetMouseMode() == Input.MouseMode.Visible;
    }

    private bool MouseHidden()
    {
        return Input.GetMouseMode() == Input.MouseMode.Captured;
    }
}
