using System.Globalization;

namespace MacDock;

/// <summary>Bản địa hoá: chuỗi gốc tiếng Việt, tra từ điển khi chọn English.</summary>
public static partial class L
{
    public static bool En => Config.Current.General.Language == "en";
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(En ? "en-US" : "vi-VN");

    public static string T(string vi) => En && Dict.TryGetValue(vi, out var en) ? en : vi;
    public static string F(string viFormat, params object?[] args) => string.Format(Culture, T(viFormat), args);

    static readonly Dictionary<string, string> Dict = Build();
}
