using Godot;
using System;

public class PlayerWalking : Node
{
    // Nodes
    [Export] private NodePath PlayerBody = "..";
    private KinematicBody Body;
    private Spatial Head;

    // Exports
    [Export] public float MovementSpeed = 9f;
    [Export] private float Accelleration = 20f;
    [Export] private float Friction = 0.9f;

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

        Player.MoveDirection = (Player.Grounded) ? Vector3.Zero : Player.MoveDirection;
        Player.MoveDirection += -transform.basis.z * movement.y;
        Player.MoveDirection +=  transform.basis.x * movement.x;
        Player.MoveDirection = Player.MoveDirection.Normalized();

        var targetVel = Player.MoveDirection * MovementSpeed;

        Player.Velocity = Player.Velocity.LinearInterpolate( targetVel, Accelleration * delta);
        
        if (movement == Vector2.Zero) Player.Velocity *= Friction;
    }
}
