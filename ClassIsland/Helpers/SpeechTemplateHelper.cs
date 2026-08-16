using System;
using System.Text.RegularExpressions;
using ClassIsland.Shared.Models.Profile;

namespace ClassIsland.Helpers;

/// <summary>
/// Classland CE 播报模板引擎 — 支持 {subject} {teacher} {startTime} {endTime} {location} {subjectIndex} 占位符
/// </summary>
public static class SpeechTemplateHelper
{
    public static string Render(string template, Subject subject, TimeLayoutItem? timeItem, int subjectIndex = 0, string location = "")
    {
        if (string.IsNullOrWhiteSpace(template))
            return "";

        var result = template;

        result = result.Replace("{subject}", subject.Name);

        var teacherName = subject.GetFirstName();
        if (!string.IsNullOrWhiteSpace(teacherName))
            result = Regex.Replace(result, @"\{teacher\}", $"{teacherName}老师");
        else
            result = Regex.Replace(result, @"\{teacher\}", "");
        result = Regex.Replace(result, @"\s*[,，、]?\s*\{teacher\}", "");

        if (timeItem != null)
        {
            result = result.Replace("{startTime}",
                $"{timeItem.StartTime.Hours}:{timeItem.StartTime.Minutes:D2}");
            result = result.Replace("{endTime}",
                $"{timeItem.EndTime.Hours}:{timeItem.EndTime.Minutes:D2}");
        }

        result = result.Replace("{location}", string.IsNullOrWhiteSpace(location) ? "" : location);
        result = result.Replace("{subjectIndex}", (subjectIndex + 1).ToString());

        result = Regex.Replace(result, @"\s*[,，、]?\s*$", "");
        result = result.Replace("开始。", "开始。").Replace("。。", "。");

        return result.Trim();
    }

    public static string GetDefaultTemplate(string type)
    {
        return type switch
        {
            "prepare" => "距上课还有{startTime}分钟。下节课是：{subject}，{teacher}{startTime}开始。",
            "onClass" => "现在是{subject}，{teacher}{endTime}下课。",
            "onBreak" => "本节{subject}结束。下节课是：{nextSubject}，{nextTeacher}。",
            _ => "下节课是：{subject}，{teacher}{startTime}开始。"
        };
    }
}