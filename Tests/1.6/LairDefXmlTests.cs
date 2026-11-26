using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Verse;
using Xunit;

namespace SanguophageLords.Tests;

/// <summary>
/// Reads the repo's lair def XML as plain text and checks what the game would only report at
/// startup: every class name resolves to a type of the right kind, and every room the layout
/// asks for is defined. Catches a renamed worker or a typo without booting the game.
/// </summary>
public class LairDefXmlTests
{
    private static readonly string DefsRoot = FindDefsRoot();

    private static string FindDefsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "1.6", "Defs")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "1.6", "Defs");
    }

    private static IEnumerable<XElement> Defs(string folder, string elementName)
    {
        return Directory.GetFiles(Path.Combine(DefsRoot, folder), "*.xml")
            .SelectMany(file => XDocument.Load(file).Root.Elements(elementName));
    }

    private static Type ModType(string name)
    {
        return typeof(LairRoomWorker).Assembly.GetType(name);
    }

    [Fact]
    public void RoomDefs_NameWorkersThatExist()
    {
        List<XElement> rooms = Defs("LairRoomDefs", "SanguophageLords.LairRoomDef").ToList();
        Assert.NotEmpty(rooms);
        foreach (XElement room in rooms)
        {
            string defName = (string)room.Element("defName");
            string worker = (string)room.Element("workerClass");
            Assert.False(string.IsNullOrEmpty(worker), $"{defName} has no workerClass and would be an empty room");
            Type type = ModType(worker);
            Assert.True(type != null && typeof(LairRoomWorker).IsAssignableFrom(type), $"{defName}: workerClass {worker} is not a LairRoomWorker");
        }
    }

    [Fact]
    public void RoomDefs_UseKnownEnumValues()
    {
        foreach (XElement room in Defs("LairRoomDefs", "SanguophageLords.LairRoomDef"))
        {
            string defName = (string)room.Element("defName");
            AssertEnum<PlanRole>(room, "role", defName);
            AssertEnum<PlanWalls>(room, "walls", defName);
            AssertEnum<PlanAnchor>(room, "anchor", defName);
            AssertEnum<PlanOpening>(room, "entry", defName);
            AssertEnum<LairFloor>(room, "floor", defName);
            foreach (XElement shape in room.Elements("shapes").Elements("li"))
            {
                AssertEnum<RoomShapeKind>(shape, "kind", defName);
            }
        }
    }

    private static void AssertEnum<T>(XElement parent, string field, string defName) where T : struct
    {
        string value = (string)parent.Element(field);
        Assert.True(value == null || Enum.TryParse(value, out T _), $"{defName}: {field} '{value}' is not a {typeof(T).Name}");
    }

    [Fact]
    public void Layouts_AskOnlyForDefinedRooms()
    {
        var defined = new HashSet<string>(Defs("LairRoomDefs", "SanguophageLords.LairRoomDef").Select(r => (string)r.Element("defName")));
        Assert.Equal(defined.Count, Defs("LairRoomDefs", "SanguophageLords.LairRoomDef").Count());
        var asked = new HashSet<string>();
        foreach (XElement layout in Defs("LairLayoutDefs", "SanguophageLords.LairLayoutDef"))
        {
            foreach (string spine in new[] { "entranceHall", "forecourt", "vestibule", "processional", "sanctum", "corridor" })
            {
                asked.Add((string)layout.Element(spine));
            }
            foreach (XElement entry in layout.Elements("rooms").Elements("li"))
            {
                asked.Add((string)entry.Element("room"));
            }
        }
        Assert.All(asked, name => Assert.Contains(name, defined));
        // A room def nothing asks for is dead weight, or a line someone forgot to add.
        Assert.All(defined, name => Assert.Contains(name, asked));
    }

    [Fact]
    public void GenSteps_NameClassesThatExist()
    {
        foreach (XElement step in Defs("GenStepDefs", "GenStepDef"))
        {
            string name = (string)step.Element("genStep").Attribute("Class");
            Type type = ModType(name);
            Assert.True(type != null && typeof(GenStep).IsAssignableFrom(type), $"{(string)step.Element("defName")}: {name} is not a GenStep");
        }
    }

    [Fact]
    public void MapGenerator_ListsEveryLairStep()
    {
        var listed = new HashSet<string>(Defs("MapGeneratorDefs", "MapGeneratorDef").Elements("genSteps").Elements("li").Select(li => li.Value));
        foreach (XElement step in Defs("GenStepDefs", "GenStepDef"))
        {
            Assert.Contains((string)step.Element("defName"), listed);
        }
    }
}
