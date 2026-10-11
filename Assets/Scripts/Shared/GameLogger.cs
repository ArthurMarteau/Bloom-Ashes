using System;
using System.Diagnostics;
using Object = UnityEngine.Object;

public static class GameLogger
{
    [Flags]
    public enum Category
    {
        None = 0,
        Movement = 1 << 0,
        Combat = 1 << 1,
        Selection = 1 << 2,
        Watersap = 1 << 3,
        Spawn = 1 << 4,
        Grid = 1 << 5,
        UI = 1 << 6,
        Match = 1 << 7,
        Network = 1 << 8,
        AI = 1 << 9,
        All = ~0
    }

    public static Category EnabledCategories = Category.None;
    public static int LogOnlyTeam = -1;

    // Indique si une catégorie de logs est active sans construire le message associé
    public static bool IsEnabled(Category category)
    {
        return (EnabledCategories & category) != 0;
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Log(Category category, string message, Object context = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if ((EnabledCategories & category) == 0)
            return;

        UnityEngine.Debug.Log($"[{category}] {message}", context);
#endif
    }

    // Journalise un message d'équipe si la catégorie et le filtre de debug l'autorisent.
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Log(Category category, string message, Object context, int teamID)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if ((EnabledCategories & category) == 0 || !IsTeamEnabled(teamID))
            return;

        UnityEngine.Debug.Log($"[{category}] {message}", context);
#endif
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Warn(Category category, string message, Object context = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if ((EnabledCategories & category) == 0)
            return;

        UnityEngine.Debug.LogWarning($"[{category}] {message}", context);
#endif
    }

    // Journalise un avertissement d'équipe si la catégorie et le filtre de debug l'autorisent.
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Warn(Category category, string message, Object context, int teamID)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if ((EnabledCategories & category) == 0 || !IsTeamEnabled(teamID))
            return;

        UnityEngine.Debug.LogWarning($"[{category}] {message}", context);
#endif
    }

    // Journalise une erreur d'équipe si le filtre de debug l'autorise.
    public static void Error(Category category, string message, Object context, int teamID)
    {
        if (!IsTeamEnabled(teamID))
            return;

        UnityEngine.Debug.LogError($"[{category}] {message}", context);
    }

    public static void Error(string message, Object context = null)
    {
        UnityEngine.Debug.LogError(message, context);
    }

    // Indique si un log taggé appartient à l'équipe retenue pour le debug.
    private static bool IsTeamEnabled(int teamID)
    {
        return LogOnlyTeam < 0 || teamID == LogOnlyTeam;
    }
}
