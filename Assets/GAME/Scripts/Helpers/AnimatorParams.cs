using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The contract between the gameplay state machines and the animator: every
/// parameter that crosses the boundary, its name, its type, and who writes it.
///
/// It exists because that boundary used to be untyped. Parameters were
/// addressed by bare string from one file and compared against bare integers
/// in the controller, and the integers were enum declaration positions - so
/// "MoveState == 4" meant Jump only for as long as nobody touched the top of
/// MovementType. Nothing on either side could check the other. A rename
/// failed silently, an inserted enum member repointed thirty transitions
/// silently, and a parameter the graph had stopped reading looked exactly like
/// one it still did.
///
/// Naming is derived from the enum member wherever the parameter IS a member,
/// via nameof, so the two cannot drift without the compiler saying so. Where a
/// parameter is a predicate rather than a member - GroundedLocomotion is three
/// states, GroundJump is a Jump qualified by how it launched - it is a const
/// here with a comment saying exactly which states it covers, and the
/// validator checks the graph actually uses it.
///
/// <see cref="Contract"/> is what the editor validator walks. A parameter that
/// is not in it is either missing from the graph or rotting in it.
/// </summary>
public static class AnimatorParams
{
    public enum Kind
    {
        Float,
        Bool,
        Trigger
    }

    /// <summary>One parameter of the contract.</summary>
    public readonly struct Entry
    {
        public readonly string Name;
        public readonly int Hash;
        public readonly Kind Kind;

        /// <summary>
        /// False for a parameter the snapshot pipeline does not drive - a
        /// mechanic that exists in the graph and not in the game. Recorded
        /// rather than hidden, so the validator can keep reporting it.
        /// </summary>
        public readonly bool WrittenByMapper;

        public Entry(string name, Kind kind, bool writtenByMapper = true)
        {
            Name = name;
            Hash = Animator.StringToHash(name);
            Kind = kind;
            WrittenByMapper = writtenByMapper;
        }
    }

    #region Movement

    /// <summary>
    /// Standing, walking or running: MovementType Idle, Walk or Move.
    /// </summary>
    /// <remarks>
    /// One name for what the graph used to spell as the pair of conditions
    /// "MoveState &gt; 0 and MoveState &lt; 4". That spelling quietly asserted
    /// that Idle, Walk and Move are contiguous and that Jump is the first
    /// member after them - two facts about an enum's layout, asserted in an
    /// asset that cannot see the enum.
    /// </remarks>
    public const string GroundedLocomotion = "GroundedLocomotion";

    public const string Move = nameof(MovementType.Move);
    public const string Jump = nameof(MovementType.Jump);
    public const string Fall = nameof(MovementType.Fall);
    public const string Dash = nameof(MovementType.Dash);
    public const string ForcedFall = nameof(MovementType.ForcedFall);

    /// <summary>
    /// A jump that left the ground, including one launched on spent coyote
    /// time. Exclusive with <see cref="AirJump"/> by construction.
    /// </summary>
    public const string GroundJump = "GroundJump";

    /// <summary>
    /// A jump launched while already airborne, whatever its index in the
    /// chain.
    /// </summary>
    /// <remarks>
    /// Replaces "JumpCount == 2", which hard-coded MovementDesignSO's
    /// MaxJumpCount into four animator conditions: raising that field from two
    /// to three in the design asset - one inspector edit, no code change -
    /// published a count matching no threshold, and the character launched
    /// while the graph sat in the fall loop.
    /// </remarks>
    public const string AirJump = "AirJump";

    /// <summary>
    /// Blend speed. The one continuous quantity here, and the one thing the
    /// graph was already doing right - it feeds a blend tree, where a number
    /// is what a number should be.
    /// </summary>
    public const string MoveSpeed = "MoveSpeed";

    #endregion

    #region Combat

    public const string GroundedPrimaryAttack = nameof(CombatType.GroundedPrimaryAttack);
    public const string Stab = nameof(CombatType.Stab);

    public const string ComboOpener = nameof(AttackId.ComboOpener);
    public const string ComboFollow = nameof(AttackId.ComboFollow);
    public const string ComboFinisher = nameof(AttackId.ComboFinisher);
    public const string GroundDashStab = nameof(AttackId.GroundDashStab);
    public const string AirDashStab = nameof(AttackId.AirDashStab);

    #endregion

    #region Reaction

    // Triggers, and the only ones. A reaction is genuinely an event - it
    // happens once, at a moment, to a character that was doing something else.
    // Everything above is a level the animator samples, because a level
    // re-asserts itself on the next snapshot while a missed edge is missed
    // forever.
    public const string LightHit = "LightHit";
    public const string Pierced = nameof(ReactionType.Pierced);
    public const string Launch = "Launch";
    public const string Knockdown = "Knockdown";
    public const string GetUp = "GetUp";

    #endregion

    /// <summary>
    /// Gates AnyState -&gt; Dodge_Roll, and nothing in the project writes it.
    /// Declared so the validator keeps naming it rather than letting it sit
    /// there looking like a live mechanic.
    /// </summary>
    public const string Roll = "Roll";

    /// <summary>
    /// Every parameter the controllers are allowed to declare, and every
    /// parameter the game is allowed to write. The validator checks both
    /// directions against this list.
    /// </summary>
    public static readonly IReadOnlyList<Entry> Contract = new[]
    {
        new Entry(GroundedLocomotion, Kind.Bool),
        new Entry(Move, Kind.Bool),
        new Entry(Jump, Kind.Bool),
        new Entry(GroundJump, Kind.Bool),
        new Entry(AirJump, Kind.Bool),
        new Entry(Fall, Kind.Bool),
        new Entry(Dash, Kind.Bool),
        new Entry(ForcedFall, Kind.Bool),
        new Entry(MoveSpeed, Kind.Float),

        new Entry(GroundedPrimaryAttack, Kind.Bool),
        new Entry(Stab, Kind.Bool),
        new Entry(ComboOpener, Kind.Bool),
        new Entry(ComboFollow, Kind.Bool),
        new Entry(ComboFinisher, Kind.Bool),
        new Entry(GroundDashStab, Kind.Bool),
        new Entry(AirDashStab, Kind.Bool),

        new Entry(LightHit, Kind.Trigger),
        new Entry(Pierced, Kind.Trigger),
        new Entry(Launch, Kind.Trigger),
        new Entry(Knockdown, Kind.Trigger),
        new Entry(GetUp, Kind.Trigger),

        new Entry(Roll, Kind.Trigger, writtenByMapper: false)
    };

    /// <summary>
    /// The one group whose members are not mutually exclusive: Jump is the
    /// superset of GroundJump and AirJump.
    /// </summary>
    /// <remarks>
    /// A transition that lists Jump beside either of the other two is asking
    /// the graph to decide by transition priority what the mechanic already
    /// decided. The validator refuses it rather than leaving it to be found by
    /// a double jump that sometimes plays the wrong clip.
    /// </remarks>
    public static readonly IReadOnlyList<string> JumpFamily = new[]
    {
        Jump,
        GroundJump,
        AirJump
    };

    private static Dictionary<string, int> _hashes;

    /// <summary>Hash for a contract parameter, by name.</summary>
    public static int HashOf(string name)
    {
        if (_hashes == null)
        {
            _hashes = new Dictionary<string, int>(Contract.Count);

            foreach (Entry entry in Contract)
                _hashes[entry.Name] = entry.Hash;
        }

        return _hashes.TryGetValue(name, out int hash)
            ? hash
            : Animator.StringToHash(name);
    }
}
