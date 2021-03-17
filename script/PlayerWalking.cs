using System;
using Godot;

public class PlayerWalking : Node
{
    // Nodes
    [Export] private NodePath PlayerBody = "..";
    private KinematicBody Body;
    private Spatial Head;

    // Exports
    [Export] public float MoveSpeed = 3f;
    [Export] private float GroundStrength = 10f;
    [Export] private float AirStrength = 5f;
    private float Friction = 0.9f;

    // Singletons
    PlayerInput PlayerInput;
    

    public override void _Ready()
    {
        PlayerInput = GetNode<PlayerInput>("/root/PlayerInput");

        Body = GetNode<KinematicBody>(PlayerBody);
        Head = GetNode<Spatial>(PlayerBody+"/Head");
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
            // if (movement == Vector2.Zero) Player.Velocity *= Friction;
        }
        else
        {
            Player.Velocity += Player.MoveDirection * AirStrength * delta; // clamp?
        }
    }
}
