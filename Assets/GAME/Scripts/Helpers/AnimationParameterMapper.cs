using System.Collections.Generic;

/// <summary>
/// Projects a gameplay snapshot onto the animator's parameters.
///
/// One named level per mechanic, and nothing else. The animator used to be
/// told what the mover's state enum happened to be worth as an integer, and
/// the graph compared that integer against literals - so the question "does
/// this transition fire on a jump" could only be answered by counting members
/// in an enum declaration. Now the question the graph asks is the question the
/// mechanic answers: is this a jump, is it an air jump, is this the combo
/// finisher.
///
/// Levels rather than triggers, throughout. A level re-asserts itself on every
/// snapshot, so a frame the animator did not sample costs nothing; a trigger
/// is an edge, and a missed edge is missed forever. The graph still carries
/// four triggers, and they are all reactions - things that genuinely happen at
/// a moment, to a character that was doing something else.
///
/// Pure, static and Unity-free by design: this is the one place the two
/// vocabularies meet, so it is the one place worth being able to reason about
/// without a scene.
/// </summary>
public static class AnimationParameterMapper
{
    public static IEnumerable<AnimatorParamUpdate> AnimatorMapper(CharacterSnapshot snapshot)
    {
        MovementSnapshot movement = snapshot.Movement;
        CombatSnapshot combat = snapshot.Combat;

        // Idle, Walk and Move under one name. The graph used to spell this as
        // "MoveState > 0 and MoveState < 4", which asserted both that those
        // three are contiguous and that Jump follows them - two facts about an
        // enum's layout, asserted in an asset that cannot see the enum.
        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.GroundedLocomotion,
            movement.State == MovementType.Idle ||
            movement.State == MovementType.Walk ||
            movement.State == MovementType.Move);

        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.Move, movement.State == MovementType.Move);

        // Jump is the superset of the two below, which are exclusive by
        // construction. Never list Jump in the same condition set as either of
        // them - see AnimatorParams.JumpFamily; the validator refuses it.
        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.Jump, movement.State == MovementType.Jump);

        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.GroundJump,
            movement.State == MovementType.Jump && !movement.IsAirJump);

        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.AirJump,
            movement.State == MovementType.Jump && movement.IsAirJump);

        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.Fall, movement.State == MovementType.Fall);

        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.Dash, movement.State == MovementType.Dash);

        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.ForcedFall, movement.State == MovementType.ForcedFall);

        yield return AnimatorParamUpdate.Float(
            AnimatorParams.MoveSpeed, movement.Speed);

        // Which family of attack is running, and then which attack within it.
        // Both are gated on IsAttacking so a stale identity from the last
        // swing cannot open a transition after the swing is over.
        bool attacking = combat.IsAttacking;

        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.GroundedPrimaryAttack,
            attacking && combat.State == CombatType.GroundedPrimaryAttack);

        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.Stab,
            attacking && combat.State == CombatType.Stab);

        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.ComboOpener,
            attacking && combat.Attack == AttackId.ComboOpener);

        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.ComboFollow,
            attacking && combat.Attack == AttackId.ComboFollow);

        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.ComboFinisher,
            attacking && combat.Attack == AttackId.ComboFinisher);

        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.GroundDashStab,
            attacking && combat.Attack == AttackId.GroundDashStab);

        yield return AnimatorParamUpdate.Bool(
            AnimatorParams.AirDashStab,
            attacking && combat.Attack == AttackId.AirDashStab);
    }

    public static IEnumerable<AnimatorParamUpdate> ReactionAnimatorMapper(
        ReactionTransition transition)
    {
        string trigger = transition.To switch
        {
            ReactionType.LightHit => AnimatorParams.LightHit,
            ReactionType.Pierced => AnimatorParams.Pierced,
            ReactionType.Launch => AnimatorParams.Launch,
            ReactionType.Knockdown => AnimatorParams.Knockdown,
            ReactionType.Recovery
                when transition.From == ReactionType.Knockdown
                    => AnimatorParams.GetUp,
            _ => null
        };

        if (trigger == null)
            yield break;

        yield return AnimatorParamUpdate.Trigger(trigger);
    }
}
