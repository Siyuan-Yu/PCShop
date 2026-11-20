using System;
using System.Collections.Generic;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class PachinkoBall
    {
        public double X, Y, VX, VY, Age;
    }

    [Serializable]
    public sealed class PachinkoPlay
    {
        public const int MaxLiveBalls = 18;
        public const double RewardWidth = .092;
        public List<PachinkoBall> Balls = new List<PachinkoBall>();
        public int Machine, Beads, Launched, Returned, LastReward;
        public double Power, LaunchDelay, Accumulator, RewardFlash;
        public bool BuyInPaid;

        public static double PegX(int row, int column) => .11 + .085 * column + row % 2 * .038;
        public static double PegY(int row) => .238 + .061 * row;

        internal void Step(double dt, bool held, int reward, GameRandom random)
        {
            LaunchDelay = Math.Max(0, LaunchDelay - dt);
            RewardFlash = Math.Max(0, RewardFlash - dt);
            if (held && LaunchDelay == 0 && Beads > 0 && Balls.Count < MaxLiveBalls)
            {
                // The launch strength shifts the entry point and the horizontal speed through the first row.
                Balls.Add(new PachinkoBall { X = .78 - .55 * Power + random.Range(-.055, .055), Y = .095,
                    VX = -.06 - .12 * Power + random.Range(-.10, .10), VY = .14 + .18 * Power });
                LaunchDelay = .145;
                Beads--;
                Launched++;
            }
            Accumulator += dt;
            const double tick = 1.0 / 120;
            while (Accumulator >= tick)
            {
                for (int i = Balls.Count - 1; i >= 0; i--)
                {
                    var ball = Balls[i];
                    Drop(ball, tick);
                    if (ball.Y < .94 && ball.Age < 7) continue;
                    int payout = ball.Y >= .94 && Math.Abs(ball.X - .5) < RewardWidth / 2 ? reward : 0;
                    if (payout > 0)
                    {
                        Beads += payout;
                        Returned += payout;
                        LastReward = payout;
                        RewardFlash = .8;
                    }
                    Balls.RemoveAt(i);
                }
                Accumulator -= tick;
            }
        }

        private static void Drop(PachinkoBall ball, double dt)
        {
            ball.Age += dt;
            ball.VY = Math.Min(.80, ball.VY + .92 * dt);
            ball.VX *= 1 - .34 * dt;
            ball.X += ball.VX * dt;
            ball.Y += ball.VY * dt;
            if (ball.X < .062 || ball.X > .938)
            {
                ball.VX = -ball.VX * .75;
                ball.X = Math.Clamp(ball.X, .062, .938);
            }
            const double radius = .028;
            int nearestRow = (int)Math.Round((ball.Y - PegY(0)) / .061);
            for (int row = Math.Max(0, nearestRow - 1); row <= Math.Min(9, nearestRow + 1); row++)
            for (int col = 0; col < 10; col++)
            {
                double dx = ball.X - PegX(row, col), dy = ball.Y - PegY(row);
                double squared = dx * dx + dy * dy;
                if (squared >= radius * radius || squared < 1e-10) continue;
                double distance = Math.Sqrt(squared);
                dx /= distance;
                dy /= distance;
                ball.X += dx * (radius - distance);
                ball.Y += dy * (radius - distance);
                double approach = ball.VX * dx + ball.VY * dy;
                if (approach >= 0) continue;
                ball.VX -= 1.57 * approach * dx;
                ball.VY -= 1.57 * approach * dy;
                // A gentle lateral nudge releases a bead balanced on a peg.
                ball.VX += (dx < 0 ? -1 : 1) * .042;
            }
        }
    }

    [Serializable]
    public sealed class GiftClawPlay
    {
        public string[] Gifts = new string[6];
        public int Attempts, PrizesWon, Target = -1;
        public double X, Z, Height = 1, Time;
        public double SlipProgress = 1;
        public int Phase;
        public bool HeldPrize, WillHold, WillSlip;
        public string Message = "移动抓爪，投币落爪。";
        public bool Animating => Phase != 0;
        public bool HasGifts => Array.Exists(Gifts, gift => !string.IsNullOrEmpty(gift));

        public static (double X, double Z) GiftPosition(int slot) => ((slot % 3 - 1) * .78, slot < 3 ? -.42 : .37);

        internal void Move(double dx, double dz)
        {
            if (Animating) return;
            X = Math.Clamp(X + dx, -1.10, 1.10);
            Z = Math.Clamp(Z + dz, -.65, .65);
        }

        internal void Drop(GameRandom random)
        {
            Attempts++;
            Time = 0;
            Phase = 1;
            Target = -1;
            HeldPrize = false;
            SlipProgress = 1;
            double best = .34;
            for (int i = 0; i < Gifts.Length; i++)
            {
                if (string.IsNullOrEmpty(Gifts[i])) continue;
                var (x, z) = GiftPosition(i);
                double error = Math.Sqrt((X - x) * (X - x) + (Z - z) * (Z - z));
                if (error >= best) continue;
                best = error;
                Target = i;
            }
            WillHold = Target >= 0 && (best <= .12 || random.Chance(1 - best / .38));
            WillSlip = WillHold && best > .16 && random.Chance(.35);
            Message = "抓爪正在下降。";
        }

        internal string Step(double dt)
        {
            if (!Animating) return null;
            if (SlipProgress < 1) SlipProgress = Math.Min(1, SlipProgress + dt / .29);
            Time += dt;
            if (Phase == 1)
            {
                Height = 1 - Math.Min(1, Time / .58);
                if (Time < .58) return null;
                Time = 0;
                Phase = 2;
                HeldPrize = WillHold;
                Message = HeldPrize ? "夹到了，正在提起来。" : "抓爪夹空了。";
            }
            else if (Phase == 2)
            {
                Height = Math.Min(1, Time / .73);
                if (WillSlip && Height > .55 && HeldPrize)
                {
                    HeldPrize = false;
                    SlipProgress = 0;
                    Message = "礼物滑回柜底了。";
                }
                if (Time < .73) return null;
                Time = 0;
                Phase = 3;
            }
            else if (Phase == 3)
            {
                X = Approach(X, .92, dt * 2.5);
                Z = Approach(Z, -.64, dt * 2.5);
                if (X != .92 || Z != -.64) return null;
                if (!HeldPrize || Target < 0) { Phase = 0; Target = -1; return null; }
                Time = 0;
                Phase = 4;
                Message = "抓爪打开，奖品掉向取物口。";
            }
            else if (Time >= .29)
            {
                Phase = 0;
                string gift = Gifts[Target];
                Gifts[Target] = null;
                HeldPrize = false;
                Target = -1;
                return gift;
            }
            return null;
        }

        private static double Approach(double from, double to, double delta) =>
            Math.Abs(to - from) <= delta ? to : from + Math.Sign(to - from) * delta;
    }
}
