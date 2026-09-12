using UnityEngine;

namespace Movement
{
    /// <summary>
    /// Single source of truth for traversal feel.
    ///
    /// Everything is authored in design units, never in physics units:
    ///   H = BodyHeight  -> "how big does this feel next to the character"
    ///   T = TileUnit    -> "what can the level gate with this"
    ///
    /// Gravity, launch velocity and dash speed are DERIVED from those, so the
    /// numbers a designer reads and the numbers the Rigidbody obeys can never
    /// drift apart. Tweak the top half, read the bottom half.
    /// </summary>
    [CreateAssetMenu(
        menuName = "ScriptableObjects/Design/Movement Design",
        fileName = "MovementDesign")]
    public class MovementDesignSO : ScriptableObject
    {
        #region Reference Units

        [Header("Reference Units")]
        [Tooltip("H - character height in world units. Measured from the " +
            "player capsule. Every vertical feel number is a multiple of this.")]
        [Min(0.01f)] public float BodyHeight = 1.8f;

        [Tooltip("T - level grid module in world units. Ledges and gaps are " +
            "authored as whole multiples of T so room geometry stays gateable.")]
        [Min(0.01f)] public float TileUnit = 1f;

        #endregion

        #region Jump Shape

        [Header("Jump Shape")]
        [Tooltip("Apex height of a full-commit ground jump, in body heights.")]
        [Min(0.1f)] public float JumpHeightInBodies = 1.8f;

        [Tooltip("Seconds from leaving the ground to the apex. This is the " +
            "single strongest floatiness dial - lower is snappier.")]
        [Min(0.05f)] public float JumpApexTime = 0.38f;

        [Tooltip("Fall gravity as a multiple of rise gravity. Above 1 the " +
            "character drops faster than it rose, which reads as weight.")]
        [Range(1f, 3f)] public float FallGravityMultiplier = 1.45f;

        [Tooltip("Upward velocity retained when the jump button is released " +
            "while still rising. Drives variable jump height.")]
        [Range(0.05f, 1f)] public float JumpCutMultiplier = 0.45f;

        [Tooltip("Downward speed cap. Only bites on long falls - a normal " +
            "jump arc should land just under it.")]
        [Min(1f)] public float TerminalFallSpeed = 26f;

        #endregion

        #region Apex Hang

        [Header("Apex Hang")]
        [Tooltip("Vertical speed band around the apex where gravity is " +
            "softened. Set to 0 to disable hang entirely.")]
        [Min(0f)] public float ApexVelocityWindow = 3f;

        [Tooltip("Gravity multiplier inside the apex window. Buys the player " +
            "aiming time at the top of the arc.")]
        [Range(0.1f, 1f)] public float ApexGravityMultiplier = 0.45f;

        #endregion

        #region Air Jumps

        [Header("Air Jumps")]
        [Tooltip("Total jumps before touching ground. 1 = single, 2 = double.")]
        [Min(1)] public int MaxJumpCount = 2;

        [Tooltip("Air jump strength as a ratio of the ground jump's height.")]
        [Range(0.3f, 1.2f)] public float AirJumpHeightRatio = 0.85f;

        #endregion

        #region Forgiveness

        [Header("Forgiveness")]
        [Tooltip("Grace period after walking off a ledge during which a " +
            "ground jump still counts.")]
        [Min(0f)] public float CoyoteTime = 0.1f;

        [Tooltip("How long an early jump press is remembered and replayed " +
            "once jumping becomes legal again.")]
        [Min(0f)] public float JumpBufferTime = 0.12f;

        #endregion

        #region Horizontal

        [Header("Horizontal")]
        [Tooltip("Authored ground run speed. Ground motion itself comes from " +
            "root motion - keep this matched to the run clip, or the character " +
            "will change speed the instant it leaves the ground.")]
        [Min(0.1f)] public float GroundSpeed = 8f;

        [Tooltip("Seconds to reach full ground speed from standstill.")]
        [Min(0f)] public float GroundAccelerationTime = 0.08f;

        [Tooltip("Seconds to come to a stop on the ground after releasing.")]
        [Min(0f)] public float GroundBrakeTime = 0.1f;

        [Tooltip("Downward speed held while grounded. Without it the body " +
            "rests at exactly zero vertical velocity, loses its contact to a " +
            "depenetration nudge and reports a phantom airborne frame.")]
        [Min(0f)] public float GroundStickSpeed = 2f;

        [Tooltip("Air speed as a ratio of ground speed. 1 = no takeoff pop.")]
        [Range(0.3f, 1.5f)] public float AirSpeedRatio = 1f;

        [Tooltip("Seconds to reach full air speed from standstill.")]
        [Min(0f)] public float AirAccelerationTime = 0.06f;

        [Tooltip("Seconds to stop horizontal air movement after releasing.")]
        [Min(0f)] public float AirBrakeTime = 0.1f;

        #endregion

        #region Dash

        [Header("Dash")]
        [Tooltip("Ground distance a dash should cover, in tiles.")]
        [Min(0.1f)] public float DashDistanceInTiles = 4f;

        [Tooltip("Intended dash length in seconds. The dash actually ends on " +
            "the DashEnded animation event, so keep the clip matched to this " +
            "or the real distance will not equal the authored one.")]
        [Min(0.02f)] public float DashDuration = 0.25f;

        [Tooltip("Vertical launch of a dash-jump, as a ratio of a normal jump.")]
        [Range(0.2f, 1f)] public float DashJumpHeightRatio = 0.66f;

        #endregion

        #region Landing

        [Header("Landing")]
        [Tooltip("Fall distance above which a landing counts as a hard land. " +
            "Purely a signal for the visual layer - it costs no control.")]
        [Min(0f)] public float HardLandDistanceInBodies = 2.5f;

        #endregion

        #region Derived - Physics

        /// <summary>Apex height of a ground jump, in world units.</summary>
        public float JumpHeight => JumpHeightInBodies * BodyHeight;

        /// <summary>Gravity applied while moving upward.</summary>
        public float RiseGravity =>
            (2f * JumpHeight) / (JumpApexTime * JumpApexTime);

        /// <summary>Gravity applied while moving downward.</summary>
        public float FallGravity => RiseGravity * FallGravityMultiplier;

        /// <summary>Take-off velocity of a ground jump.</summary>
        public float JumpVelocity => RiseGravity * JumpApexTime;

        /// <summary>Take-off velocity of an air jump.</summary>
        public float AirJumpVelocity =>
            Mathf.Sqrt(2f * RiseGravity * JumpHeight * AirJumpHeightRatio);

        /// <summary>Take-off velocity of a dash-jump.</summary>
        public float DashJumpVelocity => JumpVelocity * DashJumpHeightRatio;

        /// <summary>Horizontal speed while airborne.</summary>
        public float AirSpeed => GroundSpeed * AirSpeedRatio;

        /// <summary>Constant dash speed derived from distance and duration.</summary>
        public float DashSpeed =>
            (DashDistanceInTiles * TileUnit) / Mathf.Max(DashDuration, 1e-4f);

        /// <summary>Fall distance that flags a landing as hard.</summary>
        public float HardLandDistance => HardLandDistanceInBodies * BodyHeight;

        #endregion

        #region Derived - Traversal Envelope

        /// <summary>Seconds from apex back down to take-off height.</summary>
        public float FallTimeFromApex =>
            Mathf.Sqrt(2f * JumpHeight / FallGravity);

        /// <summary>Total airtime of a flat-ground jump.</summary>
        public float SingleJumpAirTime => JumpApexTime + FallTimeFromApex;

        /// <summary>Flat-ground horizontal reach of a single jump.</summary>
        public float SingleJumpDistance => AirSpeed * SingleJumpAirTime;

        /// <summary>Ceiling reached when the air jump is spent at the apex.</summary>
        public float DoubleJumpHeight =>
            JumpHeight * (1f + AirJumpHeightRatio);

        /// <summary>Total airtime when the air jump is spent at the apex.</summary>
        public float DoubleJumpAirTime =>
            JumpApexTime +
            (AirJumpVelocity / RiseGravity) +
            Mathf.Sqrt(2f * DoubleJumpHeight / FallGravity);

        /// <summary>Flat-ground horizontal reach of a double jump.</summary>
        public float DoubleJumpDistance => AirSpeed * DoubleJumpAirTime;

        /// <summary>Apex of the hop that follows a dash-jump.</summary>
        public float DashJumpApex =>
            (DashJumpVelocity * DashJumpVelocity) / (2f * RiseGravity);

        /// <summary>
        /// Flat-ground reach of a dash straight into a jump. The hop inherits
        /// dash speed rather than air speed, which is what makes this the
        /// widest gate in the kit.
        /// </summary>
        public float DashJumpDistance =>
            (DashDistanceInTiles * TileUnit) +
            DashSpeed * ((DashJumpVelocity / RiseGravity) +
                Mathf.Sqrt(2f * DashJumpApex / FallGravity));

        #endregion

        #region Derived - Level Gates

        /// <summary>Tallest ledge a single jump can mount, in whole tiles.</summary>
        public int SingleJumpLedgeTiles => ClearanceTiles(JumpHeight);

        /// <summary>Widest gap a single jump can clear, in whole tiles.</summary>
        public int SingleJumpGapTiles => ClearanceTiles(SingleJumpDistance);

        /// <summary>Tallest ledge a double jump can mount, in whole tiles.</summary>
        public int DoubleJumpLedgeTiles => ClearanceTiles(DoubleJumpHeight);

        /// <summary>Widest gap a double jump can clear, in whole tiles.</summary>
        public int DoubleJumpGapTiles => ClearanceTiles(DoubleJumpDistance);

        /// <summary>Widest gap a dash-jump can clear, in whole tiles.</summary>
        public int DashJumpGapTiles => ClearanceTiles(DashJumpDistance);

        /// <summary>
        /// Largest whole tile count that fits inside <paramref name="reach"/>
        /// with a safety margin, so a gate is never a pixel-perfect input.
        /// </summary>
        private int ClearanceTiles(float reach)
        {
            const float SafetyMargin = 0.1f;
            float usable = reach - SafetyMargin * BodyHeight;
            return Mathf.Max(
                0,
                Mathf.FloorToInt(usable / Mathf.Max(TileUnit, 1e-4f)));
        }

        #endregion

        #region Editor Readout

        // Written by OnValidate, read only by the inspector - which the
        // compiler cannot see, hence the suppression.
#pragma warning disable 0414
        [Header("Derived (read only)")]
        [TextArea(16, 28)]
        [SerializeField]
        private string _readout = "Edit any field above to refresh.";
#pragma warning restore 0414

#if UNITY_EDITOR
        private void OnValidate()
        {
            _readout = BuildReadout();
        }

        private string BuildReadout()
        {
            return
                "PHYSICS\n" +
                $"  rise gravity       {RiseGravity,8:0.00} u/s2  ({RiseGravity / 9.81f:0.0}x earth)\n" +
                $"  fall gravity       {FallGravity,8:0.00} u/s2  ({FallGravityMultiplier:0.00}x rise)\n" +
                $"  jump velocity      {JumpVelocity,8:0.00} u/s\n" +
                $"  air jump velocity  {AirJumpVelocity,8:0.00} u/s\n" +
                $"  dash speed         {DashSpeed,8:0.00} u/s\n" +
                $"  terminal fall      {TerminalFallSpeed,8:0.00} u/s\n" +
                "\nARC\n" +
                $"  jump apex          {JumpHeight,8:0.00} u = {JumpHeightInBodies:0.00} H = {JumpHeight / TileUnit:0.00} T\n" +
                $"  double jump apex   {DoubleJumpHeight,8:0.00} u = {DoubleJumpHeight / BodyHeight:0.00} H = {DoubleJumpHeight / TileUnit:0.00} T\n" +
                $"  rise/fall/total    {JumpApexTime:0.000}s / {FallTimeFromApex:0.000}s / {SingleJumpAirTime:0.000}s\n" +
                $"  single jump reach  {SingleJumpDistance,8:0.00} u = {SingleJumpDistance / TileUnit:0.00} T\n" +
                $"  double jump reach  {DoubleJumpDistance,8:0.00} u = {DoubleJumpDistance / TileUnit:0.00} T\n" +
                $"  dash jump reach    {DashJumpDistance,8:0.00} u = {DashJumpDistance / TileUnit:0.00} T\n" +
                "\nLEVEL GATES (whole tiles, safety margin applied)\n" +
                $"  single jump   ledge {SingleJumpLedgeTiles} T   gap {SingleJumpGapTiles} T\n" +
                $"  double jump   ledge {DoubleJumpLedgeTiles} T   gap {DoubleJumpGapTiles} T\n" +
                $"  dash jump                  gap {DashJumpGapTiles} T\n" +
                $"\n  a ledge of {SingleJumpLedgeTiles + 1} T gates content behind the double jump\n" +
                $"  a gap  of {DoubleJumpGapTiles + 1} T gates content behind the dash";
        }
#endif

        #endregion
    }
}
