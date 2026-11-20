using System;

namespace PCShopSell.Core.Session
{
    public sealed partial class OnsitePlayState
    {
        internal const int FootballPenaltyNumber = 1001;
        public const float FootballGoalHalfWidth = 2.72f;
        public const float FootballGoalHeight = 1.95f;
        public const float FootballGoalDistance = 8.3f;
        public const float FootballBallRadius = .16f;
        public const float FootballAimHeight = 1.05f;

        public const double FootballGoalLineTime = .64;
        public const double FootballShotSeconds = .91;
        internal const double FootballResetSeconds = .65;

        public bool IsFootballPenalty => Number == FootballPenaltyNumber;
        public bool FootballPreparing => Phase is "ready" or "charging"
            || Phase == "result" && Flight >= FootballShotSeconds + FootballResetSeconds;

        public double FootballAimX;
    }

    public sealed partial class GameSession
    {
        public bool AimFootballPenalty(double x)
        {
            var shot = Play;
            if (!OnsiteHere || shot?.IsFootballPenalty != true || !shot.FootballPreparing
                || shot.Phase == "charging" || ManualAttemptsLeft == 0 || double.IsNaN(x)) return false;
            shot.FootballAimX = Math.Max(-OnsitePlayState.FootballGoalHalfWidth,
                Math.Min(OnsitePlayState.FootballGoalHalfWidth, x));
            return true;
        }

        private static void ResetFootballBall(OnsitePlayState shot)
        {
            shot.Power = shot.Flight = shot.BallX = shot.BallZ = 0;
            shot.BallY = OnsitePlayState.FootballBallRadius;
        }

        private bool BeginFootballCharge(OnsitePlayState shot)
        {
            if (!shot.FootballPreparing || ManualAttemptsLeft == 0) return false;
            ResetFootballBall(shot);
            shot.Phase = "charging";
            shot.LastSportResult = OnsiteSportResult.Unrecorded;
            shot.Message = "蓄力中，松手射门。";
            return true;
        }

        private static bool ReleaseFootballShot(OnsitePlayState shot)
        {
            const double goal = OnsitePlayState.FootballGoalDistance;
            const double radius = OnsitePlayState.FootballBallRadius;
            bool shortShot = shot.Power < .24;
            shot.LandingZ = shortShot ? goal * (.28 + .68 * shot.Power / .24) : goal;
            shot.LandingX = shot.FootballAimX * shot.LandingZ / goal;
            shot.Distance = shortShot ? radius : radius + 2.15 * shot.Power * shot.Power;
            shot.ShotOriginX = shot.DefenderX;

            bool inside = !shortShot
                && Math.Abs(shot.LandingX) < OnsitePlayState.FootballGoalHalfWidth - radius
                && shot.Distance < OnsitePlayState.FootballGoalHeight - radius;
            double reaction = Math.Max(-.43, Math.Min(.43, shot.LandingX - shot.ShotOriginX));
            bool saved = inside && Math.Abs(shot.LandingX - (shot.ShotOriginX + reaction)) < .65
                && shot.Distance < 1.70;
            shot.LastSportResult = !inside ? OnsiteSportResult.Miss
                : saved ? OnsiteSportResult.Blocked : OnsiteSportResult.Success;
            shot.Phase = "flight";
            shot.Flight = 0;
            shot.Message = "球已踢出。";
            return true;
        }

        private void StepFootballPenalty(OnsitePlayState shot, double seconds)
        {
            if (shot.Phase == "charging")
            {
                shot.Flight += seconds;
                shot.Power = 1 - Math.Abs(shot.Flight % 2 - 1);
                return;
            }

            if (shot.Phase == "flight")
            {
                StepFootballFlight(shot, seconds);
                if (shot.Flight < OnsitePlayState.FootballShotSeconds) return;
                shot.Phase = "result";
                shot.Round++;
                if (shot.LastSportResult == OnsiteSportResult.Success) shot.Success++;
                Player.SpendEnergy(1);
                shot.Message = shot.LastSportResult switch
                {
                    OnsiteSportResult.Success => "进球！",
                    OnsiteSportResult.Blocked => "门将扑住了。",
                    _ when shot.LandingZ < OnsitePlayState.FootballGoalDistance => "球没到门前。",
                    _ when shot.Distance >= OnsitePlayState.FootballGoalHeight - OnsitePlayState.FootballBallRadius => "踢高了。",
                    _ => "踢偏了。"
                };
                return;
            }

            if (shot.Phase == "result" && shot.Round >= CityLifeCatalog.FootballPenaltyShots) return;
            if (shot.Phase == "result" && !shot.FootballPreparing)
            {
                shot.Flight += seconds;
                if (shot.FootballPreparing)
                {
                    shot.BallX = shot.BallZ = 0;
                    shot.BallY = OnsitePlayState.FootballBallRadius;
                    shot.Power = 0;
                }
                return;
            }

            shot.Time += seconds;
            shot.DefenderX = 1.55 * Math.Sin(shot.Time * 1.65 - .8);
        }

        private static void StepFootballFlight(OnsitePlayState shot, double seconds)
        {
            const double cross = OnsitePlayState.FootballGoalLineTime;
            const double end = OnsitePlayState.FootballShotSeconds;
            const double radius = OnsitePlayState.FootballBallRadius;
            const double goal = OnsitePlayState.FootballGoalDistance;
            shot.Flight = Math.Min(end, shot.Flight + seconds);
            double travel = Math.Min(1, shot.Flight / cross);
            double settle = Math.Max(0, (shot.Flight - cross) / (end - cross));
            double reaction = Math.Max(-.43, Math.Min(.43, shot.LandingX - shot.ShotOriginX));
            shot.DefenderX = shot.ShotOriginX + reaction * travel;
            shot.BallX = shot.LandingX * travel;
            shot.BallY = radius + (shot.Distance - radius) * (travel - settle)
                + (shot.LandingZ < goal ? .16 : .32) * Math.Sin(Math.PI * travel);
            double bounce = shot.LastSportResult switch
            {
                OnsiteSportResult.Blocked => -.95,
                OnsiteSportResult.Success => .56,
                _ when shot.LandingZ < goal => .12,
                _ => 1.25
            };
            shot.BallZ = shot.LandingZ * travel + bounce * settle;
        }
    }
}