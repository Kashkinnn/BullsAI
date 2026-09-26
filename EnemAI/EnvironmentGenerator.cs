using System;
using System.Collections.Generic;
using System.Drawing;

namespace EnemAI
{
    public static class EnvironmentGenerator
    {
        public static HashSet<Point> Generate(int cols, int rows, int blockCount, Random rng, Point playerGrid, Point enemyGrid)
        {
            var blocks = new HashSet<Point>();
            int attempts = 0;

            while (blocks.Count < blockCount && attempts < blockCount * 5)
            {
                Point p = new Point(rng.Next(cols), rng.Next(rows));
                if (p != playerGrid && p != enemyGrid)
                {
                    blocks.Add(p);
                }
                attempts++;
            }
            return blocks;
        }
    }
}