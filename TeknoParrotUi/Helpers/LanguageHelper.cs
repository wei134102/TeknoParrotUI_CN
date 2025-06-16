using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    public static class LanguageHelper
    {
        public static void SetLanguage(string languageCode)
        {
            try
            {
                // 设置当前线程的文化信息
                Thread.CurrentThread.CurrentCulture = new CultureInfo(languageCode);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(languageCode);

                // 设置应用程序级别的默认文化信息
                CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(languageCode);
                CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(languageCode);

                // 保存语言设置
                JoystickHelper.DeSerialize();
                Lazydata.ParrotData.UiLanguage = languageCode;
                JoystickHelper.Serialize();

                // 强制更新所有绑定
                foreach (Window window in Application.Current.Windows)
                {
                    window.UpdateLayout();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting language: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void InitializeLanguage()
        {
            try
            {
                // 读取保存的语言设置
                JoystickHelper.DeSerialize();
                string languageCode = Lazydata.ParrotData.UiLanguage;

                if (string.IsNullOrEmpty(languageCode))
                {
                    // 默认使用英语
                    languageCode = "en-US";
                    Lazydata.ParrotData.UiLanguage = languageCode;
                    JoystickHelper.Serialize();
                }

                // 设置语言
                SetLanguage(languageCode);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing language: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 