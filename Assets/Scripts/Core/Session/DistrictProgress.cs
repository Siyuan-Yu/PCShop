using System;

namespace PCShopSell.Core.Session
{
    // The debug facts are intentionally separate from completed story evidence and access grants.
    [Serializable]
    public sealed class DistrictProgress
    {
        public int Version = 1, UnlockedTier, Chapter;
        public string NodeId, MeetingNodeId, MeetingPlaceId, ProjectShopId, ProjectPartnerId;
        public string DebugIssuerId, DebugSectorId;
        public long DebugSectorRevenueCents, DebugSharePriceCents;
        public long MeetingAt, WorkMinutes, WorkStartedAt, RepairReadyAt, ProjectStartedAt;
        public bool HasModel, ModelDelivered, RepairPaid, DebugListedIssuer, DebugQualifiedMonth;
        public int QualifiedMonth;
        public int PendingChapter;
        public long InvitationDueAt;
        public bool InvitationWaitingForWake;
        public bool BrandCommerceRoute;
        public int CommerceVersion;
        public string CommerceBrandId;
        public string[] TaskEvidence = Array.Empty<string>();
        // Retained only for migration of the superseded trading assignment.
        public string CommunityTrialShopId;
        public int CommunityTrialSalesStart;
        public bool CommunityTrialStarted, CommunityTrialNotified;
        public bool CommunityMapQuest, HasCommunityMap, CommunityMapDelivered;
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
            copy.TaskEvidence = (string[])(TaskEvidence ?? Array.Empty<string>()).Clone();
            return copy;
        }
    }

    public sealed partial class SaveData
    {
        public DistrictProgress districtProgress;
    }
}
