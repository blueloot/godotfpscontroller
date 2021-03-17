using System;
using Godot;

public class PlayerWalking : Node
{
    // Nodes
    [Export] private NodePath PlayerBody = "..";

    // Properties
    [Export] public float MoveSpeed = 8f;
    [Export] private float GroundStrength = 10f;
    [Export] private float AirStrength = 5f;

    // Singletons
    PlayerInput PlayerInput;
    
    public override void _Ready()
    {
        PlayerInput = GetNode<PlayerInput>("/root/PlayerInput");
    }

    public override void _PhysicsProcess(float delta)
    {
        Player Player = GetNode<Player>(PlayerBody);

        var movement = PlayerInput.GetMovement();
        var transform = Player.GlobalTransform;
        Player.MoveDirection = Vector3.Zero;
        Player.MoveDirection += -transform.basis.z * movement.y;
        Player.MoveDirection +=  transform.basis.x * movement.x;
        Player.MoveDirection = Player.MoveDirection.Normalized();

        if (Player.Grounded)
        {
            var targetVel = Player.MoveDirection * MoveSpeed;
            Player.Velocity = Player.Velocity.LinearInterpolate( targetVel, GroundStrength * delta);
        }
        else
        {
            Player.Velocity += Player.MoveDirection * AirStrength * delta; // clamp?
        }
    }
}
