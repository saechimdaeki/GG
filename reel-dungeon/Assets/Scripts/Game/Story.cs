using System;

/// <summary>Dialogue lines, loaded from Assets/Resources/story.json. Speaker: narrator, boss, or a companion id.</summary>
[Serializable]
public class StoryData
{
    public Line[] prologue;
    public ChapterStory[] chapters;
    public RecruitLine[] recruits;
    public Line[] ending;
    public Line[] defeat;
}

[Serializable]
public class Line
{
    public string speaker;
    public string text;
}

[Serializable]
public class ChapterStory
{
    public Line[] intro;
    public Line[] boss;
    public Line[] bossResponses;
    public Line[] defeat;
}

[Serializable]
public class RecruitLine
{
    public string id;
    public string text;
}
