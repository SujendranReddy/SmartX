using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class ActivityService
{
    private readonly Queue<UserActivity> history = new();
    private readonly object syncRoot = new();
    private const int HistoryLimit = 500;

    public void Record(UserActivity activity)
    {
        lock (syncRoot)
        {
            history.Enqueue(activity);

            while (history.Count > HistoryLimit)
            {
                history.Dequeue();
            }
        }
    }

    public UserActivity[] GetHistory()
    {
        lock (syncRoot)
        {
            return history.ToArray();
        }
    }
}