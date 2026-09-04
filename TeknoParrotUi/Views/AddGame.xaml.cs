using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media; // needed to change text colors.
using System.IO;
using TeknoParrotUi.Common;
using System.Diagnostics;
using System.Linq;
using TeknoParrotUi.Properties;
using TeknoParrotUi.Helpers;
using MaterialDesignThemes.Wpf;
using System.Collections.Generic; // Added for List
using Microsoft.Win32; // Added for Registry

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for AddGame.xaml
    /// </summary>
    public partial class AddGame
    {
        private GameProfile _selected = new GameProfile();
        private ContentControl _contentControl;
        private Library _library;

        public AddGame(ContentControl control, Library library)
        {
            InitializeComponent();
            _contentControl = control;
            _library = library;
            InitializeGenreComboBox();
            AddGameSnackbar.MessageQueue = new SnackbarMessageQueue(TimeSpan.FromMilliseconds(2000));
        }

        private void InitializeGenreComboBox()
        {
            var genreItems = TeknoParrotUi.Helpers.GenreTranslationHelper.GetGenreItems(true);
            GenreBox.ItemsSource = genreItems;
            GenreBox.SelectedIndex = 0;
        }

        /// <summary>
        /// This is executed when the control is loaded, it grabs all the default game profiles and adds them to the list box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            //this prevents duplicates if you leave the window then come back
            stockGameList.Items.Clear();
            int fullGameCount = 0;
            foreach (var gameProfile in GameProfileLoader.GameProfiles)
            {
                // third-party emulators
                var thirdparty = gameProfile.EmulatorType == EmulatorType.SegaTools;

                // check the existing user profiles
                var existing = GameProfileLoader.UserProfiles.FirstOrDefault((profile) => profile.ProfileName == gameProfile.ProfileName) != null;

                if (gameProfile.IsLegacy && !existing)
                {
                    continue; // skip this profile
                }

                fullGameCount += 1;
                var item = new ListBoxItem
                {
                    Content = gameProfile.GameNameInternal +
                                (gameProfile.Patreon ? TeknoParrotUi.Properties.Resources.AddGameSubscriptionSuffix : "") +
                                (thirdparty ? string.Format(TeknoParrotUi.Properties.Resources.AddGameThirdPartySuffix, gameProfile.EmulatorType) : "") +
                                (existing ? TeknoParrotUi.Properties.Resources.AddGameAddedSuffix : ""),
                    Tag = gameProfile
                };


                if (existing)
                {
                    item.SetResourceReference(ForegroundProperty, "MaterialDesign.Brush.Primary.Dark");
                }

                string selectedInternalGenre = "All";
                if (GenreBox != null && GenreBox.SelectedItem != null)
                {
                    var genreItem = GenreBox.SelectedItem as TeknoParrotUi.Helpers.GenreItem;
                    selectedInternalGenre = genreItem?.InternalName ?? "All";
                }

                string searchName = "";
                if (GameSearchBox != null)
                {
                    searchName = GameSearchBox.Text;
                }

                if (gameProfile.GameNameInternal.IndexOf(searchName, 0, StringComparison.OrdinalIgnoreCase) != -1 || string.IsNullOrWhiteSpace(searchName))
                {
                    bool matchesGenre = TeknoParrotUi.Helpers.GenreTranslationHelper.DoesGameMatchGenre(selectedInternalGenre, gameProfile);

                    if (matchesGenre)
                    {
                        stockGameList.Items.Add(item);
                    }
                }

            }

            if (GameProfileLoader.GameProfiles != null && stockGameList.Items != null && GameCountLabel != null)
            {
                GameCountLabel.Content = string.Format(TeknoParrotUi.Properties.Resources.AddGameGamesShownCount, stockGameList.Items.Count, fullGameCount);
            }

            if (stockGameList.SelectedIndex < 0)
            {
                if (gameIcon != null)
                {
                    gameIcon.Source = Library.defaultIcon;
                    _selected = new GameProfile();
                    AddButton.IsEnabled = false;
                    DeleteButton.IsEnabled = false;
                }

            }
        }

        /// <summary>
        /// When the selection in the listbox is changed, it loads the appropriate game profile as the selected one.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StockGameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (stockGameList.SelectedIndex < 0) return;

            e.Handled = true;

            // 检查是否是多选模式
            if (stockGameList.SelectedItems.Count > 1)
            {
                // 多选模式
                var selectedCount = stockGameList.SelectedItems.Count;
                var addedCount = 0;
                var notAddedCount = 0;

                foreach (ListBoxItem item in stockGameList.SelectedItems)
                {
                    var gameProfile = (GameProfile)item.Tag;
                    var isAdded = item.Content.ToString().Contains(TeknoParrotUi.Properties.Resources.AddGameAddedSuffix);
                    if (isAdded)
                        addedCount++;
                    else
                        notAddedCount++;
                }

                // 更新按钮状态
                AddButton.IsEnabled = false;
                DeleteButton.IsEnabled = false;
                BatchAddButton.IsEnabled = notAddedCount > 0;
                BatchDeleteButton.IsEnabled = addedCount > 0;

                // 更新按钮文本
                if (notAddedCount > 0)
                    BatchAddButton.Content = $"批量添加 ({notAddedCount} 个游戏)";
                if (addedCount > 0)
                    BatchDeleteButton.Content = $"批量删除 ({addedCount} 个游戏)";

                // 显示选中游戏信息
                if (stockGameList.SelectedItems.Count > 0)
                {
                    var firstItem = (ListBoxItem)stockGameList.SelectedItems[0];
                    _selected = (GameProfile)firstItem.Tag;
                    Library.UpdateIcon(_selected.IconName.Split('/')[1], _selected.EmulatorType, ref gameIcon);
                }

                return;
            }

            // 单选模式 - 原有逻辑
            var gameItem = (ListBoxItem)stockGameList.SelectedValue;
            _selected = (GameProfile)gameItem.Tag;
            //_selected = GameProfileLoader.GameProfiles[stockGameList.SelectedIndex];
            Library.UpdateIcon(_selected.IconName.Split('/')[1], _selected.EmulatorType, ref gameIcon);

            var added = ((ListBoxItem)stockGameList.SelectedItem).Content.ToString().Contains(TeknoParrotUi.Properties.Resources.AddGameAddedSuffix);
            AddButton.IsEnabled = !added;
            AddContinueButton.IsEnabled = !added;
            DeleteButton.IsEnabled = added;
            BatchAddButton.IsEnabled = false;
            BatchDeleteButton.IsEnabled = false;
        }

        /// <summary>
        /// This is the code for the Add Game button, that copies the default game profile over to the UserProfiles folder so it shows up in the menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddGameButton(object sender, RoutedEventArgs e)
        {
            if (_selected == null || _selected.FileName == null) return;
            //Trace.WriteLine($@"Adding {_selected.GameNameInternal} to TP (Path: {_selected.FileName}...");
            var splitString = _selected.FileName.Split('\\');
            if (splitString.Length < 1) return;
            try
            {
                _selected.FileName = _selected.FileName.Replace("UserProfiles", "GameProfiles"); // make sure we are copying from GameProfiles
                File.Copy(_selected.FileName, Path.Combine("UserProfiles", splitString[1]));

                var addedProfile = JoystickHelper.DeSerializeGameProfile(Path.Combine("UserProfiles", splitString[1]), true);
                if (addedProfile != null && !string.IsNullOrEmpty(addedProfile.OnlineIdFieldName) && addedProfile.OnlineIdType != OnlineIdType.None)
                {
                    AutoFillOnlineId(addedProfile);
                    JoystickHelper.SerializeGameProfile(addedProfile);
                }
            }
            catch
            {

            }

            _library.ListUpdate(_selected.GameNameInternal);

            _contentControl.Content = _library;
        }

        /// <summary>
        /// This is the code for the Add Game and Continue button, that adds the game and stays on the Add Game screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddGameAndContinueButton(object sender, RoutedEventArgs e)
        {
            if (_selected == null || _selected.FileName == null) return;
            var splitString = _selected.FileName.Split('\\');
            if (splitString.Length < 1) return;
            try
            {
                _selected.FileName = _selected.FileName.Replace("UserProfiles", "GameProfiles");
                File.Copy(_selected.FileName, Path.Combine("UserProfiles", splitString[1]));

                var addedProfile = JoystickHelper.DeSerializeGameProfile(Path.Combine("UserProfiles", splitString[1]), true);
                if (addedProfile != null && !string.IsNullOrEmpty(addedProfile.OnlineIdFieldName) && addedProfile.OnlineIdType != OnlineIdType.None)
                {
                    AutoFillOnlineId(addedProfile);
                    JoystickHelper.SerializeGameProfile(addedProfile);
                }
            }
            catch
            {

            }

            _library.ListUpdate(_selected.GameNameInternal);

            var message = string.Format(TeknoParrotUi.Properties.Resources.AddGameAdded, _selected.GameNameInternal);
            AddGameSnackbar.MessageQueue.Enqueue(message);

            UserControl_Loaded(null, null);
        }

        /// <summary>
        /// This is the code for the Remove Game button, that deletes the game profile in the UserProfiles folder so it doesn't show up in the menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteGameButton(object sender, RoutedEventArgs e)
        {
            if (_selected == null || _selected.FileName == null) return;

            if (Lazydata.ParrotData.ConfirmGameDeletion)
            {
                var confirmMessage = string.Format(TeknoParrotUi.Properties.Resources.AddGameConfirmDelete, _selected.GameNameInternal);
                if (!MessageBoxHelper.WarningYesNo(confirmMessage))
                {
                    return;
                }
            }

            var splitString = _selected.FileName.Split('\\');
            try
            {
                Debug.WriteLine($@"Removing {_selected.GameNameInternal} from TP...");
                File.Delete(Path.Combine("UserProfiles", splitString[1]));
            }
            catch
            {
                // ignored
            }

            //_library.ListUpdate();
            _library.listRefreshNeeded = true;
            _contentControl.Content = _library;
        }

        private void AutoFillOnlineId(GameProfile profile)
        {
            if (string.IsNullOrEmpty(profile.OnlineIdFieldName))
                return;

            var configField = profile.ConfigValues.FirstOrDefault(x => x.FieldName == profile.OnlineIdFieldName);
            if (configField == null || !string.IsNullOrEmpty(configField.FieldValue))
                return;

            switch (profile.OnlineIdType)
            {
                case OnlineIdType.SegaId:
                    if (!string.IsNullOrEmpty(Lazydata.ParrotData.SegaId))
                        configField.FieldValue = Lazydata.ParrotData.SegaId;
                    break;
                case OnlineIdType.NamcoId:
                    if (!string.IsNullOrEmpty(Lazydata.ParrotData.NamcoId))
                        configField.FieldValue = Lazydata.ParrotData.NamcoId;
                    break;
                case OnlineIdType.HighscoreSerial:
                    if (!string.IsNullOrEmpty(Lazydata.ParrotData.ScoreSubmissionID))
                        configField.FieldValue = Lazydata.ParrotData.ScoreSubmissionID;
                    break;
                case OnlineIdType.MarioKartId:
                    if (!string.IsNullOrEmpty(Lazydata.ParrotData.MarioKartId))
                        configField.FieldValue = Lazydata.ParrotData.MarioKartId;
                    break;
            }
        }

        /// <summary>
        /// 批量添加游戏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BatchAddGameButton(object sender, RoutedEventArgs e)
        {
            if (stockGameList.SelectedItems.Count <= 1) return;

            var gamesToAdd = new List<GameProfile>();
            foreach (ListBoxItem item in stockGameList.SelectedItems)
            {
                var isAdded = item.Content.ToString().Contains(TeknoParrotUi.Properties.Resources.AddGameAddedSuffix);
                if (!isAdded)
                {
                    gamesToAdd.Add((GameProfile)item.Tag);
                }
            }

            if (gamesToAdd.Count == 0) return;

            var result = MessageBox.Show($"确定要批量添加 {gamesToAdd.Count} 个游戏吗？", "确认批量添加", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            var successCount = 0;
            var failedGames = new List<string>();

            foreach (var gameProfile in gamesToAdd)
            {
                try
                {
                    var splitString = gameProfile.FileName.Split('\\');
                    if (splitString.Length < 1) continue;

                    gameProfile.FileName = gameProfile.FileName.Replace("UserProfiles", "GameProfiles");
                    File.Copy(gameProfile.FileName, Path.Combine("UserProfiles", splitString[1]));

                    var addedProfile = JoystickHelper.DeSerializeGameProfile(Path.Combine("UserProfiles", splitString[1]), true);
                    if (addedProfile != null && !string.IsNullOrEmpty(addedProfile.OnlineIdFieldName) && addedProfile.OnlineIdType != OnlineIdType.None)
                    {
                        AutoFillOnlineId(addedProfile);
                        JoystickHelper.SerializeGameProfile(addedProfile);
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    failedGames.Add(gameProfile.GameNameInternal);
                    Debug.WriteLine($"Failed to add {gameProfile.GameNameInternal}: {ex.Message}");
                }
            }

            // 显示添加结果
            if (failedGames.Count > 0)
            {
                MessageBox.Show($"成功添加 {successCount} 个游戏，添加失败 {failedGames.Count} 个游戏：\n{string.Join("\n", failedGames)}", "批量添加结果", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"成功添加 {successCount} 个游戏！", "批量添加完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _library.ListUpdate();
            
            // 重置批量按钮文本为默认状态
            BatchAddButton.Content = "批量添加游戏";
            BatchDeleteButton.Content = "批量删除游戏";
            
            _contentControl.Content = _library;
        }

        /// <summary>
        /// 批量删除游戏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BatchDeleteGameButton(object sender, RoutedEventArgs e)
        {
            if (stockGameList.SelectedItems.Count <= 1) return;

            var gamesToDelete = new List<GameProfile>();
            foreach (ListBoxItem item in stockGameList.SelectedItems)
            {
                var isAdded = item.Content.ToString().Contains(TeknoParrotUi.Properties.Resources.AddGameAddedSuffix);
                if (isAdded)
                {
                    gamesToDelete.Add((GameProfile)item.Tag);
                }
            }

            if (gamesToDelete.Count == 0) return;

            var result = MessageBox.Show($"确定要批量删除 {gamesToDelete.Count} 个游戏吗？", "确认批量删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            var successCount = 0;
            var failedGames = new List<string>();

            foreach (var gameProfile in gamesToDelete)
            {
                try
                {
                    var splitString = gameProfile.FileName.Split('\\');
                    if (splitString.Length < 1) continue;

                    Debug.WriteLine($@"Removing {gameProfile.GameNameInternal} from TP...");
                    File.Delete(Path.Combine("UserProfiles", splitString[1]));
                    successCount++;
                }
                catch (Exception ex)
                {
                    failedGames.Add(gameProfile.GameNameInternal);
                    Debug.WriteLine($"Failed to delete {gameProfile.GameNameInternal}: {ex.Message}");
                }
            }

            // 显示删除结果
            if (failedGames.Count > 0)
            {
                MessageBox.Show($"成功删除 {successCount} 个游戏，删除失败 {failedGames.Count} 个游戏：\n{string.Join("\n", failedGames)}", "批量删除结果", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"成功删除 {successCount} 个游戏！", "批量删除完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _library.listRefreshNeeded = true;
            
            // 重置批量按钮文本为默认状态
            BatchAddButton.Content = "批量添加游戏";
            BatchDeleteButton.Content = "批量删除游戏";
            
            _contentControl.Content = _library;
        }
    }
}
