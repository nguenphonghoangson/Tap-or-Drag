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
        public float hitRadius = 0.4f;

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

        [Header("Score")]
        public int pipePoints = 1;
        public int gatePoints = 2;
        [Tooltip("Multiplier goes up by 1 every N consecutive clears.")]
        public int comboStep = 2;
        public int maxMultiplier = 9;

        [Header("Fever (reach the combo, smash everything for a few seconds)")]
        public int feverAtMultiplier = 5;
        [Tooltip("After a Fever ends, this many more consecutive clears are needed before the next one.")]
        public int feverRechargeClears = 10;
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
