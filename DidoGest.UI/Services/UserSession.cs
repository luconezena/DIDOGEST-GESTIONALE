using DidoGest.Core.Entities;

namespace DidoGest.UI.Services;

public static class UserSession
{
    public static UtenteSistema? CurrentUser { get; private set; }

    public static void Set(UtenteSistema user)
    {
        CurrentUser = user;
    }

    public static void Clear()
    {
        CurrentUser = null;
    }
}
