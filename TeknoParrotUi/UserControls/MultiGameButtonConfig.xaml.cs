using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Views;
using TeknoParrotUi.Properties;

namespace TeknoParrotUi.UserControls
{
    public partial class MultiGameButtonConfig : UserControl, INotifyPropertyChanged
    {
        private readonly ContentControl _contentControl;
        private readonly List<GameProfile> _allGameProfiles;
        private readonly Library _library;
        private InputApi _currentInputApi = InputApi.DirectInput;
        private List<GameViewModel> _filteredGames = new List<GameViewModel>();
        private List<JoystickButtons> _commonButtons = new List<JoystickButtons>();
        private bool _isLoading = true;

        private JoystickControlDirectInput _joystickControlDirectInput;
        private JoystickControlXInput _joystickControlXInput;
        private JoystickControlRawInput _joystickControlRawInput;

        // Input listener helpers
        private Thread _inputListener;
        private TextBox _lastActiveTextBox;
        private bool _isListening = false;
        private bool _hasUnsavedChanges = false;

        // Add this field to store the original text
        private Dictionary<TextBox, string> _originalTexts = new Dictionary<TextBox, string>();

        public event PropertyChangedEventHandler PropertyChanged;

        public class GameViewModel : INotifyPropertyChanged
        {
            private bool _isSelected;

            public GameProfile Profile { get; set; }
            public string GameName { get; set; } // Keep this property name, but populate from GameNameInternal
            public bool IsSelected 
            { 
                get => _isSelected; 
                set
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public class ButtonViewModel
        {
            public JoystickButtons Button { get; set; }
            public string ButtonName { get => Button.ButtonName; }
            public string BindName 
            { 
                get => Button.BindName; 
                set => Button.BindName = value;
            }
            public string Availability { get; set; }
        }

        // Update the constructor to remove the event handlers setup
        public MultiGameButtonConfig(ContentControl contentControl, Library library)
        {
            InitializeComponent();

            _contentControl = contentControl;
            _library = library;
            
            // Ensure profiles are loaded
            if (GameProfileLoader.UserProfiles == null)
                GameProfileLoader.LoadProfiles(true); // true = only load user profiles
                
            // Only use user profiles - these are the ones the user can actually modify
            _allGameProfiles = new List<GameProfile>();
            
            // Add only user profiles
            if (GameProfileLoader.UserProfiles != null)
                _allGameProfiles.AddRange(GameProfileLoader.UserProfiles);

            // Initialize controller helpers
            _joystickControlDirectInput = new JoystickControlDirectInput();
            _joystickControlXInput = new JoystickControlXInput();
            _joystickControlRawInput = new JoystickControlRawInput();

            // Set up the UI
            InputApiSelector.SelectedIndex = 0; // DirectInput by default
            GameCategorySelector.SelectedIndex = 0; // All games by default
            
            // Load the game list
            LoadGameList();
            
            _isLoading = false;
            RefreshProfilesList();
        }

        private void LoadGameList()
        {
            _filteredGames.Clear();
            string searchText = SearchBox.Text?.ToLower() ?? "";
            string category = ((GameCategorySelector.SelectedItem as ComboBoxItem)?.Content as string) ?? TeknoParrotUi.Properties.Resources.MultiGameButtonConfigAllGamesCategory;

            foreach (var profile in _allGameProfiles)
            {
                // Apply filtering
                bool matchesSearch = string.IsNullOrEmpty(searchText) || 
                                    profile.GameNameInternal.ToLower().Contains(searchText);

                bool matchesCategory = category == TeknoParrotUi.Properties.Resources.MultiGameButtonConfigAllGamesCategory || 
                                     (category == TeknoParrotUi.Properties.Resources.MultiGameButtonConfigRacingGamesCategory && IsRacingGame(profile)) ||
                                     (category == TeknoParrotUi.Properties.Resources.MultiGameButtonConfigShootingGamesCategory && IsShootingGame(profile)) ||
                                     (category == TeknoParrotUi.Properties.Resources.MultiGameButtonConfigArcadeGamesCategory && IsArcadeGame(profile));

                if (matchesSearch && matchesCategory)
                {
                    _filteredGames.Add(new GameViewModel
                    {
                        Profile = profile,
                        GameName = profile.GameNameInternal, // Use GameNameInternal here
                        IsSelected = false
                    });
                }
            }

            GameListView.ItemsSource = _filteredGames;
            UpdateButtonConfiguration();
        }

        private bool IsRacingGame(GameProfile profile)
        {
            // Determine if the game is a racing game based on profile characteristics
            return profile.JoystickButtons.Any(b => 
                b.InputMapping == InputMapping.Analog0 || // Gas
                b.InputMapping == InputMapping.Analog2);
        }

        private bool IsShootingGame(GameProfile profile)
        {
            // Determine if the game is a shooting game based on profile characteristics
            return profile.JoystickButtons.Any(b => 
                b.InputMapping == InputMapping.P1LightGun || 
                b.InputMapping == InputMapping.P2LightGun);
        }

        private bool IsArcadeGame(GameProfile profile)
        {
            // Default for other arcade games that aren't racing or shooting
            return !IsRacingGame(profile) && !IsShootingGame(profile);
        }

        private void UpdateButtonConfiguration()
        {
            WriteDebugLog("UpdateButtonConfiguration() 开始执行");
            
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            WriteDebugLog($"选中的游戏数量: {selectedGames.Count}");
            
            if (!selectedGames.Any())
            {
                WriteDebugLog("没有选中的游戏，清空按钮配置面板");
                ButtonConfigPanel.ItemsSource = null;
                StatusText.Text = TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoGamesSelected;
                WriteDebugLog("UpdateButtonConfiguration() 执行完成");
                return;
            }

            // Get all unique buttons across selected games with availability info
            WriteDebugLog("开始生成按钮视图模型");
            var buttonViewModels = GenerateButtonViewModels(selectedGames.Select(g => g.Profile).ToList());
            WriteDebugLog($"生成的按钮视图模型数量: {buttonViewModels.Count}");
            
            // 记录按钮视图模型的详细信息
            foreach (var viewModel in buttonViewModels)
            {
                WriteDebugLog($"按钮视图模型 - InputMapping: {viewModel.Button.InputMapping}, ButtonName: {viewModel.Button.ButtonName}");
                WriteDebugLog($"  DirectInputButton: {viewModel.Button.DirectInputButton}, BindNameDi: {viewModel.Button.BindNameDi}");
                WriteDebugLog($"  XInputButton: {viewModel.Button.XInputButton}, BindNameXi: {viewModel.Button.BindNameXi}");
                WriteDebugLog($"  RawInputButton: {viewModel.Button.RawInputButton}, BindNameRi: {viewModel.Button.BindNameRi}");
                WriteDebugLog($"  当前显示的BindName: {viewModel.Button.BindName}");
            }

            // Always show all buttons now
            WriteDebugLog("更新ButtonConfigPanel.ItemsSource");
            ButtonConfigPanel.ItemsSource = buttonViewModels;
            WriteDebugLog("ButtonConfigPanel.ItemsSource 更新完成");

            // Update status text
            WriteDebugLog($"更新状态栏文本: 选中{selectedGames.Count}个游戏，显示{buttonViewModels.Count}个控制");
            StatusText.Text = string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigGamesSelectedControlsShown, selectedGames.Count, buttonViewModels.Count);
            WriteDebugLog("UpdateButtonConfiguration() 执行完成");
        }

        private List<ButtonViewModel> GenerateButtonViewModels(List<GameProfile> selectedProfiles)
        {
            if (!selectedProfiles.Any())
                return new List<ButtonViewModel>();

            // Dictionary to store unique buttons by InputMapping
            var uniqueButtons = GetAllUniqueButtons(selectedProfiles);
            var buttonViewModels = new List<ButtonViewModel>();
            
            // For each unique button, calculate in how many games it appears
            foreach (var button in uniqueButtons)
            {
                int count = selectedProfiles.Count(p => 
                    p.JoystickButtons.Any(b => b.InputMapping == button.InputMapping));
                    
                buttonViewModels.Add(new ButtonViewModel
                {
                    Button = button,
                    Availability = string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigUsedInGames, count, selectedProfiles.Count)
                });
            }
            
            // Order by digital/analog type, then by name
            return buttonViewModels
                .OrderBy(b => IsAnalogButton(b.Button.InputMapping) ? 1 : 0) // Digital first (0), then analog (1)
                .ThenBy(b => b.ButtonName) // Then alphabetical by name
                .ToList();
        }

        // Helper function to determine if a button is analog
        private bool IsAnalogButton(InputMapping mapping)
        {
            string mappingName = mapping.ToString();
            
            // Check if it's an analog input
            if (mappingName.StartsWith("Analog") || 
                mappingName.Contains("Axis") || 
                mappingName.EndsWith("Positive") || 
                mappingName.EndsWith("Negative") ||
                mappingName.Contains("Throttle") ||
                mappingName.Contains("Brake"))
            {
                return true;
            }
            
            return false;
        }

        private List<JoystickButtons> FindCommonButtons(List<GameProfile> selectedProfiles)
        {
            if (!selectedProfiles.Any())
                return new List<JoystickButtons>();

            // Get the first profile's buttons
            var commonButtons = new List<JoystickButtons>(selectedProfiles.First().JoystickButtons);
            
            // For all other profiles, keep only the buttons that match by InputMapping
            foreach (var profile in selectedProfiles.Skip(1))
            {
                commonButtons = commonButtons
                    .Where(button => profile.JoystickButtons.Any(b => b.InputMapping == button.InputMapping))
                    .ToList();
            }

            return commonButtons;
        }

        private List<JoystickButtons> GetAllUniqueButtons(List<GameProfile> selectedProfiles)
        {
            if (!selectedProfiles.Any())
                return new List<JoystickButtons>();

            // Dictionary to store unique buttons by InputMapping
            Dictionary<InputMapping, JoystickButtons> uniqueButtons = new Dictionary<InputMapping, JoystickButtons>();
            
            // Gather all buttons from all profiles, keeping only one instance of each mapping
            foreach (var profile in selectedProfiles)
            {
                foreach (var button in profile.JoystickButtons)
                {
                    // If we haven't seen this InputMapping yet, add it to our dictionary
                    if (!uniqueButtons.ContainsKey(button.InputMapping))
                    {
                        // Create a clone of the button so we don't modify the original
                        var buttonClone = new JoystickButtons
                        {
                            ButtonName = button.ButtonName,
                            BindName = button.BindName,
                            BindNameDi = button.BindNameDi,
                            BindNameXi = button.BindNameXi,
                            BindNameRi = button.BindNameRi,
                            DirectInputButton = button.DirectInputButton,
                            XInputButton = button.XInputButton,
                            RawInputButton = button.RawInputButton,
                            InputMapping = button.InputMapping
                        };
                        uniqueButtons[button.InputMapping] = buttonClone;
                    }
                }
            }
            
            // Group buttons by logical categories for better organization
            var groupedButtons = uniqueButtons.Values.ToList();
            
            // Sort buttons by InputMapping to ensure consistent ordering
            return groupedButtons.OrderBy(b => b.InputMapping.ToString()).ToList();
        }

        // Replace the StartListening and related methods with this implementation:

        private void StartListening()
        {
            StopListening();
            _isListening = true;

            switch (_currentInputApi)
            {
                case InputApi.DirectInput:
                    _joystickControlDirectInput = new JoystickControlDirectInput();
                    _joystickControlDirectInput.Listen();
                    break;
                case InputApi.XInput:
                    _joystickControlXInput = new JoystickControlXInput();
                    _joystickControlXInput.Listen();
                    break;
                case InputApi.RawInput:
                case InputApi.RawInputTrackball:
                    _joystickControlRawInput = new JoystickControlRawInput();
                    _joystickControlRawInput.Listen();
                    break;
            }
        }

        private void StopListening()
        {
            _isListening = false;
            
            _joystickControlDirectInput?.StopListening();
            _joystickControlXInput?.StopListening();
            _joystickControlRawInput?.StopListening();
            
            if (_inputListener != null && _inputListener.IsAlive)
            {
                _inputListener.Join(100);
            }
            
            _inputListener = null;
        }

        // Remove these methods completely since they're not needed:
        // - CheckDirectInputState
        // - CheckXInputState
        // - CheckRawInputState

        // Replace CleanUp method to remove the event handler cleanup
        private void CleanUp()
        {
            StopListening();
        }

        private const string PROFILES_DIRECTORY = "UserProfiles\\Profiles";        
        private const string DEBUG_LOG_FILE = "UserProfiles\\0LoadProfileDebug.log";        
        
        private void WriteDebugLog(string message)
        {
            try
            {
                // 确保日志目录存在
                string logDir = Path.GetDirectoryName(DEBUG_LOG_FILE);
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                
                // 写入日志，包含时间戳
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(DEBUG_LOG_FILE, $"[{timestamp}] {message}\r\n");
            }
            catch (Exception)
            {
                // 忽略日志写入错误，不影响主程序功能
            }
        }

        private void RefreshProfilesList()
        {
            // Create the profiles directory if it doesn't exist
            Directory.CreateDirectory(PROFILES_DIRECTORY);
            
            // Get all profile directories
            var profiles = Directory.GetDirectories(PROFILES_DIRECTORY)
                                   .Select(Path.GetFileName)
                                   .ToList();
            
            ProfilesComboBox.ItemsSource = profiles;
            
            // Clear XML files combo box
            XmlFilesComboBox.ItemsSource = null;
            XmlFilesComboBox.IsEnabled = false;
            
            if (profiles.Count > 0)
            {
                ProfilesComboBox.SelectedIndex = 0;
            }
        }
        
        private void ProfilesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string profileName = ProfilesComboBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(profileName))
            {
                XmlFilesComboBox.ItemsSource = null;
                XmlFilesComboBox.IsEnabled = false;
                return;
            }
            
            string profileDir = Path.Combine(PROFILES_DIRECTORY, profileName);
            if (!Directory.Exists(profileDir))
            {
                XmlFilesComboBox.ItemsSource = null;
                XmlFilesComboBox.IsEnabled = false;
                return;
            }
            
            // Get all XML files in the profile directory
            var xmlFiles = Directory.GetFiles(profileDir, "*.xml")
                                   .Select(Path.GetFileName)
                                   .ToList();
            
            if (xmlFiles.Count > 0)
            {
                XmlFilesComboBox.ItemsSource = xmlFiles;
                XmlFilesComboBox.SelectedIndex = 0; // 默认选中第一个XML文件
                XmlFilesComboBox.IsEnabled = true;
            }
            else
            {
                XmlFilesComboBox.ItemsSource = null;
                XmlFilesComboBox.IsEnabled = false;
            }
        }

        private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (!selectedGames.Any())
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPleaseSelectAtLeastOneGame, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoGamesSelectedTitle, MessageBoxButton.OK);
                return;
            }
            
            string profileName = ProfilesComboBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileNameRequired, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileNameRequiredTitle, MessageBoxButton.OK);
                return;
            }
            
            // Sanitize the profile name for file system
            profileName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
            
            string profileDir = Path.Combine(PROFILES_DIRECTORY, profileName);
            Directory.CreateDirectory(profileDir);
            
            int savedCount = 0;
            
            try
            {
                foreach (var game in selectedGames)
                {
                    // Create a copy of the game profile for saving
                    var gameProfileCopy = new GameProfile
                    {
                        ProfileName = game.Profile.ProfileName,
                        GameNameInternal = game.Profile.GameNameInternal,
                        JoystickButtons = new List<JoystickButtons>()
                    };
                    
                    // Copy all joystick buttons to ensure we save all input types
                    foreach (var button in game.Profile.JoystickButtons)
                    {
                        var buttonCopy = new JoystickButtons
                        {
                            ButtonName = button.ButtonName,
                            InputMapping = button.InputMapping,
                            BindName = button.BindName,
                            BindNameDi = button.BindNameDi,
                            BindNameXi = button.BindNameXi,
                            BindNameRi = button.BindNameRi,
                            DirectInputButton = button.DirectInputButton,
                            XInputButton = button.XInputButton,
                            RawInputButton = button.RawInputButton
                        };
                        
                        gameProfileCopy.JoystickButtons.Add(buttonCopy);
                    }
                    
                    string fileName = Path.Combine(profileDir, game.Profile.ProfileName + ".xml");
                    
                    // Serialize the profile
                    using (var writer = XmlWriter.Create(fileName, new XmlWriterSettings { Indent = true }))
                    {
                        var serializer = new XmlSerializer(typeof(GameProfile));
                        serializer.Serialize(writer, gameProfileCopy);
                    }
                    
                    savedCount++;
                }
                
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSuccessfullySavedProfile, savedCount, profileName),
                               TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileSaved, MessageBoxButton.OK);
                
                // Refresh profiles list
                RefreshProfilesList();
                ProfilesComboBox.Text = profileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigErrorSavingProfile, ex.Message), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSaveError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (!selectedGames.Any())
            {
                WriteDebugLog("未选择任何游戏");
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSelectAtLeastOneGameToLoad, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoGamesSelectedTitle, MessageBoxButton.OK);
                return;
            }
            
            string profileName = ProfilesComboBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(profileName) || !Directory.Exists(Path.Combine(PROFILES_DIRECTORY, profileName)))
            {
                WriteDebugLog("选择的配置文件无效或不存在: " + profileName);
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSelectValidProfile, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileNotFound, MessageBoxButton.OK);
                return;
            }
            
            string profileDir = Path.Combine(PROFILES_DIRECTORY, profileName);
            int loadedCount = 0;
            WriteDebugLog("开始加载配置文件: " + profileDir);
            WriteDebugLog("选中的游戏数量: " + selectedGames.Count);
            
            // 获取选择的XML文件名
            string selectedXmlFileName = XmlFilesComboBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(selectedXmlFileName))
            {
                WriteDebugLog("未选择XML配置文件");
                MessageBox.Show("请选择一个XML配置文件", "配置文件未选择", MessageBoxButton.OK);
                return;
            }
            
            // 构建完整的配置文件路径
            string profileFilePath = Path.Combine(profileDir, selectedXmlFileName);
            
            if (!File.Exists(profileFilePath))
            {
                WriteDebugLog("配置文件不存在: " + profileFilePath);
                MessageBox.Show("选择的配置文件不存在", "配置文件未找到", MessageBoxButton.OK);
                return;
            }
            
            WriteDebugLog("找到配置文件: " + profileFilePath);
            
            try
            {
                foreach (var game in selectedGames)
                {
                    WriteDebugLog("处理游戏: " + game.GameName + " (ProfileName: " + game.Profile.ProfileName + ")");
                    
                    GameProfile savedProfile;
                    using (var reader = XmlReader.Create(profileFilePath))
                    {
                        var serializer = new XmlSerializer(typeof(GameProfile));
                        savedProfile = (GameProfile)serializer.Deserialize(reader);
                    }
                    
                    // 只有当至少有一个按钮成功匹配时才增加loadedCount
                    bool gameHasMatches = false;
                    
                    WriteDebugLog("配置文件中的按钮数量: " + savedProfile.JoystickButtons.Count);
                    WriteDebugLog("游戏中的按钮数量: " + game.Profile.JoystickButtons.Count);
                    
                    // Apply the loaded configuration to the game
                        foreach (var savedButton in savedProfile.JoystickButtons)
                        {
                            WriteDebugLog("尝试匹配按钮 - InputMapping: " + savedButton.InputMapping + ", ButtonName: " + savedButton.ButtonName);
                            
                            // 首先尝试通过InputMapping匹配（原始逻辑）
                            var gameButton = game.Profile.JoystickButtons.FirstOrDefault(b => b.InputMapping == savedButton.InputMapping);
                            
                            if (gameButton != null)
                            {
                                WriteDebugLog("通过InputMapping匹配成功");
                            }
                            
                            // 如果InputMapping匹配失败，尝试通过ButtonName匹配作为备选方案
                            if (gameButton == null)
                            {
                                WriteDebugLog("InputMapping匹配失败，尝试ButtonName匹配");
                                gameButton = game.Profile.JoystickButtons.FirstOrDefault(b => 
                                    !string.IsNullOrEmpty(b.ButtonName) && 
                                    !string.IsNullOrEmpty(savedButton.ButtonName) &&
                                    b.ButtonName.Equals(savedButton.ButtonName, StringComparison.InvariantCultureIgnoreCase)
                                );
                                
                                if (gameButton != null)
                                {
                                    WriteDebugLog("通过ButtonName匹配成功");
                                }
                            }
                            
                            if (gameButton != null)
                            {
                                // 记录替换前的按钮值
                                string beforeValues = $"DirectInputButton: {gameButton.DirectInputButton}, XInputButton: {gameButton.XInputButton}, " +
                                                    $"RawInputButton: {gameButton.RawInputButton}, BindNameDi: {gameButton.BindNameDi}, " +
                                                    $"BindNameXi: {gameButton.BindNameXi}, BindNameRi: {gameButton.BindNameRi}";
                                
                                // Copy all input types regardless of current input API
                                gameButton.DirectInputButton = savedButton.DirectInputButton;
                                gameButton.XInputButton = savedButton.XInputButton;
                                gameButton.RawInputButton = savedButton.RawInputButton;
                                gameButton.BindNameDi = savedButton.BindNameDi;
                                gameButton.BindNameXi = savedButton.BindNameXi;
                                gameButton.BindNameRi = savedButton.BindNameRi;
                                
                                // Update the current display binding based on current input API
                                string bindNameBefore = gameButton.BindName;
                                switch (_currentInputApi)
                                {
                                    case InputApi.DirectInput:
                                        gameButton.BindName = savedButton.BindNameDi;
                                        break;
                                    case InputApi.XInput:
                                        gameButton.BindName = savedButton.BindNameXi;
                                        break;
                                    case InputApi.RawInput:
                                    case InputApi.RawInputTrackball:
                                        gameButton.BindName = savedButton.BindNameRi;
                                        break;
                                }
                                
                                // 记录替换后的按钮值
                                string afterValues = $"DirectInputButton: {gameButton.DirectInputButton}, XInputButton: {gameButton.XInputButton}, " +
                                                    $"RawInputButton: {gameButton.RawInputButton}, BindNameDi: {gameButton.BindNameDi}, " +
                                                    $"BindNameXi: {gameButton.BindNameXi}, BindNameRi: {gameButton.BindNameRi}";
                                
                                // 添加详细日志
                                WriteDebugLog($"配置文件路径: {Path.Combine(profileDir, "profile.xml")}");
                                WriteDebugLog($"替换的游戏配置文件路径: {Path.Combine(Path.GetDirectoryName(PROFILES_DIRECTORY), game.Profile.ProfileName + ".xml")}");
                                WriteDebugLog($"替换的按钮名称: InputMapping={gameButton.InputMapping}, ButtonName={gameButton.ButtonName}");
                                WriteDebugLog($"按钮替换前: {beforeValues}, BindName: {bindNameBefore}");
                                WriteDebugLog($"按钮替换后: {afterValues}, BindName: {gameButton.BindName}");
                                
                                gameHasMatches = true; // 标记至少有一个按钮匹配成功
                                WriteDebugLog("成功应用按钮配置");
                            }
                            else
                            {
                                WriteDebugLog($"未找到匹配的按钮: InputMapping={savedButton.InputMapping}, ButtonName={savedButton.ButtonName}");
                            }
                        }
                    
                    if (gameHasMatches)
                    {
                        loadedCount++; // 只有当至少有一个按钮匹配成功时才增加计数
                        WriteDebugLog("游戏配置加载成功，loadedCount = " + loadedCount);
                    }
                    else
                    {
                        WriteDebugLog("游戏无匹配的按钮配置");
                    }
                }
                
                WriteDebugLog("加载完成，总成功加载的游戏数量: " + loadedCount);
                
                if (loadedCount > 0)
                {
                    WriteDebugLog("显示加载成功消息");
                    MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSuccessfullyLoadedProfile, loadedCount, profileName),
                                  TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileLoaded, MessageBoxButton.OK);
                    
                    _hasUnsavedChanges = true; // Set flag after loading a profile
                    // Update the UI to show the loaded configuration
                    WriteDebugLog("调用UpdateButtonConfiguration()更新右侧窗口显示");
                    UpdateButtonConfiguration();
                    WriteDebugLog("UpdateButtonConfiguration()调用完成");
                }
                else
                {
                    WriteDebugLog("显示无匹配配置消息");
                    MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoMatchingConfigurations,
                                  TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoConfigurationsFound, MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigErrorLoadingProfile, ex.Message), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigLoadError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            string profileName = ProfilesComboBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(profileName) || !Directory.Exists(Path.Combine(PROFILES_DIRECTORY, profileName)))
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSelectValidProfileToDelete, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileNotFound, MessageBoxButton.OK);
                return;
            }
            
            if (MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigConfirmDeleteProfile, profileName),
                              TeknoParrotUi.Properties.Resources.MultiGameButtonConfigConfirmDelete, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    string profileDir = Path.Combine(PROFILES_DIRECTORY, profileName);
                    Directory.Delete(profileDir, true);
                    
                    MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileDeleted, profileName), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileDeletedTitle, MessageBoxButton.OK);
                    
                    // Refresh profiles list
                    RefreshProfilesList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigErrorDeletingProfile, ex.Message), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigDeleteError, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ProfilesComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Enter key to create a new profile
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SaveProfileButton_Click(sender, e);
            }
        }

        #region Event Handlers

        private void InputApiSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            // First, stop any active input listening
            StopListening();
            
            // Update the input API
            string apiString = ((ComboBoxItem)InputApiSelector.SelectedItem).Content.ToString();
            switch (apiString)
            {
                case "DirectInput":
                    _currentInputApi = InputApi.DirectInput;
                    break;
                case "XInput":
                    _currentInputApi = InputApi.XInput;
                    break;
                case "RawInput":
                    _currentInputApi = InputApi.RawInput;
                    break;
                case "RawInputTrackball":
                    _currentInputApi = InputApi.RawInputTrackball;
                    break;
            }

            // Clean up and recreate input control instances
            _joystickControlDirectInput?.StopListening();
            _joystickControlXInput?.StopListening();
            _joystickControlRawInput?.StopListening();
            
            _joystickControlDirectInput = new JoystickControlDirectInput();
            _joystickControlXInput = new JoystickControlXInput();
            _joystickControlRawInput = new JoystickControlRawInput();
            
            // Update button bindings for display
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            foreach (var game in selectedGames)
            {
                foreach (var button in game.Profile.JoystickButtons)
                {
                    // Update the visible binding name based on the current input API
                    switch (_currentInputApi)
                    {
                        case InputApi.DirectInput:
                            button.BindName = button.BindNameDi;
                            break;
                        case InputApi.XInput:
                            button.BindName = button.BindNameXi;
                            break;
                        case InputApi.RawInput:
                        case InputApi.RawInputTrackball:
                            button.BindName = button.BindNameRi;
                            break;
                    }
                }
            }
            
            // Rebuild the button viewmodels and update the UI
            UpdateButtonConfiguration();

            // Update status message
            StatusText.Text = string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSwitchedToMode, apiString);
        }

        private void GameCategorySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            LoadGameList();
        }

        private void CommonButtonsOnly_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            UpdateButtonConfiguration();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoading) return;
            LoadGameList();
        }

        private void GameListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            UpdateButtonConfiguration();
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var game in _filteredGames)
            {
                game.IsSelected = true;
            }
            UpdateButtonConfiguration();
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var game in _filteredGames)
            {
                game.IsSelected = false;
            }
            UpdateButtonConfiguration();
        }

        private void GameCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            UpdateButtonConfiguration();
        }

        private void ConfigTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var txtBox = sender as TextBox;
            if (txtBox == null) return;

            // Store original text in our dictionary instead of Tag
            _originalTexts[txtBox] = txtBox.Text;
            
            // Make the textbox read-only to prevent manual typing
            txtBox.IsReadOnly = true;
            txtBox.Text = TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPressAButton;
            _lastActiveTextBox = txtBox;
            
            // Start listening for input
            StartListening();
        }

        private void ConfigTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var txtBox = sender as TextBox;
            if (txtBox == null) return;

            // Stop listening first to prevent further input
            StopListening();

            // If the user didn't press anything (text still shows prompt)
            if (txtBox.Text == TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPressAButton)
            {
                // Restore original text from our dictionary
                if (_originalTexts.ContainsKey(txtBox))
                {
                    txtBox.Text = _originalTexts[txtBox];
                    _originalTexts.Remove(txtBox);
                }
                else
                {
                    // Fallback to binding name
                    var buttonViewModel = txtBox.DataContext as ButtonViewModel;
                    if (buttonViewModel != null)
                    {
                        txtBox.Text = buttonViewModel.BindName;
                    }
                }
            }
            else if (txtBox.Text != TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPressAButton)
            {
                // Text was set by the joystick control, not by manual typing
                var buttonViewModel = txtBox.DataContext as ButtonViewModel;
                if (buttonViewModel != null)
                {
                    var text = txtBox.Text;
                    
                    // Update the appropriate binding
                    switch (_currentInputApi)
                    {
                        case InputApi.DirectInput:
                            buttonViewModel.Button.BindNameDi = text;
                            buttonViewModel.Button.BindName = text;
                            break;
                        case InputApi.XInput:
                            buttonViewModel.Button.BindNameXi = text;
                            buttonViewModel.Button.BindName = text;
                            break;
                        case InputApi.RawInput:
                        case InputApi.RawInputTrackball:
                            buttonViewModel.Button.BindNameRi = text;
                            buttonViewModel.Button.BindName = text;
                            break;
                    }
                }
                
                // Remove the entry from our dictionary
                if (_originalTexts.ContainsKey(txtBox))
                {
                    _originalTexts.Remove(txtBox);
                }

                // After updating the binding
                _hasUnsavedChanges = true;
            }

            // Make the textbox editable again
            txtBox.IsReadOnly = false;
            _lastActiveTextBox = null;
        }

        private void ConfigTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var txtBox = sender as TextBox;
            if (txtBox == null) return;
            
            // If we're in binding mode (shown by read-only status)
            if (txtBox.IsReadOnly)
            {
                // Allow ESC key to cancel binding
                if (e.Key == Key.Escape)
                {
                    // Restore original text from our dictionary
                    if (_originalTexts.ContainsKey(txtBox))
                    {
                        txtBox.Text = _originalTexts[txtBox];
                        _originalTexts.Remove(txtBox);
                    }
                    
                    txtBox.IsReadOnly = false;
                    StopListening();
                    
                    // Move focus away from textbox to complete the cancellation
                    FocusManager.SetFocusedElement(FocusManager.GetFocusScope(txtBox), null);
                    Keyboard.ClearFocus();
                }
                
                // Block all other keyboard input during binding
                e.Handled = true;
            }
        }

        private void ApplyToSelectedGames_Click(object sender, RoutedEventArgs e)
        {
            ApplyChangesToSelectedGames();
            _hasUnsavedChanges = true; // Set unsaved changes flag after applying
        }

        private void CopyFromGame_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (selectedGames.Count != 1)
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPleaseSelectExactlyOneGame, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSelectionError, MessageBoxButton.OK);
                return;
            }

            // Show a dialog to select which game to copy from
            var window = new Window
            {
                Title = TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSelectGameToCopyFrom,
                Width = 400,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var panel = new DockPanel();
            var listBox = new ListBox
            {
                ItemsSource = _allGameProfiles.Select(p => p.GameNameInternal), // Use GameNameInternal here
                Margin = new Thickness(10)
            };
            
            DockPanel.SetDock(listBox, Dock.Top);
            panel.Children.Add(listBox);

            var buttonPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10) 
            };
            
            var cancelBtn = new Button { Content = TeknoParrotUi.Properties.Resources.Cancel, Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            cancelBtn.Click += (s, args) => window.DialogResult = false;
            
            var selectBtn = new Button { Content = "Select", Width = 80 };
            selectBtn.Click += (s, args) => 
            {
                if (listBox.SelectedItem != null)
                {
                    window.Tag = listBox.SelectedItem;
                    window.DialogResult = true;
                }
            };
            
            buttonPanel.Children.Add(cancelBtn);
            buttonPanel.Children.Add(selectBtn);
            
            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            panel.Children.Add(buttonPanel);
            
            window.Content = panel;
            
            if (window.ShowDialog() == true && window.Tag is string selectedGameName)
            {
                var sourceProfile = _allGameProfiles.FirstOrDefault(p => p.GameNameInternal == selectedGameName);
                var targetProfile = selectedGames[0].Profile;

                if (sourceProfile != null)
                {
                    // Copy button configurations that match by InputMapping
                    foreach (var sourceButton in sourceProfile.JoystickButtons)
                    {
                        var targetButton = targetProfile.JoystickButtons.FirstOrDefault(b => b.InputMapping == sourceButton.InputMapping);
                        if (targetButton != null)
                        {
                            // Copy the binding based on the current input API
                            switch (_currentInputApi)
                            {
                                case InputApi.DirectInput:
                                    targetButton.DirectInputButton = sourceButton.DirectInputButton;
                                    targetButton.BindNameDi = sourceButton.BindNameDi;
                                    break;
                                case InputApi.XInput:
                                    targetButton.XInputButton = sourceButton.XInputButton;
                                    targetButton.BindNameXi = sourceButton.BindNameXi;
                                    break;
                                case InputApi.RawInput:
                                case InputApi.RawInputTrackball:
                                    targetButton.RawInputButton = sourceButton.RawInputButton;
                                    targetButton.BindNameRi = sourceButton.BindNameRi;
                                    break;
                            }
                            targetButton.BindName = sourceButton.BindName;
                        }
                    }

                    StatusText.Text = string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigButtonConfigurationCopied, sourceProfile.GameNameInternal); // Use GameNameInternal here
                    _hasUnsavedChanges = true;
                    UpdateButtonConfiguration();
                }
            }
        }

        private void ResetToDefault_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (!selectedGames.Any())
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPleaseSelectAtLeastOneGameToReset, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoSelection, MessageBoxButton.OK);
                return;
            }

            if (MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigConfirmResetConfiguration, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigConfirmReset, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var game in selectedGames)
                {
                    // Reset all button bindings for the current input API
                    foreach (var button in game.Profile.JoystickButtons)
                    {
                        switch (_currentInputApi)
                        {
                            case InputApi.DirectInput:
                                button.DirectInputButton = null;
                                button.BindNameDi = "";
                                break;
                            case InputApi.XInput:
                                button.XInputButton = null;
                                button.BindNameXi = "";
                                break;
                            case InputApi.RawInput:
                            case InputApi.RawInputTrackball:
                                button.RawInputButton = null;
                                button.BindNameRi = "";
                                break;
                        }
                        button.BindName = "";
                    }
                }

                StatusText.Text = string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigButtonConfigurationReset, selectedGames.Count);
                _hasUnsavedChanges = true;
                UpdateButtonConfiguration();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // First, explicitly apply changes just like the Apply button does
            ApplyChangesToSelectedGames();
            
            // Save all changed profiles
            var modifiedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            int savedCount = 0;
            
            try
            {
                foreach (var game in modifiedGames)
                {
                    // Add debug information
                    Console.WriteLine($"Saving profile for {game.GameName}");
                    Console.WriteLine($"Profile has {game.Profile.JoystickButtons.Count} buttons");
                    foreach (var button in game.Profile.JoystickButtons)
                    {
                        Console.WriteLine($"Button: {button.ButtonName}, Binding: {button.BindName}");
                    }
                    
                    JoystickHelper.SerializeGameProfile(game.Profile);
                    savedCount++;
                }

                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigChangesSaved, savedCount), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSaveSuccessful, MessageBoxButton.OK);
                _hasUnsavedChanges = false; // Clear the unsaved changes flag
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigErrorSavingProfiles, ex.Message, ex.StackTrace), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSaveError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            CleanUp();
            
            // Use the same navigation logic as BtnGoBack
            if (LaunchedFromSetupWizard && SetupWizardInstance != null)
            {
                LaunchedFromSetupWizard = false;
                SetupWizardInstance.ReturnFromButtonConfig();
            }
            else
            {
                // Original code to return to library
                _contentControl.Content = _library;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if there are unsaved changes before exiting
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    TeknoParrotUi.Properties.Resources.MultiGameButtonConfigUnsavedChanges,
                    TeknoParrotUi.Properties.Resources.MultiGameButtonConfigUnsavedChangesTitle,
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);
                    
                if (result == MessageBoxResult.Yes)
                {
                    // Save and exit
                    SaveButton_Click(sender, e);
                    return; // SaveButton_Click already handles exiting
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    // Stay on the current screen
                    return;
                }
                // If No, continue with exiting without saving
            }
            
            // Discard changes, clean up, and return to library
            CleanUp();
            _contentControl.Content = _library;
        }

        private void ApplyChangesToUserProfiles(List<GameViewModel> games)
        {
            foreach (var game in games)
            {
                try
                {
                    JoystickHelper.SerializeGameProfile(game.Profile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to save profile {game.GameName}: {ex.Message}");
                }
            }
        }

        private void ApplyToUserProfilesButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (!selectedGames.Any())
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPleaseSelectAtLeastOneGameToApply, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoGamesSelectedTitle, MessageBoxButton.OK);
                return;
            }
            
            try
            {
                ApplyChangesToUserProfiles(selectedGames);
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigChangesApplied, selectedGames.Count), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigChangesAppliedTitle, MessageBoxButton.OK);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigErrorApplyingChanges, ex.Message), TeknoParrotUi.Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Extract the apply logic into a separate method to reuse in both Apply and Save buttons
        private void ApplyChangesToSelectedGames()
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            var buttonViewModels = ButtonConfigPanel.ItemsSource as List<ButtonViewModel>;
            
            if (!selectedGames.Any() || buttonViewModels == null || !buttonViewModels.Any())
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPleaseSelectGamesAndConfigureButton, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoSelection, MessageBoxButton.OK);
                return;
            }

            // Apply the current button configuration to all selected games
            int totalChanges = 0;
            
            foreach (var game in selectedGames)
            {
                int gameChanges = 0;
                
                foreach (var buttonViewModel in buttonViewModels)
                {
                    var sourceButton = buttonViewModel.Button;
                    
                    // Find the matching button in this specific game, if any
                    var gameButton = game.Profile.JoystickButtons.FirstOrDefault(b => b.InputMapping == sourceButton.InputMapping);
                    
                    // Skip if this game doesn't have this button
                    if (gameButton == null) continue;
                    
                    // Apply the binding based on the current input API
                    switch (_currentInputApi)
                    {
                        case InputApi.DirectInput:
                            if (gameButton.DirectInputButton != sourceButton.DirectInputButton || 
                                gameButton.BindNameDi != sourceButton.BindNameDi)
                            {
                                gameButton.DirectInputButton = sourceButton.DirectInputButton;
                                gameButton.BindNameDi = sourceButton.BindNameDi;
                                gameButton.BindName = sourceButton.BindNameDi;
                                gameChanges++;
                            }
                            break;
                        case InputApi.XInput:
                            if (gameButton.XInputButton != sourceButton.XInputButton ||
                                gameButton.BindNameXi != sourceButton.BindNameXi)
                            {
                                gameButton.XInputButton = sourceButton.XInputButton;
                                gameButton.BindNameXi = sourceButton.BindNameXi;
                                gameButton.BindName = sourceButton.BindNameXi;
                                gameChanges++;
                            }
                            break;
                        case InputApi.RawInput:
                        case InputApi.RawInputTrackball:
                            if (gameButton.RawInputButton != sourceButton.RawInputButton ||
                                gameButton.BindNameRi != sourceButton.BindNameRi)
                            {
                                gameButton.RawInputButton = sourceButton.RawInputButton;
                                gameButton.BindNameRi = sourceButton.BindNameRi;
                                gameButton.BindName = sourceButton.BindNameRi;
                                gameChanges++;
                            }
                            break;
                    }
                }
                
                totalChanges += gameChanges;
            }

            StatusText.Text = string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigButtonConfigurationApplied, totalChanges, selectedGames.Count);
        }

        // Add these static properties at the class level
        public static bool LaunchedFromSetupWizard { get; set; } = false;
        public static Views.SetupWizard SetupWizardInstance { get; set; } = null;

        // Modify the Go Back button click handler to check if we should return to setup wizard
        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            // If we were launched from the setup wizard, return to it
            if (LaunchedFromSetupWizard && SetupWizardInstance != null)
            {
                LaunchedFromSetupWizard = false;
                SetupWizardInstance.ReturnFromButtonConfig();
            }
            else
            {
                // Original code to return to library
                _contentControl.Content = _library;
            }
        }

        #endregion
    }
}