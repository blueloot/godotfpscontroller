using Godot;
using System;

public class Player : KinematicBody
{
    // Properties
    [Export] private float Gravity = 42f;
    [Export] private float MaxSlopeThresholdAllowed = 0.7f;
    [Export] private float MaxStepThresholdAllowed = 10f;
    public Vector3 GroundVector;
    public Vector3 Velocity;
    public Vector3 MoveDirection;
    public Vector3 MoveVelocity;
    public bool Grounded;

    private float GroundCheckDistance = 5f;
    private float GroundSnap;

    private bool test;

    private CapsuleShape Body;

    // Singletons
    PlayerInput PlayerInput;
    Mouse Mouse; // temp

    public override void _Ready()
    {
        PlayerInput = GetNode<PlayerInput>("/root/PlayerInput");
        Mouse = GetNode<Mouse>("/root/Mouse"); // temp

        Body = GetNode<CollisionShape>("Body").Shape as CapsuleShape;

        GroundSnap = GroundCheckDistance;

        Mouse.Hide(); // temp
    }

    public override void _PhysicsProcess(float delta)
    {
        // Ground check
        if (IsOnFloor())
        {
            Grounded = true;
            GroundVector = -GetFloorNormal() * GroundSnap;
            GroundSnap = GroundCheckDistance;
        }
        else
        {
            Grounded = false;
            GroundVector += Vector3.Down * Gravity * delta;
        }

        // Ground Control
        if (PlayerInput.GetJump() && Grounded)      // jumping | TODO: add cooldown
        {
            Grounded = false;
            GroundSnap = 1f;
            GroundVector = Vector3.Up * 12f;
        }
        if (GetSlideCount() > 0 && Grounded)        // stairs
        {
            for (int i = 0; i < GetSlideCount(); i++)
            {
                var col = GlobalTransform.origin.y - GetSlideCollision(i).Position.y;
                var bodysize = Body.Height/2;
                var stairnormal = Body.Height/MaxStepThresholdAllowed;

                if (col > bodysize - stairnormal && col < bodysize + stairnormal +.1f)
                {
                    if (MoveVelocity.y >= 0f && MoveDirection != Vector3.Zero)
                    {
                        GroundSnap = 1f;
                        GroundVector.y = GetFloorNormal().y * 6f;
                    }
                }
            }
        }

        // update
        MoveVelocity = new Vector3(Velocity.x + GroundVector.x, GroundVector.y, Velocity.z + GroundVector.z);
        MoveVelocity = MoveAndSlide(MoveVelocity, Vector3.Up, false, 4, MaxSlopeThresholdAllowed, false);
    }
}