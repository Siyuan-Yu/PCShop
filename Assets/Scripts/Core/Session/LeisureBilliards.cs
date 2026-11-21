using System;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class BilliardsBall
    {
        public double X, Y, VX, VY;
        public bool Pocketed;
        public BilliardsBall(double x, double y) { X = x; Y = y; }
    }

    [Serializable]
    public sealed class BilliardsPlay
    {
        public const double Width = 2, Height = 1, Radius = .027, PocketRadius = .077;
        public static readonly (double X, double Y)[] Pockets = {
            (0,0), (1,0), (2,0), (0,1), (1,1), (2,1)
        };

        public BilliardsBall[] Balls = {
            new BilliardsBall(.46,.55), new BilliardsBall(1.36,.32), new BilliardsBall(1.50,.73),
            new BilliardsBall(.83,.26), new BilliardsBall(1.72,.40), new BilliardsBall(1.13,.67)
        };
        public string Message = "移动鼠标瞄准，按住蓄力，松手出杆。", OpponentName = "球友";
        public int PlayerScore, OpponentScore, Shots;
        public double AimX = 1.36, AimY = .32, Power, Accumulator, Waiting, RollTime;
        public bool OpponentTurn, Rolling, Charging, Finished, Scratch;
        public int PocketedThisShot;
        public bool CanShoot => !Rolling && !OpponentTurn && !Finished;

        internal void Aim(double x, double y)
        {
            if (CanShoot)
            {
                AimX = x;
                AimY = y;
            }
        }

        internal void Step(double dt, bool held)
        {
            if (Finished) return;
            if (Rolling)
            {
                RollTime += dt;
                Accumulator += dt;
                const double tick = 1.0 / 180;
                while (Accumulator >= tick)
                {
                    Simulate(tick);
                    Accumulator -= tick;
                }
                if (RollTime >= 7 || !AnyMoving()) EndStroke();
                return;
            }
            if (OpponentTurn)
            {
                Waiting += dt;
                if (Waiting >= .75) OpponentStroke();
                return;
            }
            if (held)
            {
                Charging = true;
                Power = Math.Min(1, Power + dt * .83);
                return;
            }
            if (!Charging) return;
            Charging = false;
            var cue = Balls[0];
            double x = AimX - cue.X, y = AimY - cue.Y;
            if (x * x + y * y < .0001) { Power = 0; return; }
            Shots++;
            Stroke(x, y, .75 + Power * 3.6);
        }

        private void Stroke(double x, double y, double speed)
        {
            double length = Math.Sqrt(x * x + y * y);
            Balls[0].VX = speed * x / length;
            Balls[0].VY = speed * y / length;
            Rolling = true;
            Scratch = false;
            PocketedThisShot = 0;
            Accumulator = RollTime = 0;
            Message = "等球停稳。";
        }

        private void OpponentStroke()
        {
            var cue = Balls[0];
            BilliardsBall closest = null;
            double shortest = double.MaxValue;
            for (int i = 1; i < Balls.Length; i++)
            {
                var ball = Balls[i];
                if (ball.Pocketed) continue;
                double d = Math.Pow(ball.X - cue.X, 2) + Math.Pow(ball.Y - cue.Y, 2);
                if (d >= shortest) continue;
                closest = ball;
                shortest = d;
            }
            if (closest == null) { Finished = true; return; }
            (double X,double Y) pocket = Pockets[0];
            double distance = double.MaxValue;
            foreach (var candidate in Pockets)
            {
                double d = Math.Pow(candidate.X - closest.X, 2) + Math.Pow(candidate.Y - closest.Y, 2);
                if (d >= distance) continue;
                pocket = candidate;
                distance = d;
            }
            double length = Math.Max(.04, Math.Sqrt(distance));
            double x = closest.X - cue.X - 2 * Radius * (pocket.X - closest.X) / length;
            double y = closest.Y - cue.Y - 2 * Radius * (pocket.Y - closest.Y) / length;
            if (x * x + y * y < .00001) x = .1;
            Stroke(x, y, 2.8);
        }

        private void Simulate(double dt)
        {
            for (int i = 0; i < Balls.Length; i++)
            {
                var ball = Balls[i];
                if (ball.Pocketed) continue;
                ball.X += ball.VX * dt;
                ball.Y += ball.VY * dt;
                ball.VX *= 1 - dt * 1.43;
                ball.VY *= 1 - dt * 1.43;
                foreach (var pocket in Pockets)
                {
                    double x = ball.X - pocket.X, y = ball.Y - pocket.Y;
                    if (x * x + y * y > PocketRadius * PocketRadius) continue;
                    ball.Pocketed = true;
                    ball.VX = ball.VY = 0;
                    if (i == 0) Scratch = true;
                    else PocketedThisShot++;
                    break;
                }
                if (ball.Pocketed) continue;
                if (ball.X < Radius || ball.X > Width - Radius)
                {
                    ball.X = Math.Clamp(ball.X, Radius, Width - Radius);
                    ball.VX *= -.82;
                }
                if (ball.Y < Radius || ball.Y > Height - Radius)
                {
                    ball.Y = Math.Clamp(ball.Y, Radius, Height - Radius);
                    ball.VY *= -.82;
                }
            }
            for (int i = 0; i < Balls.Length; i++)
            for (int j = i + 1; j < Balls.Length; j++)
            {
                var a = Balls[i];
                var b = Balls[j];
                if (a.Pocketed || b.Pocketed) continue;
                double x = b.X - a.X, y = b.Y - a.Y;
                double distance = Math.Sqrt(x * x + y * y);
                if (distance >= 2 * Radius || distance < 1e-7) continue;
                x /= distance; y /= distance;
                double overlap = Radius - distance / 2;
                a.X -= x * overlap; a.Y -= y * overlap;
                b.X += x * overlap; b.Y += y * overlap;
                double impulse = ((a.VX - b.VX) * x + (a.VY - b.VY) * y) * .96;
                if (impulse <= 0) continue;
                a.VX -= impulse * x; a.VY -= impulse * y;
                b.VX += impulse * x; b.VY += impulse * y;
            }
        }

        private bool AnyMoving()
        {
            foreach (var ball in Balls)
                if (!ball.Pocketed && ball.VX * ball.VX + ball.VY * ball.VY > .0011) return true;
            return false;
        }

        private void EndStroke()
        {
            Rolling = false;
            Waiting = Power = 0;
            foreach (var ball in Balls) ball.VX = ball.VY = 0;
            if (Scratch) ResetCue();
            if (OpponentTurn) OpponentScore += PocketedThisShot;
            else PlayerScore += PocketedThisShot;
            if (PlayerScore >= 3 || OpponentScore >= 3)
            {
                Finished = true;
                Message = PlayerScore > OpponentScore ? "你先打进三球。" : OpponentName + "先打进三球。";
                return;
            }
            if (Scratch || PocketedThisShot == 0) OpponentTurn = !OpponentTurn;
            string next = OpponentTurn ? "轮到" + OpponentName + "。" : "轮到你。";
            Message = Scratch ? "白球入袋，已重摆白球。" + next : next;
        }

        private void ResetCue()
        {
            var cue = Balls[0];
            cue.Pocketed = false;
            cue.X = .42;
            cue.Y = .5;
            for (int i = 0; i < 9; i++)
            {
                bool free = true;
                for (int j = 1; j < Balls.Length; j++)
                {
                    var ball = Balls[j];
                    if (ball.Pocketed || Math.Pow(ball.X - cue.X, 2) + Math.Pow(ball.Y - cue.Y, 2) > 4.2 * Radius * Radius) continue;
                    free = false;
                    break;
                }
                if (free) break;
                cue.Y = .18 + i * .075;
            }
        }
    }
}
