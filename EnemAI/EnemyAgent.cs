using System;
using System.Drawing;

namespace EnemAI
{
    public class EnemyAgent
    {
        public PointF Position;
        public string CurrentState { get; private set; } = "IDLE";
        public double CurrentAggro { get; private set; } = 0.0;
        public FuzzyResult LastResult { get; private set; }

        private bool isCommittedToAttack = false;

        private double idleTimer = 0;
        private int idleDirIdx = 0;
        private static readonly PointF[] Directions = { new PointF(1, 0), new PointF(0, -1), new PointF(-1, 0), new PointF(0, 1) };

        public void UpdateBehavior(double health, double distance)
        {
            FuzzyResult result = FuzzyEngine.Evaluate(health, distance);
            LastResult = result;
            CurrentAggro = result.Aggressiveness;

            if (result.State == "DEAD")
            {
                isCommittedToAttack = false;
                CurrentState = "DEAD";
                return;
            }

            if (result.HealthLow > 0.0) isCommittedToAttack = false;
            else if (result.Aggressiveness >= 75.0) isCommittedToAttack = true;

            CurrentState = isCommittedToAttack ? "ATTACKING" : result.State;
        }

        public void ResetCommitment()
        {
            isCommittedToAttack = false;
        }

        public void Move(PointF playerPos, float speed, float fieldWidth, float fieldHeight, float circleSize)
        {
            if (CurrentState == "DEAD") return;

            PointF delta = new PointF(playerPos.X - Position.X, playerPos.Y - Position.Y);
            float dist = (float)Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            PointF dir = dist > 0.01f ? new PointF(delta.X / dist, delta.Y / dist) : new PointF(0, 0);

            switch (CurrentState)
            {
                case "IDLE":
                    idleTimer += 0.05;
                    if (idleTimer > 2.0) { idleTimer = 0; idleDirIdx = (idleDirIdx + 1) % 4; }
                    Position.X = Clamp(Position.X + Directions[idleDirIdx].X * speed, 0, fieldWidth - circleSize);
                    Position.Y = Clamp(Position.Y + Directions[idleDirIdx].Y * speed, 0, fieldHeight - circleSize);
                    break;

                case "ATTACKING":
                    Position.X = Clamp(Position.X + dir.X * speed, 0, fieldWidth - circleSize);
                    Position.Y = Clamp(Position.Y + dir.Y * speed, 0, fieldHeight - circleSize);
                    break;

                case "FLEEING":
                    Position.X = Clamp(Position.X - dir.X * speed, 0, fieldWidth - circleSize);
                    Position.Y = Clamp(Position.Y - dir.Y * speed, 0, fieldHeight - circleSize);
                    break;

                case "ALERT":
                    if (dist < 60f)
                    {
                        Position.X = Clamp(Position.X - dir.X * speed, 0, fieldWidth - circleSize);
                        Position.Y = Clamp(Position.Y - dir.Y * speed, 0, fieldHeight - circleSize);
                    }
                    break;
            }
        }

        private float Clamp(float val, float min, float max) => Math.Max(min, Math.Min(max, val));
    }
}