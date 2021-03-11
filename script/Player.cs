using Godot;
using System;

public class Player : KinematicBody
{
    // Exports
    [Export] private float Gravity = -42f;
    [Export] private float JumpPower = 22f;     // SprintRequest multiplies this by 0.8f and CrouchRequest by 0.6f;
    [Export] private float MovementSpeed = 3f;  // 3f seems like a normal walk speed for a human
    [Export] private bool CrouchModeIsToggle = false;

    // Movement
    private float MovementStrength = 20f;   // gives best result. SprintRequest multiplies this by 0.4f
    private float MoveSpeedCrouch = 0.55f;  // MovementSpeed is multiplied by this value
    private float MoveSpeedSprint = 2.60f;  // MovementSpeed is multiplied by this value. 4f feels natural.
    private float CrouchTransitionSpeed = 6f;   // between 2f and 20f otherwise it looks like it will glitch
    private Vector3 Velocity;
    private Vector3 Direction;
    private float SlideSpeed;
    private float SlideRest;

    // Block certain stuff
    private float MaxSlopeThresholdAllowed = 0.65f; // 1.0f is flat (floor),  0.0f is vertical (wall)
    private bool JumpBlocked;
    private bool SlideBlocked;

    // Player height for crouching and standing. must match player scene for best result
    private float HeightBodyStanding = 2.0f;    // feet are 0.5 units, so this gives us 2.5f height total
    private float HeightHeadStanding = 1.0f;
    private float HeightBodyCrouching = 1.2f;
    private float HeightHeadCrouching = 0.1f;

    // Requests
    private bool JumpRequest;
    private bool SlideRequest;
    private bool SprintRequest;
    private bool CrouchRequest;
    private bool oldCrouchRequest;

    // Nodes
    private CollisionShape Body;
    private MeshInstance Mesh;
    private Spatial Head;
    private Area HeadBonker;
    private Area JumpHelper;
    private Mouse Mouse;



    // Godot : READY
    public override void _Ready()
    {
        Body = GetNode<CollisionShape>("Body");
        Mesh = GetNode<MeshInstance>("Mesh");
        Head = GetNode<Spatial>("Head");
        HeadBonker = GetNode<Area>("HeadBonker");
        JumpHelper = GetNode<Area>("JumpHelper");

        Mouse = GetNode<Mouse>("/root/Mouse");

        Mouse.Hide();
        CrouchSetState(false);
    }



    // Godot : PHYSICS
    public override void _PhysicsProcess(float delta)
    {
        Mouse.HideShow();

        MovementProcess(delta);
        JumpProcess();
        CrouchGetToggle();
        CrouchProcess(delta);
        SprintProcess();
        SlideProcess(delta);
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
        if (CrouchRequest && HeadBonker.GetOverlappingBodies().Count != 1) { return; }

        // apply jumpforce (weakened when crouched or sprinting)
        if (JumpGetInput())
        {
            Velocity.y += (CrouchRequest || SprintRequest)
                ? JumpPower / (SprintRequest ? 1.2f : 1.5f)
                : JumpPower;
            JumpRequest = true;
            SlideStop();
        }
    }

    private bool JumpGetInput()
    {
        if (InputAllowed() && !JumpBlocked)
        {
            return Input.IsActionJustPressed("jump");
        }
        return false;
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
                SlideStart();
                return;
            }
        }

        // stop slide?
        if (!CrouchRequest)
        {
            SlideStop();
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
        if (body.Height < HeightBodyStanding && HeadBonker.GetOverlappingBodies().Count != 1)
        {
            CrouchSetState(true);
        }
    }

    private void CrouchTryCrouch(float delta)
    {
        var body = Body.Shape as CylinderShape;

        // skip if already max crouch
        if (body.Height == HeightBodyCrouching) { return; }

        // update collider and mesh
        body.Height = (body.Height < HeightBodyCrouching) ? HeightBodyCrouching : body.Height - CrouchTransitionSpeed * delta;
        CrouchUpdateCollider(body);
        CrouchUpdateMesh(body);

        // reposition camera
        var ht = Head.Transform;
        ht.origin.y -= CrouchTransitionSpeed * delta;
        ht.origin.y = (ht.origin.y < HeightHeadCrouching) ? HeightHeadCrouching : ht.origin.y;
        Head.Transform = ht;
    }

    private void CrouchTryStand(float delta)
    {
        var body = Body.Shape as CylinderShape;

        // skip if already max stand
        if (body.Height == HeightBodyStanding) { return; }

        // update collider and mesh
        body.Height = (body.Height > HeightBodyStanding) ? HeightBodyStanding : body.Height + CrouchTransitionSpeed * delta;
        CrouchUpdateCollider(body);
        CrouchUpdateMesh(body);

        // reposition camera
        var ht = Head.Transform;
        ht.origin.y += CrouchTransitionSpeed * delta;
        ht.origin.y = (ht.origin.y > HeightHeadStanding) ? HeightHeadStanding : ht.origin.y;
        Head.Transform = ht;
    }

    private void CrouchUpdateMesh(CylinderShape body)
    {
        var meshSizeOffset = 0.5f;      // mesh is 0.5f shorter than collider
        var meshPositionOffset = -0.2f; // mesh is positioned -0.2f below collider
        var mesh = Mesh.Mesh as CapsuleMesh;
        var mt = Mesh.Transform;
        mesh.MidHeight = body.Height - meshSizeOffset;
        mt.origin.y = meshPositionOffset - (1-((body.Height-meshSizeOffset) / (HeightBodyStanding-meshSizeOffset)));
        Mesh.Transform = mt;
    }

    private void CrouchUpdateCollider(CylinderShape body)
    {
        var bt = Body.Transform;
        bt.origin.y = 0 - (1-(body.Height / HeightBodyStanding));
        Body.Transform = bt;
    }



    // Sliding

    private bool SlideAllowed()
    {
        if (SlideRest <= 0f && !SlideBlocked)
        {
            return true;
        }
        return false;
    }
    private void SlideStart()
    {
        SlideSpeed = MovementSpeed * MoveSpeedSprint;
        SlideRequest = true;
        SlideRest = 2f;
        return;
    }
    private void SlideStop()
    {
        SlideRequest = false;
        return;
    }
    private void SlideProcess(float delta)
    {
        if (SlideRequest)
        {
            SlideSpeed -= MovementStrength * delta;

            if (SlideSpeed <= 0f || SlideBlocked)
            {
                SlideStop();
            }
        }

        if (SlideRest > 0)
        {
            SlideRest -= 2f * delta;
        }
    }



    // Movement

    private void MovementProcess(float delta)
    {
        MovementGetDirection();

        // get target velocity
        var speedMultiplier = 1.0f;
            speedMultiplier = (CrouchRequest) ? MoveSpeedCrouch : speedMultiplier;
            speedMultiplier = (SprintRequest) ? MoveSpeedSprint : speedMultiplier;
            speedMultiplier = (SlideRequest)  ? SlideSpeed : speedMultiplier;
        var targetVel = Direction * ((MovementSpeed * (JumpRequest?3:1)) * speedMultiplier);

        // set velocity
        if (Floored())
        {
            Velocity = Velocity.LinearInterpolate( targetVel, (MovementStrength * (SprintRequest?0.4f:1f)) * delta);
        }
        Velocity.y += Gravity * delta;

        // apply collision
        MoveAndSlide(Velocity, Vector3.Up, false, 4, 0.785398f, false);

        // check floor normal, and block jumping if angle is too steep
        JumpBlocked = false;
        SlideBlocked = false;
        if (GetSlideCount() > 0)
        {
            for (int i = 0; i < GetSlideCount(); i++)
            {
                if (GetSlideCollision(i).Normal.y < MaxSlopeThresholdAllowed)
                {
                    JumpBlocked = true;
                    SlideBlocked = true;
                }
            }
        }
    }

    private bool Floored()
    {
        return (IsOnFloor() || (JumpHelper.GetOverlappingBodies().Count != 1));
    }

    private void MovementGetDirection()
    {
        var movement = MovementGetInputVector();
        var transform = GlobalTransform;
        Direction = (Floored() && !SlideRequest) ? Vector3.Zero : Direction;
        Direction += -transform.basis.z * movement.y;
        Direction +=  transform.basis.x * movement.x;
        Direction = Direction.Normalized();
    }

    private Vector2 MovementGetInputVector()
    {
        if (InputAllowed() && !SlideRequest)
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

    private bool InputAllowed()
    {
        return Mouse.Hidden();
    }

}
