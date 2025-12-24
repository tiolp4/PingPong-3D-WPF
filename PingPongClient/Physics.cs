using System;
using System.Numerics;

namespace PingPongClient.Physics
{
    public static class Constants
    {
        public const float TableHalfWidth = 1.6f;
        public const float TableHalfLength = 2.4f;
        public const float PaddleHalfWidth = 0.25f;
        public const float PaddleZOffset = 2.2f;
        public const float BallRadius = 0.05f;
        public const float TableY = 0.05f;
    }

    public class PhysicsWorld
    {
        const float Gravity = -9.8f;
        const float MagnusStrength = 0.02f;
        const float BounceDamping = 0.9f;

        private Vector3 _ballPos;
        private Vector3 _ballVel;
        private Vector3 _ballSpin;

        private float[] _paddleX = new float[2];
        private float[] _paddleVelX = new float[2];

        public Vector3 BallPos => _ballPos;
        public Vector3 BallVel => _ballVel;
        public Vector3 BallSpin => _ballSpin;

        public PhysicsWorld()
        {
            ResetBall();
        }

        public void ResetBall()
        {
            _ballPos = new Vector3(0, 0.4f, 0);
            _ballVel = new Vector3(0.4f, 2.0f, 3.5f);
            _ballSpin = Vector3.Zero;
        }

        public void SetPaddle(int index, float x, float velX)
        {
            _paddleX[index] = x;
            _paddleVelX[index] = velX;
        }

        /// <summary>
        /// Обновление физики на шаг dt, возвращает true если мяч ударил по ракетке
        /// </summary>
        public bool StepWithHit(float dt)
        {
            bool hitPaddle = false;

            // Сохраняем предыдущую позицию для проверки столкновения
            Vector3 prevPos = _ballPos;

            // Gravity
            _ballVel.Y += Gravity * dt;

            // Magnus effect (spin)
            Vector3 magnus = Vector3.Cross(_ballSpin, _ballVel) * MagnusStrength;
            _ballVel += magnus * dt;

            // Интегрируем позицию
            _ballPos += _ballVel * dt;

            // Table collision (отскок от стола)
            if (_ballPos.Y <= Constants.TableY && _ballVel.Y < 0)
            {
                _ballPos.Y = Constants.TableY;
                _ballVel.Y = -_ballVel.Y * BounceDamping;
                _ballSpin *= 0.9f; // теряем часть спина при ударе
            }

            // Paddle collisions
            for (int i = 0; i < 2; i++)
            {
                float z = (i == 0) ? -Constants.PaddleZOffset : Constants.PaddleZOffset;

                // Проверяем пересечение с ракеткой
                if (Math.Abs(_ballPos.Z - z) < 0.12f &&
                    Math.Abs(_ballPos.X - _paddleX[i]) < Constants.PaddleHalfWidth &&
                    _ballPos.Y < 0.35f)
                {
                    // Мяч ударился о ракетку
                    hitPaddle = true;

                    // Отражение по Z
                    _ballVel.Z = -_ballVel.Z;

                    // Spin в зависимости от позиции удара и скорости ракетки
                    float offset = _ballPos.X - _paddleX[i];
                    _ballSpin.X += offset * 25f;
                    _ballSpin.Y += _paddleVelX[i] * 20f;

                    // Корректируем позицию мяча, чтобы он не застрял в ракетке
                    _ballPos.Z = z + Math.Sign(_ballVel.Z) * (Constants.BallRadius + 0.01f);
                }
            }

            // Wall collisions по X (отражение от боковых стенок)
            if (_ballPos.X - Constants.BallRadius <= -Constants.TableHalfWidth)
            {
                _ballPos.X = -Constants.TableHalfWidth + Constants.BallRadius;
                _ballVel.X = -_ballVel.X;
            }
            else if (_ballPos.X + Constants.BallRadius >= Constants.TableHalfWidth)
            {
                _ballPos.X = Constants.TableHalfWidth - Constants.BallRadius;
                _ballVel.X = -_ballVel.X;
            }

            return hitPaddle;
        }
    }
}
