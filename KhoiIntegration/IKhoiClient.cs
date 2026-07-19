namespace KhoiIntegration
{
    public interface IKhoiClient
    {
        // externalAthleteId is Khoi Cloud's own athlete identifier, not our AspNetUsers.Id —
        // see hardware-integration/docs/khoi-api-contract.md for the (unconfirmed) mapping gap.
        Task<KhoiCloudResponse?> GetLatestReadingsAsync(string externalAthleteId, CancellationToken ct = default);
    }
}
