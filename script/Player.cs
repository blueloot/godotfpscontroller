using Godot;
using System;

public class Player : KinematicBody
{
    // General
    private float HeightBodyStanding = 2.0f;    // collision body (including feet gives 2.5 unit tall)
    private float HeightHeadStanding = 1.0f;    // camera Y offset locally from players origin
    private float HeightBodyCrouching = 0.5f;
    private float HeightHeadCrouching = 0.1f;

    // Movement
    [Export] private float MovementSpeed = 3f;  // 3f seems like a normal walk speed for a human
    private float MovementStrength = 20f;       // (0.4f of this when sprinting)
    private float MoveSpeedCrouch = 0.55f;
    private float MoveSpeedSprint = 4f; //2.60f;
    [Export] private bool CrouchModeIsToggle = false;
    [Export(PropertyHint.Range,"2,20,0.5")] private float CrouchingSpeed = 6f;
    private bool SprintRequest;
    private bool CrouchRequest;
    private bool oldCrouchRequest;
    private Vector3 Velocity;
    private Vector3 Direction;

    // Sliding
    private bool SlideRequest;

    // Rotation
    [Export] private float RotationSensitivity = 0.05f;
    [Export] private bool RotationReverseX = false;
    [Export] private float RotationMaxPitch = 70f;
    private int MouseModeAxisX { get { return (RotationReverseX)?1:-1; } }

    // Jumping
    [Export] private float Gravity = -42f;
    [Export] private float JumpPower = 22f;
    private bool JumpRequest = false;

    // Nodes
    private CollisionShape Body;
    private MeshInstance Mesh;
    private Spatial Head;
    private RayCast HeadBonker;
    private Area JumpHelper;



    // Godot : READY
    public override void _Ready()
    {
        Body = GetNode<CollisionShape>("Body");
        Mesh = GetNode<MeshInstance>("Mesh");
        Head = GetNode<Spatial>("Head");
        HeadBonker = GetNode<RayCast>("HeadBonker");
        JumpHelper = GetNode<Area>("JumpHelper");

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
        JumpProcess();
        CrouchGetToggle();
        CrouchProcess(delta);
        SprintProcess();
    }



    // Sprinting

    private void SprintProcess()
    {
        // reset and exit if crouching
        if (CrouchRequest)
        {
            SprintRequest = false;
            return;
        }

        SprintRequest = SprintGetInput();
    }

    private bool SprintGetInput()
    {
        if (InputAllowed())
        {
            return (Input.IsActionPressed("sprint"));
        }
        return false;
    }



    // Jumping

    private void JumpProcess()
    {
        // remove request
        JumpRequest = false;

        // exit if JumpHelper only collides with 1 body (player) because it means player is airborne
        if (JumpHelper.GetOverlappingBodies().Count == 1){ return; }

        // exit if crouched and not allowed to stand
        if (CrouchRequest && HeadBonker.IsColliding()) { return; }

        // apply jumpforce (weakened when crouched or sprinting)
        if (JumpGetInput())
        {
            Velocity.y += (CrouchRequest || SprintRequest)
                ? JumpPower / (SprintRequest ? 1.2f : 1.5f)
                : JumpPower;
            JumpRequest = true;
        }
    }

    private bool JumpGetInput()
    {
        if (InputAllowed())
        {
            return Input.IsActionJustPressed("jump");
        }
        return false;
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

        // slide?
        if (CrouchRequest && SprintRequest)
        {
            if (SlideAllowed())
            {
                SlideInitiate();
            }
        }
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
        var ht = Head.Transform;
        ht.origin.y -= CrouchingSpeed * delta;
        if (ht.origin.y < HeightHeadCrouching)
        {
            ht.origin.y = HeightHeadCrouching;
        }
        Head.Transform = ht;
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



    // Sliding

    private bool SlideAllowed()
    {
        return false;
    }
    private void SlideInitiate()
    {
        return;
    }



    // Movement

    private void MovementProcess(float delta)
    {
        MovementGetDirection();

        // get target velocity
        var speedMultiplier = 1.0f;
            speedMultiplier = (CrouchRequest) ? MoveSpeedCrouch : speedMultiplier;
            speedMultiplier = (SprintRequest) ? MoveSpeedSprint : speedMultiplier;
        var targetVel = Direction * ((MovementSpeed * (JumpRequest?3:1)) * speedMultiplier);

        // set velocity
        if (Floored())
        {
            Velocity = Velocity.LinearInterpolate( targetVel, (MovementStrength * (SprintRequest?0.4f:1f)) * delta);
        }
        Velocity.y += Gravity * delta;

        // apply collision
        MoveAndSlide(Velocity, Vector3.Up, false, 4, 0.785398f, false);
    }

    private bool Floored()
    {
        return (IsOnFloor() || (JumpHelper.GetOverlappingBodies().Count != 1));
    }

    private void MovementGetDirection()
    {
        var movement = MovementGetInputVector();
        var transform = GlobalTransform;
        Direction = (Floored()) ? Vector3.Zero : Direction;
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
            var mousemotion = @event as InputEventMouseMotion;
            RotateY(Mathf.Deg2Rad(-mousemotion.Relative.x * RotationSensitivity));
            Head.RotateX(Mathf.Deg2Rad(mousemotion.Relative.y * RotationSensitivity * MouseModeAxisX));

            RotationClampCamera();
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
