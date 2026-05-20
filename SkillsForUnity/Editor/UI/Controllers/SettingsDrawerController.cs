using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace UnitySkills
{
    /// <summary>
    /// Settings drawer — slide-in panel from the right edge.
    /// Hosts what used to be on Server Tab: auto-restart, port, timeout,
    /// keepalive, log level, high-risk confirmation, reset stats.
    /// </summary>
    public class SettingsDrawerController
    {
        private const string DrawerUxmlPath = "Packages/com.besty.unity-skills/Editor/UI/Tabs/SettingsDrawer.uxml";

        private readonly VisualElement _root;
        private readonly UnitySkillsWindow _window;

        private VisualElement _drawerContainer;
        private VisualElement _drawerMask;

        // Header
        private Label  _drawerTitle;
        private Button _closeBtn;

        // Server group
        private Label           _serverGroupTitle;
        private Toggle          _autoStartToggle;
        private Label           _autoStartHint;
        private Label           _portLabel;
        private DropdownField   _portDropdown;
        private Label           _timeoutLabel;
        private IntegerField    _timeoutField;
        private Label           _timeoutUnit;
        private Label           _keepaliveLabel;
        private IntegerField    _keepaliveField;
        private Label           _keepaliveUnit;
        private Label           _keepaliveHint;

        // Runtime group
        private Label         _runtimeGroupTitle;
        private Label         _loglevelLabel;
        private DropdownField _logDropdown;
        private Toggle        _confirmToggle;
        private Label         _confirmHint;

        // Stats group
        private Label  _statsGroupTitle;
        private Label  _statsHint;
        private Button _statsResetBtn;

        public SettingsDrawerController(VisualElement root, UnitySkillsWindow window)
        {
            _root = root;
            _window = window;

            _drawerContainer = _root.Q<VisualElement>("drawer");
            _drawerMask      = _root.Q<VisualElement>("drawer-mask");

            if (_drawerContainer == null)
            {
                Debug.LogError("[UnitySkills] Drawer container not found in main UXML.");
                return;
            }

            // Clone drawer content into the drawer container
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DrawerUxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"[UnitySkills] Failed to load drawer UXML: {DrawerUxmlPath}");
                return;
            }
            uxml.CloneTree(_drawerContainer);

            CacheUiReferences();
            ApplyCloseIcon();
            BindEvents();
            InitializeValues();

            // Click on mask closes the drawer
            if (_drawerMask != null)
            {
                _drawerMask.RegisterCallback<ClickEvent>(_ => Close());
            }
        }

        private void ApplyCloseIcon()
        {
            if (_closeBtn == null) return;
            // Unity 内置 winbtn_win_close 在不同版本/平台命名不一致，
            // 直接用 Unicode × 更稳定，避免 "Unable to load the icon" 警告。
            _closeBtn.text = "✕";
        }

        private void CacheUiReferences()
        {
            _drawerTitle = _drawerContainer.Q<Label>("drawer-title");
            _closeBtn    = _drawerContainer.Q<Button>("drawer-close-btn");

            _serverGroupTitle = _drawerContainer.Q<Label>("group-server-title");
            _autoStartToggle  = _drawerContainer.Q<Toggle>("autostart-toggle");
            _autoStartHint    = _drawerContainer.Q<Label>("autostart-hint");
            _portLabel        = _drawerContainer.Q<Label>("port-label");
            _portDropdown     = _drawerContainer.Q<DropdownField>("port-dropdown");
            _timeoutLabel     = _drawerContainer.Q<Label>("timeout-label");
            _timeoutField     = _drawerContainer.Q<IntegerField>("timeout-field");
            _timeoutUnit      = _drawerContainer.Q<Label>("timeout-unit");
            _keepaliveLabel   = _drawerContainer.Q<Label>("keepalive-label");
            _keepaliveField   = _drawerContainer.Q<IntegerField>("keepalive-field");
            _keepaliveUnit    = _drawerContainer.Q<Label>("keepalive-unit");
            _keepaliveHint    = _drawerContainer.Q<Label>("keepalive-hint");

            _runtimeGroupTitle = _drawerContainer.Q<Label>("group-runtime-title");
            _loglevelLabel     = _drawerContainer.Q<Label>("loglevel-label");
            _logDropdown       = _drawerContainer.Q<DropdownField>("loglevel-dropdown");
            _confirmToggle     = _drawerContainer.Q<Toggle>("confirm-toggle");
            _confirmHint       = _drawerContainer.Q<Label>("confirm-hint");

            _statsGroupTitle = _drawerContainer.Q<Label>("group-stats-title");
            _statsHint       = _drawerContainer.Q<Label>("stats-hint");
            _statsResetBtn   = _drawerContainer.Q<Button>("stats-reset-btn");
        }

        private void BindEvents()
        {
            if (_closeBtn != null) _closeBtn.clicked += Close;

            if (_autoStartToggle != null)
                _autoStartToggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue != SkillsHttpServer.AutoStart)
                        SkillsHttpServer.AutoStart = evt.newValue;
                });

            if (_portDropdown != null)
                _portDropdown.RegisterValueChangedCallback(evt =>
                {
                    int newIdx = _portDropdown.choices.IndexOf(evt.newValue);
                    int targetPort = (newIdx <= 0) ? 0 : 8089 + newIdx;
                    if (targetPort != SkillsHttpServer.PreferredPort)
                        SkillsHttpServer.PreferredPort = targetPort;
                });

            if (_timeoutField != null)
                _timeoutField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue != SkillsHttpServer.RequestTimeoutMinutes)
                        SkillsHttpServer.RequestTimeoutMinutes = evt.newValue;
                });

            if (_keepaliveField != null)
                _keepaliveField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue != SkillsHttpServer.KeepAliveIntervalSeconds)
                        SkillsHttpServer.KeepAliveIntervalSeconds = evt.newValue;
                });

            if (_logDropdown != null)
                _logDropdown.RegisterValueChangedCallback(evt =>
                {
                    int idx = _logDropdown.choices.IndexOf(evt.newValue);
                    if (idx >= 0 && idx != (int)SkillsLogger.Level)
                        SkillsLogger.Level = (LogLevel)idx;
                });

            if (_confirmToggle != null)
                _confirmToggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue != ConfirmationTokenService.RequireConfirmation)
                        ConfirmationTokenService.RequireConfirmation = evt.newValue;
                });

            if (_statsResetBtn != null)
                _statsResetBtn.clicked += () =>
                {
                    SkillsHttpServer.ResetStatistics();
                };
        }

        private void InitializeValues()
        {
            if (_portDropdown != null)
            {
                _portDropdown.choices = new List<string>
                {
                    "Auto", "8090", "8091", "8092", "8093", "8094",
                    "8095", "8096", "8097", "8098", "8099", "8100"
                };
                int currentPort = SkillsHttpServer.PreferredPort;
                int idx = (currentPort == 0) ? 0 : currentPort - 8089;
                if (idx < 0 || idx >= _portDropdown.choices.Count) idx = 0;
                _portDropdown.value = _portDropdown.choices[idx];
            }

            if (_logDropdown != null)
            {
                _logDropdown.choices = new List<string>
                {
                    "Off", "Error", "Warning", "Info", "Agent", "Verbose"
                };
                int lvl = (int)SkillsLogger.Level;
                if (lvl < 0 || lvl >= _logDropdown.choices.Count) lvl = 0;
                _logDropdown.value = _logDropdown.choices[lvl];
            }

            if (_autoStartToggle != null) _autoStartToggle.value = SkillsHttpServer.AutoStart;
            if (_timeoutField   != null) _timeoutField.value     = SkillsHttpServer.RequestTimeoutMinutes;
            if (_keepaliveField != null) _keepaliveField.value   = SkillsHttpServer.KeepAliveIntervalSeconds;
            if (_confirmToggle  != null) _confirmToggle.value    = ConfirmationTokenService.RequireConfirmation;
        }

        public void Open()
        {
            if (_drawerContainer != null) _drawerContainer.AddToClassList("open");
            if (_drawerMask != null)
            {
                _drawerMask.RemoveFromClassList("hidden");
                // next frame add 'open' for opacity transition (avoids flash)
                _drawerMask.schedule.Execute(() => _drawerMask.AddToClassList("open")).StartingIn(0);
                _drawerMask.pickingMode = PickingMode.Position;
            }
        }

        public void Close()
        {
            if (_drawerContainer != null) _drawerContainer.RemoveFromClassList("open");
            if (_drawerMask != null)
            {
                _drawerMask.RemoveFromClassList("open");
                _drawerMask.pickingMode = PickingMode.Ignore;
                // hide after the 0.18s opacity transition completes
                _drawerMask.schedule.Execute(() => _drawerMask.AddToClassList("hidden")).StartingIn(200);
            }
        }

        public void RefreshLocalization()
        {
            if (_drawerTitle != null) _drawerTitle.text = SkillsLocalization.Get("drawer_settings_title");
            if (_closeBtn != null)    _closeBtn.tooltip = SkillsLocalization.Get("drawer_close_tooltip");

            if (_serverGroupTitle  != null) _serverGroupTitle.text  = SkillsLocalization.Get("drawer_section_server");
            if (_runtimeGroupTitle != null) _runtimeGroupTitle.text = SkillsLocalization.Get("drawer_section_runtime");
            if (_statsGroupTitle   != null) _statsGroupTitle.text   = SkillsLocalization.Get("drawer_section_stats");

            if (_autoStartToggle != null) _autoStartToggle.label = SkillsLocalization.Get("auto_restart");
            if (_autoStartHint   != null) _autoStartHint.text    = SkillsLocalization.Get("auto_restart_hint");

            if (_portLabel       != null) _portLabel.text     = SkillsLocalization.Get("drawer_port_label");
            if (_timeoutLabel    != null) _timeoutLabel.text  = SkillsLocalization.Get("drawer_timeout_label");
            if (_timeoutUnit     != null) _timeoutUnit.text   = SkillsLocalization.Get("timeout_unit");
            if (_keepaliveLabel  != null) _keepaliveLabel.text = SkillsLocalization.Get("drawer_keepalive_label");
            if (_keepaliveUnit   != null) _keepaliveUnit.text  = SkillsLocalization.Get("keepalive_unit");
            if (_keepaliveHint   != null) _keepaliveHint.text  = SkillsLocalization.Get("keepalive_hint");

            if (_loglevelLabel != null) _loglevelLabel.text = SkillsLocalization.Get("drawer_loglevel_label");
            if (_confirmToggle != null) _confirmToggle.label = SkillsLocalization.Get("drawer_confirm_label");
            if (_confirmHint   != null)
            {
                _confirmHint.text = SkillsLocalization.Current == SkillsLocalization.Language.Chinese
                    ? "开启后：删除类/RiskLevel=high 技能首次调用返回 _confirm token + dryRun 预览，5 分钟内带 token 重试才执行。"
                    : "When ON: delete / high-risk skills first return a _confirm token + dryRun preview; re-call within 5 min with the token to execute.";
            }

            if (_statsHint     != null) _statsHint.text     = SkillsLocalization.Get("drawer_stats_hint");
            if (_statsResetBtn != null) _statsResetBtn.text = SkillsLocalization.Get("drawer_reset_stats_btn");
        }
    }
}
