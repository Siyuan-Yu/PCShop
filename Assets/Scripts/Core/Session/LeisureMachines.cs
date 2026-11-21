using System;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using GiftCatalog = PCShopSell.Core.Home.Gifts;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class PachinkoBall
    {
        public double X, Y, VX, VY, Age;
        public int Tier;
        public bool FallingIntoTray;
    }

    public sealed class PachinkoBoard
    {
        public static readonly PachinkoBoard[] Machines =
        {
            new PachinkoBoard("交错盘", "交错钉阵 · 单摆导板", 0,
                new[] { (.17, .13, 1), (.39, .105, 2), (.63, .073, 5), (.84, .12, 1) },
                new[] { (.49, .46, .18, .18, 1.75, .0) }),
            new PachinkoBoard("双摆盘", "两组导板交替横移", 1,
                new[] { (.16, .12, 2), (.43, .077, 5), (.66, .13, 1), (.86, .11, 1) },
                new[] { (.30, .40, .17, .08, 1.6, .0), (.68, .61, .20, .09, 2.1, 2.2) }),
            new PachinkoBoard("窄槽盘", "三组短导板 · 两侧窄槽", 2,
                new[] { (.20, .074, 5), (.43, .125, 1), (.62, .103, 2), (.81, .085, 2) },
                new[] { (.29, .35, .14, .04, 2.2, .0), (.68, .49, .16, .05, 2.5, 1.4), (.47, .67, .14, .07, 2.8, 2.8) })
        };

        public readonly string Name, Description;
        public readonly (double X, double Y)[] Pegs;
        public readonly (double X, double Width, int Reward)[] Slots;
        private readonly (double X, double Y, double Width, double Travel, double Speed, double Phase)[] _bars;
        public int BarCount => _bars.Length;
        public const double OutletX = .5, OutletY = .12, SlotY = .885, PegRadius = .008;

        private PachinkoBoard(string name, string description, int pattern, (double, double, int)[] slots,
            (double, double, double, double, double, double)[] bars)
        {
            Name = name;
            Description = description;
            Slots = slots;
            _bars = bars;
            var pegs = new List<(double, double)>();
            for (int row = 0; row < 8; row++)
            {
                double y = .26 + .073 * row;
                for (int col = 0; col < 9; col++)
                {
                    double x = .115 + .091 * col + (row % 2) * .036;
                    if (pattern == 1 && (col + row) % 4 == 0) continue;
                    if (pattern == 2 && col is >= 3 and <= 5 && row % 3 != 0) continue;
                    pegs.Add((x, y));
                }
            }
            Pegs = pegs.ToArray();
        }

        public (double X1, double Y1, double X2, double Y2) Bar(int index, double time)
        {
            var bar = _bars[index];
            double phase = time * bar.Speed + bar.Phase;
            double center = bar.X + Math.Sin(phase) * bar.Travel;
            double angle = Math.Cos(phase) * .68;
            double dx = Math.Cos(angle) * bar.Width * .5;
            double dy = Math.Sin(angle) * bar.Width * .5;
            return (center - dx, bar.Y - dy, center + dx, bar.Y + dy);
        }
    }

    [Serializable]
    public sealed class PachinkoPlay
    {
        public const double BallRadius = .011;
        private const int MaxLiveBalls = 18;
        public List<PachinkoBall> Balls = new List<PachinkoBall>();
        public int Machine, Tier, Launched, Returned, LastReward, LastSlot = -1;
        public double Aim, LaunchDelay, Accumulator, RewardFlash, BoardTime;
        // Legacy fields for refunding unspent beads from saves made before tiered beads.
        public int Beads;
        public bool BuyInPaid;

        public PachinkoBoard Board => PachinkoBoard.Machines[Machine];
        public double LaunchAngle => Aim * 1.13;
        public double LaunchX => PachinkoBoard.OutletX + Math.Sin(LaunchAngle) * .074;
        public double LaunchY => PachinkoBoard.OutletY + Math.Cos(LaunchAngle) * .074;

        internal void Step(double dt, bool held, int[] wallet)
        {
            LaunchDelay = Math.Max(0, LaunchDelay - dt);
            RewardFlash = Math.Max(0, RewardFlash - dt);
            if (held && LaunchDelay == 0 && (uint)Tier < wallet.Length && wallet[Tier] > 0 && Balls.Count < MaxLiveBalls)
            {
                double angle = LaunchAngle;
                Balls.Add(new PachinkoBall
                {
                    X = LaunchX, Y = LaunchY,
                    VX = Math.Sin(angle) * .57, VY = Math.Cos(angle) * .57, Tier = Tier
                });
                LaunchDelay = .20;
                wallet[Tier]--;
                Launched++;
            }

            Accumulator += dt;
            const double tick = 1.0 / 120;
            var board = Board;
            while (Accumulator >= tick)
            {
                BoardTime += tick;
                for (int i = Balls.Count - 1; i >= 0; i--)
                {
                    var ball = Balls[i];
                    if (ball.FallingIntoTray)
                    {
                        ball.Y += tick * .65;
                        if (ball.Y >= .977) Balls.RemoveAt(i);
                        continue;
                    }
                    StepBall(ball, board, tick);
                    if (ball.Y < PachinkoBoard.SlotY && ball.Age < 7) continue;
                    LastSlot = -1;
                    LastReward = 0;
                    RewardFlash = .85;
                    if (ball.Y >= PachinkoBoard.SlotY)
                    {
                        ball.FallingIntoTray = true;
                        ball.VX = 0;
                        for (int slot = 0; slot < board.Slots.Length; slot++)
                        {
                            var gate = board.Slots[slot];
                            if (Math.Abs(ball.X - gate.X) > gate.Width * .5 - BallRadius) continue;
                            LastSlot = slot;
                            int room = GameSession.PachinkoWalletLimit - wallet[ball.Tier];
                            LastReward = Math.Min(gate.Reward, room);
                            wallet[ball.Tier] += LastReward;
                            Returned += LastReward;
                            break;
                        }
                    }
                    else Balls.RemoveAt(i);
                }
                Accumulator -= tick;
            }
        }

        private void StepBall(PachinkoBall ball, PachinkoBoard board, double dt)
        {
            ball.Age += dt;
            ball.VY = Math.Min(1.05, ball.VY + 1.08 * dt);
            ball.VX *= 1 - .30 * dt;
            ball.X += ball.VX * dt;
            ball.Y += ball.VY * dt;
            if (ball.X < .065 || ball.X > .935)
            {
                ball.VX = -ball.VX * .78;
                ball.X = Math.Clamp(ball.X, .065, .935);
            }
            if (ball.Y < .082)
            {
                ball.Y = .082;
                ball.VY = Math.Abs(ball.VY) * .65;
            }

            foreach (var peg in board.Pegs)
                Bounce(ball, peg.X, peg.Y, BallRadius + PachinkoBoard.PegRadius);

            for (int i = 0; i < board.BarCount; i++)
            {
                var bar = board.Bar(i, BoardTime);
                double dx = bar.X2 - bar.X1;
                double dy = bar.Y2 - bar.Y1;
                double projection = Math.Clamp(((ball.X - bar.X1) * dx + (ball.Y - bar.Y1) * dy)
                    / (dx * dx + dy * dy), 0, 1);
                Bounce(ball, bar.X1 + projection * dx, bar.Y1 + projection * dy, BallRadius + .007);
            }
        }

        private static void Bounce(PachinkoBall ball, double x, double y, double radius)
        {
            double dx = ball.X - x, dy = ball.Y - y;
            double squared = dx * dx + dy * dy;
            if (squared >= radius * radius) return;
            double distance = Math.Sqrt(squared);
            if (distance < 1e-6) { dx = .2; dy = -.98; }
            else { dx /= distance; dy /= distance; }
            ball.X = x + dx * radius;
            ball.Y = y + dy * radius;
            double approach = ball.VX * dx + ball.VY * dy;
            if (approach >= 0) return;
            ball.VX -= 1.65 * approach * dx;
            ball.VY -= 1.65 * approach * dy;
            ball.VX += (dx < 0 ? -1 : 1) * .038;
        }
    }

    [Serializable]
    public sealed class GiftClawPlay
    {
        private enum Motion { Idle, Lower, Lift, Return, Release, Sweep, Grip }

        private const double StartZ = -.64, FarZ = .54, SweepSpeed = .78, OutletX = .92;
        private const double ReleaseSeconds = .29;
        private const double LiftSeconds = .78, FallSeconds = .53;

        public string[] Gifts = new string[6];
        public double[] GiftX, GiftZ;
        public int Attempts, PrizesWon, Target = -1;
        public double X, Z = StartZ, Height = 1, Time;
        public double CarryTime, SlipAt, GripX, GripZ, GripQuality;
        public double SlipProgress = 1, FallX, FallZ, FallHeight;
        public int Phase, RoundVersion;
        public bool HeldPrize, WillHold, WillSlip;
        public string Message = "先左右对准，按空格投币。";
        private Motion Stage { get => (Motion)Phase; set => Phase = (int)value; }
        public bool Animating => Stage != Motion.Idle;
        public bool Sweeping => Stage == Motion.Sweep;
        public bool Releasing => Stage == Motion.Release;
        public bool HasGifts => Array.Exists(Gifts, gift => !string.IsNullOrEmpty(gift));
        public double FingerClosure => Stage switch
        {
            Motion.Grip => Math.Clamp(Time / .22, 0, 1),
            Motion.Lift or Motion.Return => HeldPrize ? .72 : .88,
            Motion.Release => Math.Max(0, 1 - Time / .11),
            _ => 0
        };

        public double Sway => HeldPrize && !Releasing
            ? Math.Sin(CarryTime * 12) * (.09 - .065 * GripQuality)
            : 0;
        public double HoldX => X + GripX * .64 + Sway;
        public double HoldZ => Z + GripZ * .64;
        public double ReleaseProgress => Releasing ? Math.Clamp(Time / ReleaseSeconds, 0, 1) : 0;

        public (double X, double Z) GiftPosition(int slot) => GiftX != null && GiftZ != null
            && slot < GiftX.Length && slot < GiftZ.Length
                ? (GiftX[slot], GiftZ[slot])
                : ((slot % 3 - 1) * .78, slot < 3 ? -.42 : .37);

        internal void Move(double dx)
        {
            if (Animating) return;
            X = Math.Clamp(X + dx, -1.10, 1.10);
        }

        internal void Begin()
        {
            Attempts++;
            Target = -1;
            Time = CarryTime = 0;
            Height = SlipProgress = 1;
            Z = StartZ;
            HeldPrize = WillHold = WillSlip = false;
            GripX = GripZ = GripQuality = 0;
            RoundVersion = 1;
            Stage = Motion.Sweep;
            Message = "抓爪正在前进，按空格落爪。";
        }

        internal bool Drop()
        {
            if (!Sweeping) return false;
            Time = 0;
            Stage = Motion.Lower;
            Message = "抓爪正在下降。";
            return true;
        }

        internal void RestoreRound()
        {
            if (RoundVersion != 0) return;
            if (!Animating)
            {
                Z = StartZ;
                Height = 1;
                Message = "先左右对准，按空格投币。";
                return;
            }
            SlipProgress = 1;
            // Finish a paid legacy attempt using its saved hold and slip result.
            SlipAt = WillSlip ? .42 : -1;
            if (Stage == Motion.Release) SlipAt = -1;
            RoundVersion = 1;
        }

        internal string Step(double dt, GameRandom random)
        {
            if (!Animating) return null;
            if (SlipProgress < 1) SlipProgress = Math.Min(1, SlipProgress + dt / FallSeconds);
            Time += dt;
            switch (Stage)
            {
                case Motion.Sweep:
                    Z = Math.Min(FarZ, Z + dt * SweepSpeed);
                    if (Z >= FarZ) Drop();
                    break;
                case Motion.Lower:
                    Height = Math.Max(0, 1 - Time / .53);
                    if (Time < .53) break;
                    if (Target < 0) JudgeGrip(random);
                    Time = 0;
                    Stage = Motion.Grip;
                    break;
                case Motion.Grip:
                    if (Time < .22) break;
                    HeldPrize = WillHold;
                    Time = CarryTime = 0;
                    Stage = Motion.Lift;
                    Message = HeldPrize ? "夹到了，正在提起。" : "抓爪夹空了。";
                    break;
                case Motion.Lift:
                    Height = Math.Min(1, Time / LiftSeconds);
                    CarryTime += dt;
                    CheckSlip();
                    if (Time < LiftSeconds) break;
                    Time = 0;
                    Stage = Motion.Return;
                    break;
                case Motion.Return:
                    X = Approach(X, OutletX, dt * 1.6);
                    Z = Approach(Z, StartZ, dt * 1.8);
                    CarryTime += dt;
                    CheckSlip();
                    if (X != OutletX || Z != StartZ || SlipProgress < 1) break;
                    if (!HeldPrize || Target < 0) { FinishRound(); break; }
                    Time = 0;
                    Stage = Motion.Release;
                    Message = "奖品掉向取物口。";
                    break;
                case Motion.Release:
                    if (Time < ReleaseSeconds) break;
                    string gift = Gifts[Target];
                    Gifts[Target] = null;
                    FinishRound();
                    return gift;
            }
            return null;
        }

        private void JudgeGrip(GameRandom random)
        {
            double best = 1;
            for (int i = 0; i < Gifts.Length; i++)
            {
                if (string.IsNullOrEmpty(Gifts[i])) continue;
                var prize = GiftPosition(i);
                double dx = (X - prize.X) / .33;
                double dz = (Z - prize.Z) / .28;
                double error = dx * dx + dz * dz;
                if (error >= best) continue;
                best = error;
                Target = i;
                GripX = prize.X - X;
                GripZ = prize.Z - Z;
            }
            if (Target < 0) return;

            double precision = 1 - Math.Sqrt(best);
            double drag = Gifts[Target] switch { GiftCatalog.Soda => .10, GiftCatalog.Snack => -.07, _ => .03 };
            GripQuality = Math.Clamp(.16 + .82 * precision - drag + random.Range(-.13, .13), 0, 1);
            WillHold = GripQuality >= .27;
            double slipChance = Math.Clamp(.65 - GripQuality * .46 + drag, .12, .76);
            SlipAt = WillHold && random.Chance(slipChance)
                ? random.Range(.32, 1.16) + GripQuality * .08
                : -1;
        }

        private void CheckSlip()
        {
            if (!HeldPrize || SlipAt < 0 || CarryTime < SlipAt) return;
            FallX = HoldX;
            FallZ = HoldZ;
            FallHeight = Height;
            HeldPrize = false;
            SlipProgress = 0;
            MoveFallenGift();
            Message = "没夹稳，奖品滑回柜底了。";
        }

        private void MoveFallenGift()
        {
            var landing = GiftPosition(Target);
            double best = double.PositiveInfinity;
            for (int row = 0; row < 3; row++)
            for (int column = 0; column < 6; column++)
            {
                double x = -1.03 + column * .412;
                double z = -.51 + row * .51;
                double dx = x - FallX, dz = z - FallZ;
                double dist = dx * dx + dz * dz;
                if (dist >= best) continue;
                bool occupied = false;
                for (int i = 0; i < Gifts.Length; i++)
                {
                    if (i == Target || string.IsNullOrEmpty(Gifts[i])) continue;
                    var other = GiftPosition(i);
                    if (Math.Abs(x - other.X) < .41 && Math.Abs(z - other.Z) < .36) { occupied = true; break; }
                }
                if (occupied) continue;
                best = dist;
                landing = (x, z);
            }
            if (GiftX?.Length != Gifts.Length || GiftZ?.Length != Gifts.Length)
            {
                var xs = new double[Gifts.Length];
                var zs = new double[Gifts.Length];
                for (int i = 0; i < Gifts.Length; i++) (xs[i], zs[i]) = GiftPosition(i);
                GiftX = xs;
                GiftZ = zs;
            }
            GiftX[Target] = landing.X;
            GiftZ[Target] = landing.Z;
        }

        private void FinishRound()
        {
            Stage = Motion.Idle;
            Target = -1;
            HeldPrize = false;
        }

        private static double Approach(double from, double to, double delta) =>
            Math.Abs(to - from) <= delta ? to : from + Math.Sign(to - from) * delta;
    }
}
