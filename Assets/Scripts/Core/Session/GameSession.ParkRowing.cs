using System;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class ParkRowingState
    {
        public string ActivityId;
        public double Position, Furthest, Speed, BeatElapsed, StrokeCooldown;
        public double ReturnTarget;
        public bool NextRight, Returning, AtDock;
        public int Strokes;
        public string Reply;

        public ParkRowingState Copy() => (ParkRowingState)MemberwiseClone();
    }

    public sealed partial class GameSession
    {
        public const string CommunityParkId = "life_community_park";
        public const string ParkRowingActivityId = "boat";
        public const double ParkRowingBeatSeconds = 1.15;
        public const double ParkRowingCueStart = .35;
        public const double ParkRowingCueEnd = .92;
        private ParkRowingState _parkRowing;

        public static bool IsParkRowing(CityActivity activity) => activity?.DefinitionId == ParkRowingActivityId;
        private static bool IsParkWalkService(string id) => id is "hike" or "park_walk";
        public static bool IsParkWalk(CityActivityDefinition service) => IsParkWalkService(service?.Id);
        public static bool IsParkWalkActivity(CityActivity activity) => activity != null
            && IsParkWalkService(string.IsNullOrEmpty(activity.ServiceDefinitionId) ? activity.DefinitionId : activity.ServiceDefinitionId);
        public bool ParkRowingActive => _parkRowing != null && RunningCityActivity?.Id == _parkRowing.ActivityId
            && CurrentPlace?.Id == CommunityParkId && !IsGameOver && !_sleeping && !IsWorking;
        public ParkRowingState ParkRowing => ParkRowingActive ? _parkRowing.Copy() : null;

        private void StartParkRowing(CityActivity activity)
        {
            if (!IsParkRowing(activity) || _parkRowing?.ActivityId == activity.Id) return;
            _parkRowing = new ParkRowingState
            {
                ActivityId = activity.Id,
                BeatElapsed = ParkRowingCueStart * ParkRowingBeatSeconds
            };
        }

        public bool StrokeParkRowing(bool right)
        {
            if (!ParkRowingActive || _parkRowing.AtDock || _parkRowing.Returning || _parkRowing.StrokeCooldown > 0)
                return false;

            if (right != _parkRowing.NextRight)
            {
                _parkRowing.Reply = _parkRowing.NextRight ? "这一桨换右边。" : "这一桨换左边。";
                return false;
            }

            double phase = _parkRowing.BeatElapsed / ParkRowingBeatSeconds;
            bool timely = phase >= ParkRowingCueStart && phase <= ParkRowingCueEnd;
            _parkRowing.Speed = Math.Min(.125, _parkRowing.Speed + (timely ? .087 : .052));
            _parkRowing.NextRight = !_parkRowing.NextRight;
            _parkRowing.Strokes++;
            _parkRowing.StrokeCooldown = .32;
            _parkRowing.BeatElapsed = 0;
            _parkRowing.Reply = timely ? "船顺着水滑了出去。" : "轻轻划也行，等船滑一会儿。";
            return true;
        }

        public void StepParkRowing(double seconds)
        {
            if (!ParkRowingActive || _parkRowing.AtDock || !double.IsFinite(seconds) || seconds <= 0) return;
            double dt = Math.Min(.12, seconds);
            if (_parkRowing.Returning)
            {
                double remaining = _parkRowing.ReturnTarget - _parkRowing.Position;
                _parkRowing.Position += Math.Sign(remaining) * Math.Min(Math.Abs(remaining), .13 * dt);
                if (Math.Abs(_parkRowing.Position - _parkRowing.ReturnTarget) < 1e-6) ParkRowingDocked();
                return;
            }

            _parkRowing.BeatElapsed = (_parkRowing.BeatElapsed + dt) % ParkRowingBeatSeconds;
            _parkRowing.StrokeCooldown = Math.Max(0, _parkRowing.StrokeCooldown - dt);
            _parkRowing.Position = Math.Min(1, _parkRowing.Position + _parkRowing.Speed * dt);
            _parkRowing.Speed *= Math.Exp(-1.52 * dt);
            _parkRowing.Furthest = Math.Max(_parkRowing.Furthest, _parkRowing.Position);
            if (_parkRowing.Position >= 1) ParkRowingDocked();
        }

        public void ReturnParkRowingToDock()
        {
            if (!ParkRowingActive || _parkRowing.AtDock || _parkRowing.Returning) return;
            if (_parkRowing.Position <= .001) { ParkRowingDocked(); return; }
            _parkRowing.Returning = true;
            _parkRowing.ReturnTarget = _parkRowing.Position < .5 ? 0 : 1;
            _parkRowing.Reply = "收好桨，慢慢靠岸。";
        }

        private void ParkRowingDocked()
        {
            _parkRowing.AtDock = true;
            _parkRowing.Speed = 0;
            _parkRowing.Reply = "船靠稳了。";
        }

        public bool FinishParkRowing()
        {
            var activity = RunningCityActivity;
            if (!ParkRowingActive || !_parkRowing.AtDock) return false;
            return SettleParkRowing(activity);
        }

        private bool SettleParkRowing(CityActivity activity)
        {
            var state = _parkRowing;
            bool tookBoatOut = state?.ActivityId == activity.Id && state.Strokes > 0;
            int minutes = tookBoatOut ? Math.Min(activity.Duration,
                15 + (int)Math.Round(Math.Max(0, activity.Duration - 15) * state.Furthest)) : 0;
            return SettleManualActivity(activity, minutes,
                tookBoatOut ? CityActivityPhase.Completed : CityActivityPhase.Interrupted,
                tookBoatOut ? "已靠岸还船" : "还了船，今天先在岸边走走");
        }

        private void SaveParkRowing(SaveData data) => data.parkRowing = _parkRowing?.Copy();

        private void RestoreParkRowing(SaveData data)
        {
            _parkRowing = data.parkRowing?.Copy();
            var activity = RunningCityActivity;
            if (IsParkRowing(activity)) StartParkRowing(activity);
        }
    }

    public sealed partial class SaveData
    {
        public ParkRowingState parkRowing;
    }
}
