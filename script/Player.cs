using Godot;
using System;

public class Player : KinematicBody
{

    // Properties
    [Export] private float Gravity = 42f;
    [Export] private float MaxSlopeThresholdAllowed = 0.7f;
    [Export] public float MoveSpeed = 3f;
    [Export] public float RunSpeedMultiplier = 2.5f;
    [Export] public float CrouchSpeedMultiplier = 0.55f;
    [Export] private bool CrouchModeIsToggle = false;
    [Export] private float JumpStrength = 12f;
    [Export] private float StepSize = 0.2f;
    public Vector3 GroundVector;
    public Vector3 Velocity;
    public Vector3 MoveDirection;
    public Vector3 MoveVelocity;
    public bool Grounded;

    private float GroundStrength = 10f; // TODO: changed by ground material
    private float GroundCheckDistance = 5f;
    private float AirStrength = 12f;

    public float GroundSnap;
    private float SlideSpeed;
    private float SlideCooldown;
    private bool SlideBlocked;
    private bool SlideRequest;
    public bool SprintRequest;
    public bool CrouchRequest;
    private bool oldCrouchRequest;

    private float CrouchTransitionSpeed = 6f;
    private float HeightBodyCrouching = 1.0f; // size of collider when crouched
    private float HeightHeadCrouching = -0.2f; // position of head in local space when crouched
    private float HeightBodyStanding; // grabbed automatically from editor
    private float HeightHeadStanding; // grabbed automatically from edtior

    private CollisionShape Body;
    private Spatial Head;
    private MeshInstance Mesh;
    private Area HeadBonker;

    // Singletons
    PlayerInput PlayerInput;

    Mouse Mouse; // temp

    public override void _Ready()
    {
        PlayerInput = GetNode<PlayerInput>("/root/PlayerInput");

        Body = GetNode<CollisionShape>("Body");
        Head = GetNode<Spatial>("Head");
        Mesh = GetNode<MeshInstance>("Mesh");
        HeadBonker = GetNode<Area>("HeadBonker");

        GroundSnap = GroundCheckDistance;

        HeightBodyStanding = ((CapsuleShape)Body.Shape).Height;
        HeightHeadStanding = Head.Translation.y;

        CrouchSetState(false);

        Mouse = GetNode<Mouse>("/root/Mouse"); // temp
        Mouse.Hide(); // temp
    }

    public override void _PhysicsProcess(float delta)
    {
        GroundControl(delta);

        SprintProcess();

        CrouchProcess(delta);

        SlideProcess(delta);

        StairControl();

        GetMovementDirection();

        SetMovementVelocity(delta);

        JumpProcess();

        // Update movement
        MoveVelocity = new Vector3(Velocity.x + GroundVector.x, GroundVector.y, Velocity.z + GroundVector.z);
        MoveVelocity = MoveAndSlide(MoveVelocity, Vector3.Up, false, 4, MaxSlopeThresholdAllowed, false);
    }

    private void GetMovementDirection()
    {
        var movement = PlayerInput.GetMovement();
        var transform = GlobalTransform;
        MoveDirection = (!SlideRequest && Grounded) ? Vector3.Zero : MoveDirection;
        if (!SlideRequest && Grounded)
        {
            MoveDirection += -transform.basis.z * movement.y;
            MoveDirection +=  transform.basis.x * movement.x;
            MoveDirection = MoveDirection.Normalized();
        }
    }

    private void SetMovementVelocity(float delta)
    {
        if (Grounded)
        {
            // get speed
            var speed = MoveSpeed;
                speed = (SprintRequest ? MoveSpeed * RunSpeedMultiplier : speed);
                speed = (CrouchRequest ? MoveSpeed * CrouchSpeedMultiplier : speed);
                speed = (SlideRequest  ? SlideSpeed : speed);

            // apply speed
            var JumpRequest = !Grounded;
            var targetVel = MoveDirection * speed * (JumpRequest?1.5f:1);
            Velocity = Velocity.LinearInterpolate( targetVel, GroundStrength * delta);
        }
        else
        {
            Velocity += MoveDirection * AirStrength * delta; // TODO: clamp value to prevent infinte speed
        }
    }

    private void GroundControl(float delta)
    {
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
    }

    private void StairControl()
    {
        if (GetSlideCount() > 0 && Grounded)
        {
            for (int i = 1; i < GetSlideCount(); i++)
            {
                var collisionPos = GetSlideCollision(i).Position - GlobalTransform.origin;
                var bottom = -(HeightBodyStanding+0.5f)*0.5f;
                var stepsize = -1f+StepSize;
                
                if (collisionPos.y > bottom && collisionPos.y < stepsize)
                {
                    GroundSnap = 1f;
                    GroundVector.y = GetFloorNormal().y * 6f; // this 6f seems to help, but I don't think it's universal
                }
            }
        }
    }

    // TODO: Add a cooldown time to prevent continuous jumping (e.g. jumping up a ledge)
    // TODO: Consider reducing jump strength by some value of previous landing impact
    // TODO: If head collide with ceiling set yvel to 0 to prevent stickiness
    private void JumpProcess()
    {
        if (PlayerInput.GetJump() && Grounded)
        {
            Grounded = false;
            GroundSnap = 1f;
            GroundVector = Vector3.Up * JumpStrength;
        }
    }


    // TODO: change camera fov
    private void SprintProcess()
    {
        SprintRequest = PlayerInput.GetSprint();
    }

    // BUG: if sprint+crouch is held and slide is requested but is on cooldown. it will activate anyway as soon as cd is over. it shouldn't
    // BUG: if player stand still and sprint+crouch is held, it will not move
    // TODO: consider only allowed to slide if player has sprinted for a certain amount of time
    // TODO: sliding up slopes should decrease slide speed more and sliding down slopes should increase slide speed slightly (depending on ground material)
    private void SlideProcess(float delta)
    {
        if (CrouchRequest && SprintRequest)
        {
            if (SlideAllowed())
            {
                SlideStart();
                return;
            }
        }

        if (!CrouchRequest)
        {
            SlideStop();
            SlideBlocked = false;
        }


        if (SlideRequest)
        {
            SlideSpeed -= GroundStrength * delta;

            if (SlideSpeed <= 0f)
            {
                SlideStop();
            }
        }

        if (SlideCooldown > 0)
        {
            SlideCooldown -= 2f * delta; // random number 2f for now..
        }
    }

    private void SlideStop()
    {
        SlideRequest = false;
    }

    private void SlideStart()
    {
        SlideSpeed = MoveSpeed * RunSpeedMultiplier * 2;
        SlideRequest = true;
        SlideCooldown = 2f;
        SlideBlocked = true;
    }

    private bool SlideAllowed()
    {
        return (SlideCooldown <= 0f && !SlideBlocked);
    }

    private void WatchTheHead()
    {
        var body = Body.Shape as CapsuleShape;

        if (body.Height < HeightBodyStanding && HeadBonker.GetOverlappingBodies().Count > 1)
        {
            CrouchSetState(true);
        }
    }

    private void CrouchSetState(bool state)
    {
        CrouchRequest = state;
        oldCrouchRequest = CrouchRequest;
    }

    private void CrouchGetInput()
    {
        if (CrouchHold()) { return; }
        if (CrouchToggle()) { return; }
        oldCrouchRequest = !CrouchRequest;
    }

    private bool CrouchHold()
    {
        if (!CrouchModeIsToggle)
        {
            CrouchSetState(PlayerInput.GetCrouch());
            return true;
        }
        return false;
    }

    private bool CrouchToggle()
    {
        if (PlayerInput.GetCrouch())
        {
            if (oldCrouchRequest != CrouchRequest)
            {
                CrouchSetState(!CrouchRequest);
            }
            return true;
        }
        return false;
    }

    private void CrouchProcess(float delta)
    {
        CrouchGetInput();
        CrouchSitStand(delta);
    }

    private void CrouchSitStand(float delta)
    {
        WatchTheHead();

        if (CrouchRequest)
        {
            CrouchTryCrouch(delta);
            return;
        }
        CrouchTryStand(delta);
    }

    private void CrouchTryCrouch(float delta)
    {
        var body = Body.Shape as CapsuleShape;

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
        var body = Body.Shape as CapsuleShape;

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

    private void CrouchUpdateMesh(CapsuleShape body)
    {
        var meshSizeOffset = 0.5f;      // mesh is 0.5f shorter than collider
        var meshPositionOffset = -0.2f; // mesh is positioned -0.2f below collider
        var mesh = Mesh.Mesh as CapsuleMesh;
        var mt = Mesh.Transform;
        mesh.MidHeight = body.Height - meshSizeOffset;
        mt.origin.y = meshPositionOffset - (1-((body.Height-meshSizeOffset) / (HeightBodyStanding-meshSizeOffset)));
        Mesh.Transform = mt;
    }

    private void CrouchUpdateCollider(CapsuleShape body)
    {
        var bt = Body.Transform;
        bt.origin.y = 0 - (1-(body.Height / HeightBodyStanding));
        Body.Transform = bt;
    }
}