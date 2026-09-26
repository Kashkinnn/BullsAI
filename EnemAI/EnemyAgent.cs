using System;
using System.Collections.Generic;
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

        private PointF facingDir = new PointF(1, 0);
        private double smoothedAggro = 0.0;

        public PointF FacingDirection => facingDir;
        public bool IsPlayerVisible { get; private set; }

        public void UpdateBehavior(double health, double trueDistance, PointF playerPos, List<RectangleF> obstacles, bool isManualOverride = false)
        {
            bool hasLoS = true;
            if (!isManualOverride)
            {
                float dx = playerPos.X - Position.X;
                float dy = playerPos.Y - Position.Y;
                float distPx = (float)Math.Sqrt(dx * dx + dy * dy);

                if (distPx < 90f)
                {
                    hasLoS = true;
                }
                else
                {
                    hasLoS = CheckLoS(Position, playerPos, obstacles) && InVisionCone(playerPos);
                }
            }

            IsPlayerVisible = hasLoS;
            double perceivedDistance = hasLoS ? trueDistance : 51.0;

            FuzzyResult result = FuzzyEngine.Evaluate(health, perceivedDistance);
            LastResult = result;

            if (result.State == "DEAD")
            {
                isCommittedToAttack = false;
                CurrentState = "DEAD";
                CurrentAggro = 0;
                smoothedAggro = 0;
                return;
            }

            if (isManualOverride)
            {
                smoothedAggro = result.Aggressiveness;
                float dx = playerPos.X - Position.X;
                float dy = playerPos.Y - Position.Y;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len > 0.01f) facingDir = new PointF(dx / len, dy / len);
            }
            else
            {
                smoothedAggro = (smoothedAggro * 0.85) + (result.Aggressiveness * 0.15);
            }

            CurrentAggro = smoothedAggro;

            string naturalState = "FLEEING";
            if (smoothedAggro >= 75.0) naturalState = "ATTACKING";
            else if (smoothedAggro >= 45.0) naturalState = "ALERT";
            else if (smoothedAggro >= 15.0) naturalState = "IDLE";

            if (naturalState != "ATTACKING")
                isCommittedToAttack = false;
            else if (smoothedAggro >= 75.0)
                isCommittedToAttack = true;

            CurrentState = isCommittedToAttack ? "ATTACKING" : naturalState;
        }

        public void ResetCommitment()
        {
            isCommittedToAttack = false;
            smoothedAggro = 0;
        }

        public void Move(PointF playerPos, float speed, float deltaTime, float fieldWidth, float fieldHeight, float circleSize, List<RectangleF> obstacles)
        {
            if (CurrentState == "DEAD") return;

            PointF delta = new PointF(playerPos.X - Position.X, playerPos.Y - Position.Y);
            float dist = (float)Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            PointF dir = dist > 0.01f ? new PointF(delta.X / dist, delta.Y / dist) : new PointF(0, 0);

            float targetDx = 0;
            float targetDy = 0;

            switch (CurrentState)
            {
                case "IDLE":
                    idleTimer += deltaTime;
                    if (idleTimer > 2.0) { idleTimer = 0; idleDirIdx = (idleDirIdx + 1) % 4; }
                    targetDx = Directions[idleDirIdx].X;
                    targetDy = Directions[idleDirIdx].Y;
                    break;
                case "ATTACKING":
                    targetDx = dir.X;
                    targetDy = dir.Y;
                    break;
                case "FLEEING":
                    targetDx = -dir.X;
                    targetDy = -dir.Y;
                    break;
                case "ALERT":
                    float standoff = 80f;
                    float radialWeight = (standoff - dist) / standoff;
                    radialWeight = Math.Max(-1f, Math.Min(1f, radialWeight));

                    targetDx = (-dir.X * radialWeight) + (-dir.Y);
                    targetDy = (-dir.Y * radialWeight) + (dir.X);
                    break;
            }

            PointF avoidance = GetAvoidanceVector(obstacles, circleSize);
            float finalDx = targetDx + avoidance.X * 2.5f;
            float finalDy = targetDy + avoidance.Y * 2.5f;

            float mLen = (float)Math.Sqrt(finalDx * finalDx + finalDy * finalDy);
            if (mLen > 0.01f)
            {
                finalDx = (finalDx / mLen) * speed;
                finalDy = (finalDy / mLen) * speed;
                facingDir = new PointF(finalDx / speed, finalDy / speed);
            }
            else
            {
                finalDx = 0;
                finalDy = 0;
            }

            float nx = Position.X + finalDx;
            float ny = Position.Y + finalDy;

            RectangleF rectX = new RectangleF(nx, Position.Y, circleSize, circleSize);
            RectangleF rectY = new RectangleF(Position.X, ny, circleSize, circleSize);
            bool hitX = false, hitY = false;

            foreach (var obs in obstacles)
            {
                if (obs.IntersectsWith(rectX)) hitX = true;
                if (obs.IntersectsWith(rectY)) hitY = true;
            }

            if (!hitX) Position.X = Clamp(nx, 0, fieldWidth - circleSize);
            if (!hitY) Position.Y = Clamp(ny, 0, fieldHeight - circleSize);
        }

        private PointF GetAvoidanceVector(List<RectangleF> obstacles, float circleSize)
        {
            float avoidX = 0;
            float avoidY = 0;
            float lookAhead = circleSize * 2f;

            PointF center = new PointF(Position.X + circleSize / 2, Position.Y + circleSize / 2);

            foreach (var obs in obstacles)
            {
                PointF obsCenter = new PointF(obs.X + obs.Width / 2, obs.Y + obs.Height / 2);
                float dx = center.X - obsCenter.X;
                float dy = center.Y - obsCenter.Y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);

                if (d < lookAhead && d > 0.01f)
                {
                    float force = 1.0f - (d / lookAhead);
                    avoidX += (dx / d) * force;
                    avoidY += (dy / d) * force;
                }
            }
            return new PointF(avoidX, avoidY);
        }

        private bool CheckLoS(PointF a, PointF b, List<RectangleF> obstacles)
        {
            foreach (var obs in obstacles)
            {
                if (LineIntersectsRect(a, b, obs)) return false;
            }
            return true;
        }

        private bool InVisionCone(PointF target)
        {
            float dx = target.X - Position.X;
            float dy = target.Y - Position.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            if (dist < 0.01f) return true;

            float dot = facingDir.X * (dx / dist) + facingDir.Y * (dy / dist);
            return dot >= 0.5f;
        }

        private bool LineIntersectsRect(PointF p1, PointF p2, RectangleF r)
        {
            return LineIntersectsLine(p1, p2, new PointF(r.X, r.Y), new PointF(r.X + r.Width, r.Y)) ||
                   LineIntersectsLine(p1, p2, new PointF(r.X + r.Width, r.Y), new PointF(r.X + r.Width, r.Y + r.Height)) ||
                   LineIntersectsLine(p1, p2, new PointF(r.X + r.Width, r.Y + r.Height), new PointF(r.X, r.Y + r.Height)) ||
                   LineIntersectsLine(p1, p2, new PointF(r.X, r.Y + r.Height), new PointF(r.X, r.Y)) ||
                   (r.Contains(p1) && r.Contains(p2));
        }

        private bool LineIntersectsLine(PointF l1p1, PointF l1p2, PointF l2p1, PointF l2p2)
        {
            float q = (l1p1.Y - l2p1.Y) * (l2p2.X - l2p1.X) - (l1p1.X - l2p1.X) * (l2p2.Y - l2p1.Y);
            float d = (l1p2.X - l1p1.X) * (l2p2.Y - l2p1.Y) - (l1p2.Y - l1p1.Y) * (l2p2.X - l2p1.X);

            if (d == 0) return false;
            float r = q / d;
            q = (l1p1.Y - l2p1.Y) * (l1p2.X - l1p1.X) - (l1p1.X - l2p1.X) * (l1p2.Y - l1p1.Y);
            float s = q / d;

            return r >= 0 && r <= 1 && s >= 0 && s <= 1;
        }

        private float Clamp(float val, float min, float max) => Math.Max(min, Math.Min(max, val));
    }
}