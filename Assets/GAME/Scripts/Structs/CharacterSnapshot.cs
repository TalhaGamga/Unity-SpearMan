using UnityEngine;

public struct CharacterSnapshot
{
    public readonly MovementSnapshot Movement;
    public readonly CombatSnapshot Combat;
    public readonly ReactionSnapshot Reaction;
    //public readonly RootMotionFrame RootMotion;

    //public readonly bool IsGrounded;
    //public readonly bool IsNearWall;
    //public readonly float Stamina;
    //public readonly Vector3 Position;
    //public readonly Quaternion Rotation;

    public CharacterSnapshot(
        MovementSnapshot movement,
        CombatSnapshot combat,
        ReactionSnapshot reaction
        //RootMotionFrame rootMotion,
        //bool isGrounded,
        //bool isNearWall,
        //float stamina,
        //Vector3 position,
        //Quaternion rotation
        )
    {
        Movement = movement;
        Combat = combat;
        Reaction = reaction;
        //RootMotion = rootMotion;
        //IsGrounded = isGrounded;
        //IsNearWall = isNearWall;
        //Stamina = stamina;
        //Position = position;
        //Rotation = rotation;
    }

    public bool IsJumping => Movement.State == MovementType.Jump;
    public bool IsAttacking => Combat.State == CombatType.Attack;
    public bool IsRecoiling => Reaction.State == ReactionType.Hit;
}
