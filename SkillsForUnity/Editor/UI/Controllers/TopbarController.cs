using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace UnitySkills
{
    /// <summary>
    /// Persistent topbar controller — status dot, URL pill, server toggle
    /// switch, status text, settings (gear) button.
    /// Owned by UnitySkillsWindow; bound to elements that live in the main UXML.
    /// </summary>
    public class TopbarController
    {
        private readonly VisualElement _root;
        private readonly UnitySkillsWindow _window;

        private VisualElement _statusDot;
        private TextField     _urlField;
        private Button        _copyBtn;
        private VisualElement _serverSwitch;
        private Label         _statusText;
        private Button        _settingsBtn;

        private bool? _lastRunning;

        public TopbarController(VisualElement root, UnitySkillsWindow window)
        {
            _root = root;
            _window = window;

            _statusDot    = _root.Q<VisualElement>("status-dot");
            _urlField     = _root.Q<TextField>("url-field");
            _copyBtn      = _root.Q<Button>("url-copy-btn");
            _serverSwitch = _root.Q<VisualElement>("server-switch");
            _statusText   = _root.Q<Label>("server-status-text");
            _settingsBtn  = _root.Q<Button>("open-settings-btn");

            ApplySettingsIcon();
            BindEvents();
            UpdateLiveData(); // initial paint
        }

        /// <summary>
        /// Replace the placeholder ⚙ char with Unity's built-in Settings icon.
        /// Tried in order: d_SettingsIcon, SettingsIcon, _Popup. The last one
        /// always exists as a final fallback.
        /// </summary>
        private void ApplySettingsIcon()
        {
            if (_settingsBtn == null) return;
            var icon = EditorGUIUtility.IconContent("d_SettingsIcon")?.image
                       ?? EditorGUIUtility.IconContent("SettingsIcon")?.image
                       ?? EditorGUIUtility.IconContent("_Popup")?.image;
            if (icon == null) return;

            _settingsBtn.text = "";
            _settingsBtn.style.backgroundImage = new StyleBackground((Texture2D)icon);
            _settingsBtn.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }

        private void BindEvents()
        {
            if (_copyBtn != null)
            {
                _copyBtn.clicked += () =>
                {
                    if (!string.IsNullOrEmpty(SkillsHttpServer.Url))
                        EditorGUIUtility.systemCopyBuffer = SkillsHttpServer.Url;
                };
            }

            if (_settingsBtn != null)
            {
                _settingsBtn.clicked += () => _window.OpenSettings();
            }

            if (_serverSwitch != null)
            {
                // Click anywhere on the switch toggles the server
                _serverSwitch.RegisterCallback<ClickEvent>(_ => ToggleServer());
            }
        }

        private void ToggleServer()
        {
            if (SkillsHttpServer.IsRunning)
                SkillsHttpServer.StopPermanent();
            else
                SkillsHttpServer.Start(SkillsHttpServer.PreferredPort);

            UpdateLiveData();
        }

        public void UpdateLiveData()
        {
            bool running = SkillsHttpServer.IsRunning;

            if (_statusDot != null)
            {
                _statusDot.RemoveFromClassList("success");
                _statusDot.RemoveFromClassList("error");
                _statusDot.AddToClassList(running ? "success" : "error");
            }

            if (_serverSwitch != null)
            {
                if (running) _serverSwitch.AddToClassList("on");
                else         _serverSwitch.RemoveFromClassList("on");
            }

            if (_statusText != null)
            {
                _statusText.text = SkillsLocalization.Get(running ? "topbar_running" : "topbar_stopped");
                _statusText.RemoveFromClassList("on");
                _statusText.RemoveFromClassList("off");
                _statusText.AddToClassList(running ? "on" : "off");
            }

            // Refresh URL only when state changes or text differs
            if (_urlField != null)
            {
                string url = running ? SkillsHttpServer.Url ?? "" : "";
                if (_urlField.value != url) _urlField.value = url;
            }

            _lastRunning = running;
        }

        public void RefreshLocalization()
        {
            if (_copyBtn != null)     _copyBtn.text     = SkillsLocalization.Get("topbar_copy_url");
            if (_settingsBtn != null) _settingsBtn.tooltip = SkillsLocalization.Get("topbar_settings_tooltip");
            if (_serverSwitch != null) _serverSwitch.tooltip = SkillsLocalization.Get("topbar_server_tooltip");

            // Force re-render running/stopped text in current language
            UpdateLiveData();
        }
    }
}
