using System;
using Godot;

public class PlayerWalking : Node
{
    // Nodes
    [Export] private NodePath PlayerBody = "..";
    private CollisionShape Body;
    private Spatial Head;
    private MeshInstance Mesh;
    private Area HeadBonker;

    // Properties
    [Export] public float MoveSpeed = 3f;
    [Export] public float RunSpeedMultiplier = 2.5f;
    [Export] public float CrouchSpeedMultiplier = 0.55f;
    [Export] private bool CrouchModeIsToggle = false;

    private float GroundStrength = 10f; // TODO: changed by ground material
    private float AirStrength = 12f;
    
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

    // Singletons
    PlayerInput PlayerInput;
    

    public override void _Ready()
    {
        PlayerInput = GetNode<PlayerInput>("/root/PlayerInput");

        Body = GetNode<CollisionShape>(PlayerBody+"/Body");
        Head = GetNode<Spatial>(PlayerBody+"/Head");
        Mesh = GetNode<MeshInstance>(PlayerBody+"/Mesh");
        HeadBonker = GetNode<Area>(PlayerBody+"/HeadBonker");

        HeightBodyStanding = ((CapsuleShape)Body.Shape).Height;
        HeightHeadStanding = Head.Translation.y;

        CrouchSetState(false);
    }

    public override void _PhysicsProcess(float delta)
    {
        Player Player = GetNode<Player>(PlayerBody);

        SprintProcess();

        CrouchProcess(delta);

        SlideProcess(delta);

        // movement direction (only update if not sliding)
        var movement = PlayerInput.GetMovement();
        var transform = Player.GlobalTransform;
        Player.MoveDirection = (!SlideRequest && Player.Grounded) ? Vector3.Zero : Player.MoveDirection;
        if (!SlideRequest && Player.Grounded)
        {
            Player.MoveDirection += -transform.basis.z * movement.y;
            Player.MoveDirection +=  transform.basis.x * movement.x;
            Player.MoveDirection = Player.MoveDirection.Normalized();
        }

        var JumpRequest = !Player.Grounded;

        // movement strength
        // if (Player.Grounded)
        // {
            // get speed
            var speed = MoveSpeed;
                speed = (SprintRequest ? MoveSpeed * RunSpeedMultiplier : speed);
                speed = (CrouchRequest ? MoveSpeed * CrouchSpeedMultiplier : speed);
                speed = (SlideRequest  ? SlideSpeed : speed);

            // apply speed
            var targetVel = Player.MoveDirection * speed * (JumpRequest?1.5f:1);
            Player.Velocity = Player.Velocity.LinearInterpolate( targetVel, GroundStrength * delta);
        // }
        // else
        // {
        //     Player.Velocity += Player.MoveDirection * AirStrength * delta; // TODO: clamp value to prevent infinte speed
        // }
    }

    private void SprintProcess()
    {
        // TODO: change camera fov
        SprintRequest = PlayerInput.GetSprint();
    }

    private void SlideProcess(float delta)
    {
        // BUG: if sprint+crouch is held and slide is requested but is on cooldown. it will activate anyway as soon as cd is over. it shouldn't
        // BUG: if player stand still and sprint+crouch is held, it will not move
        // TODO: consider only allowed to slide if player has sprinted for a certain amount of time
        // TODO: sliding up slopes should decrease slide speed more and sliding down slopes should increase slide speed slightly (depending on ground material)
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
        CrouchWatchYourHead();

        if (CrouchRequest)
        {
            CrouchTryCrouch(delta);
            return;
        }
        CrouchTryStand(delta);
    }

    private void CrouchWatchYourHead()
    {
        var body = Body.Shape as CapsuleShape;
        if (body.Height < HeightBodyStanding && HeadBonker.GetOverlappingBodies().Count > 1)
        {
            CrouchSetState(true);
        }
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
