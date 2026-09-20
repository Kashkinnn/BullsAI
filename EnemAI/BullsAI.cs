using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EnemAI
{
    public class BullsAI : Form
    {
        private TrackBar trackBar_EHealth, trackBar_PDistance;
        private Label label_EHealth, label_PDistance, enemyIcon, label_Aggro;
        private Panel field;
        private System.Windows.Forms.Timer demoTimer;
        private HashSet<Keys> keysDown = new HashSet<Keys>();
        private Image enemyImage;

        private EnemyAgent enemy = new EnemyAgent();
        private PointF playerPos;
        private bool demoRunning = false;
        private const float FieldW = 430, FieldH = 250, CircleSize = 24, PlayerSpeed = 3f, EnemySpeed = 2f;
        private Random rng = new Random();

        public BullsAI()
        {
            LoadEnemyResource();
            InitializeUI();
            UpdateAiState();
            demoTimer = new System.Windows.Forms.Timer { Interval = 50 };
            demoTimer.Tick += DemoTimer_Tick;
        }

        private void LoadEnemyResource()
        { // thanks oop2 learned why i must use fallbacks esp in rendering images
            try
            {
                enemyImage = Properties.Resources.Skeleton;
            }
            catch
            {
                enemyImage = null;
            }
        }

        private void InitializeUI()
        {
            this.Text = "BullsAi";
            this.Size = new Size(540, 840);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;

            // Enemy Status Card
            GroupBox statusGroup = new GroupBox { Text = "Enemy Status", Location = new Point(20, 15), Size = new Size(480, 220) };
            enemyIcon = new Label { Size = new Size(150, 120), Location = new Point(165, 25), BackColor = Color.Gray, ForeColor = Color.White, Font = new Font("Impact", 17), TextAlign = ContentAlignment.MiddleCenter, BorderStyle = BorderStyle.FixedSingle };
            label_Aggro = new Label { Location = new Point(40, 160), Size = new Size(400, 30), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Impact", 15), ForeColor = Color.DimGray };
            statusGroup.Controls.Add(enemyIcon);
            statusGroup.Controls.Add(label_Aggro);
            this.Controls.Add(statusGroup);

            // Sliders
            GroupBox inputGroup = new GroupBox { Text = "Manual Controls", Location = new Point(20, 245), Size = new Size(480, 175)};
            label_EHealth = new Label { Text = "100%", Location = new Point(400, 30), AutoSize = true, ForeColor = Color.Green };
            trackBar_EHealth = new TrackBar { Minimum = 0, Maximum = 100, Value = 100, Location = new Point(15, 55), Width = 445 };
            trackBar_EHealth.Scroll += (s, e) => UpdateAiState();

            label_PDistance = new Label { Text = "50m", Location = new Point(400, 105), AutoSize = true, ForeColor = Color.Blue };
            trackBar_PDistance = new TrackBar { Minimum = 1, Maximum = 51, Value = 51, Location = new Point(15, 130), Width = 445, TickFrequency = 5 };
            trackBar_PDistance.Scroll += (s, e) => UpdateAiState();

            inputGroup.Controls.AddRange(new Control[] { new Label { Text = "Current Health (%)", Location = new Point(15, 30), AutoSize = true }, label_EHealth, trackBar_EHealth, new Label { Text = "Player Distance (m)", Location = new Point(15, 105), AutoSize = true }, label_PDistance, trackBar_PDistance });
            this.Controls.Add(inputGroup);

            // Demo Field
            GroupBox demoGroup = new GroupBox { Text = "Live Demo (WASD to move)", Location = new Point(20, 430), Size = new Size(480, 350) };
            field = new DoubleBufferedPanel { Location = new Point(15, 25), Size = new Size((int)FieldW, (int)FieldH), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            field.Paint += Field_Paint;
            demoGroup.Controls.Add(field);

            FlowLayoutPanel buttons = new FlowLayoutPanel { Location = new Point(15, 285), Size = new Size(445, 50) };
            Button btnStart = new Button { Text = "Set", Size = new Size(90, 32), BackColor = Color.LightGreen, FlatStyle = FlatStyle.Flat };
            btnStart.Click += (s, e) => { demoRunning = true; ResetPositions(); demoTimer.Start(); };
            Button btnStop = new Button { Text = "Stop", Size = new Size(90, 32), BackColor = Color.LightCoral, FlatStyle = FlatStyle.Flat };
            btnStop.Click += (s, e) => { demoRunning = false; demoTimer.Stop(); field.Invalidate(); };
            Button btnHit = new Button { Text = "Attack (-10 HP)", Size = new Size(130, 32) };
            btnHit.Click += (s, e) => { trackBar_EHealth.Value = Math.Max(0, trackBar_EHealth.Value - 10); UpdateAiState(); };

            buttons.Controls.AddRange(new Control[] { btnStart, btnStop, btnHit });
            demoGroup.Controls.Add(buttons);
            this.Controls.Add(demoGroup);

            this.KeyDown += (s, e) => keysDown.Add(e.KeyCode);
            this.KeyUp += (s, e) => keysDown.Remove(e.KeyCode);
        }

        private void UpdateAiState()
        {
            label_EHealth.Text = $"{trackBar_EHealth.Value}%";
            label_PDistance.Text = trackBar_PDistance.Value >= 51 ? ">50m" : $"{trackBar_PDistance.Value}m";

            enemy.UpdateBehavior(trackBar_EHealth.Value, trackBar_PDistance.Value);
            label_Aggro.Text = $"Aggressiveness {enemy.CurrentAggro:F1}%";

            switch (enemy.CurrentState)
            {
                case "DEAD":
                    enemyIcon.BackColor = Color.Black; 
                    enemyIcon.Text = "DEAD\n☠️"; 
                    break;

                case "IDLE": 
                    enemyIcon.BackColor = Color.LightGray; 
                    enemyIcon.Text = "IDLE\n💤"; 
                    break;

                case "FLEEING": 
                    enemyIcon.BackColor = Color.RoyalBlue; 
                    enemyIcon.Text = "FLEEING\n😨"; 
                    break;

                case "ALERT": 
                    enemyIcon.BackColor = Color.DarkOrange; 
                    enemyIcon.Text = "ALERT\n👁️"; 
                    break;

                case "ATTACKING": 
                    enemyIcon.BackColor = Color.DarkRed; 
                    enemyIcon.Text = "ATTACKING\n⚔️"; 
                    break;
            }
        }

        private void ResetPositions()
        {
            playerPos = new PointF((float)rng.NextDouble() * (FieldW - CircleSize), (float)rng.NextDouble() * (FieldH - CircleSize));
            enemy.Position = new PointF((float)rng.NextDouble() * (FieldW - CircleSize), (float)rng.NextDouble() * (FieldH - CircleSize));
            trackBar_EHealth.Value = 100;
            UpdateAiState();
        }

        private void DemoTimer_Tick(object sender, EventArgs e)
        {
            // WASD Player Movement
            float dx = 0, dy = 0;
            if (keysDown.Contains(Keys.W)) dy -= 1;
            if (keysDown.Contains(Keys.S)) dy += 1;
            if (keysDown.Contains(Keys.A)) dx -= 1;
            if (keysDown.Contains(Keys.D)) dx += 1;

            if (dx != 0 || dy != 0)
            {
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                playerPos.X = Math.Max(0, Math.Min(FieldW - CircleSize, playerPos.X + dx / len * PlayerSpeed));
                playerPos.Y = Math.Max(0, Math.Min(FieldH - CircleSize, playerPos.Y + dy / len * PlayerSpeed));
            }

            // Sync Distance Slider based on visual positions
            double distPx = Math.Sqrt(Math.Pow(playerPos.X - enemy.Position.X, 2) + Math.Pow(playerPos.Y - enemy.Position.Y, 2));
            double diagonal = Math.Sqrt(FieldW * FieldW + FieldH * FieldH);
            trackBar_PDistance.Value = Math.Max(1, Math.Min(51, (int)(distPx / diagonal * 51)));

            UpdateAiState();
            enemy.Move(playerPos, EnemySpeed, FieldW, FieldH, CircleSize);
            field.Invalidate();
        }

        private void Field_Paint(object sender, PaintEventArgs e)
        {
            if (!demoRunning) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // render player
            e.Graphics.FillEllipse(Brushes.RoyalBlue, playerPos.X, playerPos.Y, CircleSize, CircleSize);

            // border box
            RectangleF enemyRect = new RectangleF(enemy.Position.X, enemy.Position.Y, CircleSize, CircleSize);

            // enemy
            if (enemyImage != null)
            {
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(enemyRect);
                    Region oldClip = e.Graphics.Clip;
                    e.Graphics.SetClip(path);
                    e.Graphics.DrawImage(enemyImage, enemyRect);
                    e.Graphics.Clip = oldClip;
                }
            }
            else
            {
                e.Graphics.FillEllipse(Brushes.DarkGray, enemyRect);
                e.Graphics.DrawString("E", this.Font, Brushes.Black, enemy.Position.X + 8, enemy.Position.Y + 6);
            }
            
            // enemy ring color
            Color stateColor = enemy.CurrentState == "DEAD" ? Color.Black
                : enemy.CurrentState == "ATTACKING" ? Color.Red
                : enemy.CurrentState == "FLEEING" ? Color.RoyalBlue
                : enemy.CurrentState == "ALERT" ? Color.Orange
                : Color.LightGray;

            using (Pen statePen = new Pen(stateColor, 3))
            {
                e.Graphics.DrawEllipse(statePen, enemyRect);
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
}