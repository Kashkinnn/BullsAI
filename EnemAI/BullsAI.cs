using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace EnemAI
{
    class BullsAI : Form
    {
        private TrackBar trackBar_EHealth;
        private Label label_EHealth;
        private TrackBar trackBar_PDistance;
        private Label label_PDistance;

        private Label enemyIcon;
        private Label label_Aggro;

        private Image enemyImage;

        // --- Live demo fields ---
        private Panel field;
        private System.Windows.Forms.Timer demoTimer;
        private HashSet<Keys> keysDown = new HashSet<Keys>();
        private PointF playerPos, enemyFieldPos;
        private string currentState = "IDLE";
        private const float FieldW = 430, FieldH = 250, CircleSize = 24, PlayerSpeed = 3f, EnemySpeed = 2f, DefendPanicRadius = 60f;
        private double idlePatrolTimer = 0;
        private int idleDirIndex = 0;
        private readonly PointF[] idleDirs = { new PointF(1, 0), new PointF(0, -1), new PointF(-1, 0), new PointF(0, 1) };
        private Random rng = new Random();

        private bool demoRunning = false;

        public BullsAI()
        {
            initializeUI();
            CalculateEnemyBehavior(null, null);

            demoTimer = new System.Windows.Forms.Timer { Interval = 50 };
            demoTimer.Tick += DemoTimer_Tick;
        }

        void initializeUI()
        {
            this.Text = "Fuzzy Logic RPG Enemy AI";
            this.Size = new Size(540, 940);
            this.BackColor = Color.WhiteSmoke;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;
            this.Font = new Font("Segoe UI", 9);

            // ---- Enemy status card ----
            GroupBox statusGroup = new GroupBox
            {
                Text = "Enemy Status",
                Location = new Point(20, 15),
                Size = new Size(480, 220),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            this.Controls.Add(statusGroup);

            enemyIcon = new Label
            {
                Size = new Size(150, 150),
                Location = new Point(165, 25),
                BackColor = Color.Gray,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle
            };
            statusGroup.Controls.Add(enemyIcon);

            label_Aggro = new Label
            {
                Location = new Point(40, 180),
                Size = new Size(400, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.DimGray
            };
            statusGroup.Controls.Add(label_Aggro);

            // ---- Inputs card ----
            GroupBox inputGroup = new GroupBox
            {
                Text = "Manual Controls",
                Location = new Point(20, 245),
                Size = new Size(480, 175),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            this.Controls.Add(inputGroup);

            Label healthTitle = new Label { Text = "Current Health (%)", Location = new Point(15, 30), AutoSize = true};
            inputGroup.Controls.Add(healthTitle);

            label_EHealth = new Label { Text = "100%", Location = new Point(400, 30), AutoSize = true, ForeColor = Color.Green};
            inputGroup.Controls.Add(label_EHealth);

            trackBar_EHealth = new TrackBar { Minimum = 0, Maximum = 100, Value = 100, Location = new Point(15, 55), Width = 445 };
            trackBar_EHealth.Scroll += CalculateEnemyBehavior;
            inputGroup.Controls.Add(trackBar_EHealth);

            Label distanceTitle = new Label { Text = "Player Distance (m)", Location = new Point(15, 105), AutoSize = true};
            inputGroup.Controls.Add(distanceTitle);

            label_PDistance = new Label { Text = "50m", Location = new Point(400, 105), AutoSize = true, ForeColor = Color.Blue};
            inputGroup.Controls.Add(label_PDistance);

            trackBar_PDistance = new TrackBar { Minimum = 1, Maximum = 51, Value = 51, Location = new Point(15, 130), Width = 445, TickFrequency = 5 };
            trackBar_PDistance.Scroll += CalculateEnemyBehavior;
            inputGroup.Controls.Add(trackBar_PDistance);

            // ---- Live demo card ----
            GroupBox demoGroup = new GroupBox
            {
                Text = "Live Demo (WASD to move)",
                Location = new Point(20, 430),
                Size = new Size(480, 350),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            this.Controls.Add(demoGroup);

            field = new DoubleBufferedPanel
            {
                Location = new Point(15, 25),
                Size = new Size((int)FieldW, (int)FieldH),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            field.Paint += Field_Paint;
            demoGroup.Controls.Add(field);

            FlowLayoutPanel demoButtons = new FlowLayoutPanel
            {
                Location = new Point(15, 285),
                Size = new Size(445, 50),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            demoGroup.Controls.Add(demoButtons);

            Button startBtn = new Button { Text = "Start", Size = new Size(90, 32), BackColor = Color.Red};
            startBtn.Click += (s, e) => StartDemo();
            demoButtons.Controls.Add(startBtn);

            Button stopBtn = new Button { Text = "stop", Size = new Size(90, 32), BackColor = Color.Green};
            stopBtn.Click += (s, e) => StopDemo();
            demoButtons.Controls.Add(stopBtn);

            Button attackBtn = new Button { Text = "Attack (-10 HP)", Size = new Size(145, 32)};
            attackBtn.Click += (s, e) => { trackBar_EHealth.Value = Math.Max(0, trackBar_EHealth.Value - 10); CalculateEnemyBehavior(null, null); };
            demoButtons.Controls.Add(attackBtn);

            Button resetBtn = new Button { Text = "Reset", Size = new Size(90, 32) };
            resetBtn.Click += (s, e) => ResetDemo();
            demoButtons.Controls.Add(resetBtn);

            this.KeyDown += (s, e) => keysDown.Add(e.KeyCode);
            this.KeyUp += (s, e) => keysDown.Remove(e.KeyCode);
        }

        // dunno how the parameters work but it works
        private void CalculateEnemyBehavior(Object sender, EventArgs e)
        {
            double health = trackBar_EHealth.Value;
            double distance = trackBar_PDistance.Value;

            label_EHealth.Text = $"{health}%";
            label_PDistance.Text = $"{distance}m";

            if (distance >= 51)
            {
                label_PDistance.Text = ">50m";
            }

            if (health <= 0)
            {
                label_Aggro.Text = "Aggressiveness: 0.0%";
                enemyIcon.BackColor = Color.Black;
                enemyIcon.Text = "DEAD\n☠️";
                currentState = "DEAD";
                return;
            }

            double healthLow = TriangularMembership(health, 1, 1, 40);
            double healthMed = TriangularMembership(health, 25, 50, 80);
            double healthHigh = TriangularMembership(health, 60, 100, 100);

            double distanceLow = TriangularMembership(distance, 0, 0, 20);
            double distanceMed = TriangularMembership(distance, 10, 25, 40);
            double distanceHigh = TriangularMembership(distance, 30, 50, 50);

            // if health is high and distance to player is close then attack / full aggro
            double rule1 = Math.Min(healthHigh, distanceLow);

            // if health is low and distance to player is close then run / no aggro
            double rul2 = Math.Min(healthLow, distanceLow);

            // if health is mid and distance is mid then cautios / defend / mid aggro
            double rule3 = Math.Min(healthMed, distanceMed);

            // if health is low and distance is high then idle
            double rule4 = Math.Min(healthLow, distanceHigh);

            // if health is high and distance is high then idle
            double rule5 = Math.Min(healthHigh, distanceHigh);

            // if health is high and distance is mid then defnd /citios
            // fixes sudden idling in med distance
            double rule6 = Math.Min(healthHigh, distanceMed);

            // if health is low and distance is medium then run
            double rule7 = Math.Min(healthLow, distanceMed);

            double rule8 = Math.Min(healthMed, distanceLow);

            double rule9 = Math.Min(healthMed, distanceHigh);

            double sumNum = 0.0;
            double sumDen = 0.0;

            for (double y = 0.0; y <= 100.0; y += 1.0)
            {
                // aggro
                double outIdle = TriangularMembership(y, 0, 0, 20);
                double outFleeing = TriangularMembership(y, 10, 30, 50);
                double outDefending = TriangularMembership(y, 40, 60, 80);
                double outAttacking = TriangularMembership(y, 70, 100, 100);

                // Combine Fleeing (Rules 2 OR 7)
                double combinedFlee = Math.Max(rul2, rule7);
                double clipFleeing = Math.Min(combinedFlee, outFleeing);

                // Combine Idle (Rules 4 OR 5 OR 9)
                double combinedIdle = Math.Max(rule4, Math.Max(rule5, rule9));
                double clipIdle = Math.Min(combinedIdle, outIdle);

                // Combine Defending (Rules 3 OR 6)
                double combinedDefend = Math.Max(rule3, rule6);
                double clipDefending = Math.Min(combinedDefend, outDefending);

                // Combine Attacking (Rules 1 OR 8)
                double combinedAttack = Math.Max(rule1, rule8);
                double clipAttacking = Math.Min(combinedAttack, outAttacking);

                double aggY = Math.Max(clipIdle, Math.Max(clipFleeing, Math.Max(clipDefending, clipAttacking)));

                sumNum += y * aggY;
                sumDen += aggY;
            }

            // crispy output
            double aggro = (sumDen > 0.0) ? (sumNum / sumDen) : 0.0;
            label_Aggro.Text = $"Aggressiveness {aggro:F1}%";

            if (aggro < 15.0)
            {
                enemyIcon.BackColor = Color.LightGray;
                enemyIcon.Text = "IDLE\n💤";
                currentState = "IDLE";
            }
            else if (aggro >= 15.0 && aggro < 40.0)
            {
                enemyIcon.BackColor = Color.Blue;
                enemyIcon.Text = "FLEEING\n😨";
                currentState = "FLEEING";
            }
            else if (aggro >= 40.0 && aggro < 75.0)
            {
                enemyIcon.BackColor = Color.Orange;
                enemyIcon.Text = "DEFENDING\n🛡️";
                currentState = "DEFENDING";
            }
            else
            {
                enemyIcon.BackColor = Color.DarkRed;
                enemyIcon.Text = "ATTACKING\n⚔️";
                currentState = "ATTACKING";
            }
        }

        // Straight from infoSys teams, Thank you sir Aliac
        private double TriangularMembership(double x, double a, double b, double c)
        {
            if (a == b && b == c) return (x == a) ? 1.0 : 0.0;   // singleton/point set
            if (x < a || x > c) return 0.0;                       // strict now, so boundaries fall through
            if (a == b) return (x <= b) ? 1.0 : (c - x) / (c - b); // left shoulder (flat top from a..b)
            if (b == c) return (x >= b) ? 1.0 : (x - a) / (b - a); // right shoulder (flat top from b..c)
            if (x == b) return 1.0;
            if (x < b) return (x - a) / (b - a);
            return (c - x) / (c - b);
        }

        // --- Live demo logic ---

        private void ResetDemo()
        {
            enemyImage = Image.FromFile(@"C:\\Users\Kashkin\Pictures\Screenshots\Skeleton.png");
            playerPos = new PointF((float)rng.NextDouble() * FieldW, (float)rng.NextDouble() * FieldH);
            enemyFieldPos = new PointF((float)rng.NextDouble() * FieldW, (float)rng.NextDouble() * FieldH);
            trackBar_EHealth.Value = 100;
            idlePatrolTimer = 0;
            idleDirIndex = 0;
            CalculateEnemyBehavior(null, null);
        }

        private void DemoTimer_Tick(object sender, EventArgs e)
        {
            MovePlayer();

            double distPx = Distance(playerPos, enemyFieldPos);
            double diagonal = Math.Sqrt(FieldW * FieldW + FieldH * FieldH);
            trackBar_PDistance.Value = Math.Max(1, Math.Min(51, (int)(distPx / diagonal * 51)));

            CalculateEnemyBehavior(null, null);
            MoveEnemy(distPx);
            field.Invalidate();
        }

        private void MovePlayer()
        {
            float dx = 0, dy = 0;
            if (keysDown.Contains(Keys.W)) dy -= 1;
            if (keysDown.Contains(Keys.S)) dy += 1;
            if (keysDown.Contains(Keys.A)) dx -= 1;
            if (keysDown.Contains(Keys.D)) dx += 1;
            if (dx != 0 || dy != 0)
            {
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                playerPos.X = Clamp(playerPos.X + dx / len * PlayerSpeed, 0, FieldW - CircleSize);
                playerPos.Y = Clamp(playerPos.Y + dy / len * PlayerSpeed, 0, FieldH - CircleSize);
            }
        }

        private void MoveEnemy(double distPx)
        {
            if (currentState == "DEAD") return;

            PointF toPlayer = new PointF(playerPos.X - enemyFieldPos.X, playerPos.Y - enemyFieldPos.Y);
            float dist = (float)Math.Sqrt(toPlayer.X * toPlayer.X + toPlayer.Y * toPlayer.Y);
            PointF dir = dist > 0.01f ? new PointF(toPlayer.X / dist, toPlayer.Y / dist) : new PointF(0, 0);

            switch (currentState)
            {
                case "IDLE":
                    idlePatrolTimer += 0.05;
                    if (idlePatrolTimer > 2.0) { idlePatrolTimer = 0; idleDirIndex = (idleDirIndex + 1) % 4; }
                    enemyFieldPos.X = Clamp(enemyFieldPos.X + idleDirs[idleDirIndex].X * EnemySpeed, 0, FieldW - CircleSize);
                    enemyFieldPos.Y = Clamp(enemyFieldPos.Y + idleDirs[idleDirIndex].Y * EnemySpeed, 0, FieldH - CircleSize);
                    break;

                case "ATTACKING":
                    enemyFieldPos.X = Clamp(enemyFieldPos.X + dir.X * EnemySpeed, 0, FieldW - CircleSize);
                    enemyFieldPos.Y = Clamp(enemyFieldPos.Y + dir.Y * EnemySpeed, 0, FieldH - CircleSize);
                    break;

                case "FLEEING":
                    enemyFieldPos.X = Clamp(enemyFieldPos.X - dir.X * EnemySpeed, 0, FieldW - CircleSize);
                    enemyFieldPos.Y = Clamp(enemyFieldPos.Y - dir.Y * EnemySpeed, 0, FieldH - CircleSize);
                    break;

                case "DEFENDING":
                    // On-guard: holds position, only backs off if player gets too close
                    if (distPx < DefendPanicRadius)
                    {
                        enemyFieldPos.X = Clamp(enemyFieldPos.X - dir.X * EnemySpeed, 0, FieldW - CircleSize);
                        enemyFieldPos.Y = Clamp(enemyFieldPos.Y - dir.Y * EnemySpeed, 0, FieldH - CircleSize);
                    }
                    break;
            }
        }

        private float Clamp(float v, float min, float max) => Math.Max(min, Math.Min(max, v));
        private double Distance(PointF a, PointF b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

        private void Field_Paint(object sender, PaintEventArgs e)
        {
            if (!demoRunning) return;

            e.Graphics.FillEllipse(Brushes.RoyalBlue, playerPos.X, playerPos.Y, CircleSize, CircleSize);

            RectangleF enemyRect = new RectangleF(enemyFieldPos.X, enemyFieldPos.Y, CircleSize, CircleSize);

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(enemyRect);
                Region oldClip = e.Graphics.Clip;
                e.Graphics.SetClip(path);
                e.Graphics.DrawImage(enemyImage, enemyRect);
                e.Graphics.Clip = oldClip;
            }

            // Optional: colored ring around the sprite showing current state
            Color stateColor = currentState == "DEAD" ? Color.Black
                : currentState == "ATTACKING" ? Color.Red
                : currentState == "FLEEING" ? Color.Blue
                : currentState == "DEFENDING" ? Color.Orange
                : Color.LightGray;
            e.Graphics.DrawEllipse(new Pen(stateColor, 3), enemyRect);
        }

        private void StartDemo()
        {
            demoRunning = true;
            ResetDemo();
            demoTimer.Start();
        }

        private void StopDemo()
        {
            demoRunning = false;
            demoTimer.Stop();
            field.Invalidate();
        }
    }
}

public class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
    }
}