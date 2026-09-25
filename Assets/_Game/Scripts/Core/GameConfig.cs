using UnityEngine;

namespace TapOrDrag
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Tap Or Drag/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Bird (units/s)")]
        public float gravity = 40f;
        public float flapVelocity = 12f;
        public float maxFallSpeed = 17f;
        public float hitRadius = 0.45f; // dog-pilot saucer is larger than the old bird

        [Header("Scroll speed (units/s)")]
        public float startSpeed = 3.6f;
        public float maxSpeed = 5.8f;
        public float speedPerClear = 0.05f;

        [Header("Pipes")]
        public float firstObstacleDistance = 7f;
        public float pipeSpacing = 5.4f;
        public float startGap = 4.6f;
        public float minGap = 3.4f;
        public float gapShrinkPerSpawn = 0.035f;
        public float maxGapDelta = 3.2f;
        public int movingPipesAfter = 15;
        [Range(0f, 1f)] public float movingPipeChance = 0.3f;
        public float movingPipeAmplitude = 0.9f;

        [Header("Dash gates")]
        public int firstGateAt = 3;
        [Range(0f, 1f)] public float gateChance = 0.3f;
        [Tooltip("Max gates/enemies in a row before a pipe is forced.")]
        public int maxGatesInRow = 2;
        public float gateSpacingBefore = 3.6f;
        public float gateSpacingAfter = 6.5f;
        [Tooltip("Swipe counts as correct when the next gate is at most this far ahead of the bird.")]
        public float dashWindow = 4.8f;
        public float dashBoostSpeed = 15f;
        public float dashMinDuration = 0.2f;
        public float dashMaxDuration = 0.8f;
        public float dashBoostDecay = 80f;
        public float dashCooldown = 0.12f;
        [Tooltip("true: swiping with no gate in range kills the bird. false: it only breaks the combo (MISS).")]
        public bool wrongSwipeIsFatal = false;

        [Header("Spiky enemy (dodge with taps, or dash through it to stomp)")]
        public int firstEnemyAt = 8;
        [Range(0f, 1f)] public float enemyChance = 0.2f;
        [Tooltip("Extra speed towards the bird on top of the scroll speed.")]
        public float enemyExtraSpeed = 0.8f;
        public float enemyBobAmplitude = 0.5f;
        [Tooltip("A dash locks on (and homes vertically) only if the enemy is within this height difference.")]
        public float enemyLockRange = 3f;
        public float enemySpacingBefore = 5.5f;
        public float enemySpacingAfter = 6f;
        public int enemyDodgePoints = 1;
        public int enemyStompPoints = 3;

        [Header("Red gates (fly through the gap, do NOT dash) and switch gates (flip mode when close)")]
        public int firstRedGateAt = 12;
        [Range(0f, 1f)] public float redGateChance = 0.12f;
        public int firstSwitchGateAt = 20;
        [Range(0f, 1f)] public float switchGateChance = 0.08f;
        public float redGateGap = 4f;
        public int redGatePoints = 2;
        public int switchGateBonus = 1;
        [Tooltip("Distance ahead of the bird at which a switch gate flips (it flickers for 1.3 units before).")]
        public float switchFlipDistance = 3.4f;
        [Tooltip("Show the NO SWIPE hint until the player has passed this many red gates in total.")]
        public int noSwipeHintUntil = 3;

        [Header("Gravity portals (flip gravity for a short section, then a second portal flips it back)")]
        public int firstPortalAt = 16;
        [Range(0f, 1f)] public float portalChance = 0.07f;
        [Tooltip("Obstacles in a flipped section before the return portal.")]
        public int flippedSectionLength = 5;
        public float portalSpacingBefore = 3.2f;
        public float portalSpacingAfter = 5f;
        [Tooltip("Real-time seconds of slow motion when gravity flips, to let the player re-orient.")]
        public float flipSlowMo = 0.2f;
        [Tooltip("Show the TAP PUSHES DOWN reminder for this many flips in total.")]
        public int flipHintUntil = 2;

        [Header("Switch walls (every swipe toggles GOLD/BLUE; walls of the active colour are solid)")]
        public int firstSwitchWallAt = 24;
        [Range(0f, 1f)] public float switchWallChance = 0.1f;
        public int switchWallPoints = 2;
        public float switchWallSpacingBefore = 4f;
        public float switchWallSpacingAfter = 5.5f;
        [Tooltip("Show the SWIPE TO SWITCH hint until the player has passed this many switch walls in total.")]
        public int switchHintUntil = 3;

        [Header("Biome hazards (snow: icicles, night: bats, neon: lasers, space: meteors)")]
        [Range(0f, 1f)] public float biomeHazardChance = 0.3f;
        public int hazardPoints = 1;
        [Tooltip("An icicle drops when it is this far ahead of the bird (it lands before the bird arrives).")]
        public float icicleDropDistance = 5.5f;
        public int batFlockSize = 3;
        public float batExtraSpeed = 1.4f;
        public float laserSweepPeriod = 3.6f;
        [Tooltip("Half the height the laser sweeps; the rest of the play area above/below stays safe.")]
        public float laserSweepAmplitude = 2.8f;
        public float laserSpacingBefore = 4.5f;
        public float laserSpacingAfter = 5f;
        public float meteorIntervalMin = 4f;
        public float meteorIntervalMax = 7f;
        [Tooltip("The warning follows the bird for this long, then locks its height.")]
        public float meteorTrackTime = 0.6f;
        [Tooltip("Total warning time before the meteor launches.")]
        public float meteorWarnTime = 1.1f;
        public float meteorSpeed = 11f;

        [Header("Patterns (short tight sequences that force quick input changes)")]
        public int firstPatternAt = 10;
        [Range(0f, 1f)] public float patternChance = 0.2f;
        public float patternSpacingScale = 0.85f;

        [Header("Coins")]
        [Range(0f, 1f)] public float coinInGapChance = 0.7f;
        [Range(0f, 1f)] public float coinArcChance = 0.4f;
        public float coinPickupRadius = 0.35f;
        [Tooltip("During Fever, coins within this distance fly to the bird.")]
        public float coinMagnetRange = 3f;

        [Header("Biomes (dusk > night > neon city > space, cycling)")]
        public int biomeEveryClears = 20;
        public float biomeFadeSeconds = 1.5f;

        [Header("Title screen")]
        [Tooltip("Show daily missions instead of the controls tutorial after this many runs.")]
        public int missionsAfterRuns = 3;

        [Header("Score")]
        public int pipePoints = 1;
        public int gatePoints = 2;
        [Tooltip("Multiplier goes up by 1 every N consecutive clears.")]
        public int comboStep = 2;
        public int maxMultiplier = 9;

        [Header("Fever (reach the combo, smash everything for a few seconds)")]
        public int feverAtMultiplier = 5;
        [Tooltip("After a Fever ends, this many clears must pass before the next one (misses and shield hits do not shorten it).")]
        public int feverRechargeClears = 20;
        [Tooltip("...and at least this many seconds must pass since the last Fever ended.")]
        public float feverRechargeSeconds = 3f;
        public float feverDuration = 5f;
        public float feverSpeedScale = 1.3f;
        public int feverPointMultiplier = 2;
        public float feverBirdScale = 1.35f;
        [Tooltip("Invulnerability right after Fever so the bird can get out of a pipe.")]
        public float feverGrace = 1f;

        [Header("Perfect dash / close call")]
        [Tooltip("Swipe when the target is at most this far ahead = PERFECT.")]
        public float perfectDashDistance = 1.2f;
        public int perfectBonus = 2;
        [Tooltip("Real-time seconds of slow motion on PERFECT.")]
        public float perfectSlowMo = 0.25f;
        [Tooltip("Passing a pipe with less vertical clearance than this = CLOSE.")]
        public float closeCallDistance = 0.2f;
        public int closeBonus = 1;

        [Header("Input")]
        [Tooltip("Swipe distance as a fraction of the shorter screen side.")]
        public float swipeThreshold = 0.06f;

        [Header("Skins")]
        [Tooltip("Unlock every skin regardless of best score (for playtests / reviews).")]
        public bool unlockAllSkins = false;

        [Header("Skills (one per unlockable bird)")]
        [Tooltip("BLUEJAY: clears needed to regrow the shield after it pops.")]
        public int shieldRegenClears = 15;
        [Tooltip("BLUEJAY: invulnerability after the shield absorbs a hit.")]
        public float shieldInvulnerable = 1.2f;
        [Tooltip("FLAMINGO: extra multiplier cap on top of maxMultiplier.")]
        public int royalComboBonusMax = 3;
        [Tooltip("NINJA: seconds before the dash can phase through a pipe again.")]
        public float shadowCooldown = 4f;
        [Tooltip("ROBO: physics scales. Keep flap^2/gravity near 1 to preserve jump height.")]
        public float antiGravGravity = 0.78f;
        public float antiGravFlap = 0.88f;
        public float antiGravFall = 0.8f;
        [Tooltip("GOLD: score multiplier.")]
        public int midasMultiplier = 2;

        [Header("Tutorial")]
        [Tooltip("Show the SWIPE hint until the player has cleared this many gates in total.")]
        public int swipeHintUntilGates = 5;
    }
}
