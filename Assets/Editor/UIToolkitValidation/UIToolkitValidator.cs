using System;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkitValidation
{
    public class UIToolkitValidator : AssetPostprocessor
    {
        // Whitelist of supported USS properties based on Unity UI Toolkit documentation
        private static readonly HashSet<string> USS_WHITELIST = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "width", "height", "min-width", "min-height", "max-width", "max-height", "aspect-ratio",
            "margin", "margin-left", "margin-top", "margin-right", "margin-bottom",
            "padding", "padding-left", "padding-top", "padding-right", "padding-bottom",
            "border-width", "border-left-width", "border-top-width", "border-right-width", "border-bottom-width",
            "border-color", "border-left-color", "border-top-color", "border-right-color", "border-bottom-color",
            "border-radius", "border-top-left-radius", "border-top-right-radius", "border-bottom-left-radius", "border-bottom-right-radius",
            "flex", "flex-grow", "flex-shrink", "flex-basis", "flex-direction", "flex-wrap",
            "align-self", "align-items", "align-content", "justify-content",
            "position", "left", "top", "right", "bottom",
            "background-color", "background-image", "-unity-background-scale-mode", "-unity-background-image-tint-color",
            "background-position", "background-position-x", "background-position-y", "background-repeat", "background-size",
            "-unity-slice-left", "-unity-slice-top", "-unity-slice-right", "-unity-slice-bottom", "-unity-slice-scale", "-unity-slice-type",
            "overflow", "-unity-overflow-clip-box", "visibility", "display",
            "color", "-unity-font", "-unity-font-definition", "font-size", "-unity-font-style", "-unity-text-align", "-unity-text-overflow-position", "white-space",
            "-unity-text-outline-width", "-unity-text-outline-color", "-unity-text-outline", "-unity-text-generator", "-unity-text-auto-size",
            "text-overflow", "text-shadow", "letter-spacing", "word-spacing", "-unity-paragraph-spacing",
            "cursor", "opacity", "-unity-material",
            "translate", "rotate", "scale", "transform-origin",
            "transition", "transition-property", "transition-duration", "transition-timing-function", "transition-delay",
            "filter", "-unity-background-image", "all", "-unity-editor-text-rendering-mode"
        };

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool hasUIChanges = false;
            foreach (string asset in importedAssets)
            {
                if (asset.EndsWith(".uxml") || asset.EndsWith(".uss") || asset.EndsWith(".cs"))
                {
                    hasUIChanges = true;
                    break;
                }
            }
            if (hasUIChanges)
            {
                RunValidation(silent: true);
            }
        }

        [MenuItem("Tools/UI Toolkit/Validate All UI Elements")]
        public static bool RunValidationMenu()
        {
            return RunValidation(silent: false);
        }

        public static bool RunValidation(bool silent)
        {
            int errorCount = 0;
            HashSet<string> allUxmlElementNames = new HashSet<string>();
            HashSet<string> allUxmlClasses = new HashSet<string>();

            // 1. Gather files
            string[] uxmlGuids = AssetDatabase.FindAssets("t:VisualTreeAsset");
            List<string> uxmlPaths = new List<string>();
            foreach (var guid in uxmlGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.StartsWith("Assets/") && p.EndsWith(".uxml")) uxmlPaths.Add(p);
            }

            // Find USS files in database
            string[] ussGuids = AssetDatabase.FindAssets("t:StyleSheet");
            List<string> ussPaths = new List<string>();
            foreach (var guid in ussGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.StartsWith("Assets/") && p.EndsWith(".uss")) ussPaths.Add(p);
            }

            // Find C# files
            string[] csGuids = AssetDatabase.FindAssets("t:MonoScript");
            List<string> csPaths = new List<string>();
            foreach (var guid in csGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.StartsWith("Assets/Script/") && p.EndsWith(".cs")) csPaths.Add(p);
            }

            if (!silent)
            {
                Debug.Log($"[UIToolkitValidator] Starting validation on {uxmlPaths.Count} UXML, {ussPaths.Count} USS, and {csPaths.Count} C# script(s)...");
            }

            HashSet<string> allUssClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 2. Validate USS properties
            foreach (string ussPath in ussPaths)
            {
                ValidateUSSFile(ussPath, allUssClasses, ref errorCount);
            }

            // 3. Validate UXML and extract names/classes
            foreach (string uxmlPath in uxmlPaths)
            {
                ValidateUXMLFile(uxmlPath, allUxmlElementNames, allUxmlClasses, ref errorCount);
            }

            // 4. Validate C# queries project-wide
            HashSet<string> validClasses = new HashSet<string>(allUxmlClasses, StringComparer.OrdinalIgnoreCase);
            validClasses.UnionWith(allUssClasses);
            foreach (string csPath in csPaths)
            {
                ValidateScriptBindings(csPath, allUxmlElementNames, validClasses, ref errorCount);
            }

            // 5. Validate specific GameObject/UIDocument component bindings in prefabs
            ValidateUIDocumentPrefabBindings(ref errorCount);

            // Report results
            if (errorCount > 0)
            {
                Debug.LogError($"[UIToolkitValidator] Validation FAILED with {errorCount} error(s). Please review the errors in the console.");
                return false;
            }
            else
            {
                if (!silent)
                {
                    Debug.Log("[UIToolkitValidator] Validation PASSED. All UI Toolkit assets and bindings are 100% correct!");
                }
                return true;
            }
        }

        private static void ValidateUSSFile(string path, HashSet<string> allUssClasses, ref int errorCount)
        {
            try
            {
                string content = File.ReadAllText(path);
                content = Regex.Replace(content, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

                MatchCollection matches = Regex.Matches(content, @"([^{]+)\{([^}]+)\}");
                foreach (Match match in matches)
                {
                    string selector = match.Groups[1].Value.Trim();
                    string body = match.Groups[2].Value;

                    ExtractClassesFromSelector(selector, allUssClasses);

                    string[] declarations = body.Split(';');
                    foreach (string decl in declarations)
                    {
                        if (string.IsNullOrWhiteSpace(decl)) continue;
                        int colonIdx = decl.IndexOf(':');
                        if (colonIdx < 0) continue;

                        string prop = decl.Substring(0, colonIdx).Trim();
                        string val = decl.Substring(colonIdx + 1).Trim();

                        if (val.IndexOf("calc(", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Debug.LogError($"[UIToolkitValidator] USS ERROR in '{path}': Unsupported use of 'calc()' function in property '{prop}' (value: '{val}') in selector '{selector}' (UI Toolkit does not support calc())");
                            errorCount++;
                        }

                        if (prop.StartsWith("--")) continue; // Custom CSS variables

                        if (!USS_WHITELIST.Contains(prop))
                        {
                            Debug.LogError($"[UIToolkitValidator] USS ERROR in '{path}': Unsupported property '{prop}' in selector '{selector}'");
                            errorCount++;
                        }
                        else
                        {
                            if (prop.Equals("display", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!val.Equals("flex", StringComparison.OrdinalIgnoreCase) && !val.Equals("none", StringComparison.OrdinalIgnoreCase))
                                {
                                    Debug.LogError($"[UIToolkitValidator] USS ERROR in '{path}': Invalid display value '{val}' in selector '{selector}' (only 'flex' or 'none' are supported in UI Toolkit)");
                                    errorCount++;
                                }
                            }
                            else if (prop.Equals("position", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!val.Equals("absolute", StringComparison.OrdinalIgnoreCase) && !val.Equals("relative", StringComparison.OrdinalIgnoreCase))
                                {
                                    Debug.LogError($"[UIToolkitValidator] USS ERROR in '{path}': Invalid position value '{val}' in selector '{selector}' (only 'absolute' or 'relative' are supported)");
                                    errorCount++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIToolkitValidator] Failed to validate USS file '{path}': {ex.Message}");
                errorCount++;
            }
        }

        private static void ExtractClassesFromSelector(string selector, HashSet<string> allUssClasses)
        {
            string[] parts = selector.Split(',');
            foreach (string part in parts)
            {
                MatchCollection matches = Regex.Matches(part, @"\.([a-zA-Z0-9_-]+)");
                foreach (Match m in matches)
                {
                    allUssClasses.Add(m.Groups[1].Value);
                }
            }
        }

        private static void ValidateUXMLFile(string path, HashSet<string> allUxmlElementNames, HashSet<string> allUxmlClasses, ref int errorCount)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(path);

                XmlNodeList styleNodes = xmlDoc.GetElementsByTagName("Style");
                List<XmlNode> allStyles = new List<XmlNode>();
                foreach (XmlNode node in styleNodes) allStyles.Add(node);

                XmlNodeList uiStyleNodes = xmlDoc.GetElementsByTagName("ui:Style");
                foreach (XmlNode node in uiStyleNodes) allStyles.Add(node);

                foreach (XmlNode node in allStyles)
                {
                    XmlAttribute srcAttr = node.Attributes["src"];
                    if (srcAttr != null)
                    {
                        string src = srcAttr.Value;
                        if (src.StartsWith("project://database/"))
                        {
                            string localPath = src.Replace("project://database/", string.Empty);
                            if (!File.Exists(localPath))
                            {
                                Debug.LogError($"[UIToolkitValidator] UXML ERROR in '{path}': Referenced style file '{localPath}' does not exist!");
                                errorCount++;
                            }
                        }
                    }
                }

                ExtractNamesAndClasses(xmlDoc.DocumentElement, allUxmlElementNames, allUxmlClasses);
            }
            catch (XmlException ex)
            {
                Debug.LogError($"[UIToolkitValidator] UXML XML Syntax Error in '{path}' at line {ex.LineNumber}, position {ex.LinePosition}: {ex.Message}");
                errorCount++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIToolkitValidator] Failed to validate UXML file '{path}': {ex.Message}");
                errorCount++;
            }
        }

        private static void ExtractNamesAndClasses(XmlNode node, HashSet<string> allNames, HashSet<string> allClasses)
        {
            if (node == null) return;

            if (node.Attributes != null)
            {
                XmlAttribute nameAttr = node.Attributes["name"];
                if (nameAttr != null && !string.IsNullOrWhiteSpace(nameAttr.Value))
                {
                    allNames.Add(nameAttr.Value.Trim());
                }

                XmlAttribute classAttr = node.Attributes["class"];
                if (classAttr != null && !string.IsNullOrWhiteSpace(classAttr.Value))
                {
                    string[] classes = classAttr.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string cls in classes)
                    {
                        allClasses.Add(cls.Trim());
                    }
                }
            }

            foreach (XmlNode child in node.ChildNodes)
            {
                ExtractNamesAndClasses(child, allNames, allClasses);
            }
        }

        private static void ValidateScriptBindings(string scriptPath, HashSet<string> allUxmlNames, HashSet<string> allUssClasses, ref int errorCount)
        {
            if (scriptPath.Contains("UIToolkitValidator") || scriptPath.Contains("UIToolkitValidationTests")) return;

            try
            {
                string content = File.ReadAllText(scriptPath);

                // Check for unsafe UIDocument retrieval
                if (Regex.IsMatch(content, @"(?:FindFirstObjectByType|FindObjectOfType|FindAnyObjectByType)\s*(?:<\s*UIDocument\s*>|\(\s*typeof\s*\(\s*UIDocument\s*\)\s*\))"))
                {
                    Debug.LogError($"[UIToolkitValidator] C# ERROR in '{scriptPath}': Unsafe use of FindFirstObjectByType/FindObjectOfType for UIDocument. This is extremely dangerous when creating a new UIDocument at runtime, as it may return the newly created UIDocument itself (resulting in null panelSettings). Use a filtered search instead.");
                    errorCount++;
                }

                MatchCollection qMatches = Regex.Matches(content, @"Q(?:<[^>]+>)?\s*\(\s*""([^""]+)""\s*\)");
                foreach (Match match in qMatches)
                {
                    string queryName = match.Groups[1].Value;
                    if (!allUxmlNames.Contains(queryName))
                    {
                        Debug.LogWarning($"[UIToolkitValidator] Binding Warning in '{scriptPath}': Query Q(\"{queryName}\") target name was not found in ANY UXML file in the project. Check for typos!");
                    }
                }

                MatchCollection classMatches = Regex.Matches(content, @"(?:AddToClassList|RemoveFromClassList)\s*\(\s*""([^""]+)""\s*\)");
                foreach (Match match in classMatches)
                {
                    string className = match.Groups[1].Value;
                    if (!allUssClasses.Contains(className))
                    {
                        Debug.LogWarning($"[UIToolkitValidator] Binding Warning in '{scriptPath}': Action references USS class '{className}', but it was not found in ANY USS file in the project.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIToolkitValidator] Failed to parse script '{scriptPath}': {ex.Message}");
                errorCount++;
            }
        }

        private static void ValidateUIDocumentPrefabBindings(ref int errorCount)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                UIDocument[] uiDocs = prefab.GetComponentsInChildren<UIDocument>(true);
                foreach (UIDocument doc in uiDocs)
                {
                    if (doc.visualTreeAsset == null) continue;

                    string uxmlPath = AssetDatabase.GetAssetPath(doc.visualTreeAsset);
                    if (string.IsNullOrEmpty(uxmlPath)) continue;

                    HashSet<string> specificUxmlNames = new HashSet<string>();
                    HashSet<string> specificUxmlClasses = new HashSet<string>();
                    int tempErrors = 0;
                    ValidateUXMLFile(uxmlPath, specificUxmlNames, specificUxmlClasses, ref tempErrors);

                    MonoBehaviour[] behaviours = doc.gameObject.GetComponents<MonoBehaviour>();
                    foreach (MonoBehaviour behaviour in behaviours)
                    {
                        if (behaviour == null) continue;
                        MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
                        if (script == null) continue;

                        string scriptPath = AssetDatabase.GetAssetPath(script);
                        if (string.IsNullOrEmpty(scriptPath) || scriptPath.Contains("UIToolkitValidator") || !scriptPath.StartsWith("Assets/")) continue;

                        ValidateSpecificScriptToUxmlBinding(scriptPath, uxmlPath, specificUxmlNames, ref errorCount);
                    }
                }
            }
        }

        private static void ValidateSpecificScriptToUxmlBinding(string scriptPath, string uxmlPath, HashSet<string> specificNames, ref int errorCount)
        {
            try
            {
                string content = File.ReadAllText(scriptPath);
                MatchCollection qMatches = Regex.Matches(content, @"Q(?:<[^>]+>)?\s*\(\s*""([^""]+)""\s*\)");
                foreach (Match match in qMatches)
                {
                    string queryName = match.Groups[1].Value;
                    if (!specificNames.Contains(queryName))
                    {
                        Debug.LogError($"[UIToolkitValidator] BINDING ERROR in '{Path.GetFileName(scriptPath)}': Component script queries element name '{queryName}' which is MISSING from its assigned UXML '{Path.GetFileName(uxmlPath)}'!");
                        errorCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIToolkitValidator] Failed to validate specific binding script '{scriptPath}': {ex.Message}");
                errorCount++;
            }
        }

        // --- Resolved Layout Validation ---

        public static LayoutTestWindow OpenMockWindow(VisualElement testRoot)
        {
            var window = EditorWindow.GetWindow<LayoutTestWindow>(true, "Layout Test Window", false);
            window.minSize = new Vector2(1920, 1080);
            window.maxSize = new Vector2(1920, 1080);
            window.position = new Rect(0, 0, 1920, 1080);
            window.rootVisualElement.Clear();
            window.rootVisualElement.Add(testRoot);
            window.Repaint();
            return window;
        }

        public static void ValidateResolvedLayout(string uxmlPath, ref int errorCount)
        {
            try
            {
                VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
                if (uxml == null) return;

                VisualElement root = uxml.CloneTree();
                root.style.width = Length.Percent(100);
                root.style.height = Length.Percent(100);

                var window = OpenMockWindow(root);
                
                // Perform recursive layout check
                ValidateElementLayoutRecursive(root, root, uxmlPath, ref errorCount);

                window.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIToolkitValidator] Failed to validate resolved layout of '{uxmlPath}': {ex.Message}");
                errorCount++;
            }
        }

        public static void ValidateElementLayoutRecursive(VisualElement element, VisualElement root, string uxmlPath, ref int errorCount)
        {
            if (element == null) return;

            if (element.resolvedStyle.display == DisplayStyle.None || element.resolvedStyle.opacity == 0)
            {
                return;
            }

            Rect layout = element.layout;

            if (float.IsNaN(layout.x) || float.IsNaN(layout.y) || float.IsNaN(layout.width) || float.IsNaN(layout.height))
            {
                Debug.LogError($"[UIToolkitValidator] Layout Error in '{uxmlPath}': Element '{element.name}' ({element.GetType().Name}) has NaN layout bounds!");
                errorCount++;
            }

            bool isContentElement = (element is Label) || (element is Button) || (element is TextElement);
            if (isContentElement && element.resolvedStyle.visibility == Visibility.Visible)
            {
                string text = "";
                if (element is Label lbl) text = lbl.text;
                else if (element is Button btn) text = btn.text;
                else if (element is TextElement txt) text = txt.text;

                if (!string.IsNullOrEmpty(text))
                {
                    if (layout.width <= 0 || layout.height <= 0)
                    {
                        Debug.LogError($"[UIToolkitValidator] Layout Error in '{uxmlPath}': Content element '{element.name}' ({element.GetType().Name}) has zero dimensions (width={layout.width}, height={layout.height}). It is invisible to the user!");
                        errorCount++;
                    }
                }
            }

            if (element.parent != null && element.parent != root && 
                element.resolvedStyle.position == Position.Relative)
            {
                Rect parentLayout = element.parent.layout;
                float tolerance = 1.0f;
                if (layout.xMin < -tolerance || layout.yMin < -tolerance || 
                    layout.xMax > parentLayout.width + tolerance || layout.yMax > parentLayout.height + tolerance)
                {
                    Debug.LogWarning($"[UIToolkitValidator] Layout Warning in '{uxmlPath}': Element '{element.name}' ({element.GetType().Name}) overflows its parent '{element.parent.name}' (Parent size: {parentLayout.width}x{parentLayout.height}, Child relative bounds: {layout.xMin},{layout.yMin} to {layout.xMax},{layout.yMax})");
                }
            }

            if (element.resolvedStyle.position == Position.Absolute && element.parent != null && element.parent != root)
            {
                Rect parentLayout = element.parent.layout;
                if (parentLayout.width <= 0 || parentLayout.height <= 0)
                {
                    Debug.LogWarning($"[UIToolkitValidator] Layout Warning in '{uxmlPath}': Absolute element '{element.name}' ({element.GetType().Name}) is inside parent '{element.parent.name}' which has collapsed size ({parentLayout.width}x{parentLayout.height}). This will cause absolute children to align incorrectly or go offscreen!");
                }
            }

            if (element.resolvedStyle.position == Position.Relative && element.parent != null)
            {
                int myIdx = element.parent.IndexOf(element);
                for (int i = myIdx + 1; i < element.parent.childCount; i++)
                {
                    VisualElement sibling = element.parent[i];
                    if (sibling != null && sibling.resolvedStyle.position == Position.Relative && 
                        sibling.resolvedStyle.display != DisplayStyle.None && sibling.resolvedStyle.opacity > 0)
                    {
                        if (layout.width > 0 && layout.height > 0 && sibling.layout.width > 0 && sibling.layout.height > 0)
                        {
                            if (!element.ClassListContains("allow-overlap") && !sibling.ClassListContains("allow-overlap"))
                            {
                                if (layout.Overlaps(sibling.layout))
                                {
                                    Debug.LogWarning($"[UIToolkitValidator] Layout Warning in '{uxmlPath}': Relative siblings '{element.name}' and '{sibling.name}' overlap each other! (Bounds A: {layout}, Bounds B: {sibling.layout})");
                                }
                            }
                        }
                    }
                }
            }

            foreach (var child in element.Children())
            {
                ValidateElementLayoutRecursive(child, root, uxmlPath, ref errorCount);
            }
        }
    }

    public class LayoutTestWindow : EditorWindow
    {
        // Dummy class to host visual trees for Yoga layout resolution
    }
}
