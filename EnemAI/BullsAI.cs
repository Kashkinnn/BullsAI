using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Linq;

namespace EnemAI
{
    public class BullsAI : Form
    {
        private System.Windows.Forms.Timer animTimer;
        private double dispHealth = 100, dispDistance = 51, dispAggro = 0;
        private double targetHealth = 100, targetDistance = 51, targetAggro = 0;
        private const double AnimSmoothing = 0.2;

        private TrackBar trackBar_EHealth, trackBar_PDistance;
        private TrackBar trackBar_PlayerSpeed, trackBar_EnemySpeed;
        private Label label_EHealth, label_PDistance, enemyIcon, label_Aggro;
        private Panel field;
        private DoubleBufferedPanel designField;
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
        private bool isDesignMode = false;
        private Button btnDesignEnv;

        private const float FieldW = 920, FieldH = 780, CircleSize = 30;
        private float playerSpeed = 3f, enemySpeed = 2f;
        private Random rng = new Random();

        private bool isDraggingPlayer = false;
        private bool isDraggingEnemy = false;
        private float lastEnemyHeight = 18;
        private const float IsoScaleX = 0.6f, IsoScaleY = 0.32f;

        private float camOffsetX = 0f;
        private float camOffsetY = 0f;

        private Queue<PointF> playerTrail = new Queue<PointF>();
        private Queue<PointF> enemyTrail = new Queue<PointF>();
        private const int MaxTrailLength = 15;
        private int tickCounter = 0;

        private HashSet<Point> gridBlocks = new HashSet<Point>();
        private const int TileSize = 40;

        public BullsAI()
        {
            LoadEnemyResource();
            InitializeUI();
            ResetPositions();
            demoTimer = new System.Windows.Forms.Timer { Interval = 50 };
            demoTimer.Tick += DemoTimer_Tick;

            animTimer = new System.Windows.Forms.Timer { Interval = 20 };
            animTimer.Tick += AnimTimer_Tick;
            animTimer.Start();
        }

        private void LoadEnemyResource()
        {
            try { enemyImage = Properties.Resources.Skeleton; }
            catch { enemyImage = null; }
        }

        private void InitializeUI()
        {
            this.Text = "BullsAi";
            this.Size = new Size(1850, 980);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;

            GroupBox statusGroup = new GroupBox { Text = "Enemy Status", Location = new Point(20, 15), Size = new Size(350, 220) };
            enemyIcon = new Label { Size = new Size(150, 120), Location = new Point(100, 25), BackColor = Color.Gray, ForeColor = Color.White, Font = new Font("Impact", 17), TextAlign = ContentAlignment.MiddleCenter, BorderStyle = BorderStyle.FixedSingle };
            label_Aggro = new Label { Location = new Point(20, 160), Size = new Size(310, 30), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Impact", 15), ForeColor = Color.DimGray };
            statusGroup.Controls.Add(enemyIcon);
            statusGroup.Controls.Add(label_Aggro);
            this.Controls.Add(statusGroup);

            GroupBox inputGroup = new GroupBox { Text = "Manual Controls", Location = new Point(20, 245), Size = new Size(360, 620) };
            label_EHealth = new Label { Text = "100%", Location = new Point(290, 30), AutoSize = true, ForeColor = Color.Green };
            trackBar_EHealth = new TrackBar { Minimum = 0, Maximum = 100, Value = 100, Location = new Point(15, 55), Width = 320 };
            trackBar_EHealth.Scroll += (s, e) => UpdateAiState(true);

            label_PDistance = new Label { Text = "50m", Location = new Point(290, 110), AutoSize = true, ForeColor = Color.Blue };
            trackBar_PDistance = new TrackBar { Minimum = 1, Maximum = 51, Value = 51, Location = new Point(15, 135), Width = 320, TickFrequency = 5 };
            trackBar_PDistance.Scroll += (s, e) =>
            {
                EnforceDistanceVisually(trackBar_PDistance.Value);
                UpdateAiState(true);
            };

            inputGroup.Controls.AddRange(new Control[] {
                new Label { Text = "Current Health (%)", Location = new Point(15, 30), AutoSize = true },
                label_EHealth, trackBar_EHealth,
                new Label { Text = "Player Distance (m)", Location = new Point(15, 110), AutoSize = true },
                label_PDistance, trackBar_PDistance
            });

            Label edgeLabel = new Label { Text = "Edge Case Tests:", Location = new Point(15, 195), AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
            inputGroup.Controls.Add(edgeLabel);

            FlowLayoutPanel edgeButtons = new FlowLayoutPanel { Location = new Point(15, 220), Size = new Size(330, 90), FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            AddEdgeButton(edgeButtons, "HP=0", 0, 51, 76);
            AddEdgeButton(edgeButtons, "HP=100", 100, 51, 76);
            AddEdgeButton(edgeButtons, "Dist=Min", 100, 1, 76);
            AddEdgeButton(edgeButtons, "Dist=Max", 100, 51, 76);
            AddEdgeButton(edgeButtons, "HP Bnd 40", 40, 25, 76);
            AddEdgeButton(edgeButtons, "Dist Bnd 20", 100, 20, 76);
            AddEdgeButton(edgeButtons, "Dist Bnd 40", 100, 40, 76);
            AddEdgeButton(edgeButtons, "Mid/Mid", 50, 25, 76);
            inputGroup.Controls.Add(edgeButtons);

            Label ruleTriggerLabel = new Label { Text = "Trigger Specific Rules:", Location = new Point(15, 320), AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
            inputGroup.Controls.Add(ruleTriggerLabel);

            FlowLayoutPanel ruleButtons = new FlowLayoutPanel { Location = new Point(15, 345), Size = new Size(335, 110), FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            AddEdgeButton(ruleButtons, "R1 (High/Near)", 100, 1, 105);
            AddEdgeButton(ruleButtons, "R2 (Low/Near)", 1, 1, 105);
            AddEdgeButton(ruleButtons, "R3 (Med/Med)", 50, 25, 105);
            AddEdgeButton(ruleButtons, "R4 (Low/Far)", 1, 51, 105);
            AddEdgeButton(ruleButtons, "R5 (High/Far)", 100, 51, 105);
            AddEdgeButton(ruleButtons, "R6 (High/Med)", 100, 25, 105);
            AddEdgeButton(ruleButtons, "R7 (Low/Med)", 1, 25, 105);
            AddEdgeButton(ruleButtons, "R8 (Med/Near)", 50, 1, 105);
            AddEdgeButton(ruleButtons, "R9 (Med/Far)", 50, 51, 105);
            inputGroup.Controls.Add(ruleButtons);

            Label pSpeedLbl = new Label { Text = "Player Speed", Location = new Point(15, 465), AutoSize = true };
            trackBar_PlayerSpeed = new TrackBar { Minimum = 1, Maximum = 10, Value = 3, Location = new Point(15, 485), Width = 320 };
            trackBar_PlayerSpeed.Scroll += (s, e) => { playerSpeed = trackBar_PlayerSpeed.Value; };

            Label eSpeedLbl = new Label { Text = "Enemy Speed", Location = new Point(15, 535), AutoSize = true };
            trackBar_EnemySpeed = new TrackBar { Minimum = 1, Maximum = 10, Value = 2, Location = new Point(15, 555), Width = 320 };
            trackBar_EnemySpeed.Scroll += (s, e) => { enemySpeed = trackBar_EnemySpeed.Value; };

            inputGroup.Controls.AddRange(new Control[] { pSpeedLbl, trackBar_PlayerSpeed, eSpeedLbl, trackBar_EnemySpeed });
            this.Controls.Add(inputGroup);

            GroupBox demoGroup = new GroupBox { Text = "Live Demo (Dynamic Camera, Click Blocks)", Location = new Point(390, 15), Size = new Size(950, 920), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };

            field = new DoubleBufferedPanel { Location = new Point(15, 25), Size = new Size((int)FieldW, (int)FieldH), BackColor = Color.FromArgb(235, 240, 245), BorderStyle = BorderStyle.FixedSingle };
            field.TabStop = true;
            field.Paint += Field_Paint;
            field.MouseDown += Field_MouseDown;
            field.MouseMove += Field_MouseMove;
            field.MouseUp += Field_MouseUp;
            demoGroup.Controls.Add(field);

            designField = new DoubleBufferedPanel { Location = new Point(15, 25), Size = new Size((int)FieldW, (int)FieldH), BackColor = Color.FromArgb(30, 35, 40), BorderStyle = BorderStyle.FixedSingle, Visible = false };
            designField.TabStop = true;
            designField.Paint += DesignField_Paint;
            designField.MouseDown += DesignField_MouseDown;
            designField.MouseMove += DesignField_MouseMove;
            demoGroup.Controls.Add(designField);

            FlowLayoutPanel buttons = new FlowLayoutPanel { Location = new Point(15, (int)FieldH + 35), Size = new Size((int)FieldW - 150, 50) };
            Button btnStart = new Button { Text = "Set", Size = new Size(90, 32), BackColor = Color.LightGreen, FlatStyle = FlatStyle.Flat };
            btnStart.Click += (s, e) => { demoRunning = true; ResetPositions(); demoTimer.Start(); field.Focus(); };
            Button btnStop = new Button { Text = "Stop", Size = new Size(90, 32), BackColor = Color.LightCoral, FlatStyle = FlatStyle.Flat };
            btnStop.Click += (s, e) => { demoRunning = false; demoTimer.Stop(); field.Invalidate(); };
            Button btnHit = new Button { Text = "Attack (-10 HP)", Size = new Size(130, 32) };
            btnHit.Click += (s, e) => { trackBar_EHealth.Value = Math.Max(0, trackBar_EHealth.Value - 10); UpdateAiState(true); };
            Button btnClear = new Button { Text = "Clear Blocks", Size = new Size(100, 32) };
            btnClear.Click += (s, e) => { gridBlocks.Clear(); field.Invalidate(); designField.Invalidate(); field.Focus(); };
            Button btnRandomEnv = new Button { Text = "Randomize", Size = new Size(100, 32) };
            btnRandomEnv.Click += (s, e) => { GenerateEnvironment(); field.Invalidate(); designField.Invalidate(); field.Focus(); };

            buttons.Controls.AddRange(new Control[] { btnStart, btnStop, btnHit, btnClear, btnRandomEnv });
            demoGroup.Controls.Add(buttons);

            btnDesignEnv = new Button { Text = "2D", Size = new Size(135, 32), Location = new Point((int)FieldW - 120, (int)FieldH + 35), BackColor = Color.LightSkyBlue, FlatStyle = FlatStyle.Flat };
            btnDesignEnv.Click += (s, e) => ToggleDesignMode();
            demoGroup.Controls.Add(btnDesignEnv);

            this.Controls.Add(demoGroup);

            GroupBox graphGroup = new GroupBox { Text = "Fuzzy Visualization", Location = new Point(1360, 15), Size = new Size(460, 920), Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };

            Label healthLabel = new Label { Text = "Health Membership", Location = new Point(10, 25), Size = new Size(435, 16), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.DimGray };
            healthGraph = new MembershipGraphPanel
            {
                DomainMin = 0,
                DomainMax = 100,
                Location = new Point(10, 44),
                Size = new Size(435, 105),
                Sets = new List<(string, double, double, double, Color)> {
                    ("Low", 1, 1, 40, Color.IndianRed), ("Med", 25, 50, 80, Color.Goldenrod), ("High", 60, 100, 100, Color.SeaGreen)
                }
            };

            Label distanceLabel = new Label { Text = "Distance Membership", Location = new Point(10, 158), Size = new Size(435, 16), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.DimGray };
            distanceGraph = new MembershipGraphPanel
            {
                DomainMin = 0,
                DomainMax = 51,
                Location = new Point(10, 177),
                Size = new Size(435, 105),
                Sets = new List<(string, double, double, double, Color)> {
                    ("Near", 0, 0, 20, Color.SeaGreen), ("Med", 10, 25, 40, Color.Goldenrod), ("Far", 30, 50, 50, Color.IndianRed)
                }
            };

            Label outputLabel = new Label { Text = "Aggregated Output (Centroid Defuzzification)", Location = new Point(10, 291), Size = new Size(435, 16), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.DimGray };
            outputGraph = new OutputCurvePanel { Location = new Point(10, 310), Size = new Size(435, 115) };

            Label ruleLabel = new Label { Text = "Rule Firing Table", Location = new Point(10, 434), Size = new Size(435, 16), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.DimGray };
            ruleGrid = new DataGridView
            {
                Location = new Point(10, 453),
                Size = new Size(435, 190),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                Font = new Font("Segoe UI", 7.5f)
            };
            ruleGrid.RowTemplate.Height = 18;
            ruleGrid.Columns.Add("Rule", "Rule"); ruleGrid.Columns.Add("Health", "Health"); ruleGrid.Columns.Add("Dist", "Dist");
            ruleGrid.Columns.Add("Output", "Output"); ruleGrid.Columns.Add("Strength", "Strength");
            foreach (DataGridViewColumn col in ruleGrid.Columns) col.Width = 82;

            Label surfaceLabel = new Label { Text = "Control Surface (Health x Distance -> Aggro)", Location = new Point(10, 655), Size = new Size(435, 16), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.DimGray };
            surfacePanel = new ControlSurfacePanel { Location = new Point(10, 674), Size = new Size(435, 220) };
            surfacePanel.BuildSurface();

            graphGroup.Controls.AddRange(new Control[] { healthLabel, healthGraph, distanceLabel, distanceGraph, outputLabel, outputGraph, ruleLabel, ruleGrid, surfaceLabel, surfacePanel });
            this.Controls.Add(graphGroup);

            this.KeyDown += (s, e) => keysDown.Add(e.KeyCode);
            this.KeyUp += (s, e) => keysDown.Remove(e.KeyCode);
        }

        private void ToggleDesignMode()
        {
            isDesignMode = !isDesignMode;
            if (isDesignMode)
            {
                btnDesignEnv.Text = "3D";
                btnDesignEnv.BackColor = Color.LightSalmon;
                field.Visible = false;
                designField.Visible = true;
                designField.Invalidate();
            }
            else
            {
                btnDesignEnv.Text = "2D";
                btnDesignEnv.BackColor = Color.LightSkyBlue;
                designField.Visible = false;
                field.Visible = true;
                field.Invalidate();
            }
        }

        private void GenerateEnvironment()
        {
            int cols = (int)(FieldW / TileSize);
            int rows = (int)(FieldH / TileSize);
            Point pGrid = new Point((int)(playerPos.X / TileSize), (int)(playerPos.Y / TileSize));
            Point eGrid = new Point((int)(enemy.Position.X / TileSize), (int)(enemy.Position.Y / TileSize));
            gridBlocks = EnvironmentGenerator.Generate(cols, rows, 30, rng, pGrid, eGrid);
        }

        private void AddEdgeButton(FlowLayoutPanel panel, string label, int health, int distance, int width = 100)
        {
            Button b = new Button { Text = label, Size = new Size(width, 26), Font = new Font("Segoe UI", 7f), Margin = new Padding(2) };
            b.Click += (s, e) =>
            {
                trackBar_EHealth.Value = Math.Max(trackBar_EHealth.Minimum, Math.Min(trackBar_EHealth.Maximum, health));
                trackBar_PDistance.Value = Math.Max(trackBar_PDistance.Minimum, Math.Min(trackBar_PDistance.Maximum, distance));
                EnforceDistanceVisually(trackBar_PDistance.Value);
                UpdateAiState(true);
            };
            panel.Controls.Add(b);
        }

        private void EnforceDistanceVisually(int distanceValue)
        {
            double diagonal = Math.Sqrt(FieldW * FieldW + FieldH * FieldH);
            float pxDist = (float)((distanceValue / 51.0) * diagonal);

            for (int i = 0; i < 100; i++)
            {
                float px = (float)rng.NextDouble() * (FieldW - CircleSize);
                float py = (float)rng.NextDouble() * (FieldH - CircleSize);
                double angle = rng.NextDouble() * Math.PI * 2;

                float ex = px + (float)(Math.Cos(angle) * pxDist);
                float ey = py + (float)(Math.Sin(angle) * pxDist);

                if (ex >= 0 && ex <= FieldW - CircleSize && ey >= 0 && ey <= FieldH - CircleSize)
                {
                    playerPos = new PointF(px, py);
                    enemy.Position = new PointF(ex, ey);
                    if (isDesignMode) designField.Invalidate(); else field.Invalidate();
                    return;
                }
            }

            playerPos = new PointF(0, 0);
            float maxAngle = (float)Math.Atan2(FieldH - CircleSize, FieldW - CircleSize);
            enemy.Position = new PointF((float)Math.Cos(maxAngle) * pxDist, (float)Math.Sin(maxAngle) * pxDist);
            enemy.Position.X = Math.Max(0, Math.Min(FieldW - CircleSize, enemy.Position.X));
            enemy.Position.Y = Math.Max(0, Math.Min(FieldH - CircleSize, enemy.Position.Y));
            if (isDesignMode) designField.Invalidate(); else field.Invalidate();
        }

        private void ResetPositions()
        {
            trackBar_EHealth.Value = 100;

            playerPos = new PointF((float)rng.NextDouble() * (FieldW - CircleSize), (float)rng.NextDouble() * (FieldH - CircleSize));
            enemy.Position = new PointF((float)rng.NextDouble() * (FieldW - CircleSize), (float)rng.NextDouble() * (FieldH - CircleSize));

            double distPx = Math.Sqrt(Math.Pow(playerPos.X - enemy.Position.X, 2) + Math.Pow(playerPos.Y - enemy.Position.Y, 2));
            double diagonal = Math.Sqrt(FieldW * FieldW + FieldH * FieldH);
            trackBar_PDistance.Value = Math.Max(1, Math.Min(51, (int)(distPx / diagonal * 51)));

            enemy.ResetCommitment();
            UpdateAiState(true);

            if (isDesignMode) designField.Invalidate();
            else field.Invalidate();
        }

        private List<RectangleF> GetObstacleRects()
        {
            List<RectangleF> rects = new List<RectangleF>();
            foreach (var b in gridBlocks)
            {
                rects.Add(new RectangleF(b.X * TileSize, b.Y * TileSize, TileSize, TileSize));
            }
            return rects;
        }

        private void UpdateAiState(bool isManualOverride = false)
        {
            label_EHealth.Text = $"{trackBar_EHealth.Value}%";
            label_PDistance.Text = trackBar_PDistance.Value >= 51 ? ">50m" : $"{trackBar_PDistance.Value}m";

            enemy.UpdateBehavior(trackBar_EHealth.Value, trackBar_PDistance.Value, playerPos, GetObstacleRects(), isManualOverride);
            label_Aggro.Text = $"Aggressiveness {enemy.CurrentAggro:F1}%";

            targetHealth = trackBar_EHealth.Value;
            targetDistance = trackBar_PDistance.Value;
            targetAggro = enemy.CurrentAggro;

            outputGraph.Curve = enemy.LastResult.Curve ?? new List<(double, double)>();
            RefreshRuleGrid();

            switch (enemy.CurrentState)
            {
                case "DEAD": enemyIcon.BackColor = Color.Black; enemyIcon.Text = "DEAD\n☠️"; break;
                case "IDLE": enemyIcon.BackColor = Color.LightGray; enemyIcon.Text = "IDLE\n💤"; break;
                case "FLEEING": enemyIcon.BackColor = Color.RoyalBlue; enemyIcon.Text = "FLEEING\n😨"; break;
                case "ALERT": enemyIcon.BackColor = Color.DarkOrange; enemyIcon.Text = "ALERT\n👁️"; break;
                case "ATTACKING": enemyIcon.BackColor = Color.DarkRed; enemyIcon.Text = "ATTACKING\n⚔️"; break;
            }
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            dispHealth += (targetHealth - dispHealth) * AnimSmoothing;
            dispDistance += (targetDistance - dispDistance) * AnimSmoothing;
            dispAggro += (targetAggro - dispAggro) * AnimSmoothing;

            healthGraph.CurrentValue = dispHealth;
            distanceGraph.CurrentValue = dispDistance;
            outputGraph.Centroid = dispAggro;
            surfacePanel.MarkerHealth = dispHealth;
            surfacePanel.MarkerDistance = dispDistance;

            healthGraph.Invalidate(); distanceGraph.Invalidate(); outputGraph.Invalidate(); surfacePanel.Invalidate();
        }

        private void RefreshRuleGrid()
        {
            ruleGrid.Rows.Clear();
            var rules = enemy.LastResult.Rules;
            if (rules == null || rules.Count == 0) return;

            var sortedRules = rules.OrderBy(kv => kv.Key).ToList();
            foreach (var kv in sortedRules)
            {
                int rowIdx = ruleGrid.Rows.Add(kv.Key, kv.Value.healthSet, kv.Value.distSet, kv.Value.output, kv.Value.strength.ToString("F2"));
                if (kv.Value.strength > 0.0)
                {
                    ruleGrid.Rows[rowIdx].DefaultCellStyle.BackColor = Color.LightYellow;
                    ruleGrid.Rows[rowIdx].DefaultCellStyle.Font = new Font(ruleGrid.Font, FontStyle.Bold);
                }
            }
        }

        private bool IsColliding(float x, float y)
        {
            RectangleF rect = new RectangleF(x, y, CircleSize, CircleSize);
            foreach (var obs in GetObstacleRects())
                if (obs.IntersectsWith(rect)) return true;
            return false;
        }

        private void DemoTimer_Tick(object sender, EventArgs e)
        {
            tickCounter++;

            float targetCamX = (FieldW / 2f) - playerPos.X;
            float targetCamY = (FieldH / 2f) - playerPos.Y;
            camOffsetX += (targetCamX - camOffsetX) * 0.1f;
            camOffsetY += (targetCamY - camOffsetY) * 0.1f;

            if (!isDraggingPlayer)
            {
                float dx = 0, dy = 0;
                if (keysDown.Contains(Keys.W)) dy -= 1;
                if (keysDown.Contains(Keys.S)) dy += 1;
                if (keysDown.Contains(Keys.A)) dx -= 1;
                if (keysDown.Contains(Keys.D)) dx += 1;

                if (dx != 0 || dy != 0)
                {
                    float len = (float)Math.Sqrt(dx * dx + dy * dy);
                    float mx = (dx / len) * playerSpeed;
                    float my = (dy / len) * playerSpeed;

                    if (!IsColliding(playerPos.X + mx, playerPos.Y)) playerPos.X = Math.Max(0, Math.Min(FieldW - CircleSize, playerPos.X + mx));
                    if (!IsColliding(playerPos.X, playerPos.Y + my)) playerPos.Y = Math.Max(0, Math.Min(FieldH - CircleSize, playerPos.Y + my));
                }
            }

            double distPx = Math.Sqrt(Math.Pow(playerPos.X - enemy.Position.X, 2) + Math.Pow(playerPos.Y - enemy.Position.Y, 2));
            double diagonal = Math.Sqrt(FieldW * FieldW + FieldH * FieldH);
            trackBar_PDistance.Value = Math.Max(1, Math.Min(51, (int)(distPx / diagonal * 51)));

            UpdateAiState(false);

            if (!isDraggingEnemy)
                enemy.Move(playerPos, enemySpeed, 0.05f, FieldW, FieldH, CircleSize, GetObstacleRects());

            playerTrail.Enqueue(new PointF(playerPos.X, playerPos.Y));
            if (playerTrail.Count > MaxTrailLength) playerTrail.Dequeue();

            enemyTrail.Enqueue(new PointF(enemy.Position.X, enemy.Position.Y));
            if (enemyTrail.Count > MaxTrailLength) enemyTrail.Dequeue();

            if (isDesignMode) designField.Invalidate();
            else field.Invalidate();
        }

        private PointF ToIso(float x, float y, float z = 0)
        {
            x += camOffsetX;
            y += camOffsetY;
            float isoX = (x - y) * IsoScaleX;
            float isoY = (x + y) * IsoScaleY - z;
            return new PointF(isoX + FieldW / 2.2f, isoY + 70);
        }

        private PointF FromIso(PointF screen, float z = 0)
        {
            float offsetX = FieldW / 2.2f;
            float offsetY = 70f;
            float sx = screen.X - offsetX;
            float sy = screen.Y - offsetY + z;
            float x = (sx / IsoScaleX + sy / IsoScaleY) / 2f;
            float y = (sy / IsoScaleY - sx / IsoScaleX) / 2f;
            x -= camOffsetX;
            y -= camOffsetY;
            return new PointF(x, y);
        }

        private void Field_MouseDown(object sender, MouseEventArgs e)
        {
            field.Focus();
            PointF playerTop = ToIso(playerPos.X, playerPos.Y, 26);
            PointF enemyTop = ToIso(enemy.Position.X, enemy.Position.Y, lastEnemyHeight);

            float distToPlayer = (float)Math.Sqrt(Math.Pow(e.X - playerTop.X, 2) + Math.Pow(e.Y - playerTop.Y, 2));
            float distToEnemy = (float)Math.Sqrt(Math.Pow(e.X - enemyTop.X, 2) + Math.Pow(e.Y - enemyTop.Y, 2));

            if (distToPlayer < CircleSize / 2f) isDraggingPlayer = true;
            else if (distToEnemy < CircleSize / 2f) isDraggingEnemy = true;
        }

        private void Field_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingPlayer)
            {
                PointF world = FromIso(e.Location, 26);
                playerPos.X = Math.Max(0, Math.Min(FieldW - CircleSize, world.X));
                playerPos.Y = Math.Max(0, Math.Min(FieldH - CircleSize, world.Y));
                field.Invalidate();
            }
            else if (isDraggingEnemy)
            {
                PointF world = FromIso(e.Location, lastEnemyHeight);
                enemy.Position = new PointF(Math.Max(0, Math.Min(FieldW - CircleSize, world.X)), Math.Max(0, Math.Min(FieldH - CircleSize, world.Y)));
                field.Invalidate();
            }
        }

        private void Field_MouseUp(object sender, MouseEventArgs e)
        {
            isDraggingPlayer = false; isDraggingEnemy = false; field.Focus();
        }

        private void DesignField_MouseDown(object sender, MouseEventArgs e)
        {
            designField.Focus();
            HandleDesignInput(e);
        }

        private void DesignField_MouseMove(object sender, MouseEventArgs e)
        {
            HandleDesignInput(e);
        }

        private void HandleDesignInput(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
                int tx = (int)(e.X / TileSize);
                int ty = (int)(e.Y / TileSize);

                if (tx >= 0 && ty >= 0 && tx < FieldW / TileSize && ty < FieldH / TileSize)
                {
                    Point gridPt = new Point(tx, ty);
                    if (e.Button == MouseButtons.Left) gridBlocks.Add(gridPt);
                    else if (e.Button == MouseButtons.Right) gridBlocks.Remove(gridPt);
                    designField.Invalidate();
                }
            }
        }

        private void DesignField_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen gridPen = new Pen(Color.FromArgb(80, 100, 100, 100), 1))
            {
                for (int x = 0; x <= FieldW; x += TileSize) g.DrawLine(gridPen, x, 0, x, FieldH);
                for (int y = 0; y <= FieldH; y += TileSize) g.DrawLine(gridPen, 0, y, FieldW, y);
            }

            using (Brush blockBrush = new SolidBrush(Color.SlateGray))
            using (Pen blockBorder = new Pen(Color.Black, 1))
            {
                foreach (var b in gridBlocks)
                {
                    g.FillRectangle(blockBrush, b.X * TileSize, b.Y * TileSize, TileSize, TileSize);
                    g.DrawRectangle(blockBorder, b.X * TileSize, b.Y * TileSize, TileSize, TileSize);
                }
            }

            g.FillEllipse(Brushes.RoyalBlue, playerPos.X, playerPos.Y, CircleSize, CircleSize);
            g.DrawString("P", new Font("Arial", 10, FontStyle.Bold), Brushes.White, playerPos.X + 8, playerPos.Y + 8);

            Color stateColor = GetStateColor(enemy.CurrentState);
            g.FillEllipse(new SolidBrush(stateColor), enemy.Position.X, enemy.Position.Y, CircleSize, CircleSize);
            g.DrawString("E", new Font("Arial", 10, FontStyle.Bold), Brushes.White, enemy.Position.X + 8, enemy.Position.Y + 8);
        }

        private void DrawIsoCube(Graphics g, RectangleF rect, float height)
        {
            PointF p2 = ToIso(rect.Right, rect.Y);
            PointF p3 = ToIso(rect.Right, rect.Bottom);
            PointF p4 = ToIso(rect.X, rect.Bottom);

            PointF t1 = ToIso(rect.X, rect.Y, height);
            PointF t2 = ToIso(rect.Right, rect.Y, height);
            PointF t3 = ToIso(rect.Right, rect.Bottom, height);
            PointF t4 = ToIso(rect.X, rect.Bottom, height);

            using (Brush b = new SolidBrush(Color.SlateGray)) g.FillPolygon(b, new[] { p4, p3, t3, t4 });
            using (Brush b = new SolidBrush(Color.LightSlateGray)) g.FillPolygon(b, new[] { p2, p3, t3, t2 });
            using (Brush b = new SolidBrush(Color.Silver)) g.FillPolygon(b, new[] { t1, t2, t3, t4 });

            using (Pen p = new Pen(Color.DimGray))
            {
                g.DrawPolygon(p, new[] { t1, t2, t3, t4 });
                g.DrawLine(p, p4, t4); g.DrawLine(p, p3, t3); g.DrawLine(p, p2, t2);
                g.DrawLine(p, p4, p3); g.DrawLine(p, p2, p3);
            }
        }

        private void Field_Paint(object sender, PaintEventArgs e)
        {
            if (!demoRunning) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Brush lightTile = new SolidBrush(Color.FromArgb(240, 245, 250)))
            using (Brush darkTile = new SolidBrush(Color.FromArgb(225, 230, 240)))
            using (Pen gridPen = new Pen(Color.FromArgb(50, Color.SlateGray), 1))
            {
                for (int gx = 0; gx < FieldW; gx += TileSize)
                {
                    for (int gy = 0; gy < FieldH; gy += TileSize)
                    {
                        PointF p1 = ToIso(gx, gy); PointF p2 = ToIso(gx + TileSize, gy);
                        PointF p3 = ToIso(gx + TileSize, gy + TileSize); PointF p4 = ToIso(gx, gy + TileSize);
                        Brush b = ((gx / TileSize) + (gy / TileSize)) % 2 == 0 ? lightTile : darkTile;
                        g.FillPolygon(b, new[] { p1, p2, p3, p4 });
                        g.DrawPolygon(gridPen, new[] { p1, p2, p3, p4 });
                    }
                }
            }

            using (Pen borderPen = new Pen(Color.DimGray, 3))
                g.DrawPolygon(borderPen, new[] { ToIso(0, 0), ToIso(FieldW, 0), ToIso(FieldW, FieldH), ToIso(0, FieldH) });

            float bobPlayer = (float)Math.Sin(tickCounter * 0.3f) * 4f;
            float bobEnemy = (float)Math.Cos(tickCounter * 0.3f) * 4f;

            float enemyHeight = enemy.CurrentState == "ATTACKING" ? 34 : enemy.CurrentState == "ALERT" ? 24 : enemy.CurrentState == "FLEEING" ? 14 : enemy.CurrentState == "DEAD" ? 4 : 18;
            lastEnemyHeight = enemyHeight;
            Color stateColor = GetStateColor(enemy.CurrentState);
            PointF enemyBase = ToIso(enemy.Position.X, enemy.Position.Y);

            if (enemy.CurrentAggro > 0 && enemy.CurrentState != "DEAD")
            {
                float aggroRadius = (float)(enemy.CurrentAggro / 100.0 * 150f) + 15f;
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(enemyBase.X - aggroRadius, enemyBase.Y - aggroRadius * IsoScaleY, aggroRadius * 2, aggroRadius * 2 * IsoScaleY);
                    using (PathGradientBrush pgb = new PathGradientBrush(path))
                    {
                        pgb.CenterColor = Color.FromArgb(Math.Min(180, (int)(enemy.CurrentAggro * 1.8)), Color.Crimson);
                        pgb.SurroundColors = new[] { Color.Transparent };
                        g.FillPath(pgb, path);
                    }
                }
            }

            DrawTrail(g, playerTrail, Color.RoyalBlue);
            DrawTrail(g, enemyTrail, stateColor);

            var renderables = new List<(float depth, Action draw)>();

            foreach (var obs in GetObstacleRects())
                renderables.Add((obs.X + obs.Y + TileSize, () => DrawIsoCube(g, obs, TileSize)));

            renderables.Add((playerPos.X + playerPos.Y, () =>
            {
                PointF pBase = ToIso(playerPos.X, playerPos.Y);
                float pShadowScale = Math.Max(0.4f, 1f - (26 + bobPlayer) / 100f);
                using (Brush shadowBrush = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                    g.FillEllipse(shadowBrush, pBase.X - (CircleSize * pShadowScale) / 2, pBase.Y - (CircleSize * pShadowScale) / 4, CircleSize * pShadowScale, (CircleSize * pShadowScale) / 2);

                PointF playerTop = ToIso(playerPos.X, playerPos.Y, 26 + bobPlayer);
                using (Pen stemPen = new Pen(Color.FromArgb(120, Color.RoyalBlue), 3)) g.DrawLine(stemPen, pBase, playerTop);

                g.FillEllipse(Brushes.RoyalBlue, playerTop.X - CircleSize / 2, playerTop.Y - CircleSize / 2, CircleSize, CircleSize);
                using (Brush highlight = new SolidBrush(Color.FromArgb(90, Color.White)))
                    g.FillEllipse(highlight, playerTop.X - CircleSize / 4, playerTop.Y - CircleSize / 3, CircleSize / 2, CircleSize / 3);
                using (Pen outline = new Pen(Color.White, 2)) g.DrawEllipse(outline, playerTop.X - CircleSize / 2, playerTop.Y - CircleSize / 2, CircleSize, CircleSize);
            }
            ));

            renderables.Add((enemy.Position.X + enemy.Position.Y, () =>
            {
                PointF eBase = ToIso(enemy.Position.X, enemy.Position.Y);
                float eShadowScale = Math.Max(0.4f, 1f - (enemyHeight + bobEnemy) / 100f);
                using (Brush shadowBrush = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                    g.FillEllipse(shadowBrush, eBase.X - (CircleSize * eShadowScale) / 2, eBase.Y - (CircleSize * eShadowScale) / 4, CircleSize * eShadowScale, (CircleSize * eShadowScale) / 2);

                PointF enemyTop = ToIso(enemy.Position.X, enemy.Position.Y, enemyHeight + bobEnemy);
                using (Pen stemPen = new Pen(Color.FromArgb(120, stateColor), 3)) g.DrawLine(stemPen, eBase, enemyTop);

                RectangleF enemyRect = new RectangleF(enemyTop.X - CircleSize / 2, enemyTop.Y - CircleSize / 2, CircleSize, CircleSize);
                if (enemyImage != null)
                {
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddEllipse(enemyRect); Region oldClip = g.Clip; g.SetClip(path);
                        g.DrawImage(enemyImage, enemyRect); g.Clip = oldClip;
                    }
                }
                else
                {
                    g.FillEllipse(new SolidBrush(Color.DarkGray), enemyRect);
                }
                using (Pen statePen = new Pen(stateColor, 3)) g.DrawEllipse(statePen, enemyRect);
            }
            ));

            foreach (var item in renderables.OrderBy(r => r.depth)) item.draw();

            if (enemy.CurrentState == "ATTACKING" || enemy.CurrentState == "FLEEING")
            {
                PointF pTop = ToIso(playerPos.X, playerPos.Y, 26 + bobPlayer);
                PointF eTop = ToIso(enemy.Position.X, enemy.Position.Y, enemyHeight + bobEnemy);
                using (Pen targetPen = new Pen(Color.FromArgb(140, stateColor), 2) { DashStyle = DashStyle.Dash })
                    g.DrawLine(targetPen, eTop, pTop);
            }
        }

        private Color GetStateColor(string state)
        {
            return state == "DEAD" ? Color.Black : state == "ATTACKING" ? Color.Red : state == "FLEEING" ? Color.RoyalBlue : state == "ALERT" ? Color.Orange : Color.LightGray;
        }

        private void DrawTrail(Graphics g, Queue<PointF> trail, Color color)
        {
            if (trail.Count < 2) return;
            PointF[] pts = trail.ToArray();
            for (int i = 0; i < pts.Length - 1; i++)
            {
                int alpha = (int)(200 * ((float)i / pts.Length));
                using (Pen p = new Pen(Color.FromArgb(alpha, color), (float)(CircleSize * 0.4)))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLine(p, ToIso(pts[i].X, pts[i].Y), ToIso(pts[i + 1].X, pts[i + 1].Y));
                }
            }
        }
    }

    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        }
    }

    public class MembershipGraphPanel : DoubleBufferedPanel
    {
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

                using (Brush fillBrush = new SolidBrush(Color.FromArgb(55, s.color)))
                    g.FillPolygon(fillBrush, new[] { p1, p2, p3 });

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
            if (t < 0.5) return Interp(Color.RoyalBlue, Color.Gold, t / 0.5);
            else return Interp(Color.Gold, Color.Crimson, (t - 0.5) / 0.5);
        }

        private Color Interp(Color a, Color b, double t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }
}