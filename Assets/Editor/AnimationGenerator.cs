using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;
using System.Linq;

// Script de génération automatique des Animation Clips et Animator Controller
// À exécuter APRÈS le Script 1 (SpriteRenamer) pour que les sprites soient correctement nommés
// Accès via clic droit sur un dossier → Bloom & Ashes > 2 - Generate Animations & Animator
public class AnimationGenerator
{
    // Nombre de frames par type d'animation
    private const int FRAMES_IDLE_MOVE   = 3; // frames 1-3 du spritesheet Idle
    private const int FRAMES_ATTACK      = 3; // frames 1-3 du spritesheet Attack
    private const int FRAMES_WALK        = 5; // frames 0-4
    private const int FRAMES_HIT         = 4; // frames 0-3
    private const int FRAMES_DEATH       = 5; // frames 0-4

    // Directions supportées pour les animations directionnelles
    private static readonly string[] DIRECTIONS = { "N", "NW", "W", "SW", "S" };

    // FPS par défaut pour toutes les animations
    private const float DEFAULT_FPS = 12f;

    [MenuItem("Assets/Bloom & Ashes/2 - Generate Animations & Animator", false, 2)]
    private static void GenerateAnimations()
    {
        // Récupère le dossier sélectionné
        string selectedAssetPath = GetSelectedFolderAssetPath();
        if (selectedAssetPath == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez sélectionner un dossier unité dans le panneau Project.", "OK");
            return;
        }

        // Extrait le nom de l'unité depuis le dossier
        string unitName = Path.GetFileName(selectedAssetPath);

        // Confirmation
        bool confirmed = EditorUtility.DisplayDialog(
            "Générer les animations",
            $"Générer les animations et l'Animator pour \"{unitName}\" ?\n\nLes clips existants seront mis à jour.",
            "Générer", "Annuler"
        );

        if (!confirmed) return;

        // Crée le dossier Assets/Animation/Units/<unitName>/ si nécessaire
        string animationsRootPath = "Assets/Animation/Units";
        if (!AssetDatabase.IsValidFolder("Assets/Animation"))
            AssetDatabase.CreateFolder("Assets", "Animation");
        if (!AssetDatabase.IsValidFolder(animationsRootPath))
            AssetDatabase.CreateFolder("Assets/Animation", "Units");

        string animationsPath = $"{animationsRootPath}/{unitName}";
        if (!AssetDatabase.IsValidFolder(animationsPath))
        {
            AssetDatabase.CreateFolder(animationsRootPath, unitName);
            Debug.Log($"📁 AnimationGenerator: Dossier {animationsPath} créé");
        }

        // Génère tous les clips
        GenerateAllClips(selectedAssetPath, animationsPath, unitName);

        // Génère ou met à jour l'Animator Controller
        GenerateAnimatorController(animationsPath, unitName);

        // Sauvegarde et rafraîchit
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"✅ AnimationGenerator: Génération terminée pour {unitName}");
    }

    // Valide que la sélection est bien un dossier
    [MenuItem("Assets/Bloom & Ashes/2 - Generate Animations & Animator", true)]
    private static bool ValidateGenerateAnimations()
    {
        return GetSelectedFolderAssetPath() != null;
    }

    // Génère tous les clips pour une unité
    private static void GenerateAllClips(string spritesPath, string animationsPath, string unitName)
    {
        CreateIdleAndIdleMoveClips(spritesPath, animationsPath, unitName);

        foreach (string dir in DIRECTIONS)
            CreateAttackClips(spritesPath, animationsPath, unitName, dir);

        foreach (string dir in DIRECTIONS)
            CreateWalkClip(spritesPath, animationsPath, unitName, dir);

        CreateHitClip(spritesPath, animationsPath, unitName, "NW");
        CreateHitClip(spritesPath, animationsPath, unitName, "SW");
        CreateDeathClip(spritesPath, animationsPath, unitName);
    }

    // Crée les clips Idle (frame 0) et IdleMove (frames 1-3) depuis _Idle_SW_
    private static void CreateIdleAndIdleMoveClips(string spritesPath, string animationsPath, string unitName)
    {
        string sheetAssetPath = $"{spritesPath}/{unitName}_Idle_SW.png";
        Sprite[] sprites = LoadSpritesFromSheet(sheetAssetPath);
        if (sprites == null || sprites.Length < 4) return;

        CreateClip(animationsPath, "Idle", new[] { sprites[0] }, DEFAULT_FPS, loop: false);
        CreateClip(animationsPath, "IdleMove", sprites.Skip(1).Take(FRAMES_IDLE_MOVE).ToArray(), DEFAULT_FPS, loop: false);
    }

    // Crée les clips AttackIdle_DIR (frame 0) et Attack_DIR (frames 1-3)
    private static void CreateAttackClips(string spritesPath, string animationsPath, string unitName, string direction)
    {
        string sheetAssetPath = $"{spritesPath}/{unitName}_Attack_{direction}.png";
        Sprite[] sprites = LoadSpritesFromSheet(sheetAssetPath);
        if (sprites == null || sprites.Length < 4) return;

        CreateClip(animationsPath, $"AttackIdle_{direction}", new[] { sprites[0] }, DEFAULT_FPS, loop: false);
        CreateClip(animationsPath, $"Attack_{direction}", sprites.Skip(1).Take(FRAMES_ATTACK).ToArray(), DEFAULT_FPS, loop: false);
    }

    // Crée le clip Walk_DIR (frames 0-4) en loop
    private static void CreateWalkClip(string spritesPath, string animationsPath, string unitName, string direction)
    {
        string sheetAssetPath = $"{spritesPath}/{unitName}_Walk_{direction}.png";
        Sprite[] sprites = LoadSpritesFromSheet(sheetAssetPath);
        if (sprites == null) return;

        CreateClip(animationsPath, $"Walk_{direction}", sprites.Take(FRAMES_WALK).ToArray(), DEFAULT_FPS, loop: true);
    }

    // Crée le clip Hit_DIR (frames 0-3), pas de loop
    private static void CreateHitClip(string spritesPath, string animationsPath, string unitName, string direction)
    {
        string sheetAssetPath = $"{spritesPath}/{unitName}_Hit_{direction}.png";
        Sprite[] sprites = LoadSpritesFromSheet(sheetAssetPath);
        if (sprites == null) return;

        CreateClip(animationsPath, $"Hit_{direction}", sprites.Take(FRAMES_HIT).ToArray(), DEFAULT_FPS, loop: false);
    }

    // Crée le clip Death (frames 0-4) depuis _Death_S_, pas de loop
    private static void CreateDeathClip(string spritesPath, string animationsPath, string unitName)
    {
        string sheetAssetPath = $"{spritesPath}/{unitName}_Death_S.png";
        Sprite[] sprites = LoadSpritesFromSheet(sheetAssetPath);
        if (sprites == null) return;

        CreateClip(animationsPath, "Death", sprites.Take(FRAMES_DEATH).ToArray(), DEFAULT_FPS, loop: false);
    }

    // Charge et trie les sprites d'un spritesheet par index numérique
    // Retourne null si le fichier est introuvable ou vide
    private static Sprite[] LoadSpritesFromSheet(string sheetAssetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(sheetAssetPath) == null)
        {
            Debug.LogWarning($"⚠️ AnimationGenerator: Spritesheet introuvable : {sheetAssetPath}");
            return null;
        }

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(sheetAssetPath)
            .OfType<Sprite>()
            .OrderBy(s => {
                string[] parts = s.name.Split('_');
                int index = 0;
                int.TryParse(parts[parts.Length - 1], out index);
                return index;
            })
            .ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogWarning($"⚠️ AnimationGenerator: Aucun sprite trouvé dans {sheetAssetPath}");
            return null;
        }

        return sprites;
    }

    // Crée ou met à jour un AnimationClip à partir d'un tableau de sprites
    // Les frames sont placées à 0.00, 0.01, 0.02... (temps fixe par frame)
    // Cas spécial "Idle" : frame 0 à t=0.00 et dupliquée à t=4.00
    private static AnimationClip CreateClip(string animationsPath, string clipName, Sprite[] sprites, float fps, bool loop)
    {
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning($"⚠️ AnimationGenerator: Pas de sprites pour le clip {clipName}");
            return null;
        }

        string clipPath = $"{animationsPath}/{clipName}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keyframes;

        if (clipName == "Idle")
        {
            // Cas spécial Idle : frame 0 à t=0.00 et dupliquée à t=4.00
            keyframes = new ObjectReferenceKeyframe[2];
            keyframes[0] = new ObjectReferenceKeyframe { time = 0.00f, value = sprites[0] };
            keyframes[1] = new ObjectReferenceKeyframe { time = 4.00f, value = sprites[0] };
        }
        else
        {
            // Frames à 12fps, enchaînées sans gap
            keyframes = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i * (1f / DEFAULT_FPS),
                    value = sprites[i]
                };
            }
        }

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);
        EditorUtility.SetDirty(clip);
        Debug.Log($"🎬 AnimationGenerator: Clip {clipName} créé/mis à jour ({sprites.Length} frames, loop: {loop})");

        return clip;
    }

    // Génère ou met à jour l'Animator Controller pour l'unité
    // Si l'Animator existe déjà → met à jour les clips uniquement
    private static void GenerateAnimatorController(string animationsPath, string unitName)
    {
        string controllerPath = $"{animationsPath}/{unitName}Animator.controller";

        UnityEditor.Animations.AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);

        if (controller != null)
        {
            UpdateExistingAnimatorClips(controller, animationsPath);
            Debug.Log($"🔄 AnimationGenerator: Animator existant mis à jour (clips uniquement) : {unitName}Animator");
            return;
        }

        controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        Debug.Log($"🎮 AnimationGenerator: Animator Controller créé : {unitName}Animator");

        // Paramètres
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsHitNW", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IdleMove", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        // États de base
        AnimatorState stateIdle = stateMachine.AddState("Idle");
        stateIdle.motion = LoadClip(animationsPath, "Idle");
        stateMachine.defaultState = stateIdle;

        AnimatorState stateIdleMove = stateMachine.AddState("IdleMove");
        stateIdleMove.motion = LoadClip(animationsPath, "IdleMove");
        // stateIdleMove.AddStateMachineBehaviour<IdleMoveExitBehaviour>(); // à réactiver une fois IdleMoveExitBehaviour compilé

        AnimatorState stateHitNW = stateMachine.AddState("Hit_NW");
        stateHitNW.motion = LoadClip(animationsPath, "Hit_NW");

        AnimatorState stateHitSW = stateMachine.AddState("Hit_SW");
        stateHitSW.motion = LoadClip(animationsPath, "Hit_SW");

        AnimatorState stateDeath = stateMachine.AddState("Death");
        stateDeath.motion = LoadClip(animationsPath, "Death");

        // États directionnels
        foreach (string dir in DIRECTIONS)
        {
            AnimatorState stateAttack     = stateMachine.AddState($"Attack_{dir}");
            AnimatorState stateAttackIdle = stateMachine.AddState($"AttackIdle_{dir}");
            AnimatorState stateWalk       = stateMachine.AddState($"Walk_{dir}");

            stateAttack.motion     = LoadClip(animationsPath, $"Attack_{dir}");
            stateAttackIdle.motion = LoadClip(animationsPath, $"AttackIdle_{dir}");
            stateWalk.motion       = LoadClip(animationsPath, $"Walk_{dir}");

            stateAttack.tag = "Attack";

            // Transition Attack → AttackIdle (exit time)
            AddExitTimeTransition(stateAttack, stateAttackIdle, exitTime: 1f);
        }

        // Transitions Idle ↔ IdleMove
        AddExitTimeTransition(stateIdle, stateIdleMove, exitTime: 1f);
        AddExitTimeTransition(stateIdleMove, stateIdle, exitTime: 1f);

        // Any State → Hit_NW (Hit + IsHitNW = true)
        AnimatorStateTransition transHitNW = stateMachine.AddAnyStateTransition(stateHitNW);
        transHitNW.hasExitTime = false;
        transHitNW.duration = 0f;
        transHitNW.canTransitionToSelf = false;
        transHitNW.AddCondition(AnimatorConditionMode.If, 0, "Hit");
        transHitNW.AddCondition(AnimatorConditionMode.If, 0, "IsHitNW");

        // Any State → Hit_SW (Hit + IsHitNW = false)
        AnimatorStateTransition transHitSW = stateMachine.AddAnyStateTransition(stateHitSW);
        transHitSW.hasExitTime = false;
        transHitSW.duration = 0f;
        transHitSW.canTransitionToSelf = false;
        transHitSW.AddCondition(AnimatorConditionMode.If, 0, "Hit");
        transHitSW.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHitNW");

        // Force la sauvegarde du layer
        controller.layers = controller.layers;

        EditorUtility.SetDirty(controller);
        Debug.Log($"✅ AnimationGenerator: Animator Controller configuré pour {unitName}");
    }

    // Met à jour uniquement les clips d'un Animator Controller existant
    private static void UpdateExistingAnimatorClips(UnityEditor.Animations.AnimatorController controller, string animationsPath)
    {
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            AnimatorState state = childState.state;
            AnimationClip clip = LoadClip(animationsPath, state.name);
            if (clip != null)
            {
                state.motion = clip;
                Debug.Log($"🔄 AnimationGenerator: Clip mis à jour → {state.name}");
            }
        }

        EditorUtility.SetDirty(controller);
    }

    // Crée une transition Exit Time entre deux états
    private static void AddExitTimeTransition(AnimatorState from, AnimatorState to, float exitTime)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = 0f;
        transition.hasFixedDuration = true;
    }

    // Charge un AnimationClip depuis le dossier Animations/
    private static AnimationClip LoadClip(string animationsPath, string clipName)
    {
        string clipPath = $"{animationsPath}/{clipName}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

        if (clip == null)
            Debug.LogWarning($"⚠️ AnimationGenerator: Clip introuvable : {clipPath}");

        return clip;
    }

    // Retourne le chemin Assets/ du dossier sélectionné dans le panneau Project
    private static string GetSelectedFolderAssetPath()
    {
        Object selected = Selection.activeObject;
        if (selected == null) return null;

        string path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path)) return null;

        if (!AssetDatabase.IsValidFolder(path)) return null;

        return path;
    }
}
