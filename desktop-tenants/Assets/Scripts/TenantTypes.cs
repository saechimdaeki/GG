using System;
using UnityEngine;

namespace DesktopTenants
{
    /// <summary>One kind of tenant: look, personality knobs and lines. Loaded from Resources/tenants.json.</summary>
    [Serializable]
    public class TenantType
    {
        public string name, kind, hair, shirt, pants, skin = "#f4c8a0", acc = "#22222a";
        /// <summary>Accessories painted over the pose: tie, shades, headphones, cap, longhair, apron, antenna, backpack.</summary>
        public string[] gear;
        public float walkSpeed = 28, sleepChance = 0.2f;
        public bool sleepFall, jumper;
        public int rent = 3000;
        /// <summary>Optional floor unlock override; original types keep their roster order.</summary>
        public int minFloor;
        /// <summary>Drawn at this size (the baby is smaller).</summary>
        public float scale = 1;
        /// <summary>When set, this tenant sometimes gives a snack to someone nearby, saying this.</summary>
        public string treat;
        /// <summary>Signature action: phone, yawn, pushup, groom, selfie, sing, wag, dj, cook, game, deliver, teleport, vacuum, slide, cry, patrol, magic, paint, bake, photo, yoyo, tailor, flute or stretchCoach.</summary>
        public string special;
        public string[] idle, fling, bonk, land, poke, greet, held, pay, jump, act;
        public bool IsCat => kind == "cat";
        public bool IsDog => kind == "dog";
        public bool IsRobot => kind == "robot";
        /// <summary>Animals don't talk about weekdays or sit on ledges.</summary>
        public bool IsAnimal => kind != "person";

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }

    /// <summary>A two-line exchange: the first tenant says <see cref="a"/>, the other answers <see cref="b"/>.</summary>
    [Serializable]
    public class Convo { public string a, b; }

    /// <summary>All tenant types plus lines every (human) tenant shares, keyed to the real clock.</summary>
    [Serializable]
    public class TenantBook
    {
        public string[] crowded, night, dizzy, angry;
        public string[] mon, tue, wed, thu, fri, sat, sun;
        public string[] morning, lunch, evening;
        public string[] climb, arrive, ladderFall, rain, peek, trip, stretch, pet, blackout, fans, birthday;
        public string[] clear, cloudy, storm, snow, windy, rainbow, struck, hot, memfull, wish;
        public string[] icecream, icecreamDone, meetingChair, meetingCrowd, marathon, confess, accept, birds, busking;
        /// <summary>The celebrity who visits by limo (not a resident type).</summary>
        public TenantType star;
        public Convo[] convos, fights;
        public string[] home, piggyRider, piggyCarrier, tickle, treated, scolded, admire, ghost, unbox, movie, movieEnd, bbq, snowball, snowHit, bungeo, bungeoDone;
        /// <summary>Stray cats that drop by for the cat-gathering event.</summary>
        public TenantType[] strays;
        public TenantType[] types;

        public static TenantBook Load() => JsonUtility.FromJson<TenantBook>(Resources.Load<TextAsset>("tenants").text);

        /// <summary>A random line from <paramref name="lines"/>, or null when there are none.</summary>
        public static string Pick(string[] lines) => lines == null || lines.Length == 0 ? null : lines[UnityEngine.Random.Range(0, lines.Length)];

        /// <summary>Lines about the weekday of <paramref name="now"/>.</summary>
        public string[] Day(DateTime now) => now.DayOfWeek switch
        {
            DayOfWeek.Monday => mon,
            DayOfWeek.Tuesday => tue,
            DayOfWeek.Wednesday => wed,
            DayOfWeek.Thursday => thu,
            DayOfWeek.Friday => fri,
            DayOfWeek.Saturday => sat,
            _ => sun,
        };

        /// <summary>Lines for the time of day of <paramref name="now"/>, or null between slots.</summary>
        public string[] TimeOfDay(DateTime now) => now.Hour switch
        {
            >= 6 and < 11 => morning,
            >= 11 and < 14 => lunch,
            >= 17 and < 23 => evening,
            _ => null,
        };
    }
}
