using System;
namespace PingPongClient.Core
{
    public class Vec3
    {
        public Vec3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
    }

    public class InputMessage
    {
        public string type { get; set; } = "input";
        public int playerId { get; set; }
        public float paddleX { get; set; }
    }

    public class PaddleDto
    {
        public float x { get; set; }
    }

    public class GameState
    {
        public string type { get; set; } = "state";
        public long timestamp { get; set; }
        public Vec3 ball { get; set; }
        public Vec3 ballVel { get; set; }
        public PaddleDto[] paddles { get; set; }
        public int[] scores { get; set; }
    }
}
