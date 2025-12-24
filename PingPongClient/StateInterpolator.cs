using System;
using System.Collections.Generic;
using PingPongClient.Core;

namespace PingPongClient.Interpolation
{
    
    public class StateInterpolator
    {
        private readonly object _lock = new object();
        private readonly LinkedList<GameState> _buffer = new LinkedList<GameState>();

        // Добавляем новое состояние (приходящее с сервера)
        public void PushState(GameState s)
        {
            lock (_lock)
            {
                // Keep buffer sorted by timestamp
                if (_buffer.Count == 0) { _buffer.AddLast(s); return; }
                if (s.timestamp > _buffer.Last.Value.timestamp)
                {
                    _buffer.AddLast(s);
                }
                else
                {
                    // insert in order
                    var node = _buffer.First;
                    while (node != null && node.Value.timestamp < s.timestamp) node = node.Next;
                    if (node == null) _buffer.AddLast(s);
                    else _buffer.AddBefore(node, s);
                }

                // keep only last few states
                while (_buffer.Count > 8) _buffer.RemoveFirst();
            }
        }

        // Интерполировать по serverTime; client should provide "renderTimeMs" (UTC ms)
        public GameState GetInterpolatedState(long renderTimeMs)
        {
            lock (_lock)
            {
                if (_buffer.Count == 0) return null;
                if (_buffer.Count == 1) return _buffer.First.Value;

                // Найти два состояния для интерполяции: s0 <= renderTimeMs <= s1
                GameState s0 = null, s1 = null;
                foreach (var s in _buffer)
                {
                    if (s.timestamp <= renderTimeMs) s0 = s;
                    if (s.timestamp >= renderTimeMs)
                    {
                        s1 = s;
                        break;
                    }
                }

                if (s0 == null) return _buffer.First.Value;
                if (s1 == null) return _buffer.Last.Value;
                if (s0 == s1) return s0;

                float dt = (float)(s1.timestamp - s0.timestamp);
                float t = dt <= 0 ? 0f : (float)(renderTimeMs - s0.timestamp) / dt;

                // линейная интерполяция мяча и ракеток
                GameState outState = new GameState
                {
                    timestamp = renderTimeMs,
                    ball = LerpVec(s0.ball, s1.ball, t),
                    ballVel = LerpVec(s0.ballVel, s1.ballVel, t),
                    paddles = new PaddleDto[2],
                    scores = s1.scores ?? s0.scores
                };

                for (int i = 0; i < 2; i++)
                {
                    var p0 = s0.paddles[i];
                    var p1 = s1.paddles[i];
                    outState.paddles[i] = new PaddleDto { x = Lerp(p0.x, p1.x, t) };
                }

                return outState;
            }
        }

        // Линейная интерполяция Vec3
        private Vec3 LerpVec(Vec3 a, Vec3 b, float t)
        {
            return new Vec3(
                Lerp(a.x, b.x, t),
                Lerp(a.y, b.y, t),
                Lerp(a.z, b.z, t)
            );
        }

        private float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
