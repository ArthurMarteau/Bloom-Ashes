using UnityEngine;

// Système POCO pur : évalue une courbe de flash dans le temps
// Aucune dépendance MonoBehaviour — testable en isolation
public class FlashCurveSystem
{
    // Évalue la valeur de fade-out du flash (1 → 0) selon la courbe fournie
    // elapsedTime : temps écoulé depuis le début du fade
    // duration : durée totale du fade
    // curve : AnimationCurve allant de 1 à 0 sur [0, 1]
    public float EvaluateFadeOut(float elapsedTime, float duration, AnimationCurve curve)
    {
        if (duration <= 0f) return 0f;
        float normalizedTime = Mathf.Clamp01(elapsedTime / duration);
        return Mathf.Clamp01(curve.Evaluate(normalizedTime));
    }

    // Évalue la valeur de fade-in du flash (0 → 1) en inversant la courbe de fade-out
    // La courbe stockée dans le SO est conçue pour aller de 1 → 0 — on retourne 1 - curve ici
    // elapsedTime : temps écoulé depuis le début du fade
    // duration : durée totale du fade
    // curve : AnimationCurve allant de 1 à 0 sur [0, 1] (même courbe que FadeOut)
    public float EvaluateFadeIn(float elapsedTime, float duration, AnimationCurve curve)
    {
        if (duration <= 0f) return 1f;
        float normalizedTime = Mathf.Clamp01(elapsedTime / duration);
        // Inversion de la courbe pour transformer un FadeOut (1→0) en FadeIn (0→1)
        // La courbe stockée dans le SO est conçue pour le FadeOut, on la retourne ici
        return Mathf.Clamp01(1f - curve.Evaluate(normalizedTime));
    }

    // Retourne vrai si le fade est terminé (temps écoulé >= durée)
    public bool IsComplete(float elapsedTime, float duration)
    {
        return elapsedTime >= duration;
    }
}
