using Godot;
using System;

public class Player : KinematicBody
{
    // General
    [Export] private float Gravity = -42f;
    private float HeightBodyStanding = 2.0f;    // collision body (including feet gives 2.5 unit tall)
    private float HeightHeadStanding = 1.0f;    // camera Y offset locally from players origin
    private float HeightBodyCrouching = 0.5f;
    private float HeightHeadCrouching = 0.2f;

    // Movement
    [Export] private float MovementSpeed = 3f;
    [Export(PropertyHint.Range,"10,20,0.1")] private float MovementStrength = 20f;
    [Export(PropertyHint.Range,"0.1,4,0.1")] private float MovementAirResistance = 3f;
    private float MoveSpeedCrouch = 0.55f;
    private float MoveSpeedSprint = 1.80f;
    private Vector3 Velocity;
    private Vector3 Direction;

    // Rotation
    [Export] private float RotationSensitivity = 0.05f;
    [Export] private bool RotationReverseX = false;
    [Export] private float RotationMaxPitch = 70f;
    private int MouseModeAxisX { get { return (RotationReverseX)?1:-1; } }

    // Crouching
    [Export] private bool CrouchModeIsToggle = false;
    [Export(PropertyHint.Range,"2,20,0.5")] private float CrouchingSpeed = 10f;
    private bool CrouchRequest;
    private bool oldCrouchRequest;

    // Nodes
    private CollisionShape Body;
    private MeshInstance Mesh;
    private Spatial Head;
    private RayCast HeadBonker;



    // Godot : READY
    public override void _Ready()
    {
        Body = GetNode<CollisionShape>("Body");
        Mesh = GetNode<MeshInstance>("Mesh");
        Head = GetNode<Spatial>("Head");
        HeadBonker = GetNode<RayCast>("HeadBonker");

        MouseHide();
        CrouchSetState(false);
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

        MovementProcess(delta);
        CrouchGetToggle();
        CrouchProcess(delta);

        RotationClampCamera();
    }



    // Rotation

    private void RotationClampCamera()
    {
        var clamped = Head.RotationDegrees;
        clamped.x = Mathf.Clamp(clamped.x, -RotationMaxPitch, RotationMaxPitch);
        Head.RotationDegrees = clamped;
    }



    // Crouching
    private void CrouchSetState(bool state)
    {
        CrouchRequest = state;
        oldCrouchRequest = CrouchRequest;
    }

    private void CrouchGetToggle()
    {
        if (CrouchHold()) { return; }
        if (CrouchToggle()) { return; }
        oldCrouchRequest = !CrouchRequest;
    }

    private void CrouchProcess(float delta)
    {
        CrouchWatchYourHead();

        if (CrouchRequest)
        {
            CrouchTryCrouch(delta);
            return;
        }
        CrouchTryStand(delta);
    }

    private bool CrouchHold()
    {
        if (!CrouchModeIsToggle)
        {
            CrouchSetState(CrouchGetInput());
            return true;
        }
        return false;
    }

    private bool CrouchToggle()
    {
        if (CrouchGetInput())
        {
            if (oldCrouchRequest != CrouchRequest)
            {
                CrouchSetState(!CrouchRequest);
            }
            return true;
        }
        return false;
    }

    private bool CrouchGetInput()
    {
        if (InputAllowed())
        {
            if (Input.IsActionPressed("crouch"))
            {
                return true;
            }
        }
        return false;
    }

    private void CrouchWatchYourHead()
    {
        var body = Body.Shape as CylinderShape;
        if (body.Height < HeightBodyStanding && HeadBonker.IsColliding())
        {
            CrouchSetState(true);
        }
    }

    private void CrouchTryCrouch(float delta)
    {
        var body = Body.Shape as CylinderShape;

        // skip if already max crouch
        if (body.Height == HeightBodyCrouching) { return; }

        // resize collider
        body.Height -= CrouchingSpeed * delta;
        if (body.Height < HeightBodyCrouching)
        {
            body.Height = HeightBodyCrouching;
        }

        // reposition collider
        var bt = Body.Transform;
        bt.origin.y = 0 - (1-(body.Height / HeightBodyStanding));
        Body.Transform = bt;

        // resize mesh
        var mesh = Mesh.Mesh as CapsuleMesh;
        mesh.MidHeight = body.Height - 0.5f;

        // reposition mesh
        var mt = Mesh.Transform;
        mt.origin.y = -0.2f - (1-((body.Height-0.5f) / (HeightBodyStanding-0.5f)));
        Mesh.Transform = mt;

        // reposition camera
        var transform = Head.Transform;
        transform.origin.y -= CrouchingSpeed * delta;
        if (transform.origin.y < HeightHeadCrouching)
        {
            transform.origin.y = HeightHeadCrouching;
        }
        Head.Transform = transform;
    }

    private void CrouchTryStand(float delta)
    {
        var body = Body.Shape as CylinderShape;

        // skip if already max stand
        if (body.Height == HeightBodyStanding) { return; }

        // resize collider
        body.Height += CrouchingSpeed * delta;
        if (body.Height > HeightBodyStanding)
        {
            body.Height = HeightBodyStanding;
        }

        // reposition collider
        var bt = Body.Transform;
        bt.origin.y = 0 - (1-(body.Height / HeightBodyStanding));
        Body.Transform = bt;

        // resize mesh
        var mesh = Mesh.Mesh as CapsuleMesh;
        mesh.MidHeight = body.Height - 0.5f;

        // reposition mesh
        var mt = Mesh.Transform;
        mt.origin.y = -0.2f - (1-((body.Height-0.5f) / (HeightBodyStanding-0.5f)));
        Mesh.Transform = mt;

        // reposition camera
        var ht = Head.Transform;
        ht.origin.y += CrouchingSpeed * delta;
        if (ht.origin.y > HeightHeadStanding)
        {
            ht.origin.y = HeightHeadStanding;
        }
        Head.Transform = ht;
    }


    // Movement

    private void MovementProcess(float delta)
    {
        MovementGetDirection();

        // get target velocity
        var speedMultiplier = 1.0f;
            speedMultiplier = (CrouchRequest) ? MoveSpeedCrouch : speedMultiplier;
        var targetVel = Direction * (MovementSpeed * speedMultiplier);

        // set acceleration
        var acceleration = (IsOnFloor()) ? MovementStrength : MovementAirResistance;

        // set velocity
        Velocity = Velocity.LinearInterpolate( targetVel, acceleration * delta);
        Velocity.y += Gravity * delta;

        // apply collision
        MoveAndSlideWithSnap(Velocity, Vector3.Down, Vector3.Up, false, 4, 0.785398f, false);
    }

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
