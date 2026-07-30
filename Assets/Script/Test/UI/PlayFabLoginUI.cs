using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayFabLoginUI : MonoBehaviour
{
    [Header("Panel Size (px @1920x1080)")]
    public float panelWidth = 420f;

    [Header("Main Menu (optional — auto-detected if null)")]
    public GameObject mainMenuContent;
    public GameObject profileWidget;

    private UIDocument _doc;
    private VisualElement _panel;
    private TextField _usernameInput;
    private TextField _passwordInput;
    private Label _statusText;
    private Label _titleText;
    private Label _toggleText;
    private VisualElement _actionButton;
    private Label _actionButtonLabel;
    private VisualElement _toggleButton;
    private Label _toggleButtonLabel;
    private VisualElement _logoutButton;
    private VisualElement _usernameInputContainer;
    private VisualElement _passwordInputContainer;
    private VisualElement _btnMainSec;

    // smooth 0.5s transition factors
    private System.Collections.Generic.Dictionary<VisualElement, float> _transitionFactors = new System.Collections.Generic.Dictionary<VisualElement, float>();
    private System.Collections.Generic.Dictionary<VisualElement, bool> _hoverStates = new System.Collections.Generic.Dictionary<VisualElement, bool>();
    private System.Collections.Generic.Dictionary<VisualElement, bool> _focusStates = new System.Collections.Generic.Dictionary<VisualElement, bool>();

    private bool _isRegisterMode;
    private bool _isBusy;
    private bool _isAutoLoggingIn;

    private static readonly Color ErrorColor = new Color(0.85f, 0.35f, 0.15f, 1f);
    private static readonly Color SuccessColor = new Color(0.31f, 0.878f, 0.541f, 1f);

    private VisualElement _loginRoot;
    private VisualElement _profileRoot;

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
        if (_doc == null) { enabled = false; return; }

        var root = _doc.rootVisualElement;

        // Ensure all TemplateContainer wrappers ignore picking so clicks pass through to inner panels
        foreach (var tc in root.Query<TemplateContainer>().ToList())
        {
            tc.pickingMode = PickingMode.Ignore;
        }
        
        // Find elements within the single shared document
        _loginRoot = root.Q("PlayFabLoginRoot");
        _panel = root.Q("LoginPanel");
        if (_panel != null) _panel.style.width = panelWidth;

        _profileRoot = root.Q("ProfileRoot");

        _titleText = root.Q<Label>("LoginTitle");
        _statusText = root.Q<Label>("LoginStatus");
        _usernameInput = root.Q<TextField>("UsernameInput");
        _passwordInput = root.Q<TextField>("PasswordInput");

        // Focus text field when clicking anywhere inside their respective input-container-inner
        var userInner = root.Q("InputModule_User")?.Q(className: "input-container-inner");
        if (userInner != null)
        {
            userInner.RegisterCallback<ClickEvent>(_ => _usernameInput?.Focus());
        }
        var passInner = root.Q("InputModule_Pass")?.Q(className: "input-container-inner");
        if (passInner != null)
        {
            passInner.RegisterCallback<ClickEvent>(_ => _passwordInput?.Focus());
        }

        _actionButton = root.Q("ActionButton");
        if (_actionButton != null)
        {
            _actionButton.focusable = true;
            _actionButton.RegisterCallback<ClickEvent>(_ => OnActionButtonClicked());
            _actionButtonLabel = _actionButton.Q<Label>("ActionLabel");
        }

        _toggleText = root.Q<Label>("ToggleText");
        _toggleButton = root.Q<Label>("ToggleButton");
        if (_toggleButton != null)
        {
            _toggleButton.focusable = true;
            _toggleButton.RegisterCallback<ClickEvent>(_ => ToggleMode());
            _toggleButtonLabel = _toggleButton as Label;
        }

        _logoutButton = root.Q("TacticalLogoutButton");
        if (_logoutButton != null)
        {
            _logoutButton.focusable = true;
            _logoutButton.RegisterCallback<ClickEvent>(_ => OnLogoutClicked());
        }

        // Apply custom vector API chamfer drawing with large prominent cuts for rusted steel aesthetic
        SetupChamferedPlaque(root.Q("HeaderModule"), 34f, true, false, null);
        SetupChamferedPlaque(root.Q("InputModule_User"), 30f, true, false, null);
        SetupChamferedPlaque(root.Q("InputModule_Pass"), 30f, true, false, null);
        SetupChamferedPlaque(root.Q("ActionModule"), 34f, true, false, null);

        SetupChamferedPlaque(root.Q("MainMenuModule_Play"), 30f, true, false, null);
        SetupChamferedPlaque(root.Q("MainMenuModule_Profile"), 30f, true, false, null);
        SetupChamferedPlaque(root.Q("MainMenuModule_Rankings"), 34f, true, false, null);

        // Find child input fields and button sections to apply matching chamfered cuts
        _usernameInputContainer = root.Q("InputModule_User")?.Q(className: "input-container-inner");
        _passwordInputContainer = root.Q("InputModule_Pass")?.Q(className: "input-container-inner");
        _btnMainSec = root.Q("ActionButton")?.Q(className: "btn-main-section");

        SetupChamferedPlaque(_usernameInputContainer, 12f, false, false, null);
        SetupChamferedPlaque(_passwordInputContainer, 12f, false, false, null);
        SetupChamferedPlaque(_btnMainSec, 10f, false, true, null);

        // Set up plaques for the 3 menu module button triggers
        var playBtnTrigger = root.Q("MainMenuModule_Play")?.Q(className: "button-trigger");
        var profileBtnTrigger = root.Q("MainMenuModule_Profile")?.Q(className: "button-trigger");
        var rankingsBtnTrigger = root.Q("MainMenuModule_Rankings")?.Q(className: "button-trigger");

        SetupChamferedPlaque(playBtnTrigger, 12f, false, false, null);
        SetupChamferedPlaque(profileBtnTrigger, 12f, false, false, null);
        SetupChamferedPlaque(rankingsBtnTrigger, 12f, false, false, null);

        // Hook up focus/blur listeners to trigger smooth transition border glows
        if (_usernameInput != null)
        {
            _usernameInput.RegisterCallback<FocusInEvent>(_ => {
                if (_usernameInputContainer != null) _focusStates[_usernameInputContainer] = true;
            });
            _usernameInput.RegisterCallback<FocusOutEvent>(_ => {
                if (_usernameInputContainer != null) _focusStates[_usernameInputContainer] = false;
            });
        }
        if (_passwordInput != null)
        {
            _passwordInput.RegisterCallback<FocusInEvent>(_ => {
                if (_passwordInputContainer != null) _focusStates[_passwordInputContainer] = true;
            });
            _passwordInput.RegisterCallback<FocusOutEvent>(_ => {
                if (_passwordInputContainer != null) _focusStates[_passwordInputContainer] = false;
            });
        }
    }

    private void OnEnable()
    {
        AutoDetectMainMenu();
        HideMainMenu(false);
        StartCoroutine(BindAndShow());
    }

    private void Update()
    {
        var root = _doc?.rootVisualElement;
        if (root == null || _loginRoot == null || _loginRoot.style.display == DisplayStyle.None) return;

        // Smoothly interpolate hover/focus transition factors over 0.5s
        var keys = new System.Collections.Generic.List<VisualElement>(_transitionFactors.Keys);
        foreach (var key in keys)
        {
            if (key == null) continue;
            bool active = _hoverStates.ContainsKey(key) && _hoverStates[key];
            if (_focusStates.ContainsKey(key) && _focusStates[key]) active = true;

            float target = active ? 1f : 0f;
            float current = _transitionFactors[key];
            if (current != target)
            {
                _transitionFactors[key] = Mathf.MoveTowards(current, target, Time.deltaTime / 0.5f);
                key.MarkDirtyRepaint();
            }
        }

        root.Q("HeaderModule")?.MarkDirtyRepaint();
        root.Q("InputModule_User")?.MarkDirtyRepaint();
        root.Q("InputModule_Pass")?.MarkDirtyRepaint();
        root.Q("ActionModule")?.MarkDirtyRepaint();
        root.Q("MainMenuModule_Play")?.MarkDirtyRepaint();
        root.Q("MainMenuModule_Profile")?.MarkDirtyRepaint();
        root.Q("MainMenuModule_Rankings")?.MarkDirtyRepaint();
    }

    private void AutoDetectMainMenu()
    {
        // Keep fallback support for manually assigned GameObjects
        if (mainMenuContent == null)
        {
            var tr = transform.parent?.Find("Content");
            if (tr != null) mainMenuContent = tr.gameObject;
        }
        if (profileWidget == null)
        {
            var tr = transform.parent?.Find("PlayerProfileWidget");
            if (tr != null) profileWidget = tr.gameObject;
        }
    }

    private void HideMainMenu(bool animate = true)
    {
        if (profileWidget != null) profileWidget.SetActive(false);
        if (animate)
        {
            StartCoroutine(TransitionToLoginState());
        }
        else
        {
            var rootElement = _doc?.rootVisualElement;
            if (rootElement != null)
            {
                var loginModules = rootElement.Query(className: "login-only").ToList();
                var menuModules = rootElement.Query(className: "menu-only").ToList();
                foreach (var el in menuModules)
                {
                    el.style.display = DisplayStyle.None;
                    el.RemoveFromClassList("slide-in");
                }
                foreach (var el in loginModules)
                {
                    el.style.display = DisplayStyle.Flex;
                    el.RemoveFromClassList("slide-in");
                    el.RemoveFromClassList("slide-out-right");
                }

                // Hide logout button initially
                var logoutBtn = rootElement.Q("TacticalLogoutButton");
                if (logoutBtn != null)
                {
                    logoutBtn.style.display = DisplayStyle.None;
                    logoutBtn.RemoveFromClassList("slide-in");
                }

                // Ensure quit button starts at opacity 0
                var quitBtn = rootElement.Q("TacticalQuitButton");
                if (quitBtn != null)
                {
                    quitBtn.RemoveFromClassList("slide-in");
                }
            }
        }
    }

    private void ShowMainMenu()
    {
        if (profileWidget != null) profileWidget.SetActive(true);
        StartCoroutine(TransitionToMenuState());
    }

    private IEnumerator TransitionToLoginState()
    {
        var root = _doc?.rootVisualElement;
        if (root == null) yield break;

        var loginModules = root.Query(className: "login-only").ToList();
        var menuModules = root.Query(className: "menu-only").ToList();

        // 1. Slide out menu modules to the left sequentially (bottom first)
        var logoutBtn = root.Q("TacticalLogoutButton");
        if (logoutBtn != null) logoutBtn.RemoveFromClassList("slide-in");

        for (int i = menuModules.Count - 1; i >= 0; i--)
        {
            if (menuModules[i] != null)
            {
                menuModules[i].RemoveFromClassList("slide-in");
                yield return new WaitForSeconds(0.15f);
            }
        }

        // Wait 1.5s for exit transition (translate 1.5s ease-out-back)
        yield return new WaitForSeconds(1.5f);

        // 2. Hide menu modules and show login modules in layout
        if (logoutBtn != null) logoutBtn.style.display = DisplayStyle.None;
        foreach (var el in menuModules)
        {
            el.style.display = DisplayStyle.None;
        }
        foreach (var el in loginModules)
        {
            el.style.display = DisplayStyle.Flex;
            el.RemoveFromClassList("slide-out-right");
            el.RemoveFromClassList("slide-in");
        }

        yield return null;

        // 3. Stagger slide-in of login modules from the left
        foreach (var el in loginModules)
        {
            el.AddToClassList("slide-in");
            yield return new WaitForSeconds(0.15f);
        }
    }

    private IEnumerator TransitionToMenuState()
    {
        var root = _doc?.rootVisualElement;
        if (root == null) yield break;

        var loginModules = root.Query(className: "login-only").ToList();
        var menuModules = root.Query(className: "menu-only").ToList();

        // 1. Slide out login modules to the left sequentially (bottom first)
        for (int i = loginModules.Count - 1; i >= 0; i--)
        {
            if (loginModules[i] != null)
            {
                loginModules[i].RemoveFromClassList("slide-in");
                yield return new WaitForSeconds(0.15f);
            }
        }

        // Wait 1.5s for exit transition (translate 1.5s ease-out-back)
        yield return new WaitForSeconds(1.5f);

        // 2. Hide login modules and show menu modules in layout
        var logoutBtn = root.Q("TacticalLogoutButton");
        if (logoutBtn != null)
        {
            logoutBtn.style.display = DisplayStyle.Flex;
            logoutBtn.RemoveFromClassList("slide-in");
        }
        foreach (var el in loginModules)
        {
            el.style.display = DisplayStyle.None;
        }
        foreach (var el in menuModules)
        {
            el.style.display = DisplayStyle.Flex;
            el.RemoveFromClassList("slide-out-right");
            el.RemoveFromClassList("slide-in");
        }

        yield return null;

        if (logoutBtn != null) logoutBtn.AddToClassList("slide-in");

        // 3. Stagger slide-in of menu modules from the left
        foreach (var el in menuModules)
        {
            el.AddToClassList("slide-in");
            yield return new WaitForSeconds(0.15f);
        }
    }

    public void SlideOutAllMenuElements()
    {
        var root = _doc?.rootVisualElement;
        if (root == null) return;

        var logoutBtn = root.Q("TacticalLogoutButton");
        var quitBtn = root.Q("TacticalQuitButton");
        var headerModule = root.Q("HeaderModule");
        var playModule = root.Q("MainMenuModule_Play");
        var profileModule = root.Q("MainMenuModule_Profile");
        var rankingsModule = root.Q("MainMenuModule_Rankings");

        // Login screen modules (crucial for sliding out when quitting from login screen)
        var userModule = root.Q("InputModule_User");
        var passModule = root.Q("InputModule_Pass");
        var actionModule = root.Q("ActionModule");

        if (logoutBtn != null) logoutBtn.RemoveFromClassList("slide-in");
        if (quitBtn != null) quitBtn.RemoveFromClassList("slide-in");
        if (headerModule != null) headerModule.RemoveFromClassList("slide-in");
        if (playModule != null) playModule.RemoveFromClassList("slide-in");
        if (profileModule != null) profileModule.RemoveFromClassList("slide-in");
        if (rankingsModule != null) rankingsModule.RemoveFromClassList("slide-in");

        if (userModule != null) userModule.RemoveFromClassList("slide-in");
        if (passModule != null) passModule.RemoveFromClassList("slide-in");
        if (actionModule != null) actionModule.RemoveFromClassList("slide-in");
    }

    public IEnumerator SlideOutAllMenuElementsCoroutine()
    {
        var root = _doc?.rootVisualElement;
        if (root == null) yield break;

        var logoutBtn = root.Q("TacticalLogoutButton");
        var quitBtn = root.Q("TacticalQuitButton");
        var headerModule = root.Q("HeaderModule");

        var loginModules = root.Query(className: "login-only").ToList();
        var menuModules = root.Query(className: "menu-only").ToList();

        // Build list of active slide-in elements from bottom to top
        var elements = new System.Collections.Generic.List<VisualElement>();

        // 1. TacticalQuitButton (bottom-most)
        if (quitBtn != null && quitBtn.ClassListContains("slide-in"))
            elements.Add(quitBtn);

        // 2. TacticalLogoutButton
        if (logoutBtn != null && logoutBtn.ClassListContains("slide-in"))
            elements.Add(logoutBtn);

        // 3. Login modules (bottom to top)
        for (int i = loginModules.Count - 1; i >= 0; i--)
        {
            if (loginModules[i] != null && loginModules[i].ClassListContains("slide-in"))
                elements.Add(loginModules[i]);
        }

        // 4. Menu modules (bottom to top)
        for (int i = menuModules.Count - 1; i >= 0; i--)
        {
            if (menuModules[i] != null && menuModules[i].ClassListContains("slide-in"))
                elements.Add(menuModules[i]);
        }

        // 5. HeaderModule (top-most)
        if (headerModule != null && headerModule.ClassListContains("slide-in"))
            elements.Add(headerModule);

        // Slide out sequentially with 0.15s delay
        foreach (var el in elements)
        {
            el.RemoveFromClassList("slide-in");
            yield return new WaitForSeconds(0.15f);
        }
    }

    private void OnDisable()
    {
        var pm = PlayFabManager.Instance;
        if (pm != null)
        {
            pm.OnLoginSuccess -= HandleLoginSuccess;
            pm.OnLoginError -= HandleLoginError;
            pm.OnLogout -= HandleLogout;
        }
    }

    private IEnumerator BindAndShow()
    {
        float timeout = 10f;
        while (PlayFabManager.Instance == null && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        var pm = PlayFabManager.Instance;
        if (pm == null)
        {
            Debug.LogWarning("[PlayFabLoginUI] PlayFabManager not found.");
            ShowStatus("PlayFabManager not found.", ErrorColor);
            if (_loginRoot != null) _loginRoot.style.display = DisplayStyle.Flex;
            if (_panel != null) _panel.style.display = DisplayStyle.Flex;
            yield break;
        }

        pm.OnLoginSuccess += HandleLoginSuccess;
        pm.OnLoginError += HandleLoginError;
        pm.OnLogout += HandleLogout;

        // Check for saved credentials for auto-login
        string savedUser = PlayerPrefs.GetString("Save_Username", "");
        string savedPass = PlayerPrefs.GetString("Save_Password", "");
        bool hasSavedCredentials = !string.IsNullOrEmpty(savedUser) && !string.IsNullOrEmpty(savedPass);

        bool autoLoginCompleted = false;
        bool autoLoginSuccess = false;

        if (hasSavedCredentials && !pm.IsLoggedIn)
        {
            _isAutoLoggingIn = true;
            ShowStatus("Decrypting saved session...", new Color(0.85f, 0.78f, 0.45f, 1f));
            pm.Login(savedUser, savedPass, (success, error) => {
                autoLoginSuccess = success;
                autoLoginCompleted = true;
            });
        }
        else
        {
            autoLoginCompleted = true;
        }



        HideMainMenu(false);
        UpdateUI();

        // 1. Reset all modules to slide out of screen at start (CSS handles position: absolute left)
        var modules = new string[] { 
            "HeaderModule", "InputModule_User", "InputModule_Pass", "ActionModule",
            "MainMenuModule_Play", "MainMenuModule_Profile", "MainMenuModule_Rankings"
        };
        foreach (var name in modules)
        {
            var el = _panel?.Q(name);
            el?.RemoveFromClassList("slide-in");
            el?.RemoveFromClassList("slide-out-right");
        }

        // Hide menu modules initially at layout level
        var rootElement = _doc?.rootVisualElement;
        if (rootElement != null)
        {
            var loginOnlyList = rootElement.Query(className: "login-only").ToList();
            foreach (var el in loginOnlyList) el.style.display = DisplayStyle.Flex;
            var menuOnlyList = rootElement.Query(className: "menu-only").ToList();
            foreach (var el in menuOnlyList) el.style.display = DisplayStyle.None;
        }

        // 2. Initial state: Black overlay is visible
        var overlay = _loginRoot?.Q("BlackOverlay");
        if (overlay != null)
        {
            overlay.pickingMode = PickingMode.Position; // Block clicks while black
            overlay.RemoveFromClassList("fade-out");
        }

        // Wait a frame for layout parsing
        yield return null;

        // 3. Start black screen transition fade out (3s in CSS)
        if (overlay != null)
        {
            overlay.AddToClassList("fade-out");
        }

        // Wait 3.0 seconds for black overlay fade out to complete
        yield return new WaitForSeconds(3.0f);

        // Allow clicks to pass through after fade out completes
        if (overlay != null) overlay.pickingMode = PickingMode.Ignore;

        // Wait for auto-login to complete or timeout (max 3.0 seconds additional delay)
        float autoLoginTimeout = 3.0f;
        while (!autoLoginCompleted && autoLoginTimeout > 0f)
        {
            autoLoginTimeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        // Check if auto-login was successful
        bool startAsLoggedIn = pm.IsLoggedIn || autoLoginSuccess;

        if (startAsLoggedIn)
        {
            // Transition menu modules in
            if (rootElement != null)
            {
                var loginOnlyList = rootElement.Query(className: "login-only").ToList();
                foreach (var el in loginOnlyList) el.style.display = DisplayStyle.None;
                var menuOnlyList = rootElement.Query(className: "menu-only").ToList();
                foreach (var el in menuOnlyList) el.style.display = DisplayStyle.Flex;
            }

            if (profileWidget != null) profileWidget.SetActive(true);

            // Stagger slide-in of menu modules and fade-in of logout button
            var logoutBtn = _loginRoot?.Q("TacticalLogoutButton");
            if (logoutBtn != null)
            {
                logoutBtn.style.display = DisplayStyle.Flex;
                logoutBtn.RemoveFromClassList("slide-in");
            }

            var quitBtn = _loginRoot?.Q("TacticalQuitButton");
            if (quitBtn != null)
            {
                quitBtn.RemoveFromClassList("slide-in");
            }
            
            yield return null;

            if (logoutBtn != null) logoutBtn.AddToClassList("slide-in");
            if (quitBtn != null) quitBtn.AddToClassList("slide-in");

            var startModules = new string[] { "HeaderModule", "MainMenuModule_Play", "MainMenuModule_Profile", "MainMenuModule_Rankings" };
            foreach (var name in startModules)
            {
                var el = _panel?.Q(name);
                el?.AddToClassList("slide-in");
                yield return new WaitForSeconds(0.15f);
            }
        }
        else
        {
            // Transition login modules in
            var quitBtn = _loginRoot?.Q("TacticalQuitButton");
            if (quitBtn != null) quitBtn.AddToClassList("slide-in");

            var startModules = new string[] { "HeaderModule", "InputModule_User", "InputModule_Pass", "ActionModule" };
            foreach (var name in startModules)
            {
                var el = _panel?.Q(name);
                el?.AddToClassList("slide-in");
                yield return new WaitForSeconds(0.15f);
            }
        }
    }

    private void ToggleMode()
    {
        _isRegisterMode = !_isRegisterMode;
        if (_usernameInput != null) _usernameInput.value = "";
        if (_passwordInput != null) _passwordInput.value = "";
        
        // Trigger Pop animation (swell/bounce)
        TriggerPopAnimation();

        UpdateUI();
    }

    private void TriggerPopAnimation()
    {
        var modules = new string[] { 
            "HeaderModule", "InputModule_User", "InputModule_Pass", "ActionModule",
            "MainMenuModule_Play", "MainMenuModule_Profile", "MainMenuModule_Rankings"
        };
        foreach (var name in modules)
        {
            var el = _panel?.Q(name);
            if (el != null)
            {
                el.AddToClassList("module-pop");
                el.schedule.Execute(() => {
                    el.RemoveFromClassList("module-pop");
                }).StartingIn(150);
            }
        }
    }

    private void UpdateUI()
    {
        if (_isRegisterMode)
        {
            if (_titleText != null) _titleText.text = "REGISTER";
            if (_actionButtonLabel != null) _actionButtonLabel.text = "CREATE ACCOUNT";
            if (_toggleText != null) _toggleText.text = "Already have an account?";
            if (_toggleButtonLabel != null) _toggleButtonLabel.text = "Login";
        }
        else
        {
            if (_titleText != null) _titleText.text = "LOGIN";
            if (_actionButtonLabel != null) _actionButtonLabel.text = "LOGIN";
            if (_toggleText != null) _toggleText.text = "Don't have an account?";
            if (_toggleButtonLabel != null) _toggleButtonLabel.text = "Register";
        }
    }

    private void OnActionButtonClicked()
    {
        if (_isBusy) return;

        string username = _usernameInput?.text.Trim() ?? "";
        string password = _passwordInput?.text ?? "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowStatus("Please enter username and password.", ErrorColor);
            return;
        }

        // Validate Username requirements (PlayFab constraints: 3-20 chars, alphanumeric/underscore/hyphen)
        if (username.Length < 3 || username.Length > 20)
        {
            ShowStatus("Username must be between 3 and 20 characters.", ErrorColor);
            return;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(username, "^[a-zA-Z0-9_-]+$"))
        {
            ShowStatus("Username can only contain letters, numbers, _ and -", ErrorColor);
            return;
        }

        if (!char.IsLetter(username[0]))
        {
            ShowStatus("Username must start with a letter.", ErrorColor);
            return;
        }

        // Validate Password requirements (Registration only: 6-30 chars, no spaces, complexity checks)
        if (_isRegisterMode)
        {
            if (password.Length < 6 || password.Length > 30)
            {
                ShowStatus("Password must be between 6 and 30 characters.", ErrorColor);
                return;
            }

            if (password.Contains(" "))
            {
                ShowStatus("Password cannot contain spaces.", ErrorColor);
                return;
            }

            bool hasUpper = false;
            bool hasLower = false;
            bool hasDigit = false;
            bool hasSpecial = false;
            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsLower(c)) hasLower = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else if (!char.IsLetterOrDigit(c)) hasSpecial = true;
            }

            if (!hasUpper)
            {
                ShowStatus("Password must contain an uppercase letter.", ErrorColor);
                return;
            }
            if (!hasLower)
            {
                ShowStatus("Password must contain a lowercase letter.", ErrorColor);
                return;
            }
            if (!hasDigit)
            {
                ShowStatus("Password must contain at least one number.", ErrorColor);
                return;
            }
            if (!hasSpecial)
            {
                ShowStatus("Password must contain a special character.", ErrorColor);
                return;
            }
        }

        SetBusy(true);
        ShowStatus(_isRegisterMode ? "Creating account..." : "Logging in...", new Color(0.62f, 0.66f, 0.72f, 1f));

        var pm = PlayFabManager.Instance;
        if (pm == null)
        {
            ShowStatus("PlayFabManager not found.", ErrorColor);
            SetBusy(false);
            return;
        }

        if (_isRegisterMode)
        {
            pm.Register(username, password, (success, error) =>
            {
                SetBusy(false);
                if (success)
                    ShowStatus("Account created! Welcome.", SuccessColor);
                else
                    ShowStatus(error ?? "Registration failed.", ErrorColor);
            });
        }
        else
        {
            pm.Login(username, password, (success, error) =>
            {
                SetBusy(false);
                if (success)
                    ShowStatus("Login successful!", SuccessColor);
                else
                    ShowStatus(error ?? "Login failed.", ErrorColor);
            });
        }
    }

    private void OnLogoutClicked()
    {
        PlayerPrefs.DeleteKey("Save_Username");
        PlayerPrefs.DeleteKey("Save_Password");
        PlayerPrefs.Save();

        PlayFabManager.Instance?.Logout();
        if (_loginRoot != null) _loginRoot.style.display = DisplayStyle.Flex;
        if (_panel != null) _panel.style.display = DisplayStyle.Flex;
        HideMainMenu();
        ShowStatus("Logged out.", new Color(0.62f, 0.66f, 0.72f, 1f));
    }

    private void HandleLogout()
    {
        PlayerPrefs.DeleteKey("Save_Username");
        PlayerPrefs.DeleteKey("Save_Password");
        PlayerPrefs.Save();

        _isRegisterMode = false;
        _isBusy = false;
        if (_usernameInput != null) _usernameInput.value = "";
        if (_passwordInput != null) _passwordInput.value = "";
        _actionButton?.SetEnabled(true);
        HideMainMenu();
        if (_loginRoot != null) _loginRoot.style.display = DisplayStyle.Flex;
        if (_panel != null) _panel.style.display = DisplayStyle.Flex;
        UpdateUI();
        ShowStatus("Logged out. Please log in again.", new Color(0.62f, 0.66f, 0.72f, 1f));
    }

    public void ShowLoginPanel() { HandleLogout(); }

    private void HandleLoginSuccess(string username)
    {
        if (_isAutoLoggingIn)
        {
            _isAutoLoggingIn = false;
            return;
        }

        if (_usernameInput != null && _passwordInput != null && !string.IsNullOrEmpty(_usernameInput.value))
        {
            PlayerPrefs.SetString("Save_Username", _usernameInput.value);
            PlayerPrefs.SetString("Save_Password", _passwordInput.value);
            PlayerPrefs.Save();
        }

        if (_usernameInput != null) _usernameInput.value = "";
        if (_passwordInput != null) _passwordInput.value = "";
        ShowStatus($"Welcome, {username}!", SuccessColor);
        StartCoroutine(HideAfterDelay(3.0f));
    }

    private void HandleLoginError(string error)
    {
        if (_isAutoLoggingIn)
        {
            _isAutoLoggingIn = false;
            return;
        }
        ShowStatus(error, ErrorColor);
        SetBusy(false);
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        ShowMainMenu();
    }

    private void ShowStatus(string message, Color color)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
            _statusText.style.color = color;
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _actionButton?.SetEnabled(!busy);
    }

    private void SetupChamferedPlaque(VisualElement element, float chamferSize, bool drawScanline, bool isButton, Material customMat)
    {
        if (element == null) return;
        
        // Remove default USS backgrounds/borders to avoid double rendering
        element.style.backgroundColor = Color.clear;
        element.style.borderLeftWidth = 0;
        element.style.borderRightWidth = 0;
        element.style.borderTopWidth = 0;
        element.style.borderBottomWidth = 0;

        _transitionFactors[element] = 0f;
        _hoverStates[element] = false;
        _focusStates[element] = false;

        // All drawing is done directly on the element's own generateVisualContent callback

        element.RegisterCallback<MouseEnterEvent>(_ => {
            _hoverStates[element] = true;
            element.MarkDirtyRepaint();
        });

        element.RegisterCallback<MouseLeaveEvent>(_ => {
            _hoverStates[element] = false;
            element.MarkDirtyRepaint();
        });

        element.generateVisualContent += mgc =>
        {
            var rect = element.layout;
            if (rect.width <= 0 || rect.height <= 0) return;

            var painter = mgc.painter2D;

            // Retrieve current transition factor (0.5s smooth transition)
            float t = 0f;
            if (_transitionFactors.ContainsKey(element)) t = _transitionFactors[element];

            // 1. Draw solid background shape for all elements
            Color fillCol;
            if (isButton)
            {
                // Lerp button background on hover
                fillCol = Color.Lerp(
                    new Color(230f / 255f, 80f / 255f, 40f / 255f, 1f),
                    new Color(250f / 255f, 100f / 255f, 60f / 255f, 1f),
                    t
                );
            }
            else if (drawScanline)
            {
                // Dark rusted-iron brown backing for modules
                fillCol = new Color(36f / 255f, 22f / 255f, 22f / 255f, 0.94f);
            }
            else
            {
                // Darker backing for input boxes
                fillCol = new Color(22f / 255f, 15f / 255f, 15f / 255f, 0.85f);
            }

            painter.fillColor = fillCol;
            painter.BeginPath();
            painter.MoveTo(new Vector2(chamferSize, 0));
            painter.LineTo(new Vector2(rect.width, 0));
            painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
            painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
            painter.LineTo(new Vector2(0, rect.height));
            painter.LineTo(new Vector2(0, chamferSize));
            painter.ClosePath();
            painter.Fill();

            // 2. Draw yellow-black diagonal warning stripes at the top edge of main modules
            if (drawScanline)
            {
                float badgeW = 60f;
                float badgeH = 7f;
                float startX = rect.width - badgeW - 24f;
                float startY = 4f;

                painter.lineWidth = 1.0f;
                for (float offset = 0; offset < badgeW; offset += 6f)
                {
                    // Draw a yellow stripe
                    painter.strokeColor = new Color(230f / 255f, 180f / 255f, 20f / 255f, 0.8f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(startX + offset, startY));
                    painter.LineTo(new Vector2(startX + offset - 4f, startY + badgeH));
                    painter.Stroke();

                    // Draw a black stripe
                    painter.strokeColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.9f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(startX + offset + 3f, startY));
                    painter.LineTo(new Vector2(startX + offset - 1f, startY + badgeH));
                    painter.Stroke();
                }
            }

            // 3. Determine outer stroke color and width (with smooth 0.5s transition)
            Color strokeCol;
            float lineWidth;
            if (isButton)
            {
                strokeCol = new Color(255f / 255f, 255f / 255f, 255f / 255f, 0.3f);
                lineWidth = 1.0f;
            }
            else if (drawScanline) // Pulsing modules
            {
                float pulse = 0.35f + Mathf.PingPong(Time.realtimeSinceStartup * 1.5f, 0.45f);
                // Lerp stroke color from pulse to bright orange, and width from 1.5f to 3.5f
                strokeCol = Color.Lerp(
                    new Color(230f / 255f, 80f / 255f, 40f / 255f, pulse),
                    new Color(230f / 255f, 80f / 255f, 40f / 255f, 1.0f),
                    t
                );
                lineWidth = Mathf.Lerp(1.5f, 3.5f, t);
            }
            else // Input containers
            {
                // Lerp stroke color from 0.25f to 0.8f, and width from 1.5f to 3.5f
                strokeCol = Color.Lerp(
                    new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.25f),
                    new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.8f),
                    t
                );
                lineWidth = Mathf.Lerp(1.5f, 3.5f, t);
            }

            // Draw outer border
            painter.strokeColor = strokeCol;
            painter.lineWidth = lineWidth;
            painter.BeginPath();
            painter.MoveTo(new Vector2(chamferSize, 0));
            painter.LineTo(new Vector2(rect.width, 0));
            painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
            painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
            painter.LineTo(new Vector2(0, rect.height));
            painter.LineTo(new Vector2(0, chamferSize));
            painter.ClosePath();
            painter.Stroke();

            // 4. Draw inner offset double-line border (only for main modules)
            if (drawScanline)
            {
                float d = 3.5f;
                if (rect.width > d * 2 && rect.height > d * 2)
                {
                    // Lerp inner border color opacity on hover/focus
                    Color innerCol = Color.Lerp(
                        new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.15f),
                        new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.45f),
                        t
                    );
                    
                    painter.strokeColor = innerCol;
                    painter.lineWidth = 1.0f;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(chamferSize, d));
                    painter.LineTo(new Vector2(rect.width - d, d));
                    painter.LineTo(new Vector2(rect.width - d, rect.height - chamferSize));
                    painter.LineTo(new Vector2(rect.width - chamferSize, rect.height - d));
                    painter.LineTo(new Vector2(d, rect.height - d));
                    painter.LineTo(new Vector2(d, chamferSize));
                    painter.ClosePath();
                    painter.Stroke();
                }

                // 5. Draw 4 3D metallic corner rivets (screws) to lock down the plates
                System.Action<Vector2> drawRivet = center =>
                {
                    // Dark outline shadow
                    painter.fillColor = new Color(10f / 255f, 8f / 255f, 8f / 255f, 0.9f);
                    painter.BeginPath();
                    painter.Arc(center, 3.5f, 0f, 360f);
                    painter.Fill();

                    // Inner metallic copper/steel disc
                    painter.fillColor = new Color(130f / 255f, 100f / 255f, 90f / 255f, 0.9f);
                    painter.BeginPath();
                    painter.Arc(center, 2.2f, 0f, 360f);
                    painter.Fill();

                    // Tiny shiny specular highlight
                    painter.fillColor = new Color(255f / 255f, 255f / 255f, 255f / 255f, 0.4f);
                    painter.BeginPath();
                    painter.Arc(center + new Vector2(-0.8f, -0.8f), 0.7f, 0f, 360f);
                    painter.Fill();
                };

                drawRivet(new Vector2(chamferSize + 12f, 12f));
                drawRivet(new Vector2(rect.width - 24f, 12f));
                drawRivet(new Vector2(24f, rect.height - 12f));
                drawRivet(new Vector2(rect.width - chamferSize - 12f, rect.height - 12f));
            }
        };
    }


}
