using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// Full-height column of ON/OFF blocks. Solid (deadly) while its colour matches the global switch state,
    /// a passable dashed outline otherwise. Every swipe toggles the global state (see GameManager.Switch.cs).
    /// </summary>
    public class SwitchWall : Obstacle
    {
        const float BlockHalfWidth = 0.5f;

        Art art;
        SpriteRenderer blocks;
        bool solid;
        float pop;

        public int ColorIndex { get; private set; } // 0 gold, 1 blue
        public bool Solid => solid && !Broken;
        public bool Broken { get; private set; }
        public override float HalfWidth => BlockHalfWidth;

        public void Build(Art sourceArt)
        {
            art = sourceArt;
            blocks = Part("Blocks", art.SwitchBlock[0, 0], -9);
            blocks.drawMode = SpriteDrawMode.Tiled;
        }

        public void Setup(float x, int colorIndex, bool startSolid)
        {
            X = x;
            Cleared = false;
            Broken = false;
            ColorIndex = colorIndex;
            blocks.enabled = true;
            float top = World.Top + 0.5f, bottom = World.GroundTop - 0.5f;
            blocks.size = new Vector2(BlockHalfWidth * 2f, top - bottom);
            blocks.transform.localPosition = new Vector3(0f, (top + bottom) * 0.5f, 0f);
            solid = !startSolid;
            SetSolid(startSolid);
            pop = 0f;
            Place();
        }

        public void SetSolid(bool value)
        {
            if (value == solid) return;
            solid = value;
            pop = 1f;
            blocks.sprite = art.SwitchBlock[ColorIndex, value ? 0 : 1];
        }

        public override void Tick(float dt, float speed)
        {
            base.Tick(dt, speed);
            pop = Mathf.MoveTowards(pop, 0f, dt * 6f);
            blocks.transform.localScale = new Vector3(1f + 0.18f * pop, 1f, 1f);
        }

        public void Break()
        {
            Broken = true;
            Cleared = true;
            blocks.enabled = false;
        }

        public override bool Hits(Vector2 c, float r) =>
            Solid && CircleRect(c, r, X - BlockHalfWidth, X + BlockHalfWidth, World.GroundTop - 1f, World.Top + 1f);
    }
}
