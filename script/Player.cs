using Godot;
using System;

public class Player : KinematicBody
{
    [Export] private float Gravity = 42f;                       // forgot what this does.. sorry
    [Export] private float JumpStrength = 12f;                  // how strong upwards force when jumping
    [Export] private float MoveSpeedMaxGround = 3f;             // maximum allowed speed on ground
    [Export] private float MoveSpeedMaxAir = 7f;                // maximum allowed speed in air
    [Export] private float MoveSpeedRunMultiplier = 2.5f;       // how fast to move while sprinting
    [Export] private float MoveSpeedCrouchMultiplier = 0.55f;   // how fast to move while crouched
    [Export] private bool CrouchModeIsToggle = false;           // whether crouch should be toggled or held
    [Export] private float MaxSlopeThresholdAllowed = 0.7f;     // at which point a slope is considered a wall
    [Export] private float StepSize = 0.2f;                     // changing this risks breaking stairs atm

    private Vector3 MoveDirection;                              // the players movement direction as obtained from movement input
    private Vector3 Velocity;                                   // the players velocity as obtained from movement input
    private Vector3 MoveVelocity;                               // the players (previous frame) velocity
    private Vector3 GroundVector;                               // tracks ground normal and players y velocity snapped to ground

    // Crouch and Stand
    private bool CrouchRequest, oldCrouchRequest;               // whether or not player is requesting a crouched state
    private float CrouchTransitionSpeed = 6f;                   // how fast player transition to crouched or to standing
    private float CrouchCooldown = 0f;                          // the current cooldown time
    private float CrouchCooldownTimer = 0.5f;                   // default length of cooldown
    private float HeightBodyCrouching = 1.0f;                   // size of collider when crouched
    private float HeightHeadCrouching = -0.2f;                  // position of head when crouched
    private float HeightBodyStanding;                           // size of collider when standing (grabbed automatically from editor)
    private float HeightHeadStanding;                           // position of head when standing (grabbed automatically from edtior)

    // Ground
    private bool Grounded;                                      // in order to check for a "landing" we need to know if player was grounded the previous frame
    private float GroundStrength = 10f;                         // how quickly player build or lose momentum on ground | TODO: should be changed by ground material
    private float GroundCheckDistance = 5f;                     // how far down to look for ground (snap to ground)
    private float GroundSnap;                                   // the current CheckDistance. Since it needs to be disabled if jumping

    // Air
    private float AirStrength = 8f;                             // how quickly player build or lose moment in air

    // Falling and Landing
    private float FallSpeedForceStand = 17f;                    // if fall speed exceeds this velocity, player is forced to stand (if crouched)

    // Sliding
    private float SlideSpeed;
    private bool SlideRequest;
    private float SlideAllowedAtMinimumSprintTime = 0.3f;       // how many seconds player must sprint in order to be allowed to start a slide
    private float SlideMaxAllowedSpeed = 15f;                   // how fast player is allowed to slide (prevent infinite speed)

    // Sprinting
    private bool SprintRequest;                                 // whether or not the player wants to sprint
    private float SprintTime;                                   // timer for how long player has sprinted (while sprintrequest and movement input is not 0)

    // Nodes
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

        MovementProcess(delta);

        JumpProcess();

        // Update movement
        MoveVelocity = new Vector3(Velocity.x + GroundVector.x, GroundVector.y, Velocity.z + GroundVector.z);
        MoveVelocity = MoveAndSlide(MoveVelocity, Vector3.Up, false, 4, MaxSlopeThresholdAllowed, false);
    }

    private void MovementProcess(float delta)
    {
        MovementGetDirection();

        if (Grounded)
        {
            var speed = MoveSpeedMaxGround;
                speed = (SprintRequest ? MoveSpeedMaxGround * MoveSpeedRunMultiplier : speed);
                speed = (CrouchRequest ? MoveSpeedMaxGround * MoveSpeedCrouchMultiplier : speed);
                speed = (SlideRequest  ? SlideSpeed : speed);

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

    private void MovementGetDirection()
    {
        var movement = PlayerInput.GetMovement();
        MoveDirection = (!SlideRequest) ? Vector3.Zero : MoveDirection;
        if (!SlideRequest)
        {
            MoveDirection += -GlobalTransform.basis.z * movement.y;
            MoveDirection +=  GlobalTransform.basis.x * movement.x;
            MoveDirection = MoveDirection.Normalized();
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
                // TODO: change this to force remove standing state, maybe also start slide if landing on slope
                // BUG: since GroundSnap is now dynamically changed, this needs another calculation to compensate
                if (GroundVector.y < 0-FallSpeedForceStand)  CrouchSetState(false, CrouchCooldownTimer);

                // reduce movement velocity slightly
                // TODO: adjust additionally based on landing impact strength
                Velocity.x *= .5f;
                Velocity.z *= .5f;
            }
            // On floor
            Grounded = true;
            GroundVector = -GetFloorNormal() * GroundSnap;
            GroundSnap = GroundCheckDistance + Velocity.Length();  // adding velocity length to snap to prevent airborne situation on slopes in certain situations
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
                
                if (collisionPos.y > bottom-0.1 && collisionPos.y < stepsize)
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
    private void JumpProcess()
    {
        if (PlayerInput.GetJump() && Grounded)
        {
            Grounded = false;
            GroundSnap = 1f;
            GroundVector = Vector3.Up * JumpStrength;
        }

        if (!Grounded)
        {
            if  (IsOnCeiling())
            {
                // reset y velocity to prevent stickiness to ceiling
                GroundVector.y = 0f;
            }
        }
    }


    // TODO: change camera fov
    private void SprintProcess(float delta)
    {
        SprintRequest = PlayerInput.GetSprint() && !SlideRequest && !CrouchRequest;

        if (SprintRequest && MoveDirection != Vector3.Zero)
        {
            SprintTime += 1 * delta;
        }
        else
        {
            SprintTime = 0;
        }
    }

    private void SlideProcess(float delta)
    {
        if (SprintTime > SlideAllowedAtMinimumSprintTime)
        {
            if (CrouchRequest)
            {
                SlideStart();
            }
        }

        if (!CrouchRequest)
        {
            SlideStop();
        }

        if (SlideRequest)
        {
            var _slopeFriction = 2f;
            if (GetFloorNormal().y < 0.9f)
            {
                _slopeFriction = Velocity.Dot(GetFloorNormal()) *-1;
                _slopeFriction *= 0.5f;
            }

            SlideSpeed -= (GroundStrength * _slopeFriction) * delta;
            SlideSpeed = Mathf.Clamp( SlideSpeed, -SlideMaxAllowedSpeed, SlideMaxAllowedSpeed );

            if (SlideSpeed <= 0f)
                SlideStop();
        }
    }

    private void SlideStop()
    {
        SlideRequest = false;
    }

    private void SlideStart()
    {
        SlideSpeed = MoveSpeedMaxGround * MoveSpeedRunMultiplier * 2;
        SlideRequest = true;
    }

    private void WatchTheHead()
    {
        var body = Body.Shape as CapsuleShape;

        if (body.Height < HeightBodyStanding && HeadBonker.GetOverlappingBodies().Count > 1)
        {
            CrouchCooldown = 0f; // reset cooldown on crouch to make a forced crouched state
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
        var mesh = Mesh.Mesh as CapsuleMesh;
        mesh.MidHeight = body.Height;
        var mt = Mesh.Transform;
        mt.origin.y = 0 - (1-(body.Height / HeightBodyStanding));
        Mesh.Transform = mt;
    }

    private void CrouchUpdateCollider(CapsuleShape body)
    {
        var bt = Body.Transform;
        bt.origin.y = 0 - (1-(body.Height / HeightBodyStanding));
        Body.Transform = bt;
    }
}