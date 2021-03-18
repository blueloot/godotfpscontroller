using System;
using Godot;

public class PlayerWalking : Node
{
    // Nodes
    [Export] private NodePath PlayerBody = "..";

    // Properties
    [Export] public float MoveSpeed = 3f;
    [Export] public float RunSpeedMultiplier = 2.2f;
    [Export] private float GroundStrength = 10f;
    [Export] private float AirStrength = 12f;
    public bool SprintRequest = false;

    // Singletons
    PlayerInput PlayerInput;
    
    public override void _Ready()
    {
        PlayerInput = GetNode<PlayerInput>("/root/PlayerInput");
    }

    public override void _PhysicsProcess(float delta)
    {
        Player Player = GetNode<Player>(PlayerBody);

        // sprint
        SprintRequest = PlayerInput.GetSprint();

        // direction
        var movement = PlayerInput.GetMovement();
        var transform = Player.GlobalTransform;
        Player.MoveDirection = Vector3.Zero;
        Player.MoveDirection += -transform.basis.z * movement.y;
        Player.MoveDirection +=  transform.basis.x * movement.x;
        Player.MoveDirection = Player.MoveDirection.Normalized();

        // movement
        if (Player.Grounded)
        {
            // check speed
            var speed = MoveSpeed;
                speed = (SprintRequest ? MoveSpeed * RunSpeedMultiplier : speed);

            // apply speed
            var targetVel = Player.MoveDirection * speed;
            Player.Velocity = Player.Velocity.LinearInterpolate( targetVel, GroundStrength * delta);
        }
        else
        {
            Player.Velocity += Player.MoveDirection * AirStrength * delta; // TODO: clamp
        }
    }
}
