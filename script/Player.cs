using Godot;
using System;

public class Player : KinematicBody
{
    // Properties
    [Export] private float Gravity = 42f;
    [Export] private float MaxSlopeThresholdAllowed = 0.7f;
    public Vector3 GroundVector;
    public Vector3 Velocity;
    public Vector3 MoveDirection;
    public Vector3 MoveVelocity;
    public bool Grounded;

    private float MaxStepThresholdAllowed = 20f;
    private float GroundCheckDistance = 5f;
    private float GroundSnap;

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
            // Landing
            // TODO: should remove crouch state
            if (!Grounded)
            {
                Velocity.x *= .5f;  // reduce velocity slightly
                Velocity.z *= .5f;  // TODO: adjust additionally based on landing impact strength
            }
            // On floor
            Grounded = true;
            GroundVector = -GetFloorNormal() * GroundSnap;
            GroundSnap = GroundCheckDistance;
        }
        else
        {
            // In air
            Grounded = false;
            GroundVector += Vector3.Down * Gravity * delta;
        }

        // Jumping
        // TODO: Add a cooldown time to prevent continuous jumping (e.g. jumping up a ledge)
        // TODO: Consider reducing jump strength by some value of previous landing impact
        // TODO: If head collide with ceiling set yvel to 0 to prevent stickiness
        if (PlayerInput.GetJump() && Grounded)
        {
            Grounded = false;
            GroundSnap = 1f;
            GroundVector = Vector3.Up * 12f; // temporary jump strength of 12f for testing purpose
        }

        // Stair control
        // TODO: Needs rework
        // BUG: cannot walk stairs if crouched
        if (GetSlideCount() > 0 && Grounded)
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
                        GroundVector.y = GetFloorNormal().y * 6f; // this 6f seems to help, but I don't think it's universal
                    }
                }
            }
        }

        // Update movement
        MoveVelocity = new Vector3(Velocity.x + GroundVector.x, GroundVector.y, Velocity.z + GroundVector.z);
        MoveVelocity = MoveAndSlide(MoveVelocity, Vector3.Up, false, 4, MaxSlopeThresholdAllowed, false);
    }
}