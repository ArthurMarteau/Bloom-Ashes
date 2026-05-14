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
        All = ~0
    }

    public static Category EnabledCategories = Category.None;

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

    public static void Error(string message, Object context = null)
    {
        UnityEngine.Debug.LogError(message, context);
    }
}
