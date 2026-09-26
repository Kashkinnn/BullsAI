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

        private MembershipGraphPanel healthGraph;
        private MembershipGraphPanel distanceGraph;
        private OutputCurvePanel outputGraph;
        private DataGridView ruleGrid;
        private ControlSurfacePanel surfacePanel;

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
        {
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
            this.Size = new Size(1000, 1000);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;

            // ---- Status ----
            GroupBox statusGroup = new GroupBox { Text = "Enemy Status", Location = new Point(20, 15), Size = new Size(460, 220) };
            enemyIcon = new Label { Size = new Size(150, 120), Location = new Point(155, 25), BackColor = Color.Gray, ForeColor = Color.White, Font = new Font("Impact", 17), TextAlign = ContentAlignment.MiddleCenter, BorderStyle = BorderStyle.FixedSingle };
            label_Aggro = new Label { Location = new Point(30, 160), Size = new Size(400, 30), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Impact", 15), ForeColor = Color.DimGray };
            statusGroup.Controls.Add(enemyIcon);
            statusGroup.Controls.Add(label_Aggro);
            this.Controls.Add(statusGroup);

            // ---- Manual Controls + Edge Case Presets ----
            GroupBox inputGroup = new GroupBox { Text = "Manual Controls", Location = new Point(20, 245), Size = new Size(460, 320) };
            label_EHealth = new Label { Text = "100%", Location = new Point(400, 30), AutoSize = true, ForeColor = Color.Green };
            trackBar_EHealth = new TrackBar { Minimum = 0, Maximum = 100, Value = 100, Location = new Point(15, 55), Width = 430 };
            trackBar_EHealth.Scroll += (s, e) => UpdateAiState();

            label_PDistance = new Label { Text = "50m", Location = new Point(400, 110), AutoSize = true, ForeColor = Color.Blue };
            trackBar_PDistance = new TrackBar { Minimum = 1, Maximum = 51, Value = 51, Location = new Point(15, 135), Width = 430, TickFrequency = 5 };
            trackBar_PDistance.Scroll += (s, e) => UpdateAiState();

            inputGroup.Controls.AddRange(new Control[] {
        new Label { Text = "Current Health (%)", Location = new Point(15, 30), AutoSize = true },
        label_EHealth, trackBar_EHealth,
        new Label { Text = "Player Distance (m)", Location = new Point(15, 110), AutoSize = true },
        label_PDistance, trackBar_PDistance
    });

            Label edgeLabel = new Label { Text = "Edge Case Tests:", Location = new Point(15, 195), AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
            inputGroup.Controls.Add(edgeLabel);

            FlowLayoutPanel edgeButtons = new FlowLayoutPanel { Location = new Point(15, 220), Size = new Size(430, 90), FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            AddEdgeButton(edgeButtons, "HP=0", 0, 51);
            AddEdgeButton(edgeButtons, "HP=100", 100, 51);
            AddEdgeButton(edgeButtons, "Dist=Min", 100, 1);
            AddEdgeButton(edgeButtons, "Dist=Max", 100, 51);
            AddEdgeButton(edgeButtons, "HP Bnd 40", 40, 25);
            AddEdgeButton(edgeButtons, "Dist Bnd 20", 100, 20);
            AddEdgeButton(edgeButtons, "Dist Bnd 40", 100, 40);
            AddEdgeButton(edgeButtons, "Mid/Mid", 50, 25);
            inputGroup.Controls.Add(edgeButtons);
            this.Controls.Add(inputGroup);

            // ---- Live Demo (under Manual Controls) ----
            GroupBox demoGroup = new GroupBox { Text = "Live Demo (WASD to move)", Location = new Point(20, 585), Size = new Size(460, 350) };
            field = new DoubleBufferedPanel { Location = new Point(15, 25), Size = new Size((int)FieldW, (int)FieldH), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            field.Paint += Field_Paint;
            demoGroup.Controls.Add(field);

            FlowLayoutPanel buttons = new FlowLayoutPanel { Location = new Point(15, 285), Size = new Size(430, 50) };
            Button btnStart = new Button { Text = "Set", Size = new Size(90, 32), BackColor = Color.LightGreen, FlatStyle = FlatStyle.Flat };
            btnStart.Click += (s, e) => { demoRunning = true; ResetPositions(); demoTimer.Start(); };
            Button btnStop = new Button { Text = "Stop", Size = new Size(90, 32), BackColor = Color.LightCoral, FlatStyle = FlatStyle.Flat };
            btnStop.Click += (s, e) => { demoRunning = false; demoTimer.Stop(); field.Invalidate(); };
            Button btnHit = new Button { Text = "Attack (-10 HP)", Size = new Size(130, 32) };
            btnHit.Click += (s, e) => { trackBar_EHealth.Value = Math.Max(0, trackBar_EHealth.Value - 10); UpdateAiState(); };
            buttons.Controls.AddRange(new Control[] { btnStart, btnStop, btnHit });
            demoGroup.Controls.Add(buttons);
            this.Controls.Add(demoGroup);

            // ---- Fuzzy Visualization (right column, top) ----
            GroupBox graphGroup = new GroupBox { Text = "Fuzzy Visualization", Location = new Point(500, 15), Size = new Size(460, 520) };
            healthGraph = new MembershipGraphPanel
            {
                Title = "Health Membership",
                DomainMin = 0,
                DomainMax = 100,
                Location = new Point(10, 25),
                Size = new Size(435, 100),
                Sets = new List<(string, double, double, double, Color)>
        {
            ("Low", 1, 1, 40, Color.IndianRed),
            ("Med", 25, 50, 80, Color.Goldenrod),
            ("High", 60, 100, 100, Color.SeaGreen)
        }
            };
            distanceGraph = new MembershipGraphPanel
            {
                Title = "Distance Membership",
                DomainMin = 0,
                DomainMax = 51,
                Location = new Point(10, 130),
                Size = new Size(435, 100),
                Sets = new List<(string, double, double, double, Color)>
        {
            ("Near", 0, 0, 20, Color.SeaGreen),
            ("Med", 10, 25, 40, Color.Goldenrod),
            ("Far", 30, 50, 50, Color.IndianRed)
        }
            };
            outputGraph = new OutputCurvePanel { Location = new Point(10, 235), Size = new Size(435, 110) };

            ruleGrid = new DataGridView
            {
                Location = new Point(10, 350),
                Size = new Size(435, 150),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                Font = new Font("Segoe UI", 7.5f)
            };
            ruleGrid.RowTemplate.Height = 18;
            ruleGrid.Columns.Add("Rule", "Rule");
            ruleGrid.Columns.Add("Health", "Health");
            ruleGrid.Columns.Add("Dist", "Dist");
            ruleGrid.Columns.Add("Output", "Output");
            ruleGrid.Columns.Add("Strength", "Strength");
            foreach (DataGridViewColumn col in ruleGrid.Columns) col.Width = 82;

            graphGroup.Controls.Add(healthGraph);
            graphGroup.Controls.Add(distanceGraph);
            graphGroup.Controls.Add(outputGraph);
            graphGroup.Controls.Add(ruleGrid);
            this.Controls.Add(graphGroup);

            // ---- Control Surface (right column, under Fuzzy Visualization) ----
            GroupBox surfaceGroup = new GroupBox { Text = "Control Surface (Health x Distance -> Aggro)", Location = new Point(500, 550), Size = new Size(460, 400) };
            surfacePanel = new ControlSurfacePanel { Location = new Point(10, 25), Size = new Size(435, 365) };
            surfacePanel.BuildSurface();
            surfaceGroup.Controls.Add(surfacePanel);
            this.Controls.Add(surfaceGroup);

            this.KeyDown += (s, e) => keysDown.Add(e.KeyCode);
            this.KeyUp += (s, e) => keysDown.Remove(e.KeyCode);
        }

        private void AddEdgeButton(FlowLayoutPanel panel, string label, int health, int distance)
        {
            Button b = new Button { Text = label, Size = new Size(95, 26), Font = new Font("Segoe UI", 7.5f), Margin = new Padding(2) };
            b.Click += (s, e) =>
            {
                trackBar_EHealth.Value = Math.Max(trackBar_EHealth.Minimum, Math.Min(trackBar_EHealth.Maximum, health));
                trackBar_PDistance.Value = Math.Max(trackBar_PDistance.Minimum, Math.Min(trackBar_PDistance.Maximum, distance));
                UpdateAiState();
            };
            panel.Controls.Add(b);
        }

        private void UpdateAiState()
        {
            label_EHealth.Text = $"{trackBar_EHealth.Value}%";
            label_PDistance.Text = trackBar_PDistance.Value >= 51 ? ">50m" : $"{trackBar_PDistance.Value}m";

            enemy.UpdateBehavior(trackBar_EHealth.Value, trackBar_PDistance.Value);
            label_Aggro.Text = $"Aggressiveness {enemy.CurrentAggro:F1}%";

            healthGraph.CurrentValue = trackBar_EHealth.Value;
            distanceGraph.CurrentValue = trackBar_PDistance.Value;
            healthGraph.Invalidate();
            distanceGraph.Invalidate();

            outputGraph.Curve = enemy.LastResult.Curve ?? new List<(double, double)>();
            outputGraph.Centroid = enemy.CurrentAggro;
            outputGraph.Invalidate();

            RefreshRuleGrid();
            surfacePanel.MarkerHealth = trackBar_EHealth.Value;
            surfacePanel.MarkerDistance = trackBar_PDistance.Value;
            surfacePanel.Invalidate();

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

        private void RefreshRuleGrid()
        {
            ruleGrid.Rows.Clear();
            var rules = enemy.LastResult.Rules;
            if (rules == null || rules.Count == 0) return;

            foreach (var kv in rules)
            {
                int rowIdx = ruleGrid.Rows.Add(kv.Key, kv.Value.healthSet, kv.Value.distSet, kv.Value.output, kv.Value.strength.ToString("F2"));
                if (kv.Value.strength > 0.0)
                {
                    ruleGrid.Rows[rowIdx].DefaultCellStyle.BackColor = Color.LightYellow;
                    ruleGrid.Rows[rowIdx].DefaultCellStyle.Font = new Font(ruleGrid.Font, FontStyle.Bold);
                }
            }
        }

        private void ResetPositions()
        {
            playerPos = new PointF((float)rng.NextDouble() * (FieldW - CircleSize), (float)rng.NextDouble() * (FieldH - CircleSize));
            enemy.Position = new PointF((float)rng.NextDouble() * (FieldW - CircleSize), (float)rng.NextDouble() * (FieldH - CircleSize));
            enemy.ResetCommitment();
            trackBar_EHealth.Value = 100;
            UpdateAiState();
        }

        private void DemoTimer_Tick(object sender, EventArgs e)
        {
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
            e.Graphics.FillEllipse(Brushes.RoyalBlue, playerPos.X, playerPos.Y, CircleSize, CircleSize);

            RectangleF enemyRect = new RectangleF(enemy.Position.X, enemy.Position.Y, CircleSize, CircleSize);

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

    public class MembershipGraphPanel : DoubleBufferedPanel
    {
        public string Title = "";
        public double DomainMin = 0, DomainMax = 100;
        public List<(string name, double a, double b, double c, Color color)> Sets = new List<(string, double, double, double, Color)>();
        public double CurrentValue = 0;

        public MembershipGraphPanel()
        {
            this.BackColor = Color.White;
            this.BorderStyle = BorderStyle.FixedSingle;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int mL = 30, mB = 18, mT = 16, mR = 8;
            int plotW = Math.Max(1, Width - mL - mR);
            int plotH = Math.Max(1, Height - mT - mB);

            using (Pen axisPen = new Pen(Color.LightGray))
            {
                g.DrawLine(axisPen, mL, mT, mL, mT + plotH);
                g.DrawLine(axisPen, mL, mT + plotH, mL + plotW, mT + plotH);
            }
            using (Font tf = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                g.DrawString(Title, tf, Brushes.DimGray, mL, 1);

            float legendX = Width - mR - 5;
            using (Font lf = new Font("Segoe UI", 7, FontStyle.Bold))
            {
                for (int i = Sets.Count - 1; i >= 0; i--)
                {
                    var s = Sets[i];
                    SizeF sz = g.MeasureString(s.name, lf);
                    legendX -= sz.Width;
                    g.DrawString(s.name, lf, new SolidBrush(s.color), legendX, 1);
                    legendX -= 6;
                }
            }

            float MapX(double v) => mL + (float)((v - DomainMin) / (DomainMax - DomainMin) * plotW);
            float MapY(double m) => mT + plotH - (float)(m * plotH);

            foreach (var s in Sets)
            {
                PointF p1 = new PointF(MapX(s.a), MapY(0));
                PointF p2 = new PointF(MapX(s.b), MapY(1));
                PointF p3 = new PointF(MapX(s.c), MapY(0));
                using (Pen p = new Pen(s.color, 2))
                    g.DrawLines(p, new[] { p1, p2, p3 });
            }

            float markerX = MapX(CurrentValue);
            using (Pen mp = new Pen(Color.Black, 1) { DashStyle = DashStyle.Dash })
                g.DrawLine(mp, markerX, mT, markerX, mT + plotH);
            using (Font vf = new Font("Segoe UI", 7))
                g.DrawString(CurrentValue.ToString("F0"), vf, Brushes.Black, markerX - 8, mT + plotH + 1);
        }
    }

    public class OutputCurvePanel : DoubleBufferedPanel
    {
        public List<(double y, double m)> Curve = new List<(double, double)>();
        public double Centroid = 0;

        public OutputCurvePanel()
        {
            this.BackColor = Color.White;
            this.BorderStyle = BorderStyle.FixedSingle;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int mL = 30, mB = 18, mT = 16, mR = 8;
            int plotW = Math.Max(1, Width - mL - mR);
            int plotH = Math.Max(1, Height - mT - mB);

            using (Pen axisPen = new Pen(Color.LightGray))
            {
                g.DrawLine(axisPen, mL, mT, mL, mT + plotH);
                g.DrawLine(axisPen, mL, mT + plotH, mL + plotW, mT + plotH);
            }
            using (Font tf = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                g.DrawString("Aggregated Output (Centroid Defuzzification)", tf, Brushes.DimGray, mL, 1);

            if (Curve.Count > 1)
            {
                var pts = new PointF[Curve.Count];
                for (int i = 0; i < Curve.Count; i++)
                {
                    float x = mL + (float)(Curve[i].y / 100.0 * plotW);
                    float y = mT + plotH - (float)(Curve[i].m * plotH);
                    pts[i] = new PointF(x, y);
                }
                using (Pen cp = new Pen(Color.MediumSeaGreen, 2))
                    g.DrawLines(cp, pts);
            }

            float cx = mL + (float)(Centroid / 100.0 * plotW);
            using (Pen centroidPen = new Pen(Color.Crimson, 2))
                g.DrawLine(centroidPen, cx, mT, cx, mT + plotH);
            using (Font vf = new Font("Segoe UI", 7))
                g.DrawString(Centroid.ToString("F1"), vf, Brushes.Crimson, cx - 10, mT + plotH + 1);
        }
    }

    public class ControlSurfacePanel : DoubleBufferedPanel
    {
        private double[,] grid;
        private const int Steps = 40;
        public double MarkerHealth = 100, MarkerDistance = 50;

        public ControlSurfacePanel()
        {
            this.BackColor = Color.White;
            this.BorderStyle = BorderStyle.FixedSingle;
        }

        public void BuildSurface()
        {
            grid = new double[Steps + 1, Steps + 1];
            for (int hi = 0; hi <= Steps; hi++)
            {
                double h = hi * (100.0 / Steps);
                for (int di = 0; di <= Steps; di++)
                {
                    double d = di * (51.0 / Steps);
                    var result = FuzzyEngine.Evaluate(Math.Max(0.01, h), Math.Max(0.01, d));
                    grid[hi, di] = result.Aggressiveness;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (grid == null) return;

            var g = e.Graphics;
            int mL = 35, mB = 20, mT = 8, mR = 8;
            int plotW = Math.Max(1, Width - mL - mR);
            int plotH = Math.Max(1, Height - mT - mB);

            float cellW = (float)plotW / Steps;
            float cellH = (float)plotH / Steps;

            for (int hi = 0; hi < Steps; hi++)
            {
                for (int di = 0; di < Steps; di++)
                {
                    double aggro = grid[hi, di];
                    Color c = AggroToColor(aggro);
                    float x = mL + di * cellW;
                    float y = mT + plotH - (hi + 1) * cellH;
                    using (Brush b = new SolidBrush(c))
                        g.FillRectangle(b, x, y, cellW + 1, cellH + 1);
                }
            }

            using (Pen axisPen = new Pen(Color.Black))
            {
                g.DrawLine(axisPen, mL, mT, mL, mT + plotH);
                g.DrawLine(axisPen, mL, mT + plotH, mL + plotW, mT + plotH);
            }

            using (Font f = new Font("Segoe UI", 7))
            {
                g.DrawString("Dist ->", f, Brushes.Black, mL + plotW - 40, mT + plotH + 2);
                g.DrawString("HP", f, Brushes.Black, 2, mT);
            }

            float mx = mL + (float)(MarkerDistance / 51.0 * plotW);
            float my = mT + plotH - (float)(MarkerHealth / 100.0 * plotH);
            using (Pen markerPen = new Pen(Color.Black, 2))
            {
                g.DrawLine(markerPen, mx - 6, my, mx + 6, my);
                g.DrawLine(markerPen, mx, my - 6, mx, my + 6);
            }
        }

        private Color AggroToColor(double aggro)
        {
            double t = Math.Max(0, Math.Min(100, aggro)) / 100.0;
            if (t < 0.5)
            {
                double lt = t / 0.5;
                return Interp(Color.RoyalBlue, Color.Gold, lt);
            }
            else
            {
                double lt = (t - 0.5) / 0.5;
                return Interp(Color.Gold, Color.Crimson, lt);
            }
        }

        private Color Interp(Color a, Color b, double t)
        {
            int r = (int)(a.R + (b.R - a.R) * t);
            int gg = (int)(a.G + (b.G - a.G) * t);
            int bb = (int)(a.B + (b.B - a.B) * t);
            return Color.FromArgb(r, gg, bb);
        }
    }
}