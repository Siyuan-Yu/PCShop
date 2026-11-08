using System;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class DistrictProgress
    {
        public int Version = 1, UnlockedTier, Chapter;
        public string NodeId, MeetingNodeId, MeetingPlaceId;
        public long MeetingAt;
        public int PendingChapter;
        public long InvitationDueAt;
        public bool InvitationWaitingForWake;
        public string CommerceBrandId;
        public bool CommunityMapQuest, CommunityMapDelivered;
        public int CommunityBoxesInspected;
        public int CommunitySearchVersion, CommunityClues, CommunityPieces, CommunityOutings;
        public long CommunityAcceptedAt;
        public string[] Completed = Array.Empty<string>(), Choices = Array.Empty<string>(), MeetingPeople = Array.Empty<string>();
        public DistrictProgress Copy()
        {
            var copy = (DistrictProgress)MemberwiseClone();
            copy.Completed = (string[])(Completed ?? Array.Empty<string>()).Clone();
            copy.Choices = (string[])(Choices ?? Array.Empty<string>()).Clone();
            copy.MeetingPeople = (string[])(MeetingPeople ?? Array.Empty<string>()).Clone();
            return copy;
        }
    }

    public sealed partial class SaveData
    {
        public DistrictProgress districtProgress;
    }
}
