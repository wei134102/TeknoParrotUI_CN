using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    public static class FontHelper
    {
        /// <summary>
        /// Applies the saved font settings to the entire application
        /// </summary>
        public static void ApplyFontSettings()
        {
            try
            {
                // Get font settings from ParrotData
                string simplifiedFont = Lazydata.ParrotData.SimplifiedChineseFont ?? "Microsoft YaHei UI";
                string traditionalFont = Lazydata.ParrotData.TraditionalChineseFont ?? "Microsoft JhengHei UI";
                double fontSize = Lazydata.ParrotData.FontSize > 0 ? Lazydata.ParrotData.FontSize : 14.0;

                // Create the composite font family
                string fontFamilyString = $"Inter, {simplifiedFont}, {traditionalFont}, sans-serif";
                
                // Apply to the main application
                ApplyFontToApplication(fontFamilyString, fontSize);
            }
            catch (Exception ex)
            {
                // If there's an error, fall back to default fonts
                ApplyFontToApplication("Inter, Microsoft YaHei UI, Microsoft JhengHei UI, sans-serif", 14.0);
                Console.WriteLine($"Error applying font settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies font settings to the entire application
        /// </summary>
        /// <param name="fontFamilyString">The font family string to apply</param>
        /// <param name="fontSize">The font size to apply</param>
        private static void ApplyFontToApplication(string fontFamilyString, double fontSize)
        {
            try
            {
                // Create the font family
                FontFamily fontFamily = new FontFamily(fontFamilyString);
                
                // Apply to the main application
                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Apply to the main window
                        if (Application.Current.MainWindow != null)
                        {
                            ApplyFontToElement(Application.Current.MainWindow, fontFamily, fontSize);
                        }
                        
                        // Also apply to application-wide resources
                        ApplyFontToResources(Application.Current.Resources, fontFamily, fontSize);
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying font to application: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively applies font settings to a UI element and all its children
        /// </summary>
        /// <param name="element">The UI element to apply fonts to</param>
        /// <param name="fontFamily">The font family to apply</param>
        /// <param name="fontSize">The font size to apply</param>
        private static void ApplyFontToElement(DependencyObject element, FontFamily fontFamily, double fontSize)
        {
            if (element == null) return;

            try
            {
                // Apply font family and size
                if (element is Control control)
                {
                    control.FontFamily = fontFamily;
                    control.FontSize = fontSize;
                }
                else if (element is TextBlock textBlock)
                {
                    textBlock.FontFamily = fontFamily;
                    textBlock.FontSize = fontSize;
                }

                // Recursively apply to children
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(element, i);
                    ApplyFontToElement(child, fontFamily, fontSize);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying font to element: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Applies font settings to application resources
        /// </summary>
        /// <param name="resources">The resource dictionary to apply fonts to</param>
        /// <param name="fontFamily">The font family to apply</param>
        /// <param name="fontSize">The font size to apply</param>
        private static void ApplyFontToResources(ResourceDictionary resources, FontFamily fontFamily, double fontSize)
        {
            if (resources == null) return;
            
            try
            {
                // Apply to application-level text element properties
                if (resources.Contains(SystemFonts.MessageFontFamilyKey))
                {
                    resources[SystemFonts.MessageFontFamilyKey] = fontFamily;
                }
                
                if (resources.Contains(SystemFonts.MessageFontSizeKey))
                {
                    resources[SystemFonts.MessageFontSizeKey] = fontSize;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying font to resources: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets a list of font families that support Chinese characters
        /// </summary>
        /// <returns>List of Chinese-supporting font families</returns>
        public static List<string> GetChineseSupportingFonts()
        {
            var chineseFonts = new List<string>();
            
            try
            {
                // Known Chinese fonts that we want to include
                var knownChineseFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Microsoft YaHei",
                    "Microsoft YaHei UI",
                    "Microsoft JhengHei",
                    "Microsoft JhengHei UI",
                    "SimSun",
                    "NSimSun",
                    "FangSong",
                    "KaiTi",
                    "MingLiU",
                    "PMingLiU",
                    "DFKai-SB",
                    "LiSu",
                    "YouYuan",
                    "STXihei",
                    "STKaiti",
                    "STSong",
                    "STZhongsong"
                };
                
                // Add known fonts first
                chineseFonts.AddRange(knownChineseFonts);
                
                // Add system fonts that support Chinese
                foreach (FontFamily fontFamily in Fonts.SystemFontFamilies)
                {
                    try
                    {
                        string familyName = fontFamily.Source;
                        if (!string.IsNullOrEmpty(familyName) && 
                            !chineseFonts.Contains(familyName, StringComparer.OrdinalIgnoreCase))
                        {
                            // Check if font supports Chinese characters
                            if (DoesFontSupportChinese(fontFamily))
                            {
                                chineseFonts.Add(familyName);
                            }
                        }
                    }
                    catch
                    {
                        // Skip fonts that cause errors
                    }
                }
                
                // Remove duplicates and sort
                return chineseFonts.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting Chinese supporting fonts: {ex.Message}");
                // Return default fonts if there's an error
                return new List<string>
                {
                    "Microsoft YaHei UI",
                    "Microsoft JhengHei UI",
                    "Microsoft YaHei",
                    "Microsoft JhengHei",
                    "SimSun",
                    "NSimSun"
                };
            }
        }
        
        /// <summary>
        /// Checks if a font family supports Chinese characters
        /// </summary>
        /// <param name="fontFamily">The font family to check</param>
        /// <returns>True if the font supports Chinese characters</returns>
        private static bool DoesFontSupportChinese(FontFamily fontFamily)
        {
            try
            {
                // Test with common Chinese characters
                string testChars = "中文繁體简体";
                
                // Get the typefaces for this font family
                foreach ( Typeface typeface in fontFamily.GetTypefaces() )
                {
                    try
                    {
                        // Test if the font can display Chinese characters
                        foreach (char c in testChars)
                        {
                            if (!typeface.TryGetGlyphTypeface(out GlyphTypeface glyphTypeface))
                            {
                                continue;
                            }
                            
                            // If we can get glyph metrics, the font likely supports the character
                            if (glyphTypeface.CharacterToGlyphMap.ContainsKey(c))
                            {
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Continue checking other typefaces
                    }
                }
                
                // If we couldn't confirm support, check the font name
                string familyName = fontFamily.Source.ToLowerInvariant();
                return familyName.Contains("yahei") || 
                       familyName.Contains("jheng") || 
                       familyName.Contains("simsun") || 
                       familyName.Contains("mingliu") ||
                       familyName.Contains("kai") ||
                       familyName.Contains("fangsong");
            }
            catch
            {
                // If we can't determine, assume it might support Chinese
                return true;
            }
        }
    }
}