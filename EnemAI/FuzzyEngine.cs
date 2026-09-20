using System;

namespace EnemAI
{
    public struct FuzzyResult
    {
        public double Aggressiveness;
        public string State;
    }

    public static class FuzzyEngine
    {
        public static FuzzyResult Evaluate(double health, double distance)
        {
            if (health <= 0)
            {
                return new FuzzyResult { Aggressiveness = 0.0, State = "DEAD" };
            }

            // Membership values
            double healthLow = TriangularMembership(health, 1, 1, 40);
            double healthMed = TriangularMembership(health, 25, 50, 80);
            double healthHigh = TriangularMembership(health, 60, 100, 100);

            double distanceNear = TriangularMembership(distance, 0, 0, 20);
            double distanceMed = TriangularMembership(distance, 10, 25, 40);
            double distanceFar = TriangularMembership(distance, 30, 50, 50);

            /*
               (include this for general documentation)
               if health is 0, and distance is any = dead
               if health is low and distance is mid or close = flee CHECK : Might produce result "ALERT" due to Near-distace health transition(Straddling)
               if health is any and distance is far = idle CHECK
               if health is mid or high and distance is mid = alert CHECK
               if health is mid or high and distance is close = attack CHECK
           */

            // Rule evaluation (MIN operator)
            double rule2 = Math.Min(healthLow, distanceNear);   // Fleeing CHECK
            double rule7 = Math.Min(healthLow, distanceMed);   // Fleeing CHECK

            double rule4 = Math.Min(healthLow, distanceFar);  // Idle CHECK
            double rule9 = Math.Min(healthMed, distanceFar);  // Idle CHECK
            double rule5 = Math.Min(healthHigh, distanceFar); // Idle CHECK

            double rule3 = Math.Min(healthMed, distanceMed);   // Alert CHECK
            double rule6 = Math.Min(healthHigh, distanceMed);  // Alert CHECK

            double rule8 = Math.Min(healthMed, distanceNear);   // Attacking CHECK
            double rule1 = Math.Min(healthHigh, distanceNear);  // Attacking CHECK

            // Defuzzification via Centroid (Center of Gravity)
            double sumNum = 0.0, sumDen = 0.0;
            for (double y = 0.0; y <= 100.0; y += 1.0)
            {
                double outFleeing = TriangularMembership(y, 0, 0, 25);
                double outIdle = TriangularMembership(y, 15, 30, 45);
                double outAlert = TriangularMembership(y, 35, 60, 80);
                double outAttacking = TriangularMembership(y, 70, 100, 100);

                double clipFleeing = Math.Min(Math.Max(rule2, rule7), outFleeing);
                double clipIdle = Math.Min(Math.Max(rule4, Math.Max(rule5, rule9)), outIdle);
                double clipAlert = Math.Min(Math.Max(rule3, rule6), outAlert);
                double clipAttacking = Math.Min(Math.Max(rule1, rule8), outAttacking);

                double aggY = Math.Max(clipIdle, Math.Max(clipFleeing, Math.Max(clipAlert, clipAttacking)));
                sumNum += y * aggY;
                sumDen += aggY;
            }

            double aggro = (sumDen > 0.0) ? (sumNum / sumDen) : 0.0;

            string state;
            if (aggro >= 75.0)
                state = "ATTACKING";
            else if (aggro >= 45.0)
                state = "ALERT";
            else if (aggro >= 15.0)
                state = "IDLE";
            else
                state = "FLEEING";

            return new FuzzyResult { Aggressiveness = aggro, State = state };
        }

        // Improved sir aliac TM since the first test didnt work as intended
        private static double TriangularMembership(double x, double a, double b, double c)
        {
            if (a == b && b == c) return (x == a) ? 1.0 : 0.0;
            if (a == b) return (x <= b) ? 1.0 : (x >= c) ? 0.0 : (c - x) / (c - b);
            if (b == c) return (x >= b) ? 1.0 : (x <= a) ? 0.0 : (x - a) / (b - a);
            if (x < a || x > c) return 0.0;
            if (x == b) return 1.0;
            return (x < b) ? (x - a) / (b - a) : (c - x) / (c - b);
        }
    }
}