using Godot;
using System;

public class Player : KinematicBody
{

    // Properties
    [Export] private float Gravity = 42f;
    [Export] private float MaxSlopeThresholdAllowed = 0.7f;
    [Export] private float MoveSpeedMaxGround = 3f;
    [Export] private float MoveSpeedMaxAir = 7f;
    [Export] private float RunSpeedMultiplier = 2.5f;
    [Export] private float CrouchSpeedMultiplier = 0.55f;
    [Export] private bool CrouchModeIsToggle = false;
    [Export] private float JumpStrength = 12f;
    private float StepSize = 0.2f;  // the player currently "jumps" by a factor of 6f when colliding with stairs,
                                    // so if stepsize increases it can not reach and if decreased then "jump" is too strong
    private float FallSpeedForceStand = 17f; // if fall speed exceeds this velocity, player is forced to stand (if crouched)
    private Vector3 GroundVector;
    private Vector3 Velocity;
    private Vector3 MoveDirection;
    private Vector3 MoveVelocity;
    private bool Grounded;

    private float GroundStrength = 10f; // TODO: changed by ground material
    private float GroundCheckDistance = 5f;
    private float AirStrength = 8f;

    private float GroundSnap;
    private float SlideSpeed;
    private float SlideCooldown;
    private bool SlideBlocked;
    private bool SlideRequest;
    private bool SprintRequest;
    private bool CrouchRequest;
    private bool oldCrouchRequest;

    private float CrouchTransitionSpeed = 6f;
    private float CrouchCooldown = 0f;
    private float CrouchCooldownTimer = 0.5f; // certain events trigger cooldown of crouch, set length of cooldown time
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
    }

    public override void _PhysicsProcess(float delta)
    {
        GroundProcess(delta);

        SprintProcess(delta);

        CrouchProcess(delta);

        SlideProcess(delta);

        StairControl();

        GetMovementDirection();

        MovementVelocityProcess(delta);

        JumpProcess();

        // Update movement
        MoveVelocity = new Vector3(Velocity.x + GroundVector.x, GroundVector.y, Velocity.z + GroundVector.z);
        MoveVelocity = MoveAndSlide(MoveVelocity, Vector3.Up, false, 4, MaxSlopeThresholdAllowed, false);
    }

    private void GetMovementDirection()
    {
        var movement = PlayerInput.GetMovement();
        var transform = GlobalTransform;
        MoveDirection = (!SlideRequest) ? Vector3.Zero : MoveDirection;
        if (!SlideRequest)
        {
            MoveDirection += -transform.basis.z * movement.y;
            MoveDirection +=  transform.basis.x * movement.x;
            MoveDirection = MoveDirection.Normalized();
        }
    }

    private void MovementVelocityProcess(float delta)
    {
        if (Grounded)
        {
            // get speed
            var speed = MoveSpeedMaxGround;
                speed = (SprintRequest ? MoveSpeedMaxGround * RunSpeedMultiplier : speed);
                speed = (CrouchRequest ? MoveSpeedMaxGround * CrouchSpeedMultiplier : speed);
                speed = (SlideRequest  ? SlideSpeed : speed);

            // apply speed
            var JumpRequest = !Grounded;
            var targetVel = MoveDirection * speed * (JumpRequest?1.5f:1);
            Velocity = Velocity.LinearInterpolate( targetVel, GroundStrength * delta);
        }
        else
        {
            Velocity += MoveDirection * AirStrength * delta;
            Velocity = Vector3Clamp(Velocity, MoveSpeedMaxAir);
        }
    }

    private Vector3 Vector3Clamp(Vector3 v, float f)
    {
        if (v.Length() > f)
            v = v.Normalized() * f;
        return v;
    }

    private void GroundProcess(float delta)
    {
        if (IsOnFloor())
        {
            // Landing
            if (!Grounded)
            {
                // force remove crouched state
                if (GroundVector.y < 0-FallSpeedForceStand)  CrouchSetState(false, CrouchCooldownTimer);

                // reduce movement velocity slightly
                // TODO: adjust additionally based on landing impact strength
                Velocity.x *= .5f;
                Velocity.z *= .5f;
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

    // BUG: Sometimes while sprinting, it does not recognize a stair/step and makes a full stop
    private void StairControl()
    {
        if (GetSlideCount() > 0)
        {
            for (int i = 1; i < GetSlideCount(); i++)
            {
                var collisionPos = GetSlideCollision(i).Position - GlobalTransform.origin;
                var bottom = -(HeightBodyStanding+0.5f)*0.5f;
                var stepsize = -1f+StepSize;
                
                if (collisionPos.y > bottom && collisionPos.y < stepsize)
                {
                    // approximate direction of movement compared to collision point
                    // where result of 0 is completely parallel to collision normal,
                    // and -3 to 0 is range "towards" collision, where -3 is completely perpendicular
                    // and 0 to +3 is range "away" from collision
                    if (Velocity.Dot(GetSlideCollision(i).Normal) < -0.8f)
                    {
                        GroundSnap = 1f;
                        // TODO: apply force based on distance from collision point to player body bottom to support any value of StepSize
                        GroundVector.y = GetFloorNormal().y * 6f;
                    }
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
    private void SprintProcess(float delta)
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
        SlideSpeed = MoveSpeedMaxGround * RunSpeedMultiplier * 2;
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
            CrouchCooldown = 0f; // reset cooldown to enable forced crouched state
            CrouchSetState(true);
        }
    }

    private void CrouchSetState(bool state, float timer = 0f)
    {
        if (CrouchCooldown <= 0f)
        {
            CrouchRequest = state;
            oldCrouchRequest = CrouchRequest;
            CrouchCooldown = timer;
        }
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
        if (CrouchCooldown > 0f)
        {
            CrouchCooldown -= 1 * delta;
        }
        CrouchGetInput();
        CrouchProcessChange(delta);
    }

    private void CrouchProcessChange(float delta)
    {
        WatchTheHead();

        if (CrouchRequest)
        {
            CrouchProcessCrouch(delta);
            return;
        }
        CrouchProcessStand(delta);
    }

    private void CrouchProcessCrouch(float delta)
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

    private void CrouchProcessStand(float delta)
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