using System;
using System.Collections.Generic;
using HouseFlip.Audio;
using HouseFlip.Core;
using UnityEditor;
using UnityEngine;

namespace HouseFlip.EditorTools
{
    /// <summary>
    /// Generates the placeholder sound set and wires it into an <see cref="AudioManager"/>
    /// (GDD 24, Phase 17).
    ///
    /// Every trigger in the GDD's audio table gets a clip, so the prototype is audible
    /// from the first run. Replacing one is a matter of dropping a real .wav over the
    /// generated file — the manager looks clips up by <see cref="SfxId"/>, not by name.
    /// </summary>
    public static class HouseFlipAudioBuilder
    {
        public const string AudioRoot = "Assets/_Audio";
        private const string MusicName = "Music_HouseFlipTheme";

        /// <summary>Per-sound mix, kept here so levels can be balanced without touching the synth.</summary>
        private struct Recipe
        {
            public SfxId Id;
            public Func<float[]> Generate;
            public float Volume;
            public float PitchJitter;

            public Recipe(SfxId id, Func<float[]> generate, float volume, float jitter)
            {
                Id = id;
                Generate = generate;
                Volume = volume;
                PitchJitter = jitter;
            }
        }

        private static readonly Recipe[] Recipes =
        {
            // Repeated sounds get pitch jitter so a demolition spree does not machine-gun.
            new Recipe(SfxId.HammerHit, SfxSynth.HammerHit, 0.85f, 0.18f),
            new Recipe(SfxId.ObjectBreak, SfxSynth.ObjectBreak, 0.95f, 0.12f),
            new Recipe(SfxId.BuildComplete, SfxSynth.BuildComplete, 0.7f, 0.05f),
            new Recipe(SfxId.Pickup, SfxSynth.Pickup, 0.55f, 0.10f),
            new Recipe(SfxId.Drop, SfxSynth.Drop, 0.55f, 0.10f),
            new Recipe(SfxId.Throw, SfxSynth.Throw, 0.7f, 0.14f),
            new Recipe(SfxId.WaterGurgle, SfxSynth.WaterGurgle, 0.6f, 0.03f),
            new Recipe(SfxId.ElectricalSpark, SfxSynth.ElectricalSpark, 0.8f, 0.10f),
            new Recipe(SfxId.CashRegister, SfxSynth.CashRegister, 0.75f, 0.03f),
            new Recipe(SfxId.UIClick, SfxSynth.UIClick, 0.45f, 0.06f),
            new Recipe(SfxId.Clean, SfxSynth.Clean, 0.5f, 0.12f),
            new Recipe(SfxId.Repair, SfxSynth.Repair, 0.7f, 0.08f),
            new Recipe(SfxId.Paint, SfxSynth.Paint, 0.5f, 0.12f),
            new Recipe(SfxId.SuccessFanfare, SfxSynth.SuccessFanfare, 0.9f, 0f),
            new Recipe(SfxId.FailSound, SfxSynth.FailSound, 0.9f, 0f),
            new Recipe(SfxId.EventAlarm, SfxSynth.EventAlarm, 0.8f, 0f)
        };

        [MenuItem("House Flip/Generate Placeholder Audio", priority = 20)]
        public static void GenerateMenu()
        {
            GenerateClips();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[House Flip] Generated {Recipes.Length} SFX + 1 music loop in {AudioRoot}.");
        }

        /// <summary>Writes every .wav to disk and returns them keyed by id.</summary>
        public static Dictionary<SfxId, AudioClip> GenerateClips()
        {
            HouseFlipAssetBuilder.EnsureFolders();
            SfxSynth.ResetSeed();

            var result = new Dictionary<SfxId, AudioClip>();

            foreach (Recipe recipe in Recipes)
            {
                string path = $"{AudioRoot}/SFX_{recipe.Id}.wav";
                WavWriter.Write(path, recipe.Generate());
                AssetDatabase.ImportAsset(path);

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    result[recipe.Id] = clip;
                }
            }

            WavWriter.Write($"{AudioRoot}/{MusicName}.wav", SfxSynth.BackgroundMusic());
            AssetDatabase.ImportAsset($"{AudioRoot}/{MusicName}.wav");

            return result;
        }

        public static AudioClip LoadMusic()
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioRoot}/{MusicName}.wav");
        }

        /// <summary>
        /// Fills the manager's serialized <c>sfx</c> list and music slot.
        /// Written through SerializedObject because both fields are private.
        /// </summary>
        public static void WireInto(AudioManager manager, Dictionary<SfxId, AudioClip> clips)
        {
            if (manager == null)
            {
                return;
            }

            var serialized = new SerializedObject(manager);

            SerializedProperty music = serialized.FindProperty("backgroundMusic");
            if (music != null)
            {
                music.objectReferenceValue = LoadMusic();
            }

            SerializedProperty list = serialized.FindProperty("sfx");
            if (list == null || !list.isArray)
            {
                Debug.LogWarning("[HouseFlipAudioBuilder] AudioManager has no 'sfx' list to fill.");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            list.arraySize = Recipes.Length;

            for (int i = 0; i < Recipes.Length; i++)
            {
                Recipe recipe = Recipes[i];
                SerializedProperty element = list.GetArrayElementAtIndex(i);

                SetRelative(element, "id", p => p.intValue = (int)recipe.Id);
                SetRelative(element, "volume", p => p.floatValue = recipe.Volume);
                SetRelative(element, "pitchJitter", p => p.floatValue = recipe.PitchJitter);
                SetRelative(element, "clip", p =>
                    p.objectReferenceValue = clips.TryGetValue(recipe.Id, out AudioClip clip) ? clip : null);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRelative(SerializedProperty element, string name, Action<SerializedProperty> apply)
        {
            SerializedProperty property = element.FindPropertyRelative(name);
            if (property != null)
            {
                apply(property);
            }
        }
    }
}
