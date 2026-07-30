using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEngine.UIElements;
using UIToolkitValidation;

namespace UIToolkitValidation.Tests
{
    public class UIToolkitValidationTests
    {
        [Test]
        public void ValidateAllUIToolkitAssetsAndBindings()
        {
            // 1. Run static validation (USS properties, UXML syntax, C# bindings)
            bool success = UIToolkitValidator.RunValidation(silent: false);
            Assert.IsTrue(success, "Static UI Toolkit Validation failed! Check the Unity Editor console logs for detailed error reports.");
        }

        [UnityTest]
        public IEnumerator ValidateAllUIToolkitLayouts()
        {
            // 2. Run resolved layout checks (Open mock window, yield a frame for Yoga calculation, scan bounds)
            string[] uxmlGuids = AssetDatabase.FindAssets("t:VisualTreeAsset");
            List<string> uxmlPaths = new List<string>();
            foreach (var guid in uxmlGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.StartsWith("Assets/") && p.EndsWith(".uxml")) uxmlPaths.Add(p);
            }

            int overallErrors = 0;

            foreach (string uxmlPath in uxmlPaths)
            {
                VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
                if (uxml == null) continue;

                VisualElement root = uxml.CloneTree();
                root.style.width = Length.Percent(100);
                root.style.height = Length.Percent(100);

                // Open mock window in full HD
                var window = UIToolkitValidator.OpenMockWindow(root);

                // Yield to let layout engine compute dimensions
                yield return null;



                int layoutErrors = 0;
                UIToolkitValidator.ValidateElementLayoutRecursive(root, root, uxmlPath, ref layoutErrors);

                // Close window
                window.Close();

                overallErrors += layoutErrors;
            }

            Assert.AreEqual(0, overallErrors, "UI Toolkit Resolved Layout Validation failed! Check the Unity Editor console logs for detailed layout error reports.");
        }

        [UnityTest]
        public IEnumerator ValidateCutscenePlayerIntegration()
        {
            float originalTimeScale = Time.timeScale;
            try
            {
                // 1. Create a dummy HUD UIDocument so CutscenePlayer can find it to copy panelSettings
                var hudGo = new GameObject("MockGameUICanvas", typeof(UIDocument));
                var hudDoc = hudGo.GetComponent<UIDocument>();
                var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Settings/GameUIPanelSettings.asset");
                hudDoc.panelSettings = panelSettings;

                // 2. Instantiate CutscenePlayer
                var playerGo = new GameObject("TestCutscenePlayer", typeof(CutscenePlayer));
                var player = playerGo.GetComponent<CutscenePlayer>();
                player.title = "Test Title";
                player.body = "Test Body description text here.";

                // 3. Play cutscene (Builds the UI)
                player.Play();

                // Yield a frame to let UIDocument initialize
                yield return null;

                // 4. Find the generated cutscene panel
                var panelGo = GameObject.Find("TestCutscenePlayer_Panel");
                Assert.IsNotNull(panelGo, "CutscenePlayer panel GameObject was not created!");

                var doc = panelGo.GetComponent<UIDocument>();
                Assert.IsNotNull(doc, "CutscenePlayer panel GameObject is missing UIDocument!");
                Assert.IsNotNull(doc.panelSettings, "CutscenePlayer panel UIDocument has null panelSettings! (FindFirstObjectByType self-match bug is present)");
                Assert.AreEqual(100, doc.sortingOrder, "CutscenePlayer UIDocument sortingOrder should be 100.");

                var root = doc.rootVisualElement.Q("CutscenePanel");
                Assert.IsNotNull(root, "CutscenePanel element not found in visual tree!");

                // Open mock window to let layout resolve dimensions
                var window = UIToolkitValidator.OpenMockWindow(root);
                yield return null;

                int layoutErrors = 0;
                UIToolkitValidator.ValidateElementLayoutRecursive(root, root, "CutscenePlayer", ref layoutErrors);

                // Cleanup
                window.Close();
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(hudGo);

                Assert.AreEqual(0, layoutErrors, "CutscenePlayer resolved layout has errors!");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }
    }
}
