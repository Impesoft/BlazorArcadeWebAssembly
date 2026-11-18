using Microsoft.AspNetCore.Components;

namespace BlazorArcade.Components;

/// <summary>
/// Arcade links relative to the hub page, which is /arcade on the main site and / on the games
/// site; each game page sits directly under it.
/// </summary>
public static class ArcadeNav
{
    /// <summary>The hub, seen from a game page.</summary>
    public static string Hub(NavigationManager nav)
    {
        var path = CurrentPath(nav);
        int slash = path.LastIndexOf('/');
        return "/" + (slash < 0 ? "" : path[..slash]);
    }

    /// <summary>A game, seen from the hub page.</summary>
    public static string Game(NavigationManager nav, string slug)
    {
        var path = CurrentPath(nav);
        return path.Length == 0 ? $"/{slug}" : $"/{path}/{slug}";
    }

    private static string CurrentPath(NavigationManager nav)
    {
        var path = nav.ToBaseRelativePath(nav.Uri);
        int end = path.IndexOfAny(['?', '#']);
        return (end < 0 ? path : path[..end]).Trim('/');
    }
}
